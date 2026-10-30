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

    /// <summary>
    /// Paragraphs, tables, content controls, inserted documents, bookmarks between them and content PDiffy can't read,
    /// in reading order.
    /// </summary>
    public IReadOnlyList<DocxBlock> Body { get; init; } = [];

    /// <summary>Each section's headers, by kind.</summary>
    public IReadOnlyList<DocxHeaderFooter> Headers { get; init; } = [];

    /// <summary>Each section's footers, by kind.</summary>
    public IReadOnlyList<DocxHeaderFooter> Footers { get; init; } = [];

    /// <summary>Footnotes, endnotes and comments, with what each says.</summary>
    public IReadOnlyList<DocxNote> Notes { get; init; } = [];

    /// <summary>
    /// Reads the DOCX in a stream from its start, wherever the stream was left, and puts its position back afterwards. The
    /// stream is left open.
    /// </summary>
    /// <remarks>
    /// A part that can't be read is listed in <see cref="DocxInfo.UnreadableParts"/>; a file that can't be opened throws.
    /// An empty stream, a PDF or a web page throws <see cref="InvalidDataException"/> saying so.
    /// </remarks>
    public static DocxDocument Load(Stream docx) => InputStream.Read(docx, DocumentFormat.Docx, DocxReader.Read);

    /// <summary>Reads the DOCX in a byte array.</summary>
    public static DocxDocument Load(byte[] docx) => Load(new MemoryStream(docx, writable: false));

    /// <summary>Reads the DOCX file at a path.</summary>
    public static DocxDocument Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>The canonical snapshot text: one paragraph, table cell or style per line, LF endings.</summary>
    /// <param name="configure">What to leave out or scrub, e.g. <c>o => o.Ignore(DocxSection.Styles).ScrubDates()</c>.</param>
    public string ToSnapshot(Action<DocxSnapshotOptions>? configure = null)
    {
        var options = new DocxSnapshotOptions();
        configure?.Invoke(options);
        return DocxSnapshotWriter.Write(this, options);
    }

    /// <summary>The differences between this document's snapshot and another's, element by element.</summary>
    public SnapshotDiff CompareTo(DocxDocument other, Action<DocxSnapshotOptions>? configure = null) =>
        SnapshotDiff.Compare(ToSnapshot(configure), other.ToSnapshot(configure));

    /// <summary>
    /// Every paragraph of the body in reading order, including those inside tables, content controls, text boxes and
    /// inserted documents.
    /// </summary>
    public IEnumerable<DocxParagraph> Paragraphs() => AllBlocks(Body).OfType<DocxParagraph>();

    /// <summary>
    /// Every image and other graphic (chart, diagram, shape) in the body, headers, footers and notes, such as for checking
    /// alt text.
    /// </summary>
    public IEnumerable<DocxImage> Images() =>
        EveryBlock().OfType<DocxParagraph>().SelectMany(paragraph => paragraph.Inlines.OfType<DocxImage>());

    /// <summary>
    /// The tracked changes in the body, headers, footers and notes, including inserted documents, such as for checking
    /// that none shipped: insertions, deletions, moves and formatting changes of text and paragraphs (their marks too),
    /// changes to tables and their rows and cells, and then changes to section settings.
    /// </summary>
    public IEnumerable<DocxRevision> Revisions() =>
        RevisionsIn(OuterBlocks()).Concat(PageSetups.Select(setup => setup.Revision).OfType<DocxRevision>());

    // In reading order: a table's changes, then each row's, then each cell's followed by those of what the cell holds.
    private static IEnumerable<DocxRevision> RevisionsIn(IEnumerable<DocxBlock> blocks) =>
        AllBlocks(blocks, intoTables: false).SelectMany(block => block switch
        {
            DocxParagraph paragraph => paragraph.Inlines.OfType<DocxRevision>(),
            DocxTable table => table.Revisions.Concat(table.Rows.SelectMany(row => row.Revisions.Concat(
                row.Cells.SelectMany(cell => cell.Revisions.Concat(RevisionsIn(cell.Blocks)))))),
            _ => [],
        });

    private IEnumerable<DocxBlock> EveryBlock() => AllBlocks(OuterBlocks());

    // Inserted documents' notes count as this document's, since Word brings them in with the inserted text.
    private IEnumerable<DocxBlock> OuterBlocks()
    {
        var blocks = Body.Concat(Headers.Concat(Footers).SelectMany(item => item.Blocks ?? [])).Concat(Notes.SelectMany(note => note.Blocks));
        return blocks.Concat(InsertedDocuments(Body).SelectMany(document => document.Notes.SelectMany(note => note.Blocks)));
    }

    private static IEnumerable<DocxDocument> InsertedDocuments(IEnumerable<DocxBlock> blocks) => blocks.SelectMany(block => block switch
    {
        DocxInserted { Document: { } document } => InsertedDocuments(document.Body).Prepend(document),
        DocxTable table => InsertedDocuments(table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks)),
        DocxControl control => InsertedDocuments(control.Blocks),
        _ => [],
    });

    // Each block followed by the blocks inside it, in reading order; without intoTables, a table's cells are left to
    // the caller.
    private static IEnumerable<DocxBlock> AllBlocks(IEnumerable<DocxBlock> blocks, bool intoTables = true)
    {
        foreach (var block in blocks)
        {
            yield return block;
            var nestedBlocks = block switch
            {
                DocxTable table when intoTables => table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks),
                DocxInserted inserted => inserted.Document?.Body ?? [],
                DocxControl control => control.Blocks,
                DocxParagraph withTextBoxes => withTextBoxes.Inlines.OfType<DocxImage>().SelectMany(image => image.TextBox),
                _ => [],
            };
            foreach (var nested in AllBlocks(nestedBlocks, intoTables)) yield return nested;
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

    /// <summary>A tracked change to the section's settings ("section-format"), or null.</summary>
    public DocxRevision? Revision { get; init; }
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

    /// <summary>Text struck through with two lines.</summary>
    public bool DoubleStrike { get; init; }

    /// <summary>Hidden text, which Word doesn't show or print unless asked to.</summary>
    public bool Hidden { get; init; }

    /// <summary>Text set right to left, such as Arabic or Hebrew.</summary>
    public bool RightToLeft { get; init; }

    /// <summary>
    /// The text's language, such as "fr-FR". A run's comes from the run, its styles or the document's defaults; a
    /// character style's only from the style.
    /// </summary>
    public string? Language { get; init; }
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

    /// <summary>A paragraph set right to left, such as one in Arabic or Hebrew.</summary>
    public bool RightToLeft { get; init; }

    /// <summary>Borders around the paragraph, written like a table's, such as "top:single:0.5:000000".</summary>
    public IReadOnlyList<string> Borders { get; init; } = [];

    /// <summary>The paragraph's fill color in hex.</summary>
    public string? Shading { get; init; }
}

/// <summary>
/// Something in the body, a table cell, a header or footer, a note or a text box: a paragraph, a table, a content
/// control, an inserted document, a bookmark between them, or content PDiffy can't read.
/// </summary>
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

    /// <summary>
    /// What sits in the text, in reading order: images, links, bookmarks, symbols, fields, page or column breaks, and
    /// stray text from runs in the paragraph's properties.
    /// </summary>
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
/// is also part of the paragraph's text, unless the field sits in another field's code, whose code it becomes part of.
/// </summary>
public sealed record DocxField(string Code, string Result, int Position) : DocxInline(Position);

/// <summary>
/// The text, tabs and breaks of a run where WordprocessingML doesn't allow one: inside the paragraph's properties (w:pPr). Word may
/// drop or move it, so it isn't part of the paragraph's text, but it's recorded so the text isn't lost and the problem
/// shows. Its <see cref="DocxInline.Position"/> is always 0, before the paragraph's text.
/// </summary>
public sealed record DocxStrayText(string Text, int Position) : DocxInline(Position);

/// <summary>
/// A tracked change and who made it: "insert", "delete", "move-to" or "move-from" with the text it covers; "format" for a
/// run whose formatting changed, with its text; or "paragraph-insert", "paragraph-delete", "paragraph-move-to",
/// "paragraph-move-from", "paragraph-format" or "paragraph-mark-format" for the paragraph itself, with no text;
/// "table-format" or "table-grid" for a table (<see cref="DocxTable.Revisions"/>); "row-insert", "row-delete",
/// "row-format" or "row-table-format" for a row (<see cref="DocxRow.Revisions"/>); "cell-insert", "cell-delete",
/// "cell-merge" or "cell-format" for a cell (<see cref="DocxCell.Revisions"/>); "section-format" for a section's
/// settings (<see cref="PageSetup.Revision"/>). Tables, rows, cells and sections have no text and no position.
/// </summary>
public sealed record DocxRevision(string Kind, string Text, string? Author, int Position) : DocxInline(Position);

/// <summary>Where a footnote or endnote is referenced. <see cref="DocxDocument.Notes"/> has what the note says.</summary>
public sealed record DocxNoteReference(string Kind, int Id, int Position) : DocxInline(Position);

/// <summary>Where a comment is attached. <see cref="DocxDocument.Notes"/> has what the comment says.</summary>
public sealed record DocxCommentReference(int Id, int Position) : DocxInline(Position);

/// <summary>
/// A form control in the text: a content control (Kind "text", "rich-text", "dropdown", "combobox", "date", "checkbox"
/// and so on) or a legacy form field (Kind "legacy-text", "legacy-checkbox" or "legacy-dropdown"). Name is a content
/// control's tag or a legacy field's name; a check box's Value is "yes" or "no".
/// </summary>
public sealed record DocxForm(string Kind, int Position) : DocxInline(Position)
{
    public string? Name { get; init; }

    /// <summary>A content control's title, which Word shows on it.</summary>
    public string? Title { get; init; }

    public string? Value { get; init; }

    /// <summary>The choices of a drop-down or combo box.</summary>
    public IReadOnlyList<string> Items { get; init; } = [];
}

/// <summary>A page or column break.</summary>
public sealed record DocxBreak(string Kind, int Position) : DocxInline(Position);

/// <summary>A link: the text it covers and where it goes, a URL or "#bookmark" for a place in the document.</summary>
public sealed record DocxLink(string Text, string? Target, int Position) : DocxInline(Position);

/// <summary>
/// A named place in the document that links and cross-references can point to. A bookmark in a table can cover only
/// some columns: <see cref="Columns"/> is then the first and last, counting from 0, such as "1-2".
/// </summary>
public sealed record DocxBookmark(string Name, int Position) : DocxInline(Position)
{
    public string? Columns { get; init; }
}

/// <summary>A character from a symbol font, such as Wingdings: the font and the character's code in hex.</summary>
public sealed record DocxSymbol(string? Font, string Code, int Position) : DocxInline(Position);

/// <summary>
/// An image or other graphic. Width and height are in points. For a picture, <see cref="Hash"/> is the first 16 hex
/// digits of the image's SHA-256, which catches a swapped image, and <see cref="Type"/> its file type, such as "png".
/// For an embedded object, Type is the program it belongs to, such as "Excel.Sheet.12", and Hash is of what it holds;
/// for an ActiveX control, Type is the control's name.
/// </summary>
public sealed record DocxImage(int Position) : DocxInline(Position)
{
    /// <summary>
    /// "image" for a picture, "object" for an embedded or linked object such as a spreadsheet, "control" for an ActiveX
    /// control; otherwise what the graphic is, such as "chart", "diagram" or "shape".
    /// </summary>
    public string Kind { get; init; } = "image";

    /// <summary>The file a linked object shows, which the document doesn't contain; null when it's embedded.</summary>
    public string? Source { get; init; }

    /// <summary>What a text box, or a shape with text in it, says.</summary>
    public IReadOnlyList<DocxBlock> TextBox { get; init; } = [];

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

/// <summary>
/// An equation: the characters of its text in order, such as "x=a+b", and a hash of how it's built, which changes when,
/// for example, a fraction becomes a division even though the characters stay the same.
/// </summary>
public sealed record DocxMath(string Text, string Hash, int Position) : DocxInline(Position);

/// <summary>
/// Something in a paragraph PDiffy doesn't read yet, such as a phonetic guide (w:ruby): the element's name and a hash of
/// its contents, so a change to it still shows.
/// </summary>
public sealed record DocxUnsupported(string Element, string Hash, int Position) : DocxInline(Position);

/// <summary>
/// A bookmark between paragraphs, tables, rows or cells, such as one that starts at a table; links can point to it.
/// </summary>
public sealed record DocxBookmarkBlock(string Name) : DocxBlock
{
    /// <summary>The columns a table-column bookmark covers, as for <see cref="DocxBookmark.Columns"/>.</summary>
    public string? Columns { get; init; }
}

/// <summary>
/// Something between paragraphs, tables, rows or cells that PDiffy doesn't read yet: the element's name and a hash of
/// its contents, so a change to it still shows.
/// </summary>
public sealed record DocxUnsupportedBlock(string Element, string Hash) : DocxBlock;

/// <summary>A footnote, endnote or comment: its kind, id, a comment's author and what it says.</summary>
public sealed record DocxNote(string Kind, int Id, string? Author, IReadOnlyList<DocxBlock> Blocks);

/// <summary>A content control around paragraphs or tables, such as a repeating section or a rich-text box.</summary>
public sealed record DocxControl(string Kind) : DocxBlock
{
    /// <summary>The control's tag.</summary>
    public string? Name { get; init; }

    public string? Title { get; init; }
    public IReadOnlyList<DocxBlock> Blocks { get; init; } = [];
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

/// <summary>
/// A table. <see cref="Width"/> and cell widths are in points, written as "468", or as "50%" of the page or "auto".
/// Margins are "side:points", such as "left:5.4"; borders are "side:style:points:color", such as
/// "top:single:0.5:000000", without a color when it's automatic.
/// </summary>
public sealed record DocxTable : DocxBlock
{
    public string? Style { get; init; }

    /// <summary>The width of each column of the table's grid, in points.</summary>
    public IReadOnlyList<double> Widths { get; init; } = [];

    /// <summary>The width of the whole table.</summary>
    public string? Width { get; init; }

    /// <summary>The space between each cell's edge and its text, unless a cell sets its own.</summary>
    public IReadOnlyList<string> CellMargins { get; init; } = [];

    public IReadOnlyList<string> Borders { get; init; } = [];

    /// <summary>The table's title, the first part of its alt text in Word.</summary>
    public string? Caption { get; init; }

    /// <summary>The table's description, the rest of its alt text in Word.</summary>
    public string? Description { get; init; }

    public IReadOnlyList<DocxRow> Rows { get; init; } = [];

    /// <summary>Bookmarks and content PDiffy can't read between the rows.</summary>
    public IReadOnlyList<DocxBetween> Between { get; init; } = [];

    /// <summary>Tracked changes to the table's properties ("table-format") or its columns ("table-grid").</summary>
    public IReadOnlyList<DocxRevision> Revisions { get; init; } = [];
}

/// <summary>A table row. A header row repeats at the top of each page the table runs onto.</summary>
public sealed record DocxRow(IReadOnlyList<DocxCell> Cells, bool IsHeader = false)
{
    /// <summary>
    /// The row's height in points: at least that much ("20"), exactly that much ("20-exact"), or "auto" when Word sizes it.
    /// </summary>
    public string? Height { get; init; }

    /// <summary>Whether the row is kept on one page rather than split across two.</summary>
    public bool CantSplit { get; init; }

    /// <summary>
    /// Tracked changes to the row: "row-insert", "row-delete", "row-format", or "row-table-format" for the table
    /// properties the row sets for itself.
    /// </summary>
    public IReadOnlyList<DocxRevision> Revisions { get; init; } = [];

    /// <summary>The content controls around the row, outermost first, such as a repeating section and its item.</summary>
    public IReadOnlyList<DocxControl> Controls { get; init; } = [];

    /// <summary>Bookmarks and content PDiffy can't read between the cells.</summary>
    public IReadOnlyList<DocxBetween> Between { get; init; } = [];
}

/// <summary>
/// A <see cref="DocxBookmarkBlock"/> or <see cref="DocxUnsupportedBlock"/> between a table's rows or a row's cells.
/// <see cref="Before"/> is the index of the row or cell that comes after it, or the count when it comes last.
/// </summary>
public sealed record DocxBetween(int Before, DocxBlock Block);

/// <summary>
/// A table cell. <see cref="Span"/> is how many grid columns it covers; <see cref="Merge"/> is "restart" for the top of
/// cells merged down a column and "continue" for those below it; <see cref="Shading"/> is its fill color in hex. Width,
/// margins and borders are written as for <see cref="DocxTable"/>.
/// </summary>
public sealed record DocxCell(IReadOnlyList<DocxBlock> Blocks, int Span = 1, string? Merge = null, string? Shading = null)
{
    public string? Width { get; init; }
    public IReadOnlyList<string> Margins { get; init; } = [];
    public IReadOnlyList<string> Borders { get; init; } = [];

    /// <summary>Where the text sits in the cell as Word writes it, such as "center" or "bottom".</summary>
    public string? VerticalAlign { get; init; }

    /// <summary>The direction of the cell's text when the cell sets one, as Word writes it, such as "btLr".</summary>
    public string? TextDirection { get; init; }

    /// <summary>Tracked changes to the cell: "cell-insert", "cell-delete", "cell-merge" or "cell-format".</summary>
    public IReadOnlyList<DocxRevision> Revisions { get; init; } = [];

    /// <summary>The content controls around the cell, outermost first.</summary>
    public IReadOnlyList<DocxControl> Controls { get; init; } = [];
}
