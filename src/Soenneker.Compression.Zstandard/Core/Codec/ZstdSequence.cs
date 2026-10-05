namespace Soenneker.Compression.Zstandard.Core.Codec;

internal readonly record struct ZstdSequence(int Literals, int Match, int Offset);
