using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Soenneker.Compression.Zstandard.Core.Entropy;

internal static class XxHash64
{
    private const ulong Prime1 = 11400714785074694791UL;
    private const ulong Prime2 = 14029467366897019727UL;
    private const ulong Prime3 = 1609587929392839161UL;
    private const ulong Prime4 = 9650029242287828579UL;
    private const ulong Prime5 = 2870177450012600261UL;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Hash32(ReadOnlySpan<byte> data) => (uint)Hash64(data);

    public static ulong Hash64(ReadOnlySpan<byte> data, ulong seed = 0)
    {
        int len = data.Length;
        int index = 0;
        ulong hash;

        if (len >= 32)
        {
            ulong v1 = seed + Prime1 + Prime2;
            ulong v2 = seed + Prime2;
            ulong v3 = seed;
            ulong v4 = seed - Prime1;
            int limit = len - 32;

            while (index <= limit)
            {
                v1 = Round(v1, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(index, 8)));
                v2 = Round(v2, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(index + 8, 8)));
                v3 = Round(v3, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(index + 16, 8)));
                v4 = Round(v4, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(index + 24, 8)));
                index += 32;
            }

            hash = RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);
            hash = MergeRound(hash, v1);
            hash = MergeRound(hash, v2);
            hash = MergeRound(hash, v3);
            hash = MergeRound(hash, v4);
        }
        else
        {
            hash = seed + Prime5;
        }

        hash += (ulong)len;

        while (index <= len - 8)
        {
            ulong k1 = Round(0, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(index, 8)));
            hash ^= k1;
            hash = RotateLeft(hash, 27) * Prime1 + Prime4;
            index += 8;
        }

        if (index <= len - 4)
        {
            hash ^= (ulong)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(index, 4)) * Prime1;
            hash = RotateLeft(hash, 23) * Prime2 + Prime3;
            index += 4;
        }

        while (index < len)
        {
            hash ^= data[index] * Prime5;
            hash = RotateLeft(hash, 11) * Prime1;
            index++;
        }

        hash ^= hash >> 33;
        hash *= Prime2;
        hash ^= hash >> 29;
        hash *= Prime3;
        hash ^= hash >> 32;
        return hash;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Round(ulong acc, ulong input)
    {
        acc += input * Prime2;
        acc = RotateLeft(acc, 31);
        acc *= Prime1;
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MergeRound(ulong acc, ulong val)
    {
        acc ^= Round(0, val);
        acc = acc * Prime1 + Prime4;
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));
}
