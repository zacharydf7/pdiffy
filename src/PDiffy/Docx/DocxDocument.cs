using PDiffy.Docx.Reading;

namespace PDiffy;

/// <summary>Everything PDiffy reads from a Word document (DOCX), in the order a snapshot prints it.</summary>
public sealed record DocxDocument
{
    /// <summary>Document-wide details: title, language, compatibility, protection and unreadable parts.</summary>
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
    /// <summary>The title in the file's properties (docProps/core.xml), or null when it has none.</summary>
    public string? Title { get; init; }

    /// <summary>The subject in the file's properties, or null when it has none.</summary>
    public string? Subject { get; init; }

    /// <summary>The default language, such as "en-US", which assistive technology reads with.</summary>
    public string? Language { get; init; }

    /// <summary>Word's compatibility mode, such as 15 for Word 2013 and later.</summary>
    public int? CompatibilityMode { get; init; }

    /// <summary>Whether Track Changes is on, so Word records later edits as tracked changes.</summary>
    public bool TrackChanges { get; init; }

    /// <summary>What editing is restricted to, such as "readOnly" or "forms", or null when the document isn't protected.</summary>
    public string? Protection { get; init; }

    /// <summary>Whether even and odd pages get different headers and footers.</summary>
    public bool EvenAndOddHeaders { get; init; }

    /// <summary>Whether the file's properties record when it was created. The date itself isn't kept.</summary>
    public bool HasCreatedDate { get; init; }

    /// <summary>Whether the file's properties record when it was last modified. The date itself isn't kept.</summary>
    public bool HasModifiedDate { get; init; }

    /// <summary>Parts of the file that couldn't be read, such as "word/styles.xml" with broken XML.</summary>
    public IReadOnlyList<string> UnreadableParts { get; init; } = [];
}

/// <summary>One section's page geometry. Lengths are in points.</summary>
public sealed record PageSetup
{
    /// <summary>The page width, or 0 when the section doesn't set a page size.</summary>
    public double Width { get; init; }

    /// <summary>The page height, or 0 when the section doesn't set a page size.</summary>
    public double Height { get; init; }

    /// <summary>The top margin, or 0 when the section doesn't set it.</summary>
    public double MarginTop { get; init; }

    /// <summary>The right margin, or 0 when the section doesn't set it.</summary>
    public double MarginRight { get; init; }

    /// <summary>The bottom margin, or 0 when the section doesn't set it.</summary>
    public double MarginBottom { get; init; }

    /// <summary>The left margin, or 0 when the section doesn't set it.</summary>
    public double MarginLeft { get; init; }

    /// <summary>The distance from the top edge of the page to the header.</summary>
    public double HeaderDistance { get; init; }

    /// <summary>The distance from the bottom edge of the page to the footer.</summary>
    public double FooterDistance { get; init; }

    /// <summary>The extra margin left for binding, 0 when there is none.</summary>
    public double Gutter { get; init; }

    /// <summary>How many columns the text is set in.</summary>
    public int Columns { get; init; } = 1;

    /// <summary>How the section starts: "nextPage", "continuous", "evenPage", "oddPage" or "nextColumn".</summary>
    public string Break { get; init; } = "nextPage";

    /// <summary>The number the section's first page gets, or null when numbering carries on from before.</summary>
    public int? PageNumberStart { get; init; }

    /// <summary>The page number format, such as "decimal" or "lowerRoman".</summary>
    public string? PageNumberFormat { get; init; }

    /// <summary>Whether the first page has its own header and footer.</summary>
    public bool TitlePage { get; init; }

    /// <summary>The kinds of header the section has: "default", "first" or "even".</summary>
    public IReadOnlyList<string> Headers { get; init; } = [];

    /// <summary>The kinds of footer the section has: "default", "first" or "even".</summary>
    public IReadOnlyList<string> Footers { get; init; } = [];

    /// <summary>A tracked change to the section's settings ("section-format"), or null.</summary>
    public DocxRevision? Revision { get; init; }
}

/// <summary>A style the document uses. <see cref="Run"/> and <see cref="Paragraph"/> are resolved through its based-on chain.</summary>
public sealed record DocxStyle
{
    /// <summary>The style's id, such as "Heading1", which paragraphs, runs and tables refer to it by.</summary>
    public required string Id { get; init; }

    /// <summary>"paragraph", "character", "table" or "numbering".</summary>
    public required string Type { get; init; }

    /// <summary>The style's name as the file stores it, such as "heading 1", or null when it has none.</summary>
    public string? Name { get; init; }

    /// <summary>The id of the style this one is based on, or null when it isn't based on another.</summary>
    public string? BasedOn { get; init; }

    /// <summary>Whether this is the style used when none is named.</summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// How the style's text looks: for a character style, its settings and those of the styles it's based on; for any
    /// other style, the document defaults with those on top.
    /// </summary>
    public RunFormat Run { get; init; } = new();

    /// <summary>
    /// How a paragraph style lays out paragraphs, document defaults included; empty for other kinds of style.
    /// </summary>
    public ParagraphFormat Paragraph { get; init; } = new();
}

/// <summary>How text looks. Sizes are in points; null means not set.</summary>
public sealed record RunFormat
{
    /// <summary>The character style applied, if any.</summary>
    public string? Style { get; init; }

    /// <summary>
    /// The font for Latin text, such as "Calibri"; a theme font is given as the font the theme names.
    /// </summary>
    public string? Font { get; init; }

    /// <summary>The font size, such as 11.</summary>
    public double? Size { get; init; }

    /// <summary>Whether the text is bold.</summary>
    public bool Bold { get; init; }

    /// <summary>Whether the text is italic.</summary>
    public bool Italic { get; init; }

    /// <summary>The underline kind, such as "single" or "double".</summary>
    public string? Underline { get; init; }

    /// <summary>A hex color such as "FF0000"; null means automatic or not set.</summary>
    public string? Color { get; init; }

    /// <summary>A highlight color name, such as "yellow".</summary>
    public string? Highlight { get; init; }

    /// <summary>Text shown in capitals, whatever case it's typed in.</summary>
    public bool Caps { get; init; }

    /// <summary>Text shown in small capitals, whatever case it's typed in.</summary>
    public bool SmallCaps { get; init; }

    /// <summary>Text struck through with one line.</summary>
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

    /// <summary>How far the paragraph is indented from the left margin.</summary>
    public double? IndentLeft { get; init; }

    /// <summary>How far the paragraph is indented from the right margin.</summary>
    public double? IndentRight { get; init; }

    /// <summary>How much further the first line is indented than the rest; null when it hangs instead.</summary>
    public double? FirstLine { get; init; }

    /// <summary>How far the first line sticks out to the left of the rest; null when it's indented instead.</summary>
    public double? Hanging { get; init; }

    /// <summary>The space above the paragraph.</summary>
    public double? SpacingBefore { get; init; }

    /// <summary>The space below the paragraph.</summary>
    public double? SpacingAfter { get; init; }

    /// <summary>Line spacing: a multiple such as "1.15", or a length with its rule, such as "12 exact".</summary>
    public string? LineSpacing { get; init; }

    /// <summary>Whether the paragraph is kept on the same page as the one after it.</summary>
    public bool KeepNext { get; init; }

    /// <summary>Whether all the paragraph's lines are kept on one page.</summary>
    public bool KeepLines { get; init; }

    /// <summary>Whether the paragraph starts on a new page.</summary>
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

/// <summary>A paragraph: its style, list item, layout, text and what sits in the text.</summary>
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
/// <param name="List">
/// The list's id (w:numId), the <see cref="DocxList.Id"/> of its entry in <see cref="DocxDocument.Lists"/> when the
/// list is defined. Never 0, since numId 0 takes a paragraph out of its list.
/// </param>
/// <param name="Level">The level from 0, the outermost: the paragraph's own w:ilvl, else its style's, else 0.</param>
/// <param name="Label">The label as Word shows it, such as "A." or "1.2", or the bullet character for a bullet.</param>
public sealed record DocxListItem(int List, int Level, string? Label);

/// <summary>
/// A list the document uses: its id, the definition it numbers from (w:abstractNumId, or the definition of the list style
/// it links to) and the levels used.
/// </summary>
/// <param name="Id">The list's id (w:numId), which <see cref="DocxListItem.List"/> refers to it by.</param>
/// <param name="Definition">
/// The id of the definition the list numbers from, such as "3". Lists that share a definition share its counters,
/// unless one restarts its numbering with a start override.
/// </param>
/// <param name="Levels">The levels the document's paragraphs use, outermost first, not every level defined.</param>
public sealed record DocxList(int Id, string Definition, IReadOnlyList<DocxListLevel> Levels);

/// <summary>
/// How one level of a list is numbered: its format (such as "decimal", "upperLetter" or "bullet"), its label pattern
/// (such as "%1." or a bullet character), the number it starts at, its indents in points and its bullet font. Format,
/// Text and Start are null when the list's definition has no such level.
/// </summary>
/// <param name="Level">The level from 0, the outermost, to 8.</param>
/// <param name="Format">The number format, such as "lowerRoman"; "decimal" when the level doesn't set one.</param>
/// <param name="Text">
/// The label pattern, in which %1 to %9 stand for the counters of levels 0 to 8, so "%1.%2." prints as "2.1." for the
/// first item under the second; a bullet's character for a bullet, and empty when the level doesn't set one.
/// </param>
/// <param name="Start">The number the level starts at: the list's own override, else the level's start, else 1.</param>
/// <param name="Indent">The left indent of the level's paragraphs in points, or null when the level sets none.</param>
/// <param name="Hanging">
/// How far the label hangs out to the left of the text after it, in points, or null when the level doesn't set it.
/// </param>
/// <param name="Font">The label's font, such as "Symbol" for a bullet, or null when the level doesn't set one.</param>
public sealed record DocxListLevel(int Level, string? Format, string? Text, int? Start, double? Indent, double? Hanging, string? Font);

/// <summary>Part of a paragraph's text and how it looks.</summary>
/// <param name="Text">
/// The text, never empty. The spans of a paragraph, in order, make up its <see cref="DocxParagraph.Text"/>, and two
/// next to each other always look different.
/// </param>
/// <param name="Format">
/// The full formatting: the document defaults and the paragraph style, with the character style and the run's own
/// formatting on top.
/// </param>
public sealed record DocxSpan(string Text, RunFormat Format);

/// <summary>Something in a paragraph's text. <see cref="Position"/> is where, counted in characters of its text.</summary>
/// <param name="Position">
/// How many characters of <see cref="DocxParagraph.Text"/> come before it, so 0 is the start and the text's length is
/// the end.
/// </param>
public abstract record DocxInline(int Position);

/// <summary>
/// A field: its code, such as "PAGE" or "TOC \o \"1-3\"", and its result, the text Word last showed for it. The result
/// is also part of the paragraph's text, unless the field sits in another field's code, whose code it becomes part of.
/// </summary>
/// <param name="Code">
/// The field's code with each stretch of white space made one space and the ends trimmed. A field nested in it
/// is replaced by that field's result, so { IF { PAGE } = 1 ... } on page 1 reads "IF 1 = 1 ...".
/// </param>
/// <param name="Result">The text Word last showed for the field, or empty when it showed none.</param>
/// <param name="Position">
/// Where the field starts in the paragraph's text, in characters, which is where its result starts when the result is
/// part of the text.
/// </param>
public sealed record DocxField(string Code, string Result, int Position) : DocxInline(Position);

/// <summary>
/// The text, tabs and breaks of a run where WordprocessingML doesn't allow one: inside the paragraph's properties (w:pPr). Word may
/// drop or move it, so it isn't part of the paragraph's text, but it's recorded so the text isn't lost and the problem
/// shows. Its <see cref="DocxInline.Position"/> is always 0, before the paragraph's text.
/// </summary>
/// <param name="Text">
/// The run's text, with tabs as \t, line breaks as \n, page breaks as \f and column breaks as \u000E, the characters
/// Word's own plain text uses; empty when the run holds none of these.
/// </param>
/// <param name="Position">Always 0.</param>
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
/// <param name="Kind">What changed, one of the kinds the summary lists, such as "insert" or "row-delete".</param>
/// <param name="Text">
/// The text inserted, deleted, moved or reformatted, empty for the other kinds. Deleted and moved-away text isn't part
/// of the paragraph's text.
/// </param>
/// <param name="Author">Who made the change, as Word recorded it, or null when it didn't record anyone.</param>
/// <param name="Position">
/// Where the text starts in the paragraph's text, or where deleted or moved-away text was; the end of the text for a
/// change to the paragraph mark, and 0 for "paragraph-format" and for tables, rows, cells and sections.
/// </param>
public sealed record DocxRevision(string Kind, string Text, string? Author, int Position) : DocxInline(Position);

/// <summary>Where a footnote or endnote is referenced. <see cref="DocxDocument.Notes"/> has what the note says.</summary>
/// <param name="Kind">"footnote" or "endnote".</param>
/// <param name="Id">The <see cref="DocxNote.Id"/> of the note of the same kind that this refers to.</param>
/// <param name="Position">Where the reference mark sits in the paragraph's text, in characters.</param>
public sealed record DocxNoteReference(string Kind, int Id, int Position) : DocxInline(Position);

/// <summary>Where a comment is attached. <see cref="DocxDocument.Notes"/> has what the comment says.</summary>
/// <param name="Id">The <see cref="DocxNote.Id"/> of the note of kind "comment" that this refers to.</param>
/// <param name="Position">Where the comment's mark sits in the paragraph's text, in characters.</param>
public sealed record DocxCommentReference(int Id, int Position) : DocxInline(Position);

/// <summary>
/// A form control in the text: a content control (Kind "text", "rich-text", "dropdown", "combobox", "date", "checkbox"
/// and so on) or a legacy form field (Kind "legacy-text", "legacy-checkbox" or "legacy-dropdown"). Name is a content
/// control's tag or a legacy field's name; a check box's Value is "yes" or "no".
/// </summary>
/// <param name="Kind">
/// A content control's "text", "rich-text", "dropdown", "combobox", "date", "checkbox", "picture", "gallery", "group",
/// "citation", "bibliography", "equation", "repeating-section" or "repeating-section-item", or a legacy field's kind.
/// </param>
/// <param name="Position">
/// Where the control's text starts in the paragraph's text, or for a legacy form field, where the field starts.
/// </param>
public sealed record DocxForm(string Kind, int Position) : DocxInline(Position)
{
    /// <summary>A content control's tag or a legacy form field's name, or null when it has none.</summary>
    public string? Name { get; init; }

    /// <summary>A content control's title, which Word shows on it.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// What the control holds: the text it shows, "yes" or "no" for a check box, or a legacy drop-down's chosen entry.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>The choices of a drop-down or combo box.</summary>
    public IReadOnlyList<string> Items { get; init; } = [];
}

/// <summary>A page or column break.</summary>
/// <param name="Kind">"page" or "column". A line break isn't a DocxBreak but \n in the paragraph's text.</param>
/// <param name="Position">Where the break sits in the paragraph's text, in characters.</param>
public sealed record DocxBreak(string Kind, int Position) : DocxInline(Position);

/// <summary>A link: the text it covers and where it goes, a URL or "#bookmark" for a place in the document.</summary>
/// <param name="Text">The text the link covers, as it is in the paragraph's text; empty when it covers none.</param>
/// <param name="Target">
/// Where the link goes, such as "https://example.com/trails", with "#bookmark" on the end when it also names a place;
/// just "#bookmark" for a place in this document. Null when the link's address can't be found or it has none.
/// </param>
/// <param name="Position">Where the link's text starts in the paragraph's text, in characters.</param>
public sealed record DocxLink(string Text, string? Target, int Position) : DocxInline(Position);

/// <summary>
/// A named place in the document that links and cross-references can point to. A bookmark in a table can cover only
/// some columns: <see cref="Columns"/> is then the first and last, counting from 0, such as "1-2".
/// </summary>
/// <param name="Name">
/// The bookmark's name, such as "Ridge_Loop", which a link's "#Ridge_Loop" points to. Word's own "_GoBack" bookmark
/// for the last edit is left out.
/// </param>
/// <param name="Position">Where the bookmark starts in the paragraph's text, in characters.</param>
public sealed record DocxBookmark(string Name, int Position) : DocxInline(Position)
{
    /// <summary>
    /// The first and last table columns covered, such as "1-2"; null unless it's a table bookmark covering only some
    /// columns.
    /// </summary>
    public string? Columns { get; init; }
}

/// <summary>A character from a symbol font, such as Wingdings: the font and the character's code in hex.</summary>
/// <param name="Font">The symbol font, such as "Wingdings", or null when the symbol doesn't name one.</param>
/// <param name="Code">
/// The character's code in upper-case hex, such as "F0FC", and then the character is also in the paragraph's text. A
/// code that isn't hex is kept as the file writes it and isn't in the text; empty when the symbol has no code.
/// </param>
/// <param name="Position">Where the symbol sits in the paragraph's text, in characters.</param>
public sealed record DocxSymbol(string? Font, string Code, int Position) : DocxInline(Position);

/// <summary>
/// An image or other graphic. Width and height are in points. For a picture, <see cref="Hash"/> is the first 16 hex
/// digits of the image's SHA-256, which catches a swapped image, and <see cref="Type"/> its file type, such as "png".
/// For an embedded object, Type is the program it belongs to, such as "Excel.Sheet.12", and Hash is of what it holds;
/// for an ActiveX control, Type is the control's name.
/// </summary>
/// <param name="Position">Where the image sits in the paragraph's text, in characters, floating or not.</param>
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

    /// <summary>The width as placed in the document, or 0 when the file doesn't give one.</summary>
    public double Width { get; init; }

    /// <summary>The height as placed in the document, or 0 when the file doesn't give one.</summary>
    public double Height { get; init; }

    /// <summary>The description a screen reader reads, or null when the image has none.</summary>
    public string? AltText { get; init; }

    /// <summary>The image's title, or null when it has none.</summary>
    public string? Title { get; init; }

    /// <summary>Whether the image is marked as decoration, which a screen reader skips.</summary>
    public bool Decorative { get; init; }

    /// <summary>
    /// A picture's file type, an object's program or a control's name; null when there's none, as for a chart.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// The first 16 hex digits of the SHA-256 of a picture's, embedded object's or control's data, or null when there's
    /// no data, such as for a chart, a linked object or a picture whose file is missing.
    /// </summary>
    public string? Hash { get; init; }
}

/// <summary>
/// An equation: the characters of its text in order, such as "x=a+b", and a hash of how it's built, which changes when,
/// for example, a fraction becomes a division even though the characters stay the same.
/// </summary>
/// <param name="Text">The characters of the equation's text runs (m:t), in order; empty when it has none.</param>
/// <param name="Hash">
/// The first 16 hex digits of a SHA-256 of the equation's XML, leaving out editing-session ids, relationship ids,
/// namespace prefixes and the indenting between elements, which change without the equation changing.
/// </param>
/// <param name="Position">
/// Where the equation sits in the paragraph's text, in characters. The equation's own text isn't part of that text.
/// </param>
public sealed record DocxMath(string Text, string Hash, int Position) : DocxInline(Position);

/// <summary>
/// Something in a paragraph PDiffy doesn't read yet, such as a phonetic guide (w:ruby): the element's name and a hash of
/// its contents, so a change to it still shows.
/// </summary>
/// <param name="Element">
/// The element's name with its usual prefix, "w:" or "m:", whatever prefix the file uses, such as "w:ruby"; an element
/// from another namespace is named in full, as "{namespace}name".
/// </param>
/// <param name="Hash">
/// The first 16 hex digits of a SHA-256 of the element's XML, leaving out what <see cref="DocxMath.Hash"/> leaves out.
/// </param>
/// <param name="Position">Where the element sits in the paragraph's text, in characters.</param>
public sealed record DocxUnsupported(string Element, string Hash, int Position) : DocxInline(Position);

/// <summary>
/// A bookmark between paragraphs, tables, rows or cells, such as one that starts at a table; links can point to it.
/// </summary>
/// <param name="Name">The bookmark's name, as for <see cref="DocxBookmark.Name"/>.</param>
public sealed record DocxBookmarkBlock(string Name) : DocxBlock
{
    /// <summary>The columns a table-column bookmark covers, as for <see cref="DocxBookmark.Columns"/>.</summary>
    public string? Columns { get; init; }
}

/// <summary>
/// Something between paragraphs, tables, rows or cells that PDiffy doesn't read yet: the element's name and a hash of
/// its contents, so a change to it still shows.
/// </summary>
/// <param name="Element">The element's name, written as for <see cref="DocxUnsupported.Element"/>.</param>
/// <param name="Hash">The element's hash, worked out as for <see cref="DocxUnsupported.Hash"/>.</param>
public sealed record DocxUnsupportedBlock(string Element, string Hash) : DocxBlock;

/// <summary>A footnote, endnote or comment: its kind, id, a comment's author and what it says.</summary>
/// <param name="Kind">"footnote", "endnote" or "comment".</param>
/// <param name="Id">
/// The note's id, which <see cref="DocxNoteReference.Id"/> or <see cref="DocxCommentReference.Id"/> refers to it by;
/// ids are only unique within a kind. 0 when the file gives none.
/// </param>
/// <param name="Author">A comment's author, or null for a footnote, an endnote or a comment that names no one.</param>
/// <param name="Blocks">What the note says: its paragraphs, tables and other blocks.</param>
public sealed record DocxNote(string Kind, int Id, string? Author, IReadOnlyList<DocxBlock> Blocks);

/// <summary>A content control around paragraphs or tables, such as a repeating section or a rich-text box.</summary>
/// <param name="Kind">
/// "rich-text" when the control sets no other kind; otherwise "text", "dropdown", "combobox", "date", "checkbox",
/// "picture", "gallery", "group", "citation", "bibliography", "equation", "repeating-section" or
/// "repeating-section-item".
/// </param>
public sealed record DocxControl(string Kind) : DocxBlock
{
    /// <summary>The control's tag.</summary>
    public string? Name { get; init; }

    /// <summary>The control's title, which Word shows on it.</summary>
    public string? Title { get; init; }

    /// <summary>The paragraphs, tables and other blocks inside the control.</summary>
    public IReadOnlyList<DocxBlock> Blocks { get; init; } = [];
}

/// <summary>A section's header or footer of one kind: "default", "first" or "even". Blocks is null when its part can't be found.</summary>
/// <param name="Section">
/// The section it belongs to, numbered from 1 in document order, so section 1 is the first of
/// <see cref="DocxDocument.PageSetups"/>.
/// </param>
/// <param name="Kind">"first" for the section's first page, "even" for even pages, or "default" for the rest.</param>
/// <param name="Blocks">Its paragraphs, tables and other blocks, or null when its part can't be found.</param>
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

    /// <summary>
    /// The inserted DOCX as read, or null for another format, a DOCX that can't be opened or is missing, or one nested
    /// more than five inserted documents deep.
    /// </summary>
    public DocxDocument? Document { get; init; }

    /// <summary>
    /// Whether the inserted file is a DOCX that couldn't be opened, such as a broken or too large one.
    /// </summary>
    public bool Unreadable { get; init; }

    /// <summary>
    /// The first 16 hex digits of the SHA-256 of the inserted file, when it isn't read into <see cref="Document"/>;
    /// null when the file is missing.
    /// </summary>
    public string? Hash { get; init; }
}

/// <summary>
/// A table. <see cref="Width"/> and cell widths are in points, written as "468", or as "50%" of the page or "auto".
/// Margins are "side:points", such as "left:5.4"; borders are "side:style:points:color", such as
/// "top:single:0.5:000000", without a color when it's automatic.
/// </summary>
public sealed record DocxTable : DocxBlock
{
    /// <summary>The table style's id, or null when the table names none.</summary>
    public string? Style { get; init; }

    /// <summary>The width of each column of the table's grid, in points.</summary>
    public IReadOnlyList<double> Widths { get; init; } = [];

    /// <summary>The width of the whole table.</summary>
    public string? Width { get; init; }

    /// <summary>The space between each cell's edge and its text, unless a cell sets its own.</summary>
    public IReadOnlyList<string> CellMargins { get; init; } = [];

    /// <summary>The table's own borders, one per side the table sets, such as "top:single:0.5:000000".</summary>
    public IReadOnlyList<string> Borders { get; init; } = [];

    /// <summary>The table's title, the first part of its alt text in Word.</summary>
    public string? Caption { get; init; }

    /// <summary>The table's description, the rest of its alt text in Word.</summary>
    public string? Description { get; init; }

    /// <summary>The rows in order, including those inside content controls such as a repeating section.</summary>
    public IReadOnlyList<DocxRow> Rows { get; init; } = [];

    /// <summary>Bookmarks and content PDiffy can't read between the rows.</summary>
    public IReadOnlyList<DocxBetween> Between { get; init; } = [];

    /// <summary>Tracked changes to the table's properties ("table-format") or its columns ("table-grid").</summary>
    public IReadOnlyList<DocxRevision> Revisions { get; init; } = [];
}

/// <summary>A table row. A header row repeats at the top of each page the table runs onto.</summary>
/// <param name="Cells">The cells in order, including those inside content controls around cells.</param>
/// <param name="IsHeader">Whether the row is a header row (w:tblHeader).</param>
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
/// <param name="Before">The index, counting from 0, of the row or cell after it.</param>
/// <param name="Block">The bookmark or unread content.</param>
public sealed record DocxBetween(int Before, DocxBlock Block);

/// <summary>
/// A table cell. <see cref="Span"/> is how many grid columns it covers; <see cref="Merge"/> is "restart" for the top of
/// cells merged down a column and "continue" for those below it; <see cref="Shading"/> is its fill color in hex. Width,
/// margins and borders are written as for <see cref="DocxTable"/>.
/// </summary>
/// <param name="Blocks">The paragraphs, tables and other blocks in the cell.</param>
/// <param name="Span">How many columns of the table's grid the cell covers, 1 unless it's merged across.</param>
/// <param name="Merge">"restart" or "continue", or null when it isn't merged with the cells above or below it.</param>
/// <param name="Shading">The fill color in upper-case hex, such as "D9E2F3", or null when the cell has no fill.</param>
public sealed record DocxCell(IReadOnlyList<DocxBlock> Blocks, int Span = 1, string? Merge = null, string? Shading = null)
{
    /// <summary>The cell's width, such as "120", "25%" or "auto", or null when the cell doesn't set one.</summary>
    public string? Width { get; init; }

    /// <summary>
    /// The cell's own margins, such as "left:5.4", used instead of <see cref="DocxTable.CellMargins"/> on those sides.
    /// </summary>
    public IReadOnlyList<string> Margins { get; init; } = [];

    /// <summary>The cell's own borders, such as "bottom:double:1.5", one per side the cell sets.</summary>
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
