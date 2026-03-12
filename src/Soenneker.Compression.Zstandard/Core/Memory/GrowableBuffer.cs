using Soenneker.Compression.Zstandard.Core.Errors;
using System;
using System.Buffers;

namespace Soenneker.Compression.Zstandard.Core.Memory;

internal sealed class GrowableBuffer : IDisposable
{
    private byte[] _buffer;
    private int _length;
    private bool _disposed;

    public GrowableBuffer(int initialSize = 4096)
    {
        _buffer = ArrayPool<byte>.Shared.Rent(Math.Max(initialSize, 256));
    }

    public int Length => _length;

    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _length);

    public Span<byte> GetSpan(int sizeHint)
    {
        EnsureCapacity(_length + sizeHint);
        return _buffer.AsSpan(_length);
    }

    public void Advance(int count)
    {
        if (count < 0 || _length + count > _buffer.Length)
            throw new ZstdCodecException("Invalid advance count.");
        _length += count;
    }

    public void Write(ReadOnlySpan<byte> source)
    {
        Span<byte> span = GetSpan(source.Length);
        source.CopyTo(span);
        _length += source.Length;
    }

    public byte[] ToArray()
    {
        var output = new byte[_length];
        WrittenSpan.CopyTo(output);
        return output;
    }

    private void EnsureCapacity(int target)
    {
        if (target <= _buffer.Length)
            return;

        int next = _buffer.Length;
        while (next < target)
            next <<= 1;

        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(next);
        _buffer.AsSpan(0, _length).CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = newBuffer;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = Array.Empty<byte>();
        _length = 0;
        _disposed = true;
    }
}
