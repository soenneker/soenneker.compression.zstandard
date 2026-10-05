using System;
using System.Buffers.Binary;

namespace Soenneker.Compression.Zstandard.Tests;

public sealed class ZstandardBufferTests
{
    private readonly ZstandardUtil _util = new(null!);
    private static void Require(bool condition) { if (!condition) throw new Exception("Assertion failed."); }

    [Test]
    public void RepeatingMatchesCrossBlockAndWordBoundaries()
    {
        foreach (int period in new[] { 1, 2, 3, 7, 8, 9, 31, 257 })
            foreach (int length in new[] { 7, 8, 9, 31, 4097, 131073, 262145 })
            {
                byte[] input = new byte[length];
                for (int i = 0; i < length; i++) input[i] = (byte)((i % period) * 73);
                foreach (int level in new[] { 1, 3, 6, 19 })
                {
                    byte[] frame = _util.Compress(input, level), output = new byte[length + 8];
                    output.AsSpan().Fill(123);
                    Require(_util.TryDecompress(frame, output, out int written) && written == length);
                    Require(input.AsSpan().SequenceEqual(output.AsSpan(0, length)));
                    Require(output.AsSpan(length).IndexOfAnyExcept((byte)123) < 0);
                    Require(input.AsSpan().SequenceEqual(_util.Decompress(frame)));
                }
            }
    }

    [Test]
    public void UnknownContentSizeStillUsesIncrementalDecoding()
    {
        // Standard frame, no content-size field, 1 KiB window, final raw block.
        byte[] frame = { 0x28, 0xb5, 0x2f, 0xfd, 0, 0, 25, 0, 0, 1, 2, 3 };
        Require(_util.Decompress(frame).AsSpan().SequenceEqual(new byte[] { 1, 2, 3 }));
    }

    [Test]
    public void ImpossibleContentSizeIsRejectedBeforeAllocation()
    {
        byte[] frame = { 0x28, 0xb5, 0x2f, 0xfd, 0xa0, 0, 0, 0, 0, 9, 0, 0, 1 };
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(5), 1024 * 1024 * 1024);
        bool failed = false;
        try { _util.Decompress(frame); } catch (Exception e) when (e.GetType().Name == "ZstdCodecException") { failed = true; }
        Require(failed);
    }
}

