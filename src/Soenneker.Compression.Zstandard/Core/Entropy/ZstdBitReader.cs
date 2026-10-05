using System;
using System.Numerics;
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
        Remaining = (source.Length - 1) * 8 + BitOperations.Log2(source[^1]);
    }

    public uint Read(int count)
    {
        if (count < 0 || count > 31 || count > Remaining) throw new ZstdCodecException("Truncated sequence bitstream.");
        Remaining -= count;
        int start = Remaining;
        uint value = 0;
        int shift = 0;
        while (count > 0)
        {
            int take = Math.Min(count, 8 - (start & 7));
            value |= (uint)((_source[start >> 3] >> (start & 7)) & ((1 << take) - 1)) << shift;
            count -= take; start += take; shift += take;
        }
        return value;
    }
}
