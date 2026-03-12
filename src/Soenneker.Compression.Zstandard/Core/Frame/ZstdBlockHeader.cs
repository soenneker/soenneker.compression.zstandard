namespace Soenneker.Compression.Zstandard.Core.Frame;

internal readonly record struct ZstdBlockHeader(bool IsLastBlock, ZstdBlockType BlockType, int BlockSize);
