using System;
using System.Numerics;
using Soenneker.Compression.Zstandard.Core.Errors;

namespace Soenneker.Compression.Zstandard.Core.Entropy;

internal ref struct ZstdHuffmanTable
{
    private readonly Span<ushort> _entries;
    private int _log;
    public bool HasTable => _log != 0;

    public ZstdHuffmanTable(Span<ushort> entries) => _entries = entries;

    private void Build(scoped ReadOnlySpan<byte> weights, int log)
    {
        _log = log;

        int cursor = 0;
        for (int weight = 1; weight <= log; weight++)
            for (int symbol = 0; symbol < weights.Length; symbol++)
                if (weights[symbol] == weight)
                {
                    int width = 1 << (weight - 1);
                    _entries.Slice(cursor, width).Fill((ushort)(symbol | ((log + 1 - weight) << 8)));
                    cursor += width;
                }
        if (cursor != 1 << log) throw new ZstdCodecException("Incomplete Huffman tree.");
    }

    public void Read(ReadOnlySpan<byte> source, ref int cursor)
    {
        if (cursor >= source.Length) throw new ZstdCodecException("Missing Huffman header.");
        int header = source[cursor++];
        Span<byte> weights = stackalloc byte[256];
        int count;
        if (header >= 128)
        {
            count = header - 127;
            int bytes = (count + 1) / 2;
            if (bytes > source.Length - cursor) throw new ZstdCodecException("Truncated Huffman weights.");
            for (int i = 0; i < count; i++) weights[i] = (byte)((source[cursor + i / 2] >> ((i & 1) == 0 ? 4 : 0)) & 15);
            cursor += bytes;
        }
        else
        {
            if (header == 0 || header > source.Length - cursor) throw new ZstdCodecException("Invalid Huffman weights size.");
            ReadOnlySpan<byte> packed = source.Slice(cursor, header); cursor += header;
            int offset = 0;
            ZstdFseTable table = ZstdFseTable.Read(packed, ref offset, 11, 6);
            var bits = new ZstdBitReader(packed.Slice(offset));
            int state = (int)bits.Read(table.Log), other = (int)bits.Read(table.Log);
            count = 0;
            while (true)
            {
                if (count >= 254) throw new ZstdCodecException("Too many Huffman weights.");
                ZstdFseEntry entry = table.Entries[state];
                weights[count++] = entry.Symbol;
                if (entry.Bits > bits.Remaining) { weights[count++] = table.Entries[other].Symbol; break; }
                int next = entry.Base + (int)bits.Read(entry.Bits);
                state = other; other = next;
            }
        }
        int sum = 0, ones = 0;
        for (int i = 0; i < count; i++)
        {
            int w = weights[i];
            if (w > 11) throw new ZstdCodecException("Invalid Huffman weight.");
            if (w > 0) sum += 1 << (w - 1);
            if (w == 1) ones++;
        }
        if (sum == 0) throw new ZstdCodecException("Empty Huffman tree.");
        int log = BitOperations.Log2((uint)sum) + 1;
        int remainder = (1 << log) - sum;
        if (log > 11 || !BitOperations.IsPow2(remainder)) throw new ZstdCodecException("Invalid Huffman tree total.");
        weights[count++] = (byte)(BitOperations.Log2((uint)remainder) + 1);
        if (remainder == 1) ones++;
        if (ones < 2 || (ones & 1) != 0) throw new ZstdCodecException("Invalid Huffman leaf weights.");
        Build(weights.Slice(0, count), log);
    }

    public void Decode(ReadOnlySpan<byte> source, Span<byte> output)
    {
        var bits = new ZstdBitReader(source);
        for (int i = 0; i < output.Length; i++)
        {
            int index = bits.PeekPadded(_log);
            ushort entry = _entries[index];
            bits.Skip(entry >> 8);
            output[i] = (byte)entry;
        }
        if (bits.Remaining != 0) throw new ZstdCodecException("Unconsumed Huffman bits.");
    }
}
