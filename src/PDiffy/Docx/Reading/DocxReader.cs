using System.Xml.Linq;
using static PDiffy.Docx.Reading.DocxXml;

namespace PDiffy.Docx.Reading;

/// <summary>Reads a DOCX package into a <see cref="DocxDocument"/>.</summary>
internal sealed class DocxReader
{
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace DcTerms = "http://purl.org/dc/terms/";

    // Documents inserted into inserted documents are read this many levels deep; deeper ones are recorded by hash.
    private const int MaxInsertedDepth = 5;

    private readonly StyleResolver _styles;
    private readonly NumberingResolver _numbering;
    private readonly DocxPackage _package;
    private readonly string _part;
    private readonly HashSet<string> _usedStyles;
    private readonly int _depth;
    private readonly InlineReader _inlines;
    private int _sections;

    private DocxReader(StyleResolver styles, NumberingResolver numbering, DocxPackage package, string part, HashSet<string> usedStyles,
        int depth)
    {
        (_styles, _numbering, _package, _part, _usedStyles, _depth) = (styles, numbering, package, part, usedStyles, depth);
        _inlines = new InlineReader(styles, package, part, usedStyles);
    }

    public static DocxDocument Read(Stream stream) => Read(stream, 0);

    private static DocxDocument Read(Stream stream, int depth)
    {
        using var package = new DocxPackage(stream);
        var main = package.Xml(package.MainPart)?.Root?.Element(W + "body")
            ?? throw new InvalidDataException($"The DOCX has no readable document body ({package.MainPart}).");
        var settings = package.Xml(package.Related(package.MainPart, "/settings"))?.Root;
        var styles = new StyleResolver(package.Xml(package.Related(package.MainPart, "/styles")),
            package.Xml(package.Related(package.MainPart, "/theme")));

        var numbering = new NumberingResolver(package.Xml(package.Related(package.MainPart, "/numbering")));
        var reader = new DocxReader(styles, numbering, package, package.MainPart, [], depth);
        var body = reader.Blocks(main).ToList();

        // A tracked change to a section keeps the old settings in its own sectPr, which isn't a section.
        var sections = main.Descendants(W + "sectPr").Where(sectPr => sectPr.Parent?.Name != W + "sectPrChange").ToList();
        var headers = reader.HeadersAndFooters(sections, "headerReference");
        var footers = reader.HeadersAndFooters(sections, "footerReference");
        var core = package.Xml(package.Related("", "/core-properties"))?.Root;
        return new DocxDocument
        {
            Document = Info(core, settings, styles, package.Unreadable),
            PageSetups = sections.Select(PageSetupOf).ToList(),
            Styles = reader._usedStyles.Order(StringComparer.Ordinal).Select(styles.Describe).OfType<DocxStyle>().ToList(),
            Lists = reader._numbering.UsedLists(),
            Body = body,
            Headers = headers,
            Footers = footers,
        };
    }

    // Each section names its own headers and footers by kind; a part that can't be found is recorded as missing.
    private List<DocxHeaderFooter> HeadersAndFooters(List<XElement> sections, string reference)
    {
        var found = new List<DocxHeaderFooter>();
        for (var i = 0; i < sections.Count; i++)
        {
            foreach (var item in sections[i].Elements(W + reference).OrderBy(item => Attr(item, "type") ?? "default", StringComparer.Ordinal))
            {
                var target = (string?)item.Attribute(R + "id") is { } id ? _package.Target(_part, id) : null;
                var root = _package.Xml(target)?.Root;
                var blocks = root is null ? null
                    : new DocxReader(_styles, _numbering, _package, target!, _usedStyles, _depth).Blocks(root).ToList();
                found.Add(new DocxHeaderFooter(i + 1, Attr(item, "type") ?? "default", blocks));
            }
        }

        return found;
    }

    private static DocxInfo Info(XElement? core, XElement? settings, StyleResolver styles, List<string> unreadable)
    {
        var protection = settings?.Element(W + "documentProtection");
        var compat = settings?.Element(W + "compat")?.Elements(W + "compatSetting")
            .FirstOrDefault(setting => Attr(setting, "name") == "compatibilityMode");
        return new DocxInfo
        {
            Title = NonEmpty(core?.Element(Dc + "title")?.Value),
            Subject = NonEmpty(core?.Element(Dc + "subject")?.Value),
            Language = styles.Language,
            CompatibilityMode = Integer(compat, "val"),
            TrackChanges = IsOn(settings?.Element(W + "trackRevisions")),
            Protection = IsTrue(Attr(protection, "enforcement")) ? Attr(protection, "edit") ?? "readOnly" : null,
            EvenAndOddHeaders = IsOn(settings?.Element(W + "evenAndOddHeaders")),
            HasCreatedDate = core?.Element(DcTerms + "created") is not null,
            HasModifiedDate = core?.Element(DcTerms + "modified") is not null,
            UnreadableParts = unreadable.ToList(),
        };
    }

    private static PageSetup PageSetupOf(XElement sectPr)
    {
        var size = sectPr.Element(W + "pgSz");
        var margins = sectPr.Element(W + "pgMar");
        var numbers = sectPr.Element(W + "pgNumType");
        return new PageSetup
        {
            Width = Twips(size, "w") ?? 0,
            Height = Twips(size, "h") ?? 0,
            MarginTop = Twips(margins, "top") ?? 0,
            MarginRight = Twips(margins, "right") ?? 0,
            MarginBottom = Twips(margins, "bottom") ?? 0,
            MarginLeft = Twips(margins, "left") ?? 0,
            HeaderDistance = Twips(margins, "header") ?? 0,
            FooterDistance = Twips(margins, "footer") ?? 0,
            Gutter = Twips(margins, "gutter") ?? 0,
            Columns = Integer(sectPr.Element(W + "cols"), "num") ?? 1,
            Break = Attr(sectPr.Element(W + "type"), "val") ?? "nextPage",
            PageNumberStart = Integer(numbers, "start"),
            PageNumberFormat = Attr(numbers, "fmt"),
            TitlePage = IsOn(sectPr.Element(W + "titlePg")),
            Headers = References(sectPr, "headerReference"),
            Footers = References(sectPr, "footerReference"),
        };
    }

    private static List<string> References(XElement sectPr, string name) =>
        sectPr.Elements(W + name).Select(reference => Attr(reference, "type") ?? "default")
            .Distinct().Order(StringComparer.Ordinal).ToList();

    private IEnumerable<DocxBlock> Blocks(XElement container)
    {
        foreach (var element in container.Elements())
        {
            if (element.Name == W + "p") yield return Paragraph(element);
            else if (element.Name == W + "tbl") yield return Table(element);
            else if (element.Name == W + "altChunk") yield return Inserted(element);
            else if (IsWrapper(element))
            {
                foreach (var block in Blocks(element)) yield return block;
            }
        }
    }

    private DocxTable Table(XElement table)
    {
        var style = Attr(table.Element(W + "tblPr")?.Element(W + "tblStyle"), "val");
        if (style is not null) _usedStyles.Add(style);
        var rows = Unwrapped(table, "tr").Select(row => new DocxRow(Unwrapped(row, "tc").Select(Cell).ToList(),
            IsOn(row.Element(W + "trPr")?.Element(W + "tblHeader"))));
        return new DocxTable
        {
            Style = style,
            Widths = table.Element(W + "tblGrid")?.Elements(W + "gridCol").Select(column => Twips(column, "w") ?? 0).ToList() ?? [],
            Rows = rows.ToList(),
        };
    }

    // A vMerge with no val continues the merge above it.
    private DocxCell Cell(XElement cell)
    {
        var properties = cell.Element(W + "tcPr");
        var merge = properties?.Element(W + "vMerge") is { } vMerge ? Attr(vMerge, "val") ?? "continue" : null;
        var shading = NonEmpty(Attr(properties?.Element(W + "shd"), "fill")) is { } fill and not "auto" ? fill.ToUpperInvariant() : null;
        return new DocxCell(Blocks(cell).ToList(), Integer(properties?.Element(W + "gridSpan"), "val") ?? 1, merge, shading);
    }

    // An inserted DOCX (w:altChunk) is read in place as a document of its own, and the parts of it that can't be read
    // are listed with this document's, as "{inserted part}!{its part}". Other formats, such as HTML or RTF, and a DOCX
    // that can't be opened are recorded by their hash.
    private DocxInserted Inserted(XElement chunk)
    {
        var part = (string?)chunk.Attribute(R + "id") is { } id ? _package.Target(_part, id) : null;
        var kind = part is null ? null : NonEmpty(Path.GetExtension(part).TrimStart('.').ToLowerInvariant());
        if (kind == "docx" && _depth < MaxInsertedDepth && _package.Open(part) is { } stream)
        {
            using (stream)
            {
                try
                {
                    var inserted = Read(stream, _depth + 1);
                    _package.Unreadable.AddRange(inserted.Document.UnreadableParts.Select(unreadable => $"{part}!{unreadable}"));
                    return new DocxInserted { Kind = kind, Document = inserted };
                }
                catch (InvalidDataException)
                {
                    return new DocxInserted { Kind = kind, Unreadable = true, Hash = _package.Hash(part) };
                }
            }
        }

        return new DocxInserted { Kind = kind, Hash = _package.Hash(part) };
    }

    // Children with a name, looking through content controls and custom XML around them.
    private static IEnumerable<XElement> Unwrapped(XElement parent, string name)
    {
        foreach (var child in parent.Elements())
        {
            if (child.Name == W + name) yield return child;
            else if (IsWrapper(child))
            {
                foreach (var nested in Unwrapped(child, name)) yield return nested;
            }
        }
    }

    private DocxParagraph Paragraph(XElement paragraph)
    {
        var pPr = paragraph.Element(W + "pPr");
        var style = Attr(pPr?.Element(W + "pStyle"), "val") ?? _styles.DefaultParagraphStyle;
        if (style is not null) _usedStyles.Add(style);
        var paragraphRun = _styles.ParagraphRun(style);

        var content = _inlines.Read(paragraph, paragraphRun);

        return new DocxParagraph
        {
            Style = style,
            List = ListItem(pPr?.Element(W + "numPr"), style),
            Layout = _styles.Layout(style, pPr),
            Run = paragraphRun,
            Text = content.Text.ToString(),
            Spans = content.Spans,
            Inlines = content.Inlines,
            EndsSection = pPr?.Element(W + "sectPr") is null ? null : ++_sections,
        };
    }

    // A paragraph's own numPr wins over its style's; numId 0 takes the paragraph out of its style's list.
    private DocxListItem? ListItem(XElement? numPr, string? style)
    {
        var fromStyle = _styles.Numbering(style);
        var list = Integer(numPr?.Element(W + "numId"), "val") ?? fromStyle?.List;
        if (list is null or 0) return null;
        var level = Integer(numPr?.Element(W + "ilvl"), "val") ?? fromStyle?.Level ?? 0;
        return new DocxListItem(list.Value, level, _numbering.Next(list.Value, level));
    }

}
