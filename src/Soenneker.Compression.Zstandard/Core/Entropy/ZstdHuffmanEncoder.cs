using System;
using System.Buffers.Binary;

namespace Soenneker.Compression.Zstandard.Core.Entropy;

internal static class ZstdHuffmanEncoder
{
    // Direct weight headers cover ASCII plus symbol 128. Other alphabets retain raw literals.
    public static int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length < 128) return 0;
        Span<int> frequencies = stackalloc int[257]; frequencies.Clear();
        Span<int> parents = stackalloc int[257]; parents.Fill(-1);
        Span<int> heap = stackalloc int[129];
        Span<byte> lengths = stackalloc byte[129]; lengths.Clear();
        Span<ushort> codes = stackalloc ushort[129];
        int highest = 0, count = 0;
        foreach (byte symbol in source)
        {
            if (symbol > 128) return 0;
            frequencies[symbol]++; highest = Math.Max(highest, symbol);
        }
        for (int symbol = 0; symbol <= highest; symbol++)
            if (frequencies[symbol] != 0) Push(heap, ref count, symbol, frequencies);
        if (count < 2) return 0;
        int next = 129;
        while (count > 1)
        {
            int a = Pop(heap, ref count, frequencies), b = Pop(heap, ref count, frequencies);
            frequencies[next] = frequencies[a] + frequencies[b];
            parents[a] = parents[b] = next;
            Push(heap, ref count, next++, frequencies);
        }
        int log = 0;
        for (int symbol = 0; symbol <= highest; symbol++)
        {
            if (frequencies[symbol] == 0) continue;
            int length = 0;
            for (int n = symbol; parents[n] >= 0; n = parents[n]) length++;
            if (length > 11) return 0;
            lengths[symbol] = (byte)length; log = Math.Max(log, length);
        }
        int code = 0;
        for (int length = log; length >= 1; length--)
            for (int symbol = 0; symbol <= highest; symbol++)
                if (lengths[symbol] == length)
                {
                    codes[symbol] = (ushort)(code >> (log - length));
                    code += 1 << (log - length);
                }
        bool four = source.Length >= 1024;
        int headerBytes = !four ? 3 : source.Length < 16384 ? 4 : 5;
        int cursor = headerBytes;
        destination[cursor++] = (byte)(127 + highest);
        for (int symbol = 0; symbol < highest; symbol += 2)
        {
            int a = lengths[symbol] == 0 ? 0 : log + 1 - lengths[symbol];
            int b = symbol + 1 >= highest || lengths[symbol + 1] == 0 ? 0 : log + 1 - lengths[symbol + 1];
            destination[cursor++] = (byte)((a << 4) | b);
        }
        if (four)
        {
            int jump = cursor; cursor += 6;
            int segment = (source.Length + 3) / 4;
            for (int i = 0; i < 4; i++)
            {
                int size = Encode(source.Slice(i * segment, i == 3 ? source.Length - i * segment : segment), destination.Slice(cursor), lengths, codes);
                if (i < 3) BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(jump + i * 2), (ushort)size);
                cursor += size;
            }
        }
        else cursor += Encode(source, destination.Slice(cursor), lengths, codes);
        int packed = cursor - headerBytes;
        if (cursor >= source.Length || (!four && packed >= 1024) || (headerBytes == 4 && packed >= 16384)) return 0;
        int width = headerBytes == 3 ? 10 : headerBytes == 4 ? 14 : 18;
        int format = headerBytes == 3 ? 0 : headerBytes == 4 ? 2 : 3;
        ulong header = 2u | ((uint)format << 2) | ((ulong)source.Length << 4) | ((ulong)packed << (4 + width));
        for (int i = 0; i < headerBytes; i++) destination[i] = (byte)(header >> (8 * i));
        return cursor;
    }

    private static int Encode(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> lengths, ReadOnlySpan<ushort> codes)
    {
        var bits = new ZstdBitWriter(destination);
        for (int i = source.Length - 1; i >= 0; i--) bits.Write(codes[source[i]], lengths[source[i]]);
        return bits.Finish();
    }

    private static void Push(Span<int> heap, ref int count, int node, ReadOnlySpan<int> frequencies)
    {
        int index = count++;
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (frequencies[heap[parent]] <= frequencies[node]) break;
            heap[index] = heap[parent]; index = parent;
        }
        heap[index] = node;
    }

    private static int Pop(Span<int> heap, ref int count, ReadOnlySpan<int> frequencies)
    {
        int result = heap[0], node = heap[--count], index = 0;
        while (index * 2 + 1 < count)
        {
            int child = index * 2 + 1;
            if (child + 1 < count && frequencies[heap[child + 1]] < frequencies[heap[child]]) child++;
            if (frequencies[node] <= frequencies[heap[child]]) break;
            heap[index] = heap[child]; index = child;
        }
        if (count > 0) heap[index] = node;
        return result;
    }
}
