using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>
/// What PDF/UA forbids in a Type 0 or TrueType font's dictionaries and CMaps, found without reading the font file.
/// </summary>
internal static class FontProblems
{
    private static readonly Regex WritingMode = new(@"/WMode\s+(\d+)\s+def");
    private static readonly Regex UsedCMap = new(@"/([^\s/\[\]<>(){}%]+)\s+usecmap");

    public static List<PdfFontProblem> Read(PdfObjects objects, DictionaryToken font, string? subtype,
        DictionaryToken? descriptor, bool embedded)
    {
        var problems = new List<PdfFontProblem>();
        if (subtype == PdfFontTypes.Type0) ReadType0(objects, font, embedded, problems);
        if (subtype == PdfFontTypes.TrueType) ReadTrueType(objects, font, descriptor, problems);
        return problems.Order().ToList();
    }

    private static void ReadType0(PdfObjects objects, DictionaryToken font, bool embedded, List<PdfFontProblem> problems)
    {
        var descendants = objects.Items(font.Data.GetValueOrDefault("DescendantFonts"));
        var descendant = descendants.Count > 0 ? objects.Resolve(descendants[0]) as DictionaryToken : null;
        var collection = descendant is null ? null : Collection(objects, objects.Get(descendant, "CIDSystemInfo"));

        switch (objects.Get(font, "Encoding"))
        {
            case NameToken name when !PredefinedCMaps.Contains(name.Data):
                problems.Add(PdfFontProblem.CmapNotEmbedded);
                break;
            case NameToken name:
                if (PredefinedCMaps.CollectionOf(name.Data) is { } ordering && collection is not null
                    && collection != ("Adobe", ordering))
                {
                    problems.Add(PdfFontProblem.CidSystemInfo);
                }

                break;
            case StreamToken cmap:
                ReadEmbeddedCMap(objects, cmap, collection, problems);
                break;
        }

        // A TrueType-based CID font needs the map from CIDs to its glyphs, either a stream or Identity.
        if (descendant is not null && objects.Name(descendant, "Subtype") == "CIDFontType2" && embedded
            && objects.Get(descendant, "CIDToGIDMap") is not (StreamToken or NameToken { Data: "Identity" }))
        {
            problems.Add(PdfFontProblem.CidToGidMap);
        }
    }

    private static void ReadEmbeddedCMap(PdfObjects objects, StreamToken cmap, (string?, string?)? collection,
        List<PdfFontProblem> problems)
    {
        var dictionary = cmap.StreamDictionary;
        var own = Collection(objects, objects.Get(dictionary, "CIDSystemInfo"));
        if (own is not null && collection is not null && own != collection) problems.Add(PdfFontProblem.CidSystemInfo);

        var data = Encoding.Latin1.GetString(objects.Contents(cmap) ?? []);
        var match = WritingMode.Match(data);
        var dataMode = !match.Success ? 0 : int.TryParse(match.Groups[1].Value, out var mode) ? mode : -1;
        if ((objects.Integer(dictionary, "WMode") ?? 0) != dataMode) problems.Add(PdfFontProblem.CmapWritingMode);

        var used = UsedCMap.Matches(data).Select(match => match.Groups[1].Value).ToList();
        var usesStream = false;
        switch (objects.Get(dictionary, "UseCMap"))
        {
            case NameToken name:
                used.Add(name.Data);
                break;
            case StreamToken:
                usesStream = true;
                break;
        }

        if (usesStream || used.Any(name => !PredefinedCMaps.Contains(name))) problems.Add(PdfFontProblem.CmapReference);
    }

    // A non-symbolic TrueType font's encoding is MacRoman or WinAnsi, directly or as the base of its differences; a
    // symbolic one has none, since its glyphs are picked through the font's own cmap.
    private static void ReadTrueType(PdfObjects objects, DictionaryToken font, DictionaryToken? descriptor,
        List<PdfFontProblem> problems)
    {
        var encoding = objects.Get(font, "Encoding");
        if (FontReader.IsSymbolic(objects, descriptor))
        {
            if (encoding is not null) problems.Add(PdfFontProblem.SymbolicEncoding);
            return;
        }

        var name = encoding switch
        {
            NameToken named => named.Data,
            DictionaryToken dictionary => objects.Name(dictionary, "BaseEncoding"),
            _ => null,
        };
        if (name is not ("MacRomanEncoding" or "WinAnsiEncoding")) problems.Add(PdfFontProblem.TrueTypeEncoding);
    }

    // The Registry and Ordering of a CIDSystemInfo dictionary, or of the first in an array of them.
    private static (string?, string?)? Collection(PdfObjects objects, IToken? info)
    {
        if (info is ArrayToken array && array.Data.Count > 0) info = objects.Resolve(array.Data[0]);
        if (info is not DictionaryToken dictionary) return null;

        return (objects.Text(dictionary, "Registry"), objects.Text(dictionary, "Ordering"));
    }
}
