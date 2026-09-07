using System;
using System.Runtime.CompilerServices;

namespace Soenneker.Compression.Zstandard.Core.Intrinsics;

internal static class FastOps
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRle(ReadOnlySpan<byte> source)
    {
        return source.Length > 1 && !source[1..].ContainsAnyExcept(source[0]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Fill(Span<byte> destination, byte value) => destination.Fill(value);
}
