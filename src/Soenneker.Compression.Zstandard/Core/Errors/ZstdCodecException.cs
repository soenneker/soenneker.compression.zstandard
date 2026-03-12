using System;

namespace Soenneker.Compression.Zstandard.Core.Errors;

internal sealed class ZstdCodecException : Exception
{
    public ZstdCodecException(string message) : base(message)
    {
    }
}
