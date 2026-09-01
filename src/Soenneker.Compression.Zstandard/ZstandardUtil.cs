using Soenneker.Compression.Zstandard.Abstract;
using Soenneker.Compression.Zstandard.Core.Codec;
using System;
using System.Buffers;
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
        return Compress(Encoding.UTF8.GetBytes(value), compressionLevel);
    }

    public byte[] Compress(ReadOnlySpan<byte> source, int compressionLevel = 3)
    {
        int max = GetMaxCompressedLength(source.Length);
        byte[] rented = ArrayPool<byte>.Shared.Rent(max);

        try
        {
            if (!TryCompress(source, rented.AsSpan(0, max), out int written, compressionLevel))
                throw new InvalidOperationException("Destination capacity was insufficient for compression output.");

            return rented.AsSpan(0, written).ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public bool TryCompress(string value, Span<byte> destination, out int written, int compressionLevel = 3)
    {
        return TryCompress(Encoding.UTF8.GetBytes(value), destination, out written, compressionLevel);
    }

    public bool TryCompress(ReadOnlySpan<byte> source, Span<byte> destination, out int written, int compressionLevel = 3) => _compressor.TryCompress(source, destination, out written, compressionLevel);

    public string DecompressToString(ReadOnlySpan<byte> compressed)
    {
        return _decompressor.DecompressToString(compressed);
    }

    public byte[] Decompress(ReadOnlySpan<byte> compressed) => _decompressor.Decompress(compressed);

    public void Decompress(ReadOnlySpan<byte> compressed, Span<byte> destination) => _decompressor.Decompress(compressed, destination);

    public bool TryDecompress(ReadOnlySpan<byte> compressed, Span<byte> destination, out int written) => _decompressor.TryDecompress(compressed, destination, out written);

    public async ValueTask CompressFile(string sourceFilePath, string destinationFilePath, int compressionLevel = 3, CancellationToken cancellationToken = default)
    {
        byte[] source = await _fileUtil.ReadToBytes(sourceFilePath, log: false, cancellationToken).NoSync();
        byte[] compressed = Compress(source, compressionLevel);
        await WriteFileAtomically(destinationFilePath, compressed, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DecompressFile(string sourceFilePath, string destinationFilePath, CancellationToken cancellationToken = default)
    {
        byte[] compressed = await _fileUtil.ReadToBytes(sourceFilePath, log: false, cancellationToken).NoSync();
        byte[] decompressed = Decompress(compressed);
        await WriteFileAtomically(destinationFilePath, decompressed, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask WriteFileAtomically(string destinationFilePath, byte[] content, CancellationToken cancellationToken)
    {
        await _fileUtil.WriteAtomically(destinationFilePath, content, log: false, cancellationToken).ConfigureAwait(false);
    }
}
