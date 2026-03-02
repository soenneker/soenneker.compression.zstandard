using Soenneker.Compression.Zstandard.Core.Errors;
using System;

namespace Soenneker.Compression.Zstandard.Core.Entropy;

internal ref struct BitReader
{
    private readonly ReadOnlySpan<byte> _source;
    private int _byteIndex;
    private ulong _bitContainer;
    private int _bitsInContainer;

    public BitReader(ReadOnlySpan<byte> source)
    {
        _source = source;
        _byteIndex = 0;
        _bitContainer = 0;
        _bitsInContainer = 0;
    }

    public uint ReadBits(int count)
    {
        if ((uint)count > 24)
            throw new ZstdCodecException("BitReader supports up to 24 bits per read.");

        Refill(count);
        uint mask = count == 32 ? 0xFFFF_FFFFu : ((1u << count) - 1u);
        uint value = (uint)(_bitContainer & mask);
        _bitContainer >>= count;
        _bitsInContainer -= count;
        return value;
    }

    private void Refill(int requiredBits)
    {
        while (_bitsInContainer < requiredBits && _byteIndex < _source.Length)
        {
            _bitContainer |= (ulong)_source[_byteIndex++] << _bitsInContainer;
            _bitsInContainer += 8;
        }

        if (_bitsInContainer < requiredBits)
            throw new ZstdCodecException("Unexpected end of bitstream.");
    }
}
