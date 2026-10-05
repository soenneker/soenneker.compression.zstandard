using AwesomeAssertions;
using Soenneker.Compression.Zstandard.Abstract;
using Soenneker.Tests.HostedUnit;
using System;
using System.Linq;
using System.IO;
using System.Text;

namespace Soenneker.Compression.Zstandard.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class ZstandardUtilTests : HostedUnitTest
{
    private readonly IZstandardUtil _util;

    public ZstandardUtilTests(Host host) : base(host)
    {
        _util = Resolve<IZstandardUtil>(true);
    }

    [Test]
    public void Compress_Decompress_RoundTrip_Bytes()
    {
        byte[] input = Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog.");
        byte[] compressed = _util.Compress(input);
        byte[] decompressed = _util.Decompress(compressed);

        decompressed.Should().Equal(input);
    }

    [Test]
    public void Compress_Decompress_RoundTrip_RleData()
    {
        byte[] input = Enumerable.Repeat((byte)'A', 8192).ToArray();
        byte[] compressed = _util.Compress(input);
        byte[] decompressed = _util.Decompress(compressed);

        decompressed.Should().Equal(input);
    }

    [Test]
    public void CompressString_DecompressToString_RoundTrip()
    {
        const string input = "zstd string round-trip plain ascii";
        byte[] compressed = _util.Compress(input);
        string output = _util.DecompressToString(compressed);

        output.Should().Be(input);
    }

    [Test]
    public void TryCompress_And_TryDecompress_UseProvidedSpans()
    {
        byte[] input = Encoding.UTF8.GetBytes("Span API path should round-trip.");
        int max = _util.GetMaxCompressedLength(input.Length);
        Span<byte> compressed = max <= 4096 ? stackalloc byte[max] : new byte[max];

        bool compressedOk = _util.TryCompress(input, compressed, out int compressedBytes);
        compressedOk.Should().BeTrue();
        compressedBytes.Should().BeGreaterThan(0);

        Span<byte> decompressed = new byte[input.Length];
        bool decompressedOk = _util.TryDecompress(compressed[..compressedBytes], decompressed, out int decompressedBytes);
        decompressedOk.Should().BeTrue();
        decompressedBytes.Should().Be(input.Length);
        decompressed.ToArray().Should().Equal(input);
    }

    [Test]
    public void TryCompress_String_UsesUtf8()
    {
        const string input = "Hello zstd string API";
        byte[] utf8 = Encoding.UTF8.GetBytes(input);
        Span<byte> compressed = new byte[_util.GetMaxCompressedLength(utf8.Length)];

        bool compressedOk = _util.TryCompress(input, compressed, out int compressedBytes);
        compressedOk.Should().BeTrue();

        string output = _util.DecompressToString(compressed[..compressedBytes]);
        output.Should().Be(input);
    }

    [Test]
    public void RecordingJson_CompressesSubstantially_AndRoundTrips()
    {
        byte[] input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Range(0, 2000)
            .Select(i => $"{{\"type\":3,\"timestamp\":{i},\"data\":{{\"source\":3,\"x\":120,\"y\":240}}}}\n")));
        byte[] compressed = _util.Compress(input, 6);
        compressed.Length.Should().BeLessThan(input.Length / 4);
        _util.Decompress(compressed).Should().Equal(input);
    }

    [Test]
    public void ProvidedBuffers_RejectInsufficientCapacity()
    {
        byte[] input = Encoding.UTF8.GetBytes("An ordinary recording event with multiple distinct bytes.");
        _util.TryCompress(input, new byte[1], out int written).Should().BeFalse();
        written.Should().Be(0);
        byte[] compressed = _util.Compress(input);
        _util.TryDecompress(compressed, new byte[1], out written).Should().BeFalse();
        written.Should().Be(0);
        compressed[^1] ^= 1;
        Action corrupt = () => _util.Decompress(compressed);
        corrupt.Should().Throw<Exception>();
    }

    [Test]
    public void Singleton_ConcurrentLevels_RoundTrip()
    {
        System.Threading.Tasks.Parallel.For(0, 32, i =>
        {
            byte[] input = Encoding.UTF8.GetBytes(new string((char)('A' + i % 26), 8192) + i);
            _util.Decompress(_util.Compress(input, 1 + i % 9)).Should().Equal(input);
        });
    }

    [Test]
    public void IndependentReferenceFrames_DecodeAcrossBlocks()
    {
        byte[] expected = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Range(0, 6000)
            .Select(i => $"{{\"event\":{i},\"field\":\"email\",\"value\":\"person{i % 37}@example.com\",\"label\":\"héllo 🚀\"}}\n")));
        foreach (int level in new[] { 1, 6, 19 })
        {
            byte[] frame = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"reference-{level}.zst"));
            _util.Decompress(frame).Should().Equal(expected);
            byte[] destination = new byte[expected.Length];
            _util.TryDecompress(frame, destination, out int written).Should().BeTrue();
            written.Should().Be(expected.Length);
            destination.Should().Equal(expected);
        }
    }

    [Test]
    public void TruncatedAndCorruptFrames_AreRejected()
    {
        byte[] input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("recording text with repeated fields and values;", 100)));
        byte[] frame = _util.Compress(input, 6);
        for (int length = 1; length < frame.Length; length++)
        {
            int truncated = length;
            Action decode = () => _util.Decompress(frame.AsSpan(0, truncated));
            decode.Should().Throw<Exception>();
        }
        frame[^1] ^= 0x80;
        Action corrupt = () => _util.Decompress(frame);
        corrupt.Should().Throw<Exception>();
    }

    [Test]
    public void BlockBoundaries_IncompressibleData_AndConcatenatedFrames()
    {
        var random = new Random(1827);
        foreach (int size in new[] { 0, 1, 31, 4096, 131071, 131072, 131073, 524288 })
        {
            byte[] input = new byte[size]; random.NextBytes(input);
            byte[] target = new byte[_util.GetMaxCompressedLength(size)];
            _util.TryCompress(input, target, out int written, 6).Should().BeTrue();
            _util.Decompress(target.AsSpan(0, written)).Should().Equal(input);
            byte[] second = _util.Compress("second frame");
            byte[] combined = target.AsSpan(0, written).ToArray().Concat(second).ToArray();
            _util.Decompress(combined).Should().Equal(input.Concat(Encoding.UTF8.GetBytes("second frame")));
        }
    }

    [Test]
    public void CompressionLevel_RejectsUnsupportedValues()
    {
        Action low = () => _util.Compress("value", 0);
        Action high = () => _util.Compress("value", 23);
        low.Should().Throw<ArgumentOutOfRangeException>();
        high.Should().Throw<ArgumentOutOfRangeException>();
    }
}
