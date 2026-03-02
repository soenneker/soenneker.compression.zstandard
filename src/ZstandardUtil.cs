
using Soenneker.Compression.Zstandard.Abstract;
using Soenneker.Compression.Zstandard.Core.Codec;
using System;
using System.Text;

namespace Soenneker.Compression.Zstandard;

/// <inheritdoc cref="IZstandardUtil"/>
public sealed class ZstandardUtil : IZstandardUtil
{
    private readonly ZstdCompressor _compressor = new();
    private readonly ZstdDecompressor _decompressor = new();

    public int GetMaxCompressedLength(int sourceLength)
    {
        return _compressor.GetCompressBound(sourceLength);
    }

    public byte[] Compress(string value, int compressionLevel = 3)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));

        return Compress(Encoding.UTF8.GetBytes(value), compressionLevel);
    }

    public byte[] Compress(ReadOnlySpan<byte> source, int compressionLevel = 3)
    {
        int max = GetMaxCompressedLength(source.Length);
        byte[] output = new byte[max];

        if (!TryCompress(source, output.AsSpan(), out int written, compressionLevel))
            throw new InvalidOperationException("Destination capacity was insufficient for compression output.");

        return output.AsSpan(0, written).ToArray();
    }

    public bool TryCompress(string value, Span<byte> destination, out int written, int compressionLevel = 3)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));

        return TryCompress(Encoding.UTF8.GetBytes(value), destination, out written, compressionLevel);
    }

    public bool TryCompress(ReadOnlySpan<byte> source, Span<byte> destination, out int written, int compressionLevel = 3) => _compressor.TryCompress(source, destination, out written, compressionLevel);

    public string DecompressToString(ReadOnlySpan<byte> compressed)
    {
        byte[] bytes = Decompress(compressed);
        return Encoding.UTF8.GetString(bytes);
    }

    public byte[] Decompress(ReadOnlySpan<byte> compressed) => _decompressor.Decompress(compressed);

    public void Decompress(ReadOnlySpan<byte> compressed, Span<byte> destination) => _decompressor.Decompress(compressed, destination);

    public bool TryDecompress(ReadOnlySpan<byte> compressed, Span<byte> destination, out int written) => _decompressor.TryDecompress(compressed, destination, out written);
}
