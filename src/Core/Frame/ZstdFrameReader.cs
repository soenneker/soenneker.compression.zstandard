using Soenneker.Compression.Zstandard.Core.Constants;
using Soenneker.Compression.Zstandard.Core.Errors;
using System;
using System.Buffers.Binary;

namespace Soenneker.Compression.Zstandard.Core.Frame;

internal static class ZstdFrameReader
{
    public static bool IsSkippableFrame(ReadOnlySpan<byte> source)
    {
        if (source.Length < 4)
            return false;

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(source);
        return (magic & ZstdConstants.SkippableMagicMask) == ZstdConstants.SkippableMagicStart;
    }

    public static int ReadSkippableFrameSize(ReadOnlySpan<byte> source)
    {
        if (source.Length < 8)
            throw new ZstdCodecException("Input too small for skippable frame header.");

        int payloadSize = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(4, 4));
        if (payloadSize < 0)
            throw new ZstdCodecException("Skippable frame payload size is invalid.");

        return 8 + payloadSize;
    }

    public static ZstdFrameHeader ReadFrameHeader(ReadOnlySpan<byte> source, out int bytesConsumed)
    {
        if (source.Length < 6)
            throw new ZstdCodecException("Input too small for zstd frame header.");

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(source);
        if (magic != ZstdConstants.MagicNumber)
            throw new ZstdCodecException("Invalid zstd frame magic number.");

        byte descriptor = source[4];
        bool singleSegment = (descriptor & (1 << 5)) != 0;
        bool hasChecksum = (descriptor & (1 << 2)) != 0;
        int dictIdFlag = descriptor & 0x3;
        int fcsFlag = descriptor >> 6;
        int index = 5;
        byte? windowDescriptor = null;

        if (!singleSegment)
        {
            if (source.Length <= index)
                throw new ZstdCodecException("Input too small for window descriptor.");

            windowDescriptor = source[index++];
        }

        int dictIdSize = dictIdFlag switch
        {
            0 => 0,
            1 => 1,
            2 => 2,
            3 => 4,
            _ => 0
        };

        if (source.Length < index + dictIdSize)
            throw new ZstdCodecException("Input too small for dictionary id field.");

        index += dictIdSize;

        int fcsSize = GetFrameContentSizeFieldLength(singleSegment, fcsFlag);
        if (source.Length < index + fcsSize)
            throw new ZstdCodecException("Input too small for frame content size field.");

        ulong? fcs = null;
        if (fcsSize > 0)
        {
            fcs = fcsSize switch
            {
                1 => source[index],
                2 => (ulong)(BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(index, 2)) + 256),
                4 => BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(index, 4)),
                8 => BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(index, 8)),
                _ => throw new ZstdCodecException("Unsupported frame content size field length.")
            };
            index += fcsSize;
        }

        bytesConsumed = index;
        return new ZstdFrameHeader(singleSegment, hasChecksum, fcs, descriptor, windowDescriptor);
    }

    public static ZstdBlockHeader ReadBlockHeader(ReadOnlySpan<byte> source, out int bytesConsumed)
    {
        if (source.Length < 3)
            throw new ZstdCodecException("Input too small for block header.");

        int header = source[0] | (source[1] << 8) | (source[2] << 16);
        bool lastBlock = (header & 0x1) != 0;
        ZstdBlockType blockType = (ZstdBlockType)((header >> 1) & 0x3);
        int blockSize = (header >> 3) & 0x1F_FFFF;

        if (blockType == ZstdBlockType.Reserved)
            throw new ZstdCodecException("Encountered reserved zstd block type.");

        bytesConsumed = 3;
        return new ZstdBlockHeader(lastBlock, blockType, blockSize);
    }

    private static int GetFrameContentSizeFieldLength(bool singleSegment, int fcsFlag)
    {
        if (singleSegment)
        {
            return fcsFlag switch
            {
                0 => 1,
                1 => 2,
                2 => 4,
                3 => 8,
                _ => throw new ZstdCodecException("Invalid frame content size flag.")
            };
        }

        return fcsFlag switch
        {
            0 => 0,
            1 => 2,
            2 => 4,
            3 => 8,
            _ => throw new ZstdCodecException("Invalid frame content size flag.")
        };
    }
}
