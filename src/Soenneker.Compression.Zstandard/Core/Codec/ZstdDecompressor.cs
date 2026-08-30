using Soenneker.Compression.Zstandard.Core.Entropy;
using Soenneker.Compression.Zstandard.Core.Errors;
using Soenneker.Compression.Zstandard.Core.Frame;
using Soenneker.Compression.Zstandard.Core.Intrinsics;
using Soenneker.Compression.Zstandard.Core.Memory;
using System;
using System.Buffers.Binary;
using System.Text;

namespace Soenneker.Compression.Zstandard.Core.Codec;

internal sealed class ZstdDecompressor
{
    public byte[] Decompress(ReadOnlySpan<byte> compressed)
    {
        if (compressed.IsEmpty)
            return Array.Empty<byte>();

        using var growable = new GrowableBuffer(Math.Min(4096, compressed.Length));
        DecompressFramesToBuffer(compressed, growable, out _);
        return growable.ToArray();
    }

    public string DecompressToString(ReadOnlySpan<byte> compressed)
    {
        if (compressed.IsEmpty)
            return string.Empty;

        using var growable = new GrowableBuffer(Math.Min(4096, compressed.Length));
        DecompressFramesToBuffer(compressed, growable, out _);
        return Encoding.UTF8.GetString(growable.WrittenSpan);
    }

    public void Decompress(ReadOnlySpan<byte> compressed, Span<byte> destination)
    {
        if (!TryDecompress(compressed, destination, out _))
            throw new InvalidOperationException("Destination capacity is insufficient for decompressed output.");
    }

    public bool TryDecompress(ReadOnlySpan<byte> compressed, Span<byte> destination, out int written)
    {
        bool success = DecompressFramesToDestination(compressed, destination, out written);
        if (!success)
            written = 0;

        return success;
    }

    private static void DecompressFramesToBuffer(ReadOnlySpan<byte> compressed, GrowableBuffer growable, out int written)
    {
        written = 0;
        var inputOffset = 0;
        while (inputOffset < compressed.Length)
        {
            ReadOnlySpan<byte> remaining = compressed.Slice(inputOffset);

            if (ZstdFrameReader.IsSkippableFrame(remaining))
            {
                int skip = ZstdFrameReader.ReadSkippableFrameSize(remaining);
                if (skip > remaining.Length)
                    throw new ZstdCodecException("Skippable frame exceeds input bounds.");
                inputOffset += skip;
                continue;
            }

            ZstdFrameHeader frameHeader = ZstdFrameReader.ReadFrameHeader(remaining, out int headerSize);
            inputOffset += headerSize;
            int frameStart = written;

            bool last;
            do
            {
                ZstdBlockHeader block = ZstdFrameReader.ReadBlockHeader(compressed.Slice(inputOffset), out int blockHeaderSize);
                inputOffset += blockHeaderSize;

                switch (block.BlockType)
                {
                    case ZstdBlockType.Raw:
                    {
                        if (block.BlockSize > compressed.Length - inputOffset)
                            throw new ZstdCodecException("Raw block exceeds input bounds.");

                        ReadOnlySpan<byte> blockSrc = compressed.Slice(inputOffset, block.BlockSize);
                        growable.Write(blockSrc);
                        written += blockSrc.Length;
                        inputOffset += block.BlockSize;
                        break;
                    }
                    case ZstdBlockType.Rle:
                    {
                        if (inputOffset >= compressed.Length)
                            throw new ZstdCodecException("RLE block missing payload byte.");

                        byte value = compressed[inputOffset++];
                        Span<byte> destination = growable.GetSpan(block.BlockSize)[..block.BlockSize];
                        FastOps.Fill(destination, value);
                        growable.Advance(block.BlockSize);
                        written += block.BlockSize;
                        break;
                    }
                    case ZstdBlockType.Compressed:
                        throw new ZstdCodecException("Compressed zstd blocks are not yet supported in this implementation.");
                    default:
                        throw new ZstdCodecException("Encountered unknown zstd block type.");
                }

                last = block.IsLastBlock;
            } while (!last);

            if (frameHeader.HasChecksum)
            {
                if (compressed.Length - inputOffset < 4)
                    throw new ZstdCodecException("Missing frame checksum.");

                uint expected = BinaryPrimitives.ReadUInt32LittleEndian(compressed.Slice(inputOffset, 4));
                uint actual = XxHash64.Hash32(growable.WrittenSpan.Slice(frameStart, written - frameStart));
                if (actual != expected)
                    throw new ZstdCodecException("Frame checksum validation failed.");

                inputOffset += 4;
            }

            ValidateFrameContentSize(frameHeader, written - frameStart);
        }
    }

    private static bool DecompressFramesToDestination(ReadOnlySpan<byte> compressed, Span<byte> destination, out int written)
    {
        written = 0;
        var inputOffset = 0;

        while (inputOffset < compressed.Length)
        {
            ReadOnlySpan<byte> remaining = compressed.Slice(inputOffset);

            if (ZstdFrameReader.IsSkippableFrame(remaining))
            {
                int skip = ZstdFrameReader.ReadSkippableFrameSize(remaining);
                if (skip > remaining.Length)
                    throw new ZstdCodecException("Skippable frame exceeds input bounds.");
                inputOffset += skip;
                continue;
            }

            ZstdFrameHeader frameHeader = ZstdFrameReader.ReadFrameHeader(remaining, out int headerSize);
            inputOffset += headerSize;
            int frameStart = written;

            bool last;
            do
            {
                ZstdBlockHeader block = ZstdFrameReader.ReadBlockHeader(compressed.Slice(inputOffset), out int blockHeaderSize);
                inputOffset += blockHeaderSize;

                switch (block.BlockType)
                {
                    case ZstdBlockType.Raw:
                    {
                        if (block.BlockSize > compressed.Length - inputOffset)
                            throw new ZstdCodecException("Raw block exceeds input bounds.");

                        ReadOnlySpan<byte> blockSrc = compressed.Slice(inputOffset, block.BlockSize);
                        if (destination.Length - written < blockSrc.Length)
                            return false;
                        blockSrc.CopyTo(destination.Slice(written));
                        written += blockSrc.Length;
                        inputOffset += block.BlockSize;
                        break;
                    }
                    case ZstdBlockType.Rle:
                    {
                        if (inputOffset >= compressed.Length)
                            throw new ZstdCodecException("RLE block missing payload byte.");

                        byte value = compressed[inputOffset++];
                        if (destination.Length - written < block.BlockSize)
                            return false;

                        FastOps.Fill(destination.Slice(written, block.BlockSize), value);
                        written += block.BlockSize;
                        break;
                    }
                    case ZstdBlockType.Compressed:
                        throw new ZstdCodecException("Compressed zstd blocks are not yet supported in this implementation.");
                    default:
                        throw new ZstdCodecException("Encountered unknown zstd block type.");
                }

                last = block.IsLastBlock;
            } while (!last);

            if (frameHeader.HasChecksum)
            {
                if (compressed.Length - inputOffset < 4)
                    throw new ZstdCodecException("Missing frame checksum.");

                uint expected = BinaryPrimitives.ReadUInt32LittleEndian(compressed.Slice(inputOffset, 4));
                uint actual = XxHash64.Hash32(destination.Slice(frameStart, written - frameStart));
                if (actual != expected)
                    throw new ZstdCodecException("Frame checksum validation failed.");

                inputOffset += 4;
            }

            ValidateFrameContentSize(frameHeader, written - frameStart);
        }

        return true;
    }

    private static void ValidateFrameContentSize(ZstdFrameHeader frameHeader, int actualSize)
    {
        if (frameHeader.FrameContentSize is ulong expectedSize && expectedSize != (ulong)actualSize)
            throw new ZstdCodecException("Frame content size does not match the decompressed output.");
    }
}
