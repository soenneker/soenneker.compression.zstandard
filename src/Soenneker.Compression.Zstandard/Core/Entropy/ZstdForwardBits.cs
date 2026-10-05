using System;
using Soenneker.Compression.Zstandard.Core.Errors;

namespace Soenneker.Compression.Zstandard.Core.Entropy;

internal ref struct ZstdForwardBits(ReadOnlySpan<byte> source)
{
    private readonly ReadOnlySpan<byte> _source = source;
    private int _position;
    public int BytesRead => (_position + 7) / 8;
    public int Read(int count)
    {
        if (count > _source.Length * 8L - _position) throw new ZstdCodecException("Truncated FSE distribution.");
        int result = 0;
        for (int bit = 0; bit < count; bit++, _position++)
            result |= ((_source[_position >> 3] >> (_position & 7)) & 1) << bit;
        return result;
    }
}
