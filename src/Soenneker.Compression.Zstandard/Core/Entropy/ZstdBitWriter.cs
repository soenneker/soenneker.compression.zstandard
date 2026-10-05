using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Soenneker.Compression.Zstandard.Core.Errors;

namespace Soenneker.Compression.Zstandard.Core.Entropy;

internal ref struct ZstdBitWriter(Span<byte> destination)
{
    private readonly Span<byte> _destination = destination;
    private ulong _pending;
    private int _bits;
    private int _position;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(uint value, int count)
    {
        _pending |= (ulong)value << _bits;
        _bits += count;
        if (_bits >= 32)
        {
            if (_destination.Length - _position < 4) throw new ZstdCodecException("Insufficient bitstream capacity.");
            BinaryPrimitives.WriteUInt32LittleEndian(_destination.Slice(_position), (uint)_pending);
            _position += 4;
            _pending >>= 32;
            _bits -= 32;
        }
    }

    public int Finish()
    {
        Write(1, 1);
        while (_bits > 0)
        {
            if (_position >= _destination.Length) throw new ZstdCodecException("Insufficient bitstream capacity.");
            _destination[_position++] = (byte)_pending;
            _pending >>= 8;
            _bits -= 8;
        }
        return _position;
    }
}
