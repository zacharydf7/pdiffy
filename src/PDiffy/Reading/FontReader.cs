using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

internal static class FontReader
{
    private static readonly string[] FontFileKeys = ["FontFile", "FontFile2", "FontFile3"];

    /// <summary>
    /// Every font the pages and their form XObjects use, once each, with the character codes its text uses that don't map
    /// to Unicode. Pages PdfPig couldn't load contribute the fonts in their resources but no codes.
    /// </summary>
    public static IReadOnlyList<FontInfo> Read(PdfObjects objects, PageTree pages, IReadOnlyDictionary<int, Page> loaded)
    {
        var usage = new FontUsageReader(objects);
        for (var number = 1; number <= pages.Pages.Count; number++)
        {
            var resources = pages.Pages[number - 1].Resources;
            if (loaded.TryGetValue(number, out var page)) usage.AddText(page.Operations, resources);
            else usage.AddResources(resources);
        }

        return usage.Fonts.Select(use => ReadFont(objects, use)).ToList();
    }

    private static FontInfo ReadFont(PdfObjects objects, FontUse use)
    {
        var font = use.Font;
        var subtype = objects.Name(font, "Subtype");

        // A Type0 font keeps its font descriptor on its descendant font.
        var descriptorOwner = font;
        if (subtype == "Type0" && objects.Get(font, "DescendantFonts") is ArrayToken descendants && descendants.Data.Count > 0
            && objects.Resolve(descendants.Data[0]) is DictionaryToken descendant)
        {
            descriptorOwner = descendant;
        }

        var descriptor = objects.Dictionary(descriptorOwner, "FontDescriptor");
        var embedded = descriptor is not null && FontFileKeys.Any(descriptor.Data.ContainsKey);
        var hasToUnicode = font.Data.ContainsKey("ToUnicode");
        var name = WithoutSubsetPrefix(objects.Name(font, "BaseFont") ?? "(none)");
        return new FontInfo(name, subtype, embedded, hasToUnicode) { UnmappedCodes = UnmappedCodes(objects, use, subtype) };
    }

    // A CID font's codes are two bytes (true of the Identity encodings nearly every CID font uses); a simple font's are one.
    private static IReadOnlyList<int> UnmappedCodes(PdfObjects objects, FontUse use, string? subtype)
    {
        var width = subtype == "Type0" ? 2 : 1;
        var codes = new SortedSet<int>();
        foreach (var bytes in use.Bytes)
        {
            for (var i = 0; i + width <= bytes.Length; i += width)
            {
                codes.Add(width == 2 ? bytes[i] << 8 | bytes[i + 1] : bytes[i]);
            }
        }

        var maps = UnicodeMap.For(objects, use.Font, subtype);
        var unmapped = codes.Where(code => !maps(code)).ToList();
        // [] is the same shared empty list as the property's default, so a font with none still equals new FontInfo(...).
        if (unmapped.Count == 0) return [];
        return unmapped;
    }

    // A subset font is named like ABCDEF+Arial: six capital letters and a plus sign.
    internal static string WithoutSubsetPrefix(string name)
    {
        var isSubset = name.Length > 7 && name[6] == '+' && name[..6].All(char.IsAsciiLetterUpper);
        return isSubset ? name[7..] : name;
    }

}
