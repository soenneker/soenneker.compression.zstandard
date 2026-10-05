using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using Soenneker.Compression.Zstandard.Core.Errors;

namespace Soenneker.Compression.Zstandard.Core.Entropy;

internal ref struct ZstdBitReader
{
    private readonly ReadOnlySpan<byte> _source;
    public int Remaining { get; private set; }

    public ZstdBitReader(ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty || source[^1] == 0) throw new ZstdCodecException("Missing bitstream end marker.");
        _source = source;
        Remaining = checked((source.Length - 1) * 8 + BitOperations.Log2(source[^1]));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Read(int count)
    {
        Skip(count);
        return Extract(Remaining, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(int count)
    {
        if ((uint)count > 31 || count > Remaining) throw new ZstdCodecException("Truncated sequence bitstream.");
        Remaining -= count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PeekPadded(int width)
    {
        int available = Math.Min(width, Remaining);
        return (int)(Extract(Remaining - available, available) << (width - available));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint Extract(int start, int count)
    {
        if (count == 0) return 0;
        int index = start >> 3;
        ulong value;
        if (index <= _source.Length - sizeof(ulong))
            value = BinaryPrimitives.ReadUInt64LittleEndian(_source.Slice(index));
        else
        {
            value = 0;
            int bytes = ((start & 7) + count + 7) >> 3;
            for (int i = 0; i < bytes; i++) value |= (ulong)_source[index + i] << (i * 8);
        }
        return (uint)((value >> (start & 7)) & ((1UL << count) - 1));
    }
}
