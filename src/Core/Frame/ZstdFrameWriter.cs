using Soenneker.Compression.Zstandard.Core.Constants;
using System;
using System.Buffers.Binary;

namespace Soenneker.Compression.Zstandard.Core.Frame;

internal static class ZstdFrameWriter
{
    public static bool TryWriteFrameHeader(Span<byte> destination, ulong contentSize, bool writeChecksum, out int written)
    {
        // Magic (4) + descriptor (1) + content size (8)
        if (destination.Length < 13)
        {
            written = 0;
            return false;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(destination, ZstdConstants.MagicNumber);
        byte descriptor = BuildDescriptor(writeChecksum);
        destination[4] = descriptor;
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(5), contentSize);
        written = 13;
        return true;
    }

    public static bool TryWriteBlockHeader(Span<byte> destination, bool isLastBlock, ZstdBlockType blockType, int blockSize, out int written)
    {
        if ((uint)blockSize > 0x1F_FFFFu || destination.Length < 3)
        {
            written = 0;
            return false;
        }

        int header = (blockSize << 3) | (((int)blockType & 0x3) << 1) | (isLastBlock ? 1 : 0);
        destination[0] = (byte)(header & 0xFF);
        destination[1] = (byte)((header >> 8) & 0xFF);
        destination[2] = (byte)((header >> 16) & 0xFF);
        written = 3;
        return true;
    }

    private static byte BuildDescriptor(bool writeChecksum)
    {
        // singleSegment=1, frameContentSizeFlag=3 (8 bytes), checksum flag configurable
        byte descriptor = (byte)((3 << 6) | (1 << 5));
        if (writeChecksum)
            descriptor |= 1 << 2;
        return descriptor;
    }
}
