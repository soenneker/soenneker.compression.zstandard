using System;

namespace Soenneker.Compression.Zstandard.Abstract;

/// <summary>
/// A utility library for Zstandard compression and decompression
/// </summary>
public interface IZstandardUtil
{
    int GetMaxCompressedLength(int sourceLength);
    byte[] Compress(string value, int compressionLevel = 3);
    byte[] Compress(ReadOnlySpan<byte> source, int compressionLevel = 3);
    bool TryCompress(string value, Span<byte> destination, out int written, int compressionLevel = 3);
    bool TryCompress(ReadOnlySpan<byte> source, Span<byte> destination, out int written, int compressionLevel = 3);
    string DecompressToString(ReadOnlySpan<byte> compressed);
    byte[] Decompress(ReadOnlySpan<byte> compressed);
    void Decompress(ReadOnlySpan<byte> compressed, Span<byte> destination);
    bool TryDecompress(ReadOnlySpan<byte> compressed, Span<byte> destination, out int written);
}
