namespace Soenneker.Compression.Zstandard.Core.Frame;

internal readonly record struct ZstdFrameHeader(
    bool SingleSegment,
    bool HasChecksum,
    ulong? FrameContentSize,
    byte Descriptor,
    byte? WindowDescriptor)
{
    public ulong WindowSize
    {
        get
        {
            if (SingleSegment) return FrameContentSize ?? 0;
            int descriptor = WindowDescriptor ?? 0;
            ulong basis = 1UL << (10 + (descriptor >> 3));
            return basis + (basis >> 3) * (ulong)(descriptor & 7);
        }
    }
}
