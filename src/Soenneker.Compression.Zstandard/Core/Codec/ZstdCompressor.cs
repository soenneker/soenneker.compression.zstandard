using Soenneker.Compression.Zstandard.Core.Constants;
using Soenneker.Compression.Zstandard.Core.Entropy;
using Soenneker.Compression.Zstandard.Core.Frame;
using Soenneker.Compression.Zstandard.Core.Intrinsics;
using System;
using System.Buffers;
using System.Buffers.Binary;

namespace Soenneker.Compression.Zstandard.Core.Codec;

internal sealed class ZstdCompressor
{
    public static int GetCompressBound(int sourceLength)
    {
        if (sourceLength < 0)
            throw new InvalidOperationException("Source length cannot be negative.");

        long blocks = ((long)sourceLength + (ZstdConstants.MaxBlockSize - 1)) / ZstdConstants.MaxBlockSize;
        long bound = sourceLength + (sourceLength >> 8) + 256L + (blocks * 3) + 16;

        if (bound > Array.MaxLength)
            throw new InvalidOperationException("Compressed output exceeds the supported in-memory size.");

        return (int)bound;
    }

    public bool TryCompress(ReadOnlySpan<byte> source, Span<byte> destination, out int written, int compressionLevel)
    {
        if (compressionLevel is < 1 or > 22) throw new ArgumentOutOfRangeException(nameof(compressionLevel), "Supported levels are 1 through 22.");
        byte[] scratch = ArrayPool<byte>.Shared.Rent(Math.Max(32, Math.Min(source.Length, ZstdConstants.MaxBlockSize) * 2));
        try
        {
            written = 0;

            if (!ZstdFrameWriter.TryWriteFrameHeader(destination, (ulong)source.Length, writeChecksum: true, out int headerWritten))
                return false;

            int outputOffset = headerWritten;
            var sourceOffset = 0;

            while (sourceOffset < source.Length || source.Length == 0)
            {
                int remaining = source.Length - sourceOffset;
                int blockSize = Math.Min(remaining, ZstdConstants.MaxBlockSize);
                bool isLast = sourceOffset + blockSize >= source.Length;
                ReadOnlySpan<byte> block = blockSize > 0 ? source.Slice(sourceOffset, blockSize) : ReadOnlySpan<byte>.Empty;

                bool writeRle = blockSize > 1 && FastOps.IsRle(block);
                int compressedSize = !writeRle && blockSize >= 16 ? ZstdCompressedBlock.Compress(block, scratch, compressionLevel) : 0;
                bool useCompressed = compressedSize > 0 && compressedSize < blockSize;
                ZstdBlockType type = writeRle ? ZstdBlockType.Rle : useCompressed ? ZstdBlockType.Compressed : ZstdBlockType.Raw;
                int payloadSize = writeRle ? 1 : useCompressed ? compressedSize : blockSize;

                if (destination.Length - outputOffset < payloadSize + 3 + (isLast ? 4 : 0))
                    return false;

                if (!ZstdFrameWriter.TryWriteBlockHeader(destination.Slice(outputOffset), isLast, type, useCompressed ? compressedSize : blockSize, out int blockHeaderWritten))
                    return false;

                outputOffset += blockHeaderWritten;
                if (writeRle)
                {
                    destination[outputOffset++] = block[0];
                }
                else if (useCompressed)
                {
                    scratch.AsSpan(0, compressedSize).CopyTo(destination.Slice(outputOffset));
                    outputOffset += compressedSize;
                }
                else if (blockSize > 0)
                {
                    block.CopyTo(destination.Slice(outputOffset, blockSize));
                    outputOffset += blockSize;
                }

                sourceOffset += blockSize;
                if (source.Length == 0)
                    break;
            }

            uint checksum = XxHash64.Hash32(source);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(outputOffset, 4), checksum);
            outputOffset += 4;
            written = outputOffset;
            return true;
        }
        finally { ArrayPool<byte>.Shared.Return(scratch, clearArray: true); }
    }
}
