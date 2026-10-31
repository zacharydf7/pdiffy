using System.Xml.Linq;
using static PDiffy.Docx.Reading.DocxXml;

namespace PDiffy.Docx.Reading;

/// <summary>
/// The formatting PDiffy doesn't read in a properties element (w:rPr, w:pPr, w:tblPr, w:trPr, w:tcPr, w:sectPr and so
/// on), as one hash, so changing it still changes the snapshot. Only the element's own children count, not what it
/// inherits.
/// </summary>
internal static class OtherProperties
{
    public static readonly HashSet<string> ReadInRun =
    [
        "rStyle", "rFonts", "sz", "b", "i", "u", "color", "highlight", "caps", "smallCaps", "strike", "vertAlign",
        "dstrike", "vanish", "rtl", "lang", "ins", "del", "moveFrom", "moveTo", "rPrChange",
    ];

    public static readonly HashSet<string> ReadInParagraph =
    [
        "pStyle", "numPr", "jc", "ind", "spacing", "outlineLvl", "keepNext", "keepLines", "pageBreakBefore", "tabs",
        "bidi", "pBdr", "shd", "sectPr", "pPrChange", "r",
    ];

    public static readonly HashSet<string> ReadInTable =
        ["tblStyle", "tblW", "tblCellMar", "tblBorders", "tblCaption", "tblDescription", "tblPrChange"];

    public static readonly HashSet<string> ReadInRow =
        ["tblHeader", "trHeight", "cantSplit", "ins", "del", "trPrChange"];

    public static readonly HashSet<string> ReadInCell =
    [
        "vMerge", "shd", "gridSpan", "tcW", "tcMar", "tcBorders", "vAlign", "textDirection", "cellIns", "cellDel",
        "cellMerge", "tcPrChange",
    ];

    public static readonly HashSet<string> ReadInSection =
    [
        "pgSz", "pgMar", "pgNumType", "cols", "type", "titlePg", "headerReference", "footerReference", "sectPrChange",
    ];

    // Left out as noise Word writes into nearly every document: proofing and web-view marks, the complex-script copies
    // of bold, italic and size and the document grid (East Asian and right-to-left settings, for #145),
    // conditional-format markers a table style sets, and a paragraph mark's own formatting. Later Word versions'
    // extensions (w14: and so on) are in other namespaces and left out too.
    private static readonly HashSet<string> Noise =
        ["noProof", "webHidden", "szCs", "bCs", "iCs", "cs", "docGrid", "cnfStyle", "divId", "printerSettings", "rPr"];

    /// <summary>The hash of the unread children of each properties element, or null when none has any.</summary>
    public static string? Hash(params (XElement? Properties, HashSet<string> Read)[] elements) =>
        HashOf(new XElement("other", elements.Select(element => Unread(element.Properties, element.Read))));

    /// <summary>
    /// The hash of a style's unread formatting. A style line prints only its run look and, for a paragraph style, its
    /// layout, so its table, row and cell properties and a table style's conditional formatting (w:tblStylePr) all
    /// count.
    /// </summary>
    public static string? HashStyle(XElement style, bool isParagraphStyle)
    {
        var unread = new XElement("other", Unread(style.Element(W + "rPr"), ReadInRun),
            Unread(style.Element(W + "pPr"), isParagraphStyle ? ReadInParagraph : []),
            Unread(style.Element(W + "tblPr"), []), Unread(style.Element(W + "trPr"), []),
            Unread(style.Element(W + "tcPr"), []));
        foreach (var conditional in style.Elements(W + "tblStylePr"))
        {
            var formats = new XElement("tblStylePr", conditional.Attributes(),
                conditional.Elements().Select(properties => Unread(properties, [])));
            if (formats.HasElements) unread.Add(formats);
        }

        return HashOf(unread);
    }

    private static XElement? Unread(XElement? properties, HashSet<string> read)
    {
        var children = properties?.Elements()
            .Where(child => child.Name.Namespace == W && !read.Contains(child.Name.LocalName)
                && !Noise.Contains(child.Name.LocalName))
            .OrderBy(child => child.Name.LocalName, StringComparer.Ordinal)
            .ToList() ?? [];
        return children.Count == 0 ? null : new XElement(properties!.Name.LocalName, children);
    }

    private static string? HashOf(XElement unread) => unread.HasElements ? DocxXml.Hash(unread) : null;
}
