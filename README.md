[![](https://img.shields.io/nuget/v/soenneker.compression.zstandard.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.compression.zstandard/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.compression.zstandard/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.compression.zstandard/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.compression.zstandard.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.compression.zstandard/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.compression.zstandard/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.compression.zstandard/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Compression.Zstandard
Managed Zstandard frame encoding and decoding for in-memory values and files.

## Install

```bash
dotnet add package Soenneker.Compression.Zstandard
```

## Codec support

This package is a fully managed C# Zstandard implementation (no native `libzstd`, no external binaries).

- Encoding emits standards-compliant `.zst` frames using raw or run-length encoded blocks and includes a checksum.
- Decoding accepts raw and run-length encoded blocks, concatenated frames, and skippable frames, and validates frame checksums and declared content sizes. Dictionary-dependent frames are not supported.
- Entropy-coded compressed blocks are not supported. Most `.zst` files created by general-purpose Zstandard tools therefore cannot be decoded by this package.
- `compressionLevel` is reserved for future entropy encoding and does not change output today.

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

`TryCompress` and `TryDecompress` return `false` when the destination is too small. Invalid frames and unsupported entropy-coded blocks throw a codec exception.

## Files

```csharp
await zstandardUtil.CompressFile("events.json", "events.json.zst", cancellationToken: cancellationToken);
await zstandardUtil.DecompressFile("events.json.zst", "events.json", cancellationToken);
```

File methods buffer the complete input and output in memory. They write through a sibling temporary file so an existing destination is replaced only after the codec operation succeeds. The destination directory must already exist.

When decoding untrusted input, prefer the span overload with an application-defined destination limit. The allocating overload and file decoder can allocate according to the expanded frame data.
