namespace Soenneker.Compression.Zstandard.Core.Constants;

internal static class ZstdConstants
{
    public const uint MagicNumber = 0xFD2FB528;
    public const uint SkippableMagicStart = 0x184D2A50;
    public const uint SkippableMagicMask = 0xFFFFFFF0;
    public const int MaxBlockSize = 1 << 17; // 128 KiB
}
