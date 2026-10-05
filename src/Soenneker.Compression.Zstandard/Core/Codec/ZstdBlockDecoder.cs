using System;
using System.Buffers;
using System.Buffers.Binary;
using Soenneker.Compression.Zstandard.Core.Entropy;
using Soenneker.Compression.Zstandard.Core.Errors;
using Soenneker.Compression.Zstandard.Core.Constants;

namespace Soenneker.Compression.Zstandard.Core.Codec;

internal struct ZstdBlockDecoder
{
    private ZstdFseTable? _ll, _ml, _of;
    private ZstdHuffmanTable? _huffman;
    private readonly int _windowSize;
    private readonly int _blockLimit;
    private int _repeat1, _repeat2, _repeat3;

    public ZstdBlockDecoder(int windowSize)
    {
        _repeat1 = 1; _repeat2 = 4; _repeat3 = 8;
        _windowSize = windowSize;
        _blockLimit = Math.Min(windowSize, ZstdConstants.MaxBlockSize);
    }

    public bool Decode(ReadOnlySpan<byte> source, Span<byte> output, int start, out int written)
    {
        byte[]? scratch = !source.IsEmpty && (source[0] & 3) >= 2 ? ArrayPool<byte>.Shared.Rent(ZstdConstants.MaxBlockSize) : null;
        try { return DecodeCore(source, output, start, scratch, out written); }
        finally { if (scratch != null) ArrayPool<byte>.Shared.Return(scratch, clearArray: true); }
    }

    private bool DecodeCore(ReadOnlySpan<byte> source, Span<byte> output, int start, Span<byte> scratch, out int written)
    {
        written = 0;
        int cursor = 0;
        int first = Take(source, ref cursor);
        int type = first & 3, format = (first >> 2) & 3;
        int literalSize;
        ReadOnlySpan<byte> literals;
        if (type >= 2)
        {
            int headerBytes = format < 2 ? 3 : format == 2 ? 4 : 5;
            ulong header = (uint)first;
            for (int i = 1; i < headerBytes; i++) header |= (ulong)Take(source, ref cursor) << (8 * i);
            int width = format < 2 ? 10 : format == 2 ? 14 : 18;
            literalSize = (int)((header >> 4) & ((1u << width) - 1));
            int packedSize = (int)(header >> (4 + width));
            if (literalSize > _blockLimit || packedSize == 0 || packedSize > source.Length - cursor) throw new ZstdCodecException("Invalid Huffman literals size.");
            ReadOnlySpan<byte> packed = source.Slice(cursor, packedSize); cursor += packedSize;
            int offset = 0;
            if (type == 2) _huffman = ZstdHuffmanTable.Read(packed, ref offset);
            if (_huffman == null) throw new ZstdCodecException("Missing previous Huffman tree.");
            if (format == 0) _huffman.Decode(packed.Slice(offset), scratch.Slice(0, literalSize));
            else
            {
                if (literalSize < 6 || packed.Length - offset < 10) throw new ZstdCodecException("Invalid Huffman jump table.");
                int a = BinaryPrimitives.ReadUInt16LittleEndian(packed.Slice(offset));
                int b = BinaryPrimitives.ReadUInt16LittleEndian(packed.Slice(offset + 2));
                int c = BinaryPrimitives.ReadUInt16LittleEndian(packed.Slice(offset + 4));
                offset += 6;
                if (a == 0 || b == 0 || c == 0 || a + b + c >= packed.Length - offset) throw new ZstdCodecException("Invalid Huffman stream sizes.");
                int segment = (literalSize + 3) / 4;
                _huffman.Decode(packed.Slice(offset, a), scratch.Slice(0, segment)); offset += a;
                _huffman.Decode(packed.Slice(offset, b), scratch.Slice(segment, segment)); offset += b;
                _huffman.Decode(packed.Slice(offset, c), scratch.Slice(segment * 2, segment)); offset += c;
                _huffman.Decode(packed.Slice(offset), scratch.Slice(segment * 3, literalSize - segment * 3));
            }
            literals = scratch.Slice(0, literalSize);
        }
        else
        {
            literalSize = format is 0 or 2 ? first >> 3 : format == 1 ? (first >> 4) + (Take(source, ref cursor) << 4) :
                (first >> 4) + (Take(source, ref cursor) << 4) + (Take(source, ref cursor) << 12);
            if (literalSize > _blockLimit) throw new ZstdCodecException("Literals exceed block limit.");
            int stored = type == 1 ? 1 : literalSize;
            if (stored > source.Length - cursor) throw new ZstdCodecException("Truncated literals.");
            literals = source.Slice(cursor, stored); cursor += stored;
        }
        int sequences = Take(source, ref cursor);
        if (sequences == 255) sequences = Take(source, ref cursor) + (Take(source, ref cursor) << 8) + 32512;
        else if (sequences >= 128) sequences = ((sequences - 128) << 8) + Take(source, ref cursor);
        int position = start, literalPosition = 0;
        if (sequences > 0)
        {
            int modes = Take(source, ref cursor);
            if ((modes & 3) != 0) throw new ZstdCodecException("Reserved sequence mode bits.");
            _ll = Table(modes >> 6, source, ref cursor, _ll, ZstdSequenceCodes.Literals, 35);
            _of = Table((modes >> 4) & 3, source, ref cursor, _of, ZstdSequenceCodes.Offsets, 31);
            _ml = Table((modes >> 2) & 3, source, ref cursor, _ml, ZstdSequenceCodes.Matches, 52);
            var bits = new ZstdBitReader(source.Slice(cursor));
            int ls = (int)bits.Read(_ll.Log), os = (int)bits.Read(_of.Log), ms = (int)bits.Read(_ml.Log);
            for (int i = 0; i < sequences; i++)
            {
                ZstdFseEntry le = _ll.Entries[ls], oe = _of.Entries[os], me = _ml.Entries[ms];
                long offsetValue = (1L << oe.Symbol) + bits.Read(oe.Symbol);
                int match = ZstdSequenceCodes.MatchBases[me.Symbol] + (int)bits.Read(ZstdSequenceCodes.MatchBits[me.Symbol]);
                int length = ZstdSequenceCodes.LiteralBases[le.Symbol] + (int)bits.Read(ZstdSequenceCodes.LiteralBits[le.Symbol]);
                if (length > literalSize - literalPosition || length + match > _blockLimit - (position - start)) throw new ZstdCodecException("Invalid sequence length.");
                if (length + match > output.Length - position) return false;
                if (type == 1) output.Slice(position, length).Fill(literals[0]);
                else literals.Slice(literalPosition, length).CopyTo(output.Slice(position));
                literalPosition += length; position += length;
                int offset;
                if (offsetValue > 3)
                {
                    if (offsetValue - 3 > int.MaxValue) throw new ZstdCodecException("Invalid match offset.");
                    offset = (int)offsetValue - 3;
                    _repeat3 = _repeat2; _repeat2 = _repeat1; _repeat1 = offset;
                }
                else
                {
                    int code = (int)offsetValue + (length == 0 ? 1 : 0);
                    offset = code == 1 ? _repeat1 : code == 2 ? _repeat2 : code == 3 ? _repeat3 : _repeat1 - 1;
                    if (code != 1) { if (code != 2) _repeat3 = _repeat2; _repeat2 = _repeat1; _repeat1 = offset; }
                }
                if (offset <= 0 || offset > position || offset > _windowSize) throw new ZstdCodecException("Match offset exceeds frame history.");
                // Forward copy is required for overlapping matches.
                for (int n = 0; n < match; n++) output[position + n] = output[position + n - offset];
                position += match;
                if (i + 1 < sequences)
                {
                    ls = le.Base + (int)bits.Read(le.Bits);
                    ms = me.Base + (int)bits.Read(me.Bits);
                    os = oe.Base + (int)bits.Read(oe.Bits);
                }
            }
            if (bits.Remaining != 0) throw new ZstdCodecException("Unconsumed sequence bits.");
        }
        else if (cursor != source.Length) throw new ZstdCodecException("Trailing bytes after literals-only block.");
        int tail = literalSize - literalPosition;
        if (tail > _blockLimit - (position - start)) throw new ZstdCodecException("Block exceeds size limit.");
        if (tail > output.Length - position) return false;
        if (type == 1) output.Slice(position, tail).Fill(literals[0]);
        else literals.Slice(literalPosition, tail).CopyTo(output.Slice(position));
        written = position + tail - start;
        return true;
    }

    private static int Take(ReadOnlySpan<byte> source, ref int cursor)
    {
        if (cursor >= source.Length) throw new ZstdCodecException("Truncated compressed block.");
        return source[cursor++];
    }

    private static ZstdFseTable Table(int mode, ReadOnlySpan<byte> source, ref int cursor, ZstdFseTable? previous, ZstdFseTable predefined, int maxSymbol)
    {
        if (mode == 0) return predefined;
        if (mode == 3) return previous ?? throw new ZstdCodecException("Missing previous FSE table.");
        if (mode == 2) return ZstdFseTable.Read(source, ref cursor, maxSymbol, maxSymbol == 31 ? 8 : 9);
        int symbol = Take(source, ref cursor);
        if (symbol > maxSymbol) throw new ZstdCodecException("Invalid sequence symbol.");
        Span<short> counts = stackalloc short[maxSymbol + 1];
        counts.Clear(); counts[symbol] = 1;
        return new ZstdFseTable(counts, 0, buildEncoding: false);
    }
}
