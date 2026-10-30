using System.Buffers.Binary;
using System.Text;

namespace PDiffy.Tests.Unit.Pdfs;

/// <summary>
/// A minimal TrueType font program: glyphs with no outlines, their advance widths, and cmap subtables from codes to
/// glyphs.
/// </summary>
internal sealed class TestTrueType
{
    /// <summary>
    /// The advance width of each glyph, glyph 0 (.notdef) first, in units of <see cref="UnitsPerEm"/>.
    /// </summary>
    public int[] Widths { get; init; } = [500, 600, 700];

    public int UnitsPerEm { get; init; } = 1000;

    /// <summary>Each glyph's name in a format 2 post table; null for a format 3 table, which names none.</summary>
    public string[]? GlyphNames { get; init; }

    /// <summary>The cmap subtables: platform, encoding, and each code's glyph.</summary>
    public List<(int Platform, int Encoding, Dictionary<int, int> Glyphs)> Cmaps { get; init; } =
        [(3, 1, new() { [0x41] = 1, [0x42] = 2 })];

    /// <summary>The program as a PDF stream object.</summary>
    public string ToStream()
    {
        var glyphs = Widths.Length;
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["cmap"] = Cmap(),
            ["glyf"] = [],
            ["head"] = Concat(U32(0x00010000), U32(0x00010000), U32(0), U32(0x5F0F3CF5), U16(0), U16(UnitsPerEm),
                new byte[16], U16(0), U16(0), U16(1000), U16(1000), U16(0), U16(8), U16(2), U16(0), U16(0)),
            ["hhea"] = Concat(U32(0x00010000), U16(800), U16(-200 & 0xFFFF), U16(0), U16(Widths.Max()), U16(0), U16(0),
                U16(0), U16(1), U16(0), U16(0), new byte[8], U16(0), U16(glyphs)),
            ["hmtx"] = Concat([.. Widths.Select(width => Concat(U16(width), U16(0)))]),
            // Short offsets, all zero: every glyph is empty.
            ["loca"] = new byte[2 * (glyphs + 1)],
            ["maxp"] = Concat(U32(0x00005000), U16(glyphs)),
            ["post"] = Post(),
        };

        var directory = new List<byte>();
        var data = new List<byte>();
        var start = 12 + 16 * tables.Count;
        foreach (var (tag, bytes) in tables)
        {
            var offset = (uint)(start + data.Count);
            directory.AddRange([.. Encoding.ASCII.GetBytes(tag), .. U32(0), .. U32(offset), .. U32((uint)bytes.Length)]);
            data.AddRange(bytes);
            while (data.Count % 4 != 0) data.Add(0);
        }

        var font = Concat(U32(0x00010000), U16(tables.Count), U16(0), U16(0), U16(0), [.. directory], [.. data]);
        return TestPdf.Stream(Encoding.Latin1.GetString(font));
    }

    // Format 2 gives every glyph a name of its own, after the 258 standard Macintosh ones, as a Pascal string.
    private byte[] Post()
    {
        if (GlyphNames is null) return Concat(U32(0x00030000), new byte[28]);

        var indices = Concat([.. GlyphNames.Select((_, i) => U16(258 + i))]);
        var names = Concat([.. GlyphNames.Select(name => Concat([(byte)name.Length], Encoding.ASCII.GetBytes(name)))]);
        return Concat(U32(0x00020000), new byte[28], U16(GlyphNames.Length), indices, names);
    }

    // Each cmap is format 4, with one segment per code and the closing 0xFFFF segment.
    private byte[] Cmap()
    {
        var subtables = Cmaps.Select(cmap => Format4(cmap.Glyphs)).ToList();
        var header = new List<byte>([.. U16(0), .. U16(Cmaps.Count)]);
        var offset = 4 + 8 * Cmaps.Count;
        for (var i = 0; i < Cmaps.Count; i++)
        {
            header.AddRange([.. U16(Cmaps[i].Platform), .. U16(Cmaps[i].Encoding), .. U32((uint)offset)]);
            offset += subtables[i].Length;
        }

        return Concat([[.. header], .. subtables]);
    }

    private static byte[] Format4(Dictionary<int, int> glyphs)
    {
        var codes = glyphs.Keys.Order().Append(0xFFFF).ToList();
        var count = codes.Count;
        var ends = Concat([.. codes.Select(code => U16(code))]);
        var deltas = Concat([.. codes.Select(code => U16(code == 0xFFFF ? 1 : (glyphs[code] - code) & 0xFFFF))]);
        var body = Concat(U16(2 * count), U16(0), U16(0), U16(0), ends, U16(0), ends, deltas, new byte[2 * count]);
        return Concat(U16(4), U16(6 + body.Length), U16(0), body);
    }

    private static byte[] U16(int value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, (ushort)value);
        return bytes;
    }

    private static byte[] U32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];
}
