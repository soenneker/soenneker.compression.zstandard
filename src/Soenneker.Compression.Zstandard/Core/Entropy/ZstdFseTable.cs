using System;
using System.Numerics;
using Soenneker.Compression.Zstandard.Core.Errors;

namespace Soenneker.Compression.Zstandard.Core.Entropy;

// Sequence coding tables follow the Zstandard format specification (facebook/zstd/doc).
internal sealed class ZstdFseTable
{
    public int Log { get; }
    public ZstdFseEntry[] Entries { get; }
    private readonly ushort[] _encoding;

    public ZstdFseTable(ReadOnlySpan<short> counts, int log, bool buildEncoding = true)
    {
        Log = log;
        int size = 1 << log;
        Entries = new ZstdFseEntry[size];
        _encoding = buildEncoding ? new ushort[counts.Length * size] : [];
        Array.Fill(_encoding, ushort.MaxValue);
        Span<byte> symbols = stackalloc byte[size];
        Span<int> next = stackalloc int[counts.Length];
        int high = size - 1, total = 0;
        for (int s = 0; s < counts.Length; s++)
        {
            int n = counts[s];
            if (n < -1) throw new ZstdCodecException("Invalid FSE probability.");
            total += Math.Abs(n);
            if (n == -1) { symbols[high--] = (byte)s; next[s] = 1; }
            else next[s] = n;
        }
        if (total != size) throw new ZstdCodecException("Invalid FSE probability total.");
        int position = 0, step = (size >> 1) + (size >> 3) + 3;
        for (int s = 0; s < counts.Length; s++)
            for (int n = 0; n < counts[s]; n++)
            {
                symbols[position] = (byte)s;
                do { position = (position + step) & (size - 1); } while (position > high);
            }
        if (position != 0) throw new ZstdCodecException("Invalid FSE distribution.");
        for (int state = 0; state < size; state++)
        {
            byte symbol = symbols[state];
            int n = next[symbol]++;
            int bits = log - BitOperations.Log2((uint)n);
            int baseline = (n << bits) - size;
            Entries[state] = new(symbol, (byte)bits, (ushort)baseline);
            for (int value = baseline; buildEncoding && value < baseline + (1 << bits); value++)
                _encoding[symbol * size + value] = (ushort)state;
        }
    }

    public static ZstdFseTable Read(ReadOnlySpan<byte> source, ref int cursor, int maxSymbol, int maxLog)
    {
        var bits = new ZstdForwardBits(source.Slice(cursor));
        int log = bits.Read(4) + 5;
        if (log > maxLog) throw new ZstdCodecException("FSE accuracy exceeds limit.");
        Span<short> counts = stackalloc short[maxSymbol + 1];
        counts.Clear();
        int remaining = 1 << log, symbol = 0, present = 0;
        while (remaining > 0)
        {
            if (symbol > maxSymbol) throw new ZstdCodecException("FSE symbol exceeds limit.");
            int width = BitOperations.Log2((uint)(remaining + 1)) + 1;
            int cutoff = (1 << width) - 2 - remaining;
            int value = bits.Read(width - 1);
            if (value >= cutoff)
            {
                value += bits.Read(1) << (width - 1);
                if (value >= (1 << (width - 1))) value -= cutoff;
            }
            int count = value - 1;
            counts[symbol++] = (short)count;
            remaining -= Math.Abs(count);
            if (count != 0) present++;
            if (remaining < 0) throw new ZstdCodecException("Invalid FSE probability total.");
            if (count == 0)
            {
                int skip;
                do { skip = bits.Read(2); symbol += skip; if (symbol > maxSymbol) throw new ZstdCodecException("FSE zero run exceeds symbols."); } while (skip == 3);
            }
        }
        if (present < 2) throw new ZstdCodecException("FSE distribution requires multiple symbols.");
        cursor += bits.BytesRead;
        return new ZstdFseTable(counts, log, buildEncoding: false);
    }

    public int Initial(int symbol) => _encoding[symbol * Entries.Length];

    public int Encode(int symbol, int next, ref ZstdBitWriter writer)
    {
        int state = _encoding[symbol * Entries.Length + next];
        if (state == ushort.MaxValue) throw new ZstdCodecException("Unrepresentable FSE symbol.");
        ZstdFseEntry entry = Entries[state];
        writer.Write((uint)(next - entry.Base), entry.Bits);
        return state;
    }
}
