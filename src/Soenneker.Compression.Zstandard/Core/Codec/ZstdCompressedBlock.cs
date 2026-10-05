using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using Soenneker.Compression.Zstandard.Core.Entropy;

namespace Soenneker.Compression.Zstandard.Core.Codec;

internal static class ZstdCompressedBlock
{
    public static int Compress(ReadOnlySpan<byte> source, Span<byte> destination, int level)
    {
        int[] heads = ArrayPool<int>.Shared.Rent(65536);
        int[] previous = ArrayPool<int>.Shared.Rent(source.Length);
        byte[] literals = ArrayPool<byte>.Shared.Rent(source.Length);
        ZstdSequence[] sequences = ArrayPool<ZstdSequence>.Shared.Rent(source.Length / 4 + 1);
        try
        {
            heads.AsSpan(0, 65536).Fill(-1);
            int literalCount = 0, sequenceCount = 0, anchor = 0, position = 0;
            int depth = 1 << Math.Min(7, (level - 1) / 3);
            while (position + 4 <= source.Length)
            {
                int hash = Hash(source, position);
                int candidate = heads[hash];
                previous[position] = candidate;
                heads[hash] = position;
                int best = 3, offset = 0;
                for (int attempt = 0; candidate >= 0 && attempt < depth; attempt++, candidate = previous[candidate])
                {
                    if (BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(candidate)) != BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(position))) continue;
                    int length = 4;
                    while (position + length + 8 <= source.Length &&
                        BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(candidate + length)) == BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(position + length))) length += 8;
                    while (position + length < source.Length && source[candidate + length] == source[position + length]) length++;
                    if (length > best) { best = length; offset = position - candidate; }
                    if (position + best == source.Length) break;
                }
                if (offset == 0) { position++; continue; }
                int ll = position - anchor;
                source.Slice(anchor, ll).CopyTo(literals.AsSpan(literalCount));
                literalCount += ll;
                sequences[sequenceCount++] = new(ll, best, offset);
                int end = position + best;
                for (int insert = position + 1; insert < end && insert + 4 <= source.Length; insert++)
                {
                    int h = Hash(source, insert); previous[insert] = heads[h]; heads[h] = insert;
                }
                position = anchor = end;
            }
            source.Slice(anchor).CopyTo(literals.AsSpan(literalCount));
            literalCount += source.Length - anchor;
            if (sequenceCount == 0) return 0;
            // Keep Huffman literals only when they save space; sequence tables are predefined.
            int cursor = ZstdHuffmanEncoder.Compress(literals.AsSpan(0, literalCount), destination);
            if (cursor == 0)
            {
                if (literalCount < 32) { destination[0] = (byte)(literalCount << 3); cursor = 1; }
                else if (literalCount < 4096) { BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)((literalCount << 4) | 4)); cursor = 2; }
                else { int h = (literalCount << 4) | 12; destination[0] = (byte)h; destination[1] = (byte)(h >> 8); destination[2] = (byte)(h >> 16); cursor = 3; }
                literals.AsSpan(0, literalCount).CopyTo(destination.Slice(cursor)); cursor += literalCount;
            }
            if (sequenceCount < 128) destination[cursor++] = (byte)sequenceCount;
            else if (sequenceCount < 32512) { destination[cursor++] = (byte)((sequenceCount >> 8) + 128); destination[cursor++] = (byte)sequenceCount; }
            else { destination[cursor++] = 255; BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(cursor), (ushort)(sequenceCount - 32512)); cursor += 2; }
            destination[cursor++] = 0;
            var writer = new ZstdBitWriter(destination.Slice(cursor));
            int llState = 0, mlState = 0, ofState = 0;
            for (int i = sequenceCount - 1; i >= 0; i--)
            {
                ZstdSequence sequence = sequences[i];
                int ll = ZstdSequenceCodes.Code(sequence.Literals, ZstdSequenceCodes.LiteralBases);
                int ml = ZstdSequenceCodes.Code(sequence.Match, ZstdSequenceCodes.MatchBases);
                int of = BitOperations.Log2((uint)(sequence.Offset + 3));
                if (i == sequenceCount - 1)
                {
                    llState = ZstdSequenceCodes.Literals.Initial(ll);
                    mlState = ZstdSequenceCodes.Matches.Initial(ml);
                    ofState = ZstdSequenceCodes.Offsets.Initial(of);
                }
                else
                {
                    ofState = ZstdSequenceCodes.Offsets.Encode(of, ofState, ref writer);
                    mlState = ZstdSequenceCodes.Matches.Encode(ml, mlState, ref writer);
                    llState = ZstdSequenceCodes.Literals.Encode(ll, llState, ref writer);
                }
                writer.Write((uint)(sequence.Literals - ZstdSequenceCodes.LiteralBases[ll]), ZstdSequenceCodes.LiteralBits[ll]);
                writer.Write((uint)(sequence.Match - ZstdSequenceCodes.MatchBases[ml]), ZstdSequenceCodes.MatchBits[ml]);
                writer.Write((uint)(sequence.Offset + 3 - (1 << of)), of);
            }
            writer.Write((uint)mlState, ZstdSequenceCodes.Matches.Log);
            writer.Write((uint)ofState, ZstdSequenceCodes.Offsets.Log);
            writer.Write((uint)llState, ZstdSequenceCodes.Literals.Log);
            return cursor + writer.Finish();
        }
        finally
        {
            ArrayPool<int>.Shared.Return(heads);
            ArrayPool<int>.Shared.Return(previous);
            ArrayPool<byte>.Shared.Return(literals, clearArray: true);
            ArrayPool<ZstdSequence>.Shared.Return(sequences);
        }
    }

    private static int Hash(ReadOnlySpan<byte> source, int position) => (int)(unchecked(BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(position)) * 2654435761u) >> 16);
}
