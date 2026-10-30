using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>
/// Whether a character code shows the font's .notdef glyph, the empty box drawn for a character the font lacks, as far
/// as the font's dictionaries tell. A simple font's encoding names the code .notdef; a CID font with an Identity
/// encoding picks CID 0, or its CIDToGIDMap sends the code to glyph 0. A glyph missing from the font file itself, or
/// a code a font's built-in encoding or a predefined CMap decides, isn't found.
/// </summary>
internal static class MissingGlyphs
{
    public static Func<int, bool> For(PdfObjects objects, DictionaryToken font, string? subtype)
    {
        if (subtype == "Type0") return ForCidFont(objects, font);

        // No default encoding: without a named one, the font file's built-in encoding may give the code a glyph.
        var glyphName = UnicodeMap.GlyphNames(objects, font, null);
        return code => glyphName(code) == ".notdef";
    }

    // The Identity encodings use the code as the CID. A TrueType-based CID font's CIDToGIDMap stream gives each CID's
    // glyph as two bytes; without one, the glyph is the CID, and CID 0 is .notdef.
    private static Func<int, bool> ForCidFont(PdfObjects objects, DictionaryToken font)
    {
        if (objects.Name(font, "Encoding") is not ("Identity-H" or "Identity-V")) return _ => false;

        var descendants = objects.Items(font.Data.GetValueOrDefault("DescendantFonts"));
        var descendant = descendants.Count > 0 ? objects.Resolve(descendants[0]) as DictionaryToken : null;
        var map = descendant is not null && objects.Name(descendant, "Subtype") == "CIDFontType2"
            && objects.Get(descendant, "CIDToGIDMap") is StreamToken stream ? objects.Contents(stream) : null;
        if (map is null) return cid => cid == 0;

        return cid => 2 * cid + 1 < map.Length && (map[2 * cid] << 8 | map[2 * cid + 1]) == 0;
    }
}
