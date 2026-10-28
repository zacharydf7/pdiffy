using PDiffy.Docx.Reading;

namespace PDiffy;

/// <summary>Everything PDiffy reads from a Word document (DOCX), in the order a snapshot prints it.</summary>
public sealed record DocxDocument
{
    public required DocxInfo Document { get; init; }

    /// <summary>Each section's page size, margins, columns and page numbering, in document order.</summary>
    public IReadOnlyList<PageSetup> PageSetups { get; init; } = [];

    /// <summary>The styles the document uses, with their properties resolved through the styles they're based on.</summary>
    public IReadOnlyList<DocxStyle> Styles { get; init; } = [];

    /// <summary>The lists the document uses, each with the levels it uses.</summary>
    public IReadOnlyList<DocxList> Lists { get; init; } = [];

    /// <summary>Paragraphs, tables and inserted documents in reading order.</summary>
    public IReadOnlyList<DocxBlock> Body { get; init; } = [];

    /// <summary>Each section's headers, by kind.</summary>
    public IReadOnlyList<DocxHeaderFooter> Headers { get; init; } = [];

    /// <summary>Each section's footers, by kind.</summary>
    public IReadOnlyList<DocxHeaderFooter> Footers { get; init; } = [];

    /// <summary>Reads the DOCX in a stream. The stream is left open.</summary>
    /// <remarks>A part that can't be read is listed in <see cref="DocxInfo.UnreadableParts"/>; a file that can't be opened throws.</remarks>
    public static DocxDocument Load(Stream docx) => DocxReader.Read(docx);

    /// <summary>Reads the DOCX file at a path.</summary>
    public static DocxDocument Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>The canonical snapshot text: one paragraph, table cell or style per line, LF endings.</summary>
    public string ToSnapshot() => DocxSnapshotWriter.Write(this);

    /// <summary>The differences between this document's snapshot and another's, element by element.</summary>
    public SnapshotDiff CompareTo(DocxDocument other) => SnapshotDiff.Compare(ToSnapshot(), other.ToSnapshot());

    /// <summary>Every paragraph of the body in reading order, including those inside tables and inserted documents.</summary>
    public IEnumerable<DocxParagraph> Paragraphs() => Paragraphs(Body);

    /// <summary>
    /// Every image and other graphic (chart, diagram, shape) in the body, headers and footers, such as for checking alt text.
    /// </summary>
    public IEnumerable<DocxImage> Images() =>
        Paragraphs(Body.Concat(Headers.Concat(Footers).SelectMany(item => item.Blocks ?? [])))
            .SelectMany(paragraph => paragraph.Inlines.OfType<DocxImage>());

    private static IEnumerable<DocxParagraph> Paragraphs(IEnumerable<DocxBlock> blocks)
    {
        foreach (var block in blocks)
        {
            if (block is DocxParagraph paragraph) yield return paragraph;
            var nestedBlocks = block switch
            {
                DocxTable table => table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks),
                DocxInserted inserted => inserted.Document?.Body ?? [],
                _ => [],
            };
            foreach (var nested in Paragraphs(nestedBlocks)) yield return nested;
        }
    }
}

/// <summary>Document-wide details: title, language, compatibility and protection settings.</summary>
public sealed record DocxInfo
{
    public string? Title { get; init; }
    public string? Subject { get; init; }

    /// <summary>The default language, such as "en-US", which assistive technology reads with.</summary>
    public string? Language { get; init; }

    /// <summary>Word's compatibility mode, such as 15 for Word 2013 and later.</summary>
    public int? CompatibilityMode { get; init; }

    public bool TrackChanges { get; init; }

    /// <summary>What editing is restricted to, such as "readOnly" or "forms", or null when the document isn't protected.</summary>
    public string? Protection { get; init; }

    /// <summary>Whether even and odd pages get different headers and footers.</summary>
    public bool EvenAndOddHeaders { get; init; }

    public bool HasCreatedDate { get; init; }
    public bool HasModifiedDate { get; init; }

    /// <summary>Parts of the file that couldn't be read, such as "word/styles.xml" with broken XML.</summary>
    public IReadOnlyList<string> UnreadableParts { get; init; } = [];
}

/// <summary>One section's page geometry. Lengths are in points.</summary>
public sealed record PageSetup
{
    public double Width { get; init; }
    public double Height { get; init; }
    public double MarginTop { get; init; }
    public double MarginRight { get; init; }
    public double MarginBottom { get; init; }
    public double MarginLeft { get; init; }
    public double HeaderDistance { get; init; }
    public double FooterDistance { get; init; }
    public double Gutter { get; init; }
    public int Columns { get; init; } = 1;

    /// <summary>How the section starts: "nextPage", "continuous", "evenPage", "oddPage" or "nextColumn".</summary>
    public string Break { get; init; } = "nextPage";

    public int? PageNumberStart { get; init; }

    /// <summary>The page number format, such as "decimal" or "lowerRoman".</summary>
    public string? PageNumberFormat { get; init; }

    /// <summary>Whether the first page has its own header and footer.</summary>
    public bool TitlePage { get; init; }

    /// <summary>The kinds of header the section has: "default", "first" or "even".</summary>
    public IReadOnlyList<string> Headers { get; init; } = [];

    public IReadOnlyList<string> Footers { get; init; } = [];
}

/// <summary>A style the document uses. <see cref="Run"/> and <see cref="Paragraph"/> are resolved through its based-on chain.</summary>
public sealed record DocxStyle
{
    public required string Id { get; init; }

    /// <summary>"paragraph", "character", "table" or "numbering".</summary>
    public required string Type { get; init; }

    public string? Name { get; init; }
    public string? BasedOn { get; init; }

    /// <summary>Whether this is the style used when none is named.</summary>
    public bool IsDefault { get; init; }

    public RunFormat Run { get; init; } = new();
    public ParagraphFormat Paragraph { get; init; } = new();
}

/// <summary>How text looks. Sizes are in points; null means not set.</summary>
public sealed record RunFormat
{
    /// <summary>The character style applied, if any.</summary>
    public string? Style { get; init; }

    public string? Font { get; init; }
    public double? Size { get; init; }
    public bool Bold { get; init; }
    public bool Italic { get; init; }

    /// <summary>The underline kind, such as "single" or "double".</summary>
    public string? Underline { get; init; }

    /// <summary>A hex color such as "FF0000"; null means automatic or not set.</summary>
    public string? Color { get; init; }

    /// <summary>A highlight color name, such as "yellow".</summary>
    public string? Highlight { get; init; }

    public bool Caps { get; init; }
    public bool SmallCaps { get; init; }
    public bool Strike { get; init; }

    /// <summary>"superscript" or "subscript".</summary>
    public string? VerticalAlign { get; init; }
}

/// <summary>How a paragraph is laid out. Lengths are in points; null means not set.</summary>
public sealed record ParagraphFormat
{
    /// <summary>"left", "center", "right", "both" (justified) and so on, as Word writes it.</summary>
    public string? Align { get; init; }

    public double? IndentLeft { get; init; }
    public double? IndentRight { get; init; }
    public double? FirstLine { get; init; }
    public double? Hanging { get; init; }
    public double? SpacingBefore { get; init; }
    public double? SpacingAfter { get; init; }

    /// <summary>Line spacing: a multiple such as "1.15", or a length with its rule, such as "12 exact".</summary>
    public string? LineSpacing { get; init; }

    public bool KeepNext { get; init; }
    public bool KeepLines { get; init; }
    public bool PageBreakBefore { get; init; }

    /// <summary>The heading level from 1, when the paragraph is a heading.</summary>
    public int? HeadingLevel { get; init; }

    /// <summary>Custom tab stops, such as "left:72" or "right:468:dot".</summary>
    public IReadOnlyList<string> Tabs { get; init; } = [];
}

/// <summary>A paragraph or a table in the body, a table cell or a header.</summary>
public abstract record DocxBlock;

public sealed record DocxParagraph : DocxBlock
{
    /// <summary>The paragraph style's id: the one named, or the document's default paragraph style.</summary>
    public string? Style { get; init; }

    /// <summary>The list item this paragraph is, or null when it isn't one.</summary>
    public DocxListItem? List { get; init; }

    /// <summary>How the paragraph is laid out: its style's layout with the paragraph's own formatting on top.</summary>
    public ParagraphFormat Layout { get; init; } = new();

    /// <summary>How the paragraph's text looks by default: document defaults and its style.</summary>
    public RunFormat Run { get; init; } = new();

    /// <summary>The whole text with runs merged. Tabs are \t and line breaks \n.</summary>
    public string Text { get; init; } = "";

    /// <summary>The text cut where its look changes, each part with its full formatting.</summary>
    public IReadOnlyList<DocxSpan> Spans { get; init; } = [];

    /// <summary>What sits in the text, in reading order: images, links, bookmarks, symbols and page or column breaks.</summary>
    public IReadOnlyList<DocxInline> Inlines { get; init; } = [];

    /// <summary>The section this paragraph ends, numbered from 1, or null.</summary>
    public int? EndsSection { get; init; }
}

/// <summary>
/// A paragraph's place in a list: the list's id (w:numId), its level from 0, and the label Word shows, such as "A." or
/// "1.2", counted from the items before it. The label is null when the list or its level isn't defined.
/// </summary>
public sealed record DocxListItem(int List, int Level, string? Label);

/// <summary>
/// A list the document uses: its id, the definition it numbers from (w:abstractNumId, or the definition of the list style
/// it links to) and the levels used.
/// </summary>
public sealed record DocxList(int Id, string Definition, IReadOnlyList<DocxListLevel> Levels);

/// <summary>
/// How one level of a list is numbered: its format (such as "decimal", "upperLetter" or "bullet"), its label pattern
/// (such as "%1." or a bullet character), the number it starts at, its indents in points and its bullet font. Format,
/// Text and Start are null when the list's definition has no such level.
/// </summary>
public sealed record DocxListLevel(int Level, string? Format, string? Text, int? Start, double? Indent, double? Hanging, string? Font);

/// <summary>Part of a paragraph's text and how it looks.</summary>
public sealed record DocxSpan(string Text, RunFormat Format);

/// <summary>Something in a paragraph's text. <see cref="Position"/> is where, counted in characters of its text.</summary>
public abstract record DocxInline(int Position);

/// <summary>
/// A field: its code, such as "PAGE" or "TOC \o \"1-3\"", and its result, the text Word last showed for it. The result
/// is also part of the paragraph's text.
/// </summary>
public sealed record DocxField(string Code, string Result, int Position) : DocxInline(Position);

/// <summary>A page or column break.</summary>
public sealed record DocxBreak(string Kind, int Position) : DocxInline(Position);

/// <summary>A link: the text it covers and where it goes, a URL or "#bookmark" for a place in the document.</summary>
public sealed record DocxLink(string Text, string? Target, int Position) : DocxInline(Position);

/// <summary>A named place in the document that links and cross-references can point to.</summary>
public sealed record DocxBookmark(string Name, int Position) : DocxInline(Position);

/// <summary>A character from a symbol font, such as Wingdings: the font and the character's code in hex.</summary>
public sealed record DocxSymbol(string? Font, string Code, int Position) : DocxInline(Position);

/// <summary>
/// An image or other graphic. Width and height are in points. For a picture, <see cref="Hash"/> is the first 16 hex
/// digits of the image's SHA-256, which catches a swapped image, and <see cref="Type"/> its file type, such as "png".
/// </summary>
public sealed record DocxImage(int Position) : DocxInline(Position)
{
    /// <summary>"image" for a picture; otherwise what the graphic is, such as "chart", "diagram" or "shape".</summary>
    public string Kind { get; init; } = "image";

    /// <summary>Whether text flows around the image rather than the image sitting in the line.</summary>
    public bool Floating { get; init; }

    public double Width { get; init; }
    public double Height { get; init; }

    /// <summary>The description a screen reader reads, or null when the image has none.</summary>
    public string? AltText { get; init; }

    public string? Title { get; init; }

    /// <summary>Whether the image is marked as decoration, which a screen reader skips.</summary>
    public bool Decorative { get; init; }

    public string? Type { get; init; }
    public string? Hash { get; init; }
}

/// <summary>A section's header or footer of one kind: "default", "first" or "even". Blocks is null when its part can't be found.</summary>
public sealed record DocxHeaderFooter(int Section, string Kind, IReadOnlyList<DocxBlock>? Blocks);

/// <summary>
/// A document inserted into this one (w:altChunk). A DOCX is read in place into <see cref="Document"/>, with its own
/// styles and lists; other formats, such as HTML or RTF, and a DOCX that can't be opened (<see cref="Unreadable"/>) are
/// recorded by <see cref="Hash"/>, which is null when the inserted file is missing.
/// </summary>
public sealed record DocxInserted : DocxBlock
{
    /// <summary>The inserted file's type, such as "docx", "html" or "rtf", or null when it can't be told.</summary>
    public string? Kind { get; init; }

    public DocxDocument? Document { get; init; }
    public bool Unreadable { get; init; }
    public string? Hash { get; init; }
}

public sealed record DocxTable : DocxBlock
{
    public string? Style { get; init; }

    /// <summary>The width of each column of the table's grid, in points.</summary>
    public IReadOnlyList<double> Widths { get; init; } = [];

    public IReadOnlyList<DocxRow> Rows { get; init; } = [];
}

/// <summary>A table row. A header row repeats at the top of each page the table runs onto.</summary>
public sealed record DocxRow(IReadOnlyList<DocxCell> Cells, bool IsHeader = false);

/// <summary>
/// A table cell. <see cref="Span"/> is how many grid columns it covers; <see cref="Merge"/> is "restart" for the top of
/// cells merged down a column and "continue" for those below it; <see cref="Shading"/> is its fill color in hex.
/// </summary>
public sealed record DocxCell(IReadOnlyList<DocxBlock> Blocks, int Span = 1, string? Merge = null, string? Shading = null);
