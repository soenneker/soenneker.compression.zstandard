using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Compression.Zstandard.Abstract;

/// <summary>
/// A utility for Zstandard compression and decompression of in-memory data and files.
/// </summary>
public interface IZstandardUtil
{
    /// <summary>
    /// Gets the maximum length in bytes of compressed output for a given source length.
    /// </summary>
    /// <param name="sourceLength">Length in bytes of the uncompressed source data.</param>
    /// <returns>Maximum number of bytes required for the compressed output.</returns>
    [Pure]
    int GetMaxCompressedLength(int sourceLength);

    /// <summary>
    /// Compresses a UTF-8 string and returns the compressed bytes.
    /// </summary>
    /// <param name="value">The string to compress.</param>
    /// <param name="compressionLevel">Compression level (default is 3).</param>
    /// <returns>Compressed bytes.</returns>
    [Pure]
    byte[] Compress(string value, int compressionLevel = 3);

    /// <summary>
    /// Compresses the source bytes and returns the compressed output.
    /// </summary>
    /// <param name="source">Uncompressed source data.</param>
    /// <param name="compressionLevel">Compression level (default is 3).</param>
    /// <returns>Compressed bytes.</returns>
    [Pure]
    byte[] Compress(ReadOnlySpan<byte> source, int compressionLevel = 3);

    /// <summary>
    /// Tries to compress a UTF-8 string into the destination buffer.
    /// </summary>
    /// <param name="value">The string to compress.</param>
    /// <param name="destination">Buffer to write compressed data into.</param>
    /// <param name="written">Number of bytes written when successful.</param>
    /// <param name="compressionLevel">Compression level (default is 3).</param>
    /// <returns><c>true</c> if compression succeeded; otherwise <c>false</c>.</returns>
    [Pure]
    bool TryCompress(string value, Span<byte> destination, out int written, int compressionLevel = 3);

    /// <summary>
    /// Tries to compress the source bytes into the destination buffer.
    /// </summary>
    /// <param name="source">Uncompressed source data.</param>
    /// <param name="destination">Buffer to write compressed data into.</param>
    /// <param name="written">Number of bytes written when successful.</param>
    /// <param name="compressionLevel">Compression level (default is 3).</param>
    /// <returns><c>true</c> if compression succeeded; otherwise <c>false</c>.</returns>
    [Pure]
    bool TryCompress(ReadOnlySpan<byte> source, Span<byte> destination, out int written, int compressionLevel = 3);

    /// <summary>
    /// Decompresses the data and decodes the result as a UTF-8 string.
    /// </summary>
    /// <param name="compressed">Compressed bytes.</param>
    /// <returns>Decompressed string.</returns>
    [Pure]
    string DecompressToString(ReadOnlySpan<byte> compressed);

    /// <summary>
    /// Decompresses the compressed data and returns the raw bytes.
    /// </summary>
    /// <param name="compressed">Compressed bytes.</param>
    /// <returns>Decompressed bytes.</returns>
    [Pure]
    byte[] Decompress(ReadOnlySpan<byte> compressed);

    /// <summary>
    /// Decompresses the compressed data into the destination buffer.
    /// </summary>
    /// <param name="compressed">Compressed bytes.</param>
    /// <param name="destination">Buffer to write decompressed data into.</param>
    [Pure]
    void Decompress(ReadOnlySpan<byte> compressed, Span<byte> destination);

    /// <summary>
    /// Tries to decompress the compressed data into the destination buffer.
    /// </summary>
    /// <param name="compressed">Compressed bytes.</param>
    /// <param name="destination">Buffer to write decompressed data into.</param>
    /// <param name="written">Number of bytes written when successful.</param>
    /// <returns><c>true</c> if decompression succeeded; otherwise <c>false</c>.</returns>
    [Pure]
    bool TryDecompress(ReadOnlySpan<byte> compressed, Span<byte> destination, out int written);

    /// <summary>
    /// Asynchronously reads a file, compresses its contents, and writes the result to the destination path.
    /// </summary>
    /// <param name="sourceFilePath">Path of the file to compress.</param>
    /// <param name="destinationFilePath">Path where the compressed file will be written.</param>
    /// <param name="compressionLevel">Compression level (default is 3).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A value task that completes when the file has been compressed and written.</returns>
    ValueTask CompressFile(string sourceFilePath, string destinationFilePath, int compressionLevel = 3, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously reads a compressed file, decompresses its contents, and writes the result to the destination path.
    /// </summary>
    /// <param name="sourceFilePath">Path of the compressed file.</param>
    /// <param name="destinationFilePath">Path where the decompressed file will be written.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A value task that completes when the file has been decompressed and written.</returns>
    ValueTask DecompressFile(string sourceFilePath, string destinationFilePath, CancellationToken cancellationToken = default);
}
