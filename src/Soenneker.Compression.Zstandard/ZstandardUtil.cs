using Soenneker.Extensions.String;
using Soenneker.Compression.Zstandard.Abstract;
using Soenneker.Compression.Zstandard.Core.Codec;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.Task;
using Soenneker.Utils.File.Abstract;

namespace Soenneker.Compression.Zstandard;

/// <inheritdoc cref="IZstandardUtil"/>
public sealed class ZstandardUtil : IZstandardUtil
{
    private readonly IFileUtil _fileUtil;
    private readonly ZstdCompressor _compressor = new();
    private readonly ZstdDecompressor _decompressor = new();

    public ZstandardUtil(IFileUtil fileUtil)
    {
        _fileUtil = fileUtil;
    }

    public int GetMaxCompressedLength(int sourceLength)
    {
        return ZstdCompressor.GetCompressBound(sourceLength);
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
        var output = new byte[max];

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

    public async ValueTask CompressFile(string sourceFilePath, string destinationFilePath, int compressionLevel = 3, CancellationToken cancellationToken = default)
    {
        if (sourceFilePath.IsNullOrWhiteSpace())
            throw new ArgumentException("Source file path cannot be null or empty.", nameof(sourceFilePath));

        if (destinationFilePath.IsNullOrWhiteSpace())
            throw new ArgumentException("Destination file path cannot be null or empty.", nameof(destinationFilePath));

        byte[] source = await _fileUtil.ReadToBytes(sourceFilePath, log: true, cancellationToken).NoSync();
        byte[] compressed = Compress(source, compressionLevel);
        await _fileUtil.Write(destinationFilePath, compressed, log: true, cancellationToken).NoSync();
    }

    public async ValueTask DecompressFile(string sourceFilePath, string destinationFilePath, CancellationToken cancellationToken = default)
    {
        if (sourceFilePath.IsNullOrWhiteSpace())
            throw new ArgumentException("Source file path cannot be null or empty.", nameof(sourceFilePath));

        if (destinationFilePath.IsNullOrWhiteSpace())
            throw new ArgumentException("Destination file path cannot be null or empty.", nameof(destinationFilePath));

        byte[] compressed = await _fileUtil.ReadToBytes(sourceFilePath, log: true, cancellationToken).NoSync();
        byte[] decompressed = Decompress(compressed);
        await _fileUtil.Write(destinationFilePath, decompressed, log: true, cancellationToken).NoSync();
    }
}
