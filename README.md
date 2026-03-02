[![](https://img.shields.io/nuget/v/soenneker.compression.zstandard.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.compression.zstandard/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.compression.zstandard/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.compression.zstandard/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.compression.zstandard.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.compression.zstandard/)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Compression.Zstandard
### A utility library for Zstandard compression and decompression

## Installation

```
dotnet add package Soenneker.Compression.Zstandard
```

## Implementation notes

This package is a fully managed C# Zstandard implementation (no native `libzstd`, no external binaries).

Current codec status:
- Strict Zstandard frame format writing/reading with checksum support.
- Compression emits valid `.zst` frames using fast RAW/RLE block paths (single-threaded).
- Decompression supports RAW/RLE frame blocks and validates frame checksums.
- Compressed entropy-coded blocks are not implemented yet.

## Usage

```csharp
using Soenneker.Compression.Zstandard.Abstract;

// via DI
byte[] compressed = zstandardUtil.Compress(data);
byte[] decompressed = zstandardUtil.Decompress(compressed);
```

Allocation-free hot path:

```csharp
int max = zstandardUtil.GetMaxCompressedLength(source.Length);
Span<byte> compressed = max <= 4096 ? stackalloc byte[max] : new byte[max];

if (zstandardUtil.TryCompress(source, compressed, out int compressedBytes))
{
    Span<byte> decompressed = new byte[source.Length];
    zstandardUtil.Decompress(compressed[..compressedBytes], decompressed);
}
```
