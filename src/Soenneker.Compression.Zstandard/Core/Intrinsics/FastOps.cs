using System;
using System.Runtime.CompilerServices;

namespace Soenneker.Compression.Zstandard.Core.Intrinsics;

internal static class FastOps
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRle(ReadOnlySpan<byte> source)
    {
        if (source.Length <= 1)
            return false;

        byte value = source[0];
        for (var i = 1; i < source.Length; i++)
        {
            if (source[i] != value)
                return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Fill(Span<byte> destination, byte value) => destination.Fill(value);
}
