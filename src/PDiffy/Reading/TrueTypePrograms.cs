using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts;
using UglyToad.PdfPig.Fonts.Encodings;
using UglyToad.PdfPig.Fonts.TrueType;
using UglyToad.PdfPig.Fonts.TrueType.Parser;
using UglyToad.PdfPig.Fonts.TrueType.Tables.CMapSubTables;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>
/// What an embedded TrueType program says about the codes a TrueType font, or a TrueType-based CID font, uses: which
/// glyphs it lacks, and where it disagrees with the font's dictionaries.
/// </summary>
internal static class TrueTypePrograms
{
    /// <param name="codes">Every code the font's text uses.</param>
    /// <param name="rendered">The codes of text that's drawn, not invisible (render mode 3), which the width and glyph
    /// rules cover.</param>
    public static (List<int> MissingCodes, List<PdfFontProblem> Problems) Read(PdfObjects objects, DictionaryToken font,
        string? subtype, DictionaryToken? descriptor, IReadOnlyCollection<int> codes, IReadOnlySet<int> rendered)
    {
        var missing = new List<int>();
        var problems = new List<PdfFontProblem>();
        var file = descriptor is null ? null : objects.Get(descriptor, "FontFile2") as StreamToken;
        if (descriptor is null || file is null) return (missing, problems);

        if (Parse(objects.Contents(file)) is not { } program)
        {
            problems.Add(PdfFontProblem.UnreadableProgram);
            return (missing, problems);
        }

        var used = new Codes(codes, rendered);
        if (subtype == PdfFontTypes.TrueType) ReadSimple(objects, font, descriptor, program, used, missing, problems);
        if (subtype == PdfFontTypes.Type0) ReadCid(objects, font, descriptor, program, used, missing, problems);
        return (missing, problems);
    }

    private sealed record Codes(IReadOnlyCollection<int> All, IReadOnlySet<int> Rendered);

    // PdfPig throws many exception types on a damaged program; the caller records it as unreadable.
    private static TrueTypeFont? Parse(byte[]? bytes)
    {
        if (bytes is null) return null;

        try
        {
            return TrueTypeFontParser.Parse(new TrueTypeDataBytes(new MemoryInputBytes(bytes)));
        }
        catch (Exception)
        {
            return null;
        }
    }

    // A non-symbolic font picks a glyph by the Unicode of the code's glyph name in a (3,1) cmap, by its Mac Roman code
    // in a (1,0) one, or by the name in the program's post table; a symbolic font by the code itself in a (3,0) cmap,
    // also at F000-F2FF, or in its only one (ISO 32000-1 9.6.6.4).
    private static void ReadSimple(PdfObjects objects, DictionaryToken font, DictionaryToken descriptor,
        TrueTypeFont program, Codes codes, List<int> missing, List<PdfFontProblem> problems)
    {
        var subtables = program.TableRegister.CMapTable?.SubTables ?? [];
        var unicode = subtables.FirstOrDefault(table => Is(table, 3, 1));
        var macRoman = subtables.FirstOrDefault(table => Is(table, 1, 0));
        var symbol = subtables.FirstOrDefault(table => Is(table, 3, 0));
        Func<int, int> glyphOf;
        if (FontReader.IsSymbolic(objects, descriptor))
        {
            if (subtables.Count != 1 && symbol is null) problems.Add(PdfFontProblem.SymbolicCmap);
            var table = symbol ?? (subtables.Count == 1 ? subtables[0] : macRoman);
            glyphOf = code => table is null ? 0 : symbol is null ? table.CharacterCodeToGlyphIndex(code)
                : new[] { code, 0xF000 + code, 0xF100 + code, 0xF200 + code }
                    .Select(table.CharacterCodeToGlyphIndex).FirstOrDefault(glyph => glyph != 0);
        }
        else
        {
            if (unicode is null && macRoman is null) problems.Add(PdfFontProblem.TrueTypeCmap);
            var nameOf = UnicodeMap.GlyphNames(objects, font, StandardEncoding.Instance);
            IReadOnlyList<string> postNames = program.TableRegister.PostScriptTable?.GlyphNames ?? [];
            glyphOf = code => nameOf(code) is not { } name ? 0
                : unicode is not null && GlyphByUnicode(unicode, name) is var byUnicode and not 0 ? byUnicode
                : macRoman is not null && GlyphByMacRoman(macRoman, name) is var byMac and not 0 ? byMac
                : Math.Max(postNames.ToList().IndexOf(name), 0);
        }

        var firstChar = objects.Integer(font, "FirstChar") ?? 0;
        var widths = objects.Items(font.Data.GetValueOrDefault("Widths"));
        var missingWidth = Number(objects, objects.Get(descriptor, "MissingWidth")) ?? 0;
        double WidthOf(int code) => code - firstChar is var index && index >= 0 && index < widths.Count
            ? Number(objects, widths[index]) ?? 0 : missingWidth;
        Compare(program, codes, glyphOf, WidthOf, missing, problems);
    }

    private static int GlyphByUnicode(ICMapSubTable table, string name) =>
        GlyphList.AdobeGlyphList.NameToUnicode(name) is { Length: > 0 } text
            ? table.CharacterCodeToGlyphIndex(text[0]) : 0;

    private static int GlyphByMacRoman(ICMapSubTable table, string name) =>
        MacRomanEncoding.Instance.ContainsName(name)
            ? table.CharacterCodeToGlyphIndex(MacRomanEncoding.Instance.GetCode(name)) : 0;

    // With an Identity encoding, each code is its CID, so the text's glyphs and widths can be compared. The CIDSet
    // covers the whole program, whatever the encoding.
    private static void ReadCid(PdfObjects objects, DictionaryToken font, DictionaryToken descriptor,
        TrueTypeFont program, Codes codes, List<int> missing, List<PdfFontProblem> problems)
    {
        var descendants = objects.Items(font.Data.GetValueOrDefault("DescendantFonts"));
        if (descendants.Count == 0 || objects.Resolve(descendants[0]) is not DictionaryToken descendant) return;

        var glyphOfCid = MissingGlyphs.GlyphOfCid(objects, descendant);
        if (objects.Name(font, "Encoding") is "Identity-H" or "Identity-V")
        {
            var widths = CidWidths(objects, descendant);
            var defaultWidth = Number(objects, objects.Get(descendant, "DW")) ?? 1000;
            Compare(program, codes, cid => glyphOfCid(cid) ?? 0, cid => widths.GetValueOrDefault(cid, defaultWidth),
                missing, problems);
        }

        // A CIDSet marks each CID the program has, one bit per CID, the highest bit of the first byte for CID 0.
        if (objects.Get(descriptor, "CIDSet") is StreamToken cidSet && GlyphCount(program) is int glyphs)
        {
            var bits = objects.Contents(cidSet) ?? [];
            var map = objects.Get(descendant, "CIDToGIDMap") is StreamToken stream ? objects.Contents(stream) : null;
            var present = Enumerable.Range(0, map is null ? glyphs : map.Length / 2)
                .Where(cid => glyphOfCid(cid) is int glyph && glyph < glyphs && (cid == 0 || glyph != 0));
            if (present.Any(cid => cid / 8 >= bits.Length || (bits[cid / 8] & (0x80 >> cid % 8)) == 0))
            {
                problems.Add(PdfFontProblem.CidSet);
            }
        }
    }

    // [first [w1 w2 ...]] gives widths from first on; [first last w] gives one width to the range.
    private static Dictionary<int, double> CidWidths(PdfObjects objects, DictionaryToken descendant)
    {
        var widths = new Dictionary<int, double>();
        var items = objects.Items(descendant.Data.GetValueOrDefault("W")).Select(objects.Resolve).ToList();
        for (var i = 0; i + 1 < items.Count;)
        {
            if (items[i] is not NumericToken first) break;
            if (items[i + 1] is ArrayToken list)
            {
                for (var j = 0; j < list.Data.Count; j++) widths[first.Int + j] = Number(objects, list.Data[j]) ?? 0;
                i += 2;
            }
            else if (i + 2 < items.Count && items[i + 1] is NumericToken last
                && Number(objects, items[i + 2]) is { } width)
            {
                // A range is kept to the CIDs a code can name, so a hostile one can't fill memory.
                for (var cid = Math.Max(first.Int, 0); cid <= Math.Min(last.Int, 0xFFFF); cid++) widths[cid] = width;
                i += 3;
            }
            else
            {
                break;
            }
        }

        return widths;
    }

    // Glyph 0 (.notdef) is missing in any text. For drawn text, a glyph past the program's last is missing too, and a
    // width more than one unit in a thousand from the program's disagrees.
    private static void Compare(TrueTypeFont program, Codes codes, Func<int, int> glyphOf,
        Func<int, double> dictionaryWidth, List<int> missing, List<PdfFontProblem> problems)
    {
        var glyphCount = GlyphCount(program);
        var metrics = program.TableRegister.HorizontalMetricsTable;
        var unitsPerEm = program.TableRegister.HeaderTable?.UnitsPerEm ?? 1000;
        var widthsDisagree = false;
        foreach (var code in codes.All)
        {
            var glyph = glyphOf(code);
            var drawn = codes.Rendered.Contains(code);
            if (glyph == 0 || drawn && glyph >= glyphCount)
            {
                missing.Add(code);
            }
            else if (drawn && metrics is not null && glyph < glyphCount)
            {
                var width = metrics.GetAdvanceWidth(glyph) * 1000.0 / unitsPerEm;
                if (Math.Abs(dictionaryWidth(code) - width) > 1) widthsDisagree = true;
            }
        }

        if (widthsDisagree) problems.Add(PdfFontProblem.GlyphWidths);
    }

    private static int? GlyphCount(TrueTypeFont program) =>
        program.TableRegister.IndexToLocationTable?.GlyphOffsets.Count - 1;

    private static bool Is(ICMapSubTable table, int platform, int encoding) =>
        (int)table.PlatformId == platform && table.EncodingId == encoding;

    private static double? Number(PdfObjects objects, IToken? token) =>
        objects.Resolve(token) is NumericToken number ? number.Double : null;
}
