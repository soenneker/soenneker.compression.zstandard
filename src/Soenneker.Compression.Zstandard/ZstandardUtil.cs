using Soenneker.Extensions.ValueTask;
using Soenneker.Compression.Zstandard.Abstract;
using System.IO;
using System.IO.Compression;
using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.Task;
using Soenneker.Utils.File.Abstract;

namespace Soenneker.Compression.Zstandard;

public sealed class ZstandardUtil : IZstandardUtil
{
    private readonly IFileUtil _fileUtil;

    public ZstandardUtil(IFileUtil fileUtil)
    {
        _fileUtil = fileUtil;
    }

    public int GetMaxCompressedLength(int sourceLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceLength);
        return checked((int)ZstandardEncoder.GetMaxCompressedLength(sourceLength));
    }

    public byte[] Compress(string value, int compressionLevel = 3)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);
        byte[]? rented = null;
        Span<byte> utf8 = byteCount <= 1024 ? stackalloc byte[byteCount] : (rented = ArrayPool<byte>.Shared.Rent(byteCount)).AsSpan(0, byteCount);
        try
        {
            Encoding.UTF8.GetBytes(value, utf8);
            return Compress(utf8, compressionLevel);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
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
        int byteCount = Encoding.UTF8.GetByteCount(value);
        byte[]? rented = null;
        Span<byte> utf8 = byteCount <= 1024 ? stackalloc byte[byteCount] : (rented = ArrayPool<byte>.Shared.Rent(byteCount)).AsSpan(0, byteCount);
        try
        {
            Encoding.UTF8.GetBytes(value, utf8);
            return TryCompress(utf8, destination, out written, compressionLevel);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public bool TryCompress(ReadOnlySpan<byte> source, Span<byte> destination, out int written, int compressionLevel = 3)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(compressionLevel, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(compressionLevel, 22);

        using var encoder = new ZstandardEncoder(new ZstandardCompressionOptions
        {
            Quality = compressionLevel,
            AppendChecksum = true
        });
        encoder.SetSourceLength(source.Length);
        OperationStatus status = encoder.Compress(source, destination, out int consumed, out written, isFinalBlock: true);
        if (status == OperationStatus.Done && consumed == source.Length)
            return true;

        written = 0;
        if (status == OperationStatus.DestinationTooSmall)
            return false;

        throw new InvalidOperationException("Zstandard compression did not complete.");
    }

    public string DecompressToString(ReadOnlySpan<byte> compressed) => Encoding.UTF8.GetString(Decompress(compressed));

    public byte[] Decompress(ReadOnlySpan<byte> compressed)
    {
        if (compressed.IsEmpty)
            return Array.Empty<byte>();

        using var decoder = new ZstandardDecoder();
        using var output = new MemoryStream();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            while (true)
            {
                OperationStatus status = decoder.Decompress(compressed, buffer, out int consumed, out int written);
                compressed = compressed[consumed..];
                output.Write(buffer.AsSpan(0, written));
                if (status == OperationStatus.Done)
                {
                    if (compressed.IsEmpty)
                        return output.ToArray();
                    decoder.Reset();
                }
                else if (status == OperationStatus.InvalidData ||
                         status == OperationStatus.NeedMoreData && compressed.IsEmpty ||
                         consumed == 0 && written == 0)
                {
                    throw new InvalidDataException("Invalid or truncated Zstandard frame.");
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void Decompress(ReadOnlySpan<byte> compressed, Span<byte> destination)
    {
        if (!TryDecompress(compressed, destination, out _))
            throw new InvalidOperationException("Destination capacity is insufficient for decompressed output.");
    }

    public bool TryDecompress(ReadOnlySpan<byte> compressed, Span<byte> destination, out int written)
    {
        written = 0;
        if (compressed.IsEmpty)
            return true;

        using var decoder = new ZstandardDecoder();
        Span<byte> overflow = stackalloc byte[1];
        var total = 0;
        while (true)
        {
            // A nonempty scratch span lets the decoder consume checksums and empty frames
            // even when the caller's destination is exactly full.
            bool full = destination.IsEmpty;
            OperationStatus status = decoder.Decompress(compressed, full ? overflow : destination, out int consumed, out int count);
            if (status == OperationStatus.InvalidData)
                throw new InvalidDataException("Invalid Zstandard frame.");
            if (full && count != 0)
                return false;

            compressed = compressed[consumed..];
            destination = destination[count..];
            total += count;
            if (status == OperationStatus.Done)
            {
                if (compressed.IsEmpty)
                {
                    written = total;
                    return true;
                }
                decoder.Reset();
            }
            else if (status == OperationStatus.NeedMoreData && compressed.IsEmpty || consumed == 0 && count == 0)
            {
                throw new InvalidDataException("Invalid or truncated Zstandard frame.");
            }
        }
    }

    public async ValueTask CompressFile(string sourceFilePath, string destinationFilePath, int compressionLevel = 3, CancellationToken cancellationToken = default)
    {
        byte[] source = await _fileUtil.ReadToBytes(sourceFilePath, log: false, cancellationToken).NoSync();
        byte[] compressed = Compress(source, compressionLevel);
        await _fileUtil.WriteAtomically(destinationFilePath, compressed, log: false, cancellationToken).NoSync();
    }

    public async ValueTask DecompressFile(string sourceFilePath, string destinationFilePath, CancellationToken cancellationToken = default)
    {
        byte[] compressed = await _fileUtil.ReadToBytes(sourceFilePath, log: false, cancellationToken).NoSync();
        byte[] decompressed = Decompress(compressed);
        await _fileUtil.WriteAtomically(destinationFilePath, decompressed, log: false, cancellationToken).NoSync();
    }
}
