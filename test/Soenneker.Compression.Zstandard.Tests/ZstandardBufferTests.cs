using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

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
        try { _util.Decompress(frame); } catch (IOException) { failed = true; }
        Require(failed);
    }

    [Test]
    public void EmptyAndSkippableFramesFitEmptyDestination()
    {
        byte[] empty = _util.Compress(ReadOnlySpan<byte>.Empty);
        byte[] skippable = { 0x50, 0x2a, 0x4d, 0x18, 3, 0, 0, 0, 1, 2, 3 };
        byte[] combined = new byte[empty.Length * 2 + skippable.Length];
        empty.CopyTo(combined, 0);
        skippable.CopyTo(combined, empty.Length);
        empty.CopyTo(combined, empty.Length + skippable.Length);

        Require(_util.TryDecompress(combined, Span<byte>.Empty, out int written) && written == 0);
        Require(_util.Decompress(combined).Length == 0);
        Require(_util.TryDecompress(ReadOnlySpan<byte>.Empty, Span<byte>.Empty, out written) && written == 0);
    }

    [Test]
    public void SpanDecoderRejectsTruncatedAndCorruptFrames()
    {
        byte[] frame = _util.Compress("hello");
        for (int length = 1; length < frame.Length; length++)
        {
            bool failed = false;
            try { _util.TryDecompress(frame.AsSpan(0, length), new byte[5], out _); }
            catch (InvalidDataException) { failed = true; }
            Require(failed);
        }

        frame[^1] ^= 1;
        bool corrupt = false;
        try { _util.TryDecompress(frame, new byte[5], out _); }
        catch (InvalidDataException) { corrupt = true; }
        Require(corrupt);
    }

    [Test]
    public void EncodedFramesCanBeDecodedByDotNet()
    {
        byte[] input = new byte[131073];
        new Random(17).NextBytes(input);
        byte[] frame = _util.Compress(input);
        byte[] output = new byte[input.Length];
        Require(ZstandardDecoder.TryDecompress(frame, output, out int written));
        Require(written == input.Length && input.AsSpan().SequenceEqual(output));
    }
}

