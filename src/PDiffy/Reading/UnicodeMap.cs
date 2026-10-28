using UglyToad.PdfPig.Fonts;
using UglyToad.PdfPig.Fonts.Encodings;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>
/// Whether a font maps a character code to Unicode, which is what lets its text be copied, searched and read aloud.
/// A ToUnicode CMap is checked first. A simple font also maps a code when its encoding gives the code a standard glyph
/// name, as readers fall back to that; a CID font only when it uses a predefined CMap (not Identity) over one of Adobe's
/// character collections.
/// </summary>
internal static class UnicodeMap
{
    private static readonly string[] AdobeCollections = ["Japan1", "GB1", "CNS1", "Korea1", "KR"];

    public static Func<int, bool> For(PdfObjects objects, DictionaryToken font, string? subtype)
    {
        var toUnicode = objects.Get(font, "ToUnicode");
        if (toUnicode is NameToken) return _ => true;

        var cmap = toUnicode is StreamToken stream ? ToUnicodeCMap.Parse(objects.Contents(stream)) : null;
        if (subtype == "Type0")
        {
            var usesCollection = UsesAdobeCollection(objects, font);
            return code => usesCollection || cmap?.Maps(code) == true;
        }

        var glyphName = GlyphNames(objects, font);
        return code => cmap?.Maps(code) == true || glyphName(code) is { } name && HasUnicode(name);
    }

    private static bool HasUnicode(string glyphName) =>
        !string.IsNullOrEmpty(GlyphList.AdobeGlyphList.NameToUnicode(glyphName))
        || !string.IsNullOrEmpty(GlyphList.ZapfDingbats.NameToUnicode(glyphName));

    // The encoding's name for each code, with the /Differences array replacing names for the codes it lists.
    private static Func<int, string?> GlyphNames(PdfObjects objects, DictionaryToken font)
    {
        var baseEncoding = DefaultEncoding(objects, font);
        var differences = new Dictionary<int, string>();
        switch (objects.Get(font, "Encoding"))
        {
            case NameToken name:
                baseEncoding = Named(name) ?? baseEncoding;
                break;
            case DictionaryToken encoding:
                if (objects.Get(encoding, "BaseEncoding") is NameToken baseName) baseEncoding = Named(baseName) ?? baseEncoding;
                ReadDifferences(objects, encoding, differences);
                break;
        }

        return code => differences.TryGetValue(code, out var name) ? name : baseEncoding?.GetName(code);
    }

    // [code /name /name ... code /name ...]: each number sets the code for the names after it.
    private static void ReadDifferences(PdfObjects objects, DictionaryToken encoding, Dictionary<int, string> differences)
    {
        var code = 0;
        foreach (var item in objects.Items(encoding.Data.GetValueOrDefault("Differences")))
        {
            if (objects.Resolve(item) is NumericToken number) code = number.Int;
            else if (objects.Resolve(item) is NameToken name) differences[code++] = name.Data;
        }
    }

    // Without an /Encoding, Symbol and ZapfDingbats use their own encodings, other symbolic fonts use one built into the
    // font file (unknown here), and everything else uses the standard encoding.
    private static Encoding? DefaultEncoding(PdfObjects objects, DictionaryToken font)
    {
        var baseFont = FontReader.WithoutSubsetPrefix(objects.Name(font, "BaseFont") ?? "");
        if (baseFont == "Symbol") return SymbolEncoding.Instance;
        if (baseFont == "ZapfDingbats") return ZapfDingbatsEncoding.Instance;

        const int symbolicFlag = 1 << 2;
        var descriptor = objects.Dictionary(font, "FontDescriptor");
        var symbolic = descriptor is not null && ((objects.Integer(descriptor, "Flags") ?? 0) & symbolicFlag) != 0;
        return symbolic ? null : StandardEncoding.Instance;
    }

    private static Encoding? Named(NameToken name) => Encoding.TryGetNamedEncoding(name, out var encoding) ? encoding : null;

    private static bool UsesAdobeCollection(PdfObjects objects, DictionaryToken font)
    {
        var encoding = objects.Name(font, "Encoding");
        if (encoding is null or "Identity-H" or "Identity-V") return false;

        var descendants = objects.Items(font.Data.GetValueOrDefault("DescendantFonts"));
        var info = descendants.Count > 0 && objects.Resolve(descendants[0]) is DictionaryToken descendant
            ? objects.Dictionary(descendant, "CIDSystemInfo") : null;
        return info is not null && objects.Text(info, "Registry") == "Adobe"
            && AdobeCollections.Contains(objects.Text(info, "Ordering"));
    }
}
