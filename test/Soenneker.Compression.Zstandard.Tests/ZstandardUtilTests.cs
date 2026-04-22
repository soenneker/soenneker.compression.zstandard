using AwesomeAssertions;
using Soenneker.Compression.Zstandard.Abstract;
using Soenneker.Tests.HostedUnit;
using System;
using System.Linq;
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
}
