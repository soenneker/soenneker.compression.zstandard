[![](https://img.shields.io/nuget/v/soenneker.compression.zstandard.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.compression.zstandard/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.compression.zstandard/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.compression.zstandard/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.compression.zstandard.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.compression.zstandard/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.compression.zstandard/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.compression.zstandard/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Compression.Zstandard
.NET 11 Zstandard compression and decompression for in-memory values and files.

## Install

```bash
dotnet add package Soenneker.Compression.Zstandard
```

## Codec support

This package uses the built-in .NET 11 `System.IO.Compression` Zstandard encoder and decoder.

- Encoding emits checksummed `.zst` frames.
- Decoding supports compressed, raw, and run-length encoded blocks, concatenated frames, and skippable frames, with checksum and content-size validation. Dictionary-dependent frames are not supported by this utility.
- `compressionLevel` accepts 1 through 22 (default 3); higher levels favor compression ratio over speed.
- Browser/WebAssembly is not supported by the built-in codec.

## Registration

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soenneker.Compression.Zstandard.Registrars;

services.AddZstandardUtilAsSingleton();
```

Use `AddZstandardUtilAsScoped()` when its lifetime should follow a dependency-injection scope.

## Usage

```csharp
using Soenneker.Compression.Zstandard.Abstract;

byte[] compressed = zstandardUtil.Compress(data);
byte[] decompressed = zstandardUtil.Decompress(compressed);
```

For caller-owned buffers:

```csharp
int max = zstandardUtil.GetMaxCompressedLength(source.Length);
Span<byte> compressed = max <= 4096 ? stackalloc byte[max] : new byte[max];

if (zstandardUtil.TryCompress(source, compressed, out int compressedBytes))
{
    Span<byte> decompressed = new byte[source.Length];
    bool decoded = zstandardUtil.TryDecompress(compressed[..compressedBytes], decompressed, out int decompressedBytes);
}
```

`TryCompress` and `TryDecompress` return `false` when the destination is too small. Invalid or truncated frames throw an exception.

## Files

```csharp
await zstandardUtil.CompressFile("events.json", "events.json.zst", cancellationToken: cancellationToken);
await zstandardUtil.DecompressFile("events.json.zst", "events.json", cancellationToken);
```

File methods buffer the complete input and output in memory. They write through a sibling temporary file so an existing destination is replaced only after the codec operation succeeds. The destination directory must already exist.

When decoding untrusted input, prefer the span overload with an application-defined destination limit. The allocating overload and file decoder can allocate according to the expanded frame data.
