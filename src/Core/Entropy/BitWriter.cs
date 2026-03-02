using System;

namespace Soenneker.Compression.Zstandard.Core.Entropy;

internal ref struct BitWriter
{
    private Span<byte> _destination;
    private int _byteIndex;
    private ulong _bitContainer;
    private int _bitsInContainer;

    public BitWriter(Span<byte> destination)
    {
        _destination = destination;
        _byteIndex = 0;
        _bitContainer = 0;
        _bitsInContainer = 0;
    }

    public void WriteBits(uint value, int count)
    {
        ulong masked = value & ((1UL << count) - 1UL);
        _bitContainer |= masked << _bitsInContainer;
        _bitsInContainer += count;
        FlushWholeBytes();
    }

    public int Finish()
    {
        FlushWholeBytes();
        if (_bitsInContainer > 0)
        {
            Ensure(1);
            _destination[_byteIndex++] = (byte)_bitContainer;
            _bitContainer = 0;
            _bitsInContainer = 0;
        }

        return _byteIndex;
    }

    private void FlushWholeBytes()
    {
        while (_bitsInContainer >= 8)
        {
            Ensure(1);
            _destination[_byteIndex++] = (byte)_bitContainer;
            _bitContainer >>= 8;
            _bitsInContainer -= 8;
        }
    }

    private void Ensure(int size)
    {
        if (_byteIndex + size > _destination.Length)
            throw new ArgumentException("Destination too small.");
    }
}
