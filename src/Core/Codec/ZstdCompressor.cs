using Soenneker.Compression.Zstandard.Core.Constants;
using Soenneker.Compression.Zstandard.Core.Entropy;
using Soenneker.Compression.Zstandard.Core.Errors;
using Soenneker.Compression.Zstandard.Core.Frame;
using Soenneker.Compression.Zstandard.Core.Intrinsics;
using System;
using System.Buffers.Binary;

namespace Soenneker.Compression.Zstandard.Core.Codec;

internal sealed class ZstdCompressor
{
    public int GetCompressBound(int sourceLength)
    {
        if (sourceLength < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceLength));

        int blocks = (sourceLength + (ZstdConstants.MaxBlockSize - 1)) / ZstdConstants.MaxBlockSize;
        return sourceLength + (sourceLength >> 8) + 256 + (blocks * 3) + 16;
    }

    public bool TryCompress(ReadOnlySpan<byte> source, Span<byte> destination, out int written, int compressionLevel)
    {
        // v1 keeps a deterministic, extremely fast path: raw + optional RLE blocks.
        _ = compressionLevel;

        written = 0;

        if (!ZstdFrameWriter.TryWriteFrameHeader(destination, (ulong)source.Length, writeChecksum: true, out int headerWritten))
            return false;

        int outputOffset = headerWritten;
        int sourceOffset = 0;

        while (sourceOffset < source.Length || source.Length == 0)
        {
            int remaining = source.Length - sourceOffset;
            int blockSize = Math.Min(remaining, ZstdConstants.MaxBlockSize);
            bool isLast = sourceOffset + blockSize >= source.Length;
            ReadOnlySpan<byte> block = blockSize > 0 ? source.Slice(sourceOffset, blockSize) : ReadOnlySpan<byte>.Empty;

            bool writeRle = blockSize > 1 && FastOps.IsRle(block);
            ZstdBlockType type = writeRle ? ZstdBlockType.Rle : ZstdBlockType.Raw;
            int payloadSize = writeRle ? 1 : blockSize;

            if (destination.Length - outputOffset < payloadSize + 3 + (isLast ? 4 : 0))
                return false;

            if (!ZstdFrameWriter.TryWriteBlockHeader(destination.Slice(outputOffset), isLast, type, blockSize, out int blockHeaderWritten))
                return false;

            outputOffset += blockHeaderWritten;
            if (writeRle)
            {
                destination[outputOffset++] = block[0];
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
}
