using System;
using Soenneker.Compression.Zstandard.Core.Errors;

namespace Soenneker.Compression.Zstandard.Core.Entropy;

internal ref struct ZstdBitWriter(Span<byte> destination)
{
    private readonly Span<byte> _destination = destination;
    private ulong _pending;
    private int _bits;
    private int _position;

    public void Write(uint value, int count)
    {
        _pending |= (ulong)value << _bits;
        _bits += count;
        while (_bits >= 8)
        {
            if (_position >= _destination.Length) throw new ZstdCodecException("Insufficient bitstream capacity.");
            _destination[_position++] = (byte)_pending;
            _pending >>= 8;
            _bits -= 8;
        }
    }

    public int Finish()
    {
        Write(1, 1);
        if (_bits > 0)
        {
            if (_position >= _destination.Length) throw new ZstdCodecException("Insufficient bitstream capacity.");
            _destination[_position++] = (byte)_pending;
        }
        return _position;
    }
}
