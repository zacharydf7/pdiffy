using System.Xml.Linq;
using PDiffy.Reading;
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
        _inlines = new InlineReader(styles, package, part, usedStyles, container => Blocks(container).ToList());
    }

    public static DocxDocument Read(Stream stream) => Read(stream, 0, ReadLimits.MaxDepth);

    private static DocxDocument Read(Stream stream, int depth, int maxXmlDepth)
    {
        using var package = new DocxPackage(stream, maxXmlDepth);
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
        var notes = reader.Notes("/footnotes", "footnote").Concat(reader.Notes("/endnotes", "endnote"))
            .Concat(reader.Notes("/comments", "comment")).ToList();
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
            Notes = notes,
        };
    }

    // Footnotes, endnotes and comments, each read like the body. The separator lines Word keeps as notes are left out.
    private List<DocxNote> Notes(string relationship, string kind)
    {
        var part = _package.Related(_part, relationship);
        if (_package.Xml(part)?.Root is not { } root) return [];
        var reader = new DocxReader(_styles, _numbering, _package, part!, _usedStyles, _depth);
        return root.Elements(W + kind).Where(note => Attr(note, "type") is null or "normal")
            .Select(note => new DocxNote(kind, Integer(note, "id") ?? 0, NonEmpty(Attr(note, "author")), reader.Blocks(note).ToList()))
            .ToList();
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
            Revision = sectPr.Element(W + "sectPrChange") is { } change
                ? new DocxRevision("section-format", "", Attr(change, "author"), 0)
                : null,
        };
    }

    private static List<string> References(XElement sectPr, string name) =>
        sectPr.Elements(W + name).Select(reference => Attr(reference, "type") ?? "default")
            .Distinct().Order(StringComparer.Ordinal).ToList();

    // Left out on purpose: section and cell properties read elsewhere, wrappers' own properties, and range markers.
    private static readonly HashSet<string> SkippedInBlocks = [.. RangeMarkers, "sectPr", "tcPr", "customXmlPr", "smartTagPr"];

    private IEnumerable<DocxBlock> Blocks(XElement container)
    {
        foreach (var element in container.Elements())
        {
            if (element.Name == W + "p") yield return Paragraph(element);
            else if (element.Name == W + "tbl") yield return Table(element);
            else if (element.Name == W + "altChunk") yield return Inserted(element);
            else if (element.Name == W + "sdt") yield return Control(element);
            else if (element.Name == Mc + "AlternateContent")
            {
                foreach (var block in Chosen(element) is { } choice ? Blocks(choice) : []) yield return block;
            }
            else if (IsWrapper(element))
            {
                foreach (var block in Blocks(element)) yield return block;
            }
            else if (Stray(element, SkippedInBlocks) is { } stray) yield return stray;
        }
    }

    private DocxTable Table(XElement table)
    {
        var properties = table.Element(W + "tblPr");
        var style = Attr(properties?.Element(W + "tblStyle"), "val");
        if (style is not null) _usedStyles.Add(style);
        var (rows, between) = (new List<DocxRow>(), new List<DocxBetween>());
        ReadRows(table, [], rows, between);
        return new DocxTable
        {
            Style = style,
            Widths = table.Element(W + "tblGrid")?.Elements(W + "gridCol").Select(column => Twips(column, "w") ?? 0).ToList() ?? [],
            Width = TableFormat.Width(properties?.Element(W + "tblW")),
            CellMargins = TableFormat.Margins(properties?.Element(W + "tblCellMar")),
            Borders = TableFormat.Borders(properties?.Element(W + "tblBorders")),
            Rows = rows,
            Between = between,
            Revisions =
            [
                .. Changes(properties, [("tblPrChange", "table-format")]),
                .. Changes(table.Element(W + "tblGrid"), [("tblGridChange", "table-grid")]),
            ],
        };
    }

    // Left out on purpose among rows and cells: properties read elsewhere, wrappers' own properties, and range markers.
    private static readonly HashSet<string> SkippedInRows =
        [.. RangeMarkers, "tblPr", "tblGrid", "sdtPr", "sdtEndPr", "customXmlPr", "smartTagPr"];

    private static readonly HashSet<string> SkippedInCells =
        [.. RangeMarkers, "trPr", "tblPrEx", "sdtPr", "sdtEndPr", "customXmlPr", "smartTagPr"];

    // The rows of a table, with the content controls they sit in (such as a repeating section's items), and what's
    // between them.
    private void ReadRows(XElement parent, IReadOnlyList<DocxControl> controls, List<DocxRow> rows,
        List<DocxBetween> between)
    {
        foreach (var child in parent.Elements())
        {
            if (child.Name == W + "tr") rows.Add(Row(child, controls));
            else if (child.Name == W + "sdt" && child.Element(W + "sdtContent") is { } inner)
            {
                ReadRows(inner, [.. controls, ControlAround(child)], rows, between);
            }
            else if (child.Name == Mc + "AlternateContent")
            {
                if (Chosen(child) is { } choice) ReadRows(choice, controls, rows, between);
            }
            else if (IsWrapper(child)) ReadRows(child, controls, rows, between);
            else if (Stray(child, SkippedInRows) is { } stray) between.Add(new DocxBetween(rows.Count, stray));
        }
    }

    // The cells of a row, with the content controls they sit in, and what's between them.
    private void ReadCells(XElement parent, IReadOnlyList<DocxControl> controls, List<DocxCell> cells,
        List<DocxBetween> between)
    {
        foreach (var child in parent.Elements())
        {
            if (child.Name == W + "tc") cells.Add(Cell(child, controls));
            else if (child.Name == W + "sdt" && child.Element(W + "sdtContent") is { } inner)
            {
                ReadCells(inner, [.. controls, ControlAround(child)], cells, between);
            }
            else if (child.Name == Mc + "AlternateContent")
            {
                if (Chosen(child) is { } choice) ReadCells(choice, controls, cells, between);
            }
            else if (IsWrapper(child)) ReadCells(child, controls, cells, between);
            else if (Stray(child, SkippedInCells) is { } stray) between.Add(new DocxBetween(cells.Count, stray));
        }
    }

    // A content control around rows or cells, with its kind, tag and title; what it holds is read with them.
    private static DocxControl ControlAround(XElement sdt)
    {
        var (kind, name, title) = FormReader.Describe(sdt.Element(W + "sdtPr"));
        return new DocxControl(kind) { Name = name, Title = title };
    }

    // A bookmark, or anything else not left out on purpose, which is kept by its hash so a change to it still shows.
    private static DocxBlock? Stray(XElement element, HashSet<string> skipped)
    {
        if (element.Name == W + "bookmarkStart")
        {
            return Attr(element, "name") is { } name and not LastEditBookmark
                ? new DocxBookmarkBlock(name) { Columns = BookmarkColumns(element) }
                : null;
        }

        if (element.Name.Namespace == W && skipped.Contains(element.Name.LocalName)) return null;
        return new DocxUnsupportedBlock(NameOf(element), Hash(element));
    }

    private DocxRow Row(XElement row, IReadOnlyList<DocxControl> controls)
    {
        var properties = row.Element(W + "trPr");
        var (cells, between) = (new List<DocxCell>(), new List<DocxBetween>());
        ReadCells(row, [], cells, between);
        return new DocxRow(cells, IsOn(properties?.Element(W + "tblHeader")))
        {
            Revisions =
            [
                .. Changes(properties, RowChanges),
                .. Changes(row.Element(W + "tblPrEx"), [("tblPrExChange", "row-table-format")]),
            ],
            Controls = controls,
            Between = between,
        };
    }

    // A table, row or cell changed with tracking on keeps who did it in its properties.
    private static List<DocxRevision> Changes(XElement? properties, (string Element, string Kind)[] kinds)
    {
        var revisions = new List<DocxRevision>();
        foreach (var (element, kind) in kinds)
        {
            if (properties?.Element(W + element) is { } change)
            {
                revisions.Add(new DocxRevision(kind, "", Attr(change, "author"), 0));
            }
        }

        return revisions;
    }

    private static readonly (string Element, string Kind)[] RowChanges =
        [("ins", "row-insert"), ("del", "row-delete"), ("trPrChange", "row-format")];

    private static readonly (string Element, string Kind)[] CellChanges =
    [
        ("cellIns", "cell-insert"), ("cellDel", "cell-delete"), ("cellMerge", "cell-merge"), ("tcPrChange", "cell-format"),
    ];

    // A vMerge with no val continues the merge above it.
    private DocxCell Cell(XElement cell, IReadOnlyList<DocxControl> controls)
    {
        var properties = cell.Element(W + "tcPr");
        var merge = properties?.Element(W + "vMerge") is { } vMerge ? Attr(vMerge, "val") ?? "continue" : null;
        var shading = NonEmpty(Attr(properties?.Element(W + "shd"), "fill")) is { } fill and not "auto" ? fill.ToUpperInvariant() : null;
        return new DocxCell(Blocks(cell).ToList(), Integer(properties?.Element(W + "gridSpan"), "val") ?? 1, merge, shading)
        {
            Width = TableFormat.Width(properties?.Element(W + "tcW")),
            Margins = TableFormat.Margins(properties?.Element(W + "tcMar")),
            Borders = TableFormat.Borders(properties?.Element(W + "tcBorders")),
            Revisions = Changes(properties, CellChanges),
            Controls = controls,
        };
    }

    // A content control around paragraphs or tables keeps them, with the control's kind, tag and title.
    private DocxControl Control(XElement control)
    {
        var (kind, name, title) = FormReader.Describe(control.Element(W + "sdtPr"));
        var inner = control.Element(W + "sdtContent");
        return new DocxControl(kind) { Name = name, Title = title, Blocks = inner is null ? [] : Blocks(inner).ToList() };
    }

    // An inserted DOCX (w:altChunk) is read in place as a document of its own, and the parts of it that can't be read
    // are listed with this document's, as "{inserted part}!{its part}". Other formats, such as HTML or RTF, and a DOCX
    // that can't be opened or is too large are recorded by their hash.
    private DocxInserted Inserted(XElement chunk)
    {
        var part = (string?)chunk.Attribute(R + "id") is { } id ? _package.Target(_part, id) : null;
        var kind = part is null ? null : NonEmpty(Path.GetExtension(part).TrimStart('.').ToLowerInvariant());
        if (kind == "docx" && _depth < MaxInsertedDepth)
        {
            try
            {
                using var stream = _package.Open(part);
                if (stream is not null)
                {
                    // It's read on the same stack as this document, so its nesting starts from where it's inserted.
                    var inserted = Read(stream, _depth + 1, _package.MaxDepth - chunk.Ancestors().Count());
                    _package.Unreadable.AddRange(inserted.Document.UnreadableParts.Select(unreadable => $"{part}!{unreadable}"));
                    return new DocxInserted { Kind = kind, Document = inserted };
                }
            }
            catch (InvalidDataException)
            {
                return new DocxInserted { Kind = kind, Unreadable = true, Hash = _package.Hash(part) };
            }
        }

        return new DocxInserted { Kind = kind, Hash = _package.Hash(part) };
    }

    private DocxParagraph Paragraph(XElement paragraph)
    {
        var pPr = paragraph.Element(W + "pPr");
        var style = Attr(pPr?.Element(W + "pStyle"), "val") ?? _styles.DefaultParagraphStyle;
        if (style is not null) _usedStyles.Add(style);
        var paragraphRun = _styles.ParagraphRun(style);

        // The paragraph is numbered before a text box in it, whose list items come after it in reading order.
        var list = ListItem(pPr?.Element(W + "numPr"), style);
        var content = _inlines.Read(paragraph, paragraphRun);

        return new DocxParagraph
        {
            Style = style,
            List = list,
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
