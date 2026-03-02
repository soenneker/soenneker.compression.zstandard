namespace Soenneker.Compression.Zstandard.Core.Frame;

internal enum ZstdBlockType : byte
{
    Raw = 0,
    Rle = 1,
    Compressed = 2,
    Reserved = 3
}
