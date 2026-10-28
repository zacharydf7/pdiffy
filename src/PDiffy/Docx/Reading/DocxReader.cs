using System.Xml.Linq;
using static PDiffy.Docx.Reading.DocxXml;

namespace PDiffy.Docx.Reading;

/// <summary>Reads a DOCX package into a <see cref="DocxDocument"/>.</summary>
internal sealed class DocxReader
{
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace DcTerms = "http://purl.org/dc/terms/";

    private readonly StyleResolver _styles;
    private readonly NumberingResolver _numbering;
    private readonly InlineReader _inlines;
    private readonly HashSet<string> _usedStyles = [];
    private int _sections;

    private DocxReader(StyleResolver styles, NumberingResolver numbering, DocxPackage package, string part)
    {
        _styles = styles;
        _numbering = numbering;
        _inlines = new InlineReader(styles, package, part, _usedStyles);
    }

    public static DocxDocument Read(Stream stream)
    {
        using var package = new DocxPackage(stream);
        var main = package.Xml(package.MainPart)?.Root?.Element(W + "body")
            ?? throw new InvalidDataException($"The DOCX has no readable document body ({package.MainPart}).");
        var settings = package.Xml(package.Related(package.MainPart, "/settings"))?.Root;
        var styles = new StyleResolver(package.Xml(package.Related(package.MainPart, "/styles")),
            package.Xml(package.Related(package.MainPart, "/theme")));

        var numbering = new NumberingResolver(package.Xml(package.Related(package.MainPart, "/numbering")));
        var reader = new DocxReader(styles, numbering, package, package.MainPart);
        var body = reader.Blocks(main).ToList();
        var core = package.Xml(package.Related("", "/core-properties"))?.Root;
        return new DocxDocument
        {
            Document = Info(core, settings, styles, package.Unreadable),

            // A tracked change to a section keeps the old settings in its own sectPr, which isn't a section.
            PageSetups = main.Descendants(W + "sectPr").Where(sectPr => sectPr.Parent?.Name != W + "sectPrChange")
                .Select(PageSetupOf).ToList(),
            Styles = reader._usedStyles.Order(StringComparer.Ordinal).Select(styles.Describe).OfType<DocxStyle>().ToList(),
            Lists = reader._numbering.UsedLists(),
            Body = body,
        };
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
