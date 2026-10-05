namespace Soenneker.Compression.Zstandard.Core.Entropy;

internal readonly record struct ZstdFseEntry(byte Symbol, byte Bits, ushort Base);
