namespace Soenneker.Compression.Zstandard.Core.Frame;

internal readonly record struct ZstdFrameHeader(
    bool SingleSegment,
    bool HasChecksum,
    ulong? FrameContentSize,
    byte Descriptor,
    byte? WindowDescriptor);
