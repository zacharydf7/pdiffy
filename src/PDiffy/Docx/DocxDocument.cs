using PDiffy.Docx.Reading;

namespace PDiffy;

/// <summary>Everything PDiffy reads from a Word document (DOCX), in the order a snapshot prints it.</summary>
public sealed record DocxDocument
{
    internal DocxDocument()
    {
    }

    /// <summary>Document-wide details: title, language, compatibility, protection and unreadable parts.</summary>
    public required DocxInfo Document { get; init; }

    /// <summary>Each section's page size, margins, columns and page numbering, in document order.</summary>
    public IReadOnlyList<PageSetup> PageSetups
    {
        get;
        init => field = ValueList<PageSetup>.Of(value);
    } = ValueList<PageSetup>.Empty;

    /// <summary>The styles the document uses, with their properties resolved through the styles they're based on.</summary>
    public IReadOnlyList<DocxStyle> Styles
    {
        get;
        init => field = ValueList<DocxStyle>.Of(value);
    } = ValueList<DocxStyle>.Empty;

    /// <summary>The lists the document uses, each with the levels it uses.</summary>
    public IReadOnlyList<DocxList> Lists
    {
        get;
        init => field = ValueList<DocxList>.Of(value);
    } = ValueList<DocxList>.Empty;

    /// <summary>
    /// Paragraphs, tables, content controls, inserted documents, bookmarks between them and content PDiffy can't read,
    /// in reading order.
    /// </summary>
    public IReadOnlyList<DocxBlock> Body
    {
        get;
        init => field = ValueList<DocxBlock>.Of(value);
    } = ValueList<DocxBlock>.Empty;

    /// <summary>Each section's headers, by kind.</summary>
    public IReadOnlyList<DocxHeaderFooter> Headers
    {
        get;
        init => field = ValueList<DocxHeaderFooter>.Of(value);
    } = ValueList<DocxHeaderFooter>.Empty;

    /// <summary>Each section's footers, by kind.</summary>
    public IReadOnlyList<DocxHeaderFooter> Footers
    {
        get;
        init => field = ValueList<DocxHeaderFooter>.Of(value);
    } = ValueList<DocxHeaderFooter>.Empty;

    /// <summary>Footnotes, endnotes and comments, with what each says.</summary>
    public IReadOnlyList<DocxNote> Notes
    {
        get;
        init => field = ValueList<DocxNote>.Of(value);
    } = ValueList<DocxNote>.Empty;

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

    /// <summary>
    /// The differences between this document's snapshot and a later one's, element by element, as
    /// <see cref="SnapshotDiff.Compare(DocxDocument, DocxDocument, Action{DocxSnapshotOptions}?)"/> gives them.
    /// </summary>
    /// <param name="after">The later document, whose new content counts as added.</param>
    /// <param name="configure">What to leave out or scrub from both, as for <see cref="ToSnapshot"/>.</param>
    public SnapshotDiff CompareTo(DocxDocument after, Action<DocxSnapshotOptions>? configure = null) =>
        SnapshotDiff.Compare(this, after, configure);

    /// <summary>
    /// Applies the checks and lists what they find, grouped by check in the order <see cref="DocxCheck"/> declares them
    /// and then in snapshot order; an empty list means every check passed. With no checks given, applies every check.
    /// </summary>
    public IReadOnlyList<DocxCheckFinding> Check(params DocxCheck[] checks) =>
        DocxChecker.Run(this, checks.Length == 0 ? DocxCheckSet.Latest.Checks : checks);

    /// <summary>
    /// Applies the rules in a set, such as <see cref="DocxCheckSet.Latest"/>, and lists what they find as
    /// <see cref="Check(DocxCheck[])"/> does.
    /// </summary>
    public IReadOnlyList<DocxCheckFinding> Check(DocxCheckSet set) => DocxChecker.Run(this, set.Checks);

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
    internal DocxInfo()
    {
    }

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

    /// <summary>
    /// What editing is restricted to, such as "readOnly" or "forms" (<see cref="DocxProtections"/>), or null when the
    /// document isn't protected.
    /// </summary>
    public string? Protection { get; init; }

    /// <summary>Whether even and odd pages get different headers and footers.</summary>
    public bool EvenAndOddHeaders { get; init; }

    /// <summary>Whether the file's properties record when it was created. The date itself isn't kept.</summary>
    public bool HasCreatedDate { get; init; }

    /// <summary>Whether the file's properties record when it was last modified. The date itself isn't kept.</summary>
    public bool HasModifiedDate { get; init; }

    /// <summary>Parts of the file that couldn't be read, such as "word/styles.xml" with broken XML.</summary>
    public IReadOnlyList<string> UnreadableParts
    {
        get;
        init => field = ValueList<string>.Of(value);
    } = ValueList<string>.Empty;
}

/// <summary>One section's page geometry. Lengths are in points.</summary>
public sealed record PageSetup
{
    internal PageSetup()
    {
    }

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

    /// <summary>
    /// How the section starts: "nextPage", "continuous", "evenPage", "oddPage" or "nextColumn", as in
    /// <see cref="DocxSectionBreaks"/>.
    /// </summary>
    public string Break { get; init; } = DocxSectionBreaks.NextPage;

    /// <summary>The number the section's first page gets, or null when numbering carries on from before.</summary>
    public int? PageNumberStart { get; init; }

    /// <summary>The page number format, such as "decimal" or "lowerRoman".</summary>
    public string? PageNumberFormat { get; init; }

    /// <summary>Whether the first page has its own header and footer.</summary>
    public bool TitlePage { get; init; }

    /// <summary>
    /// The kinds of header the section has: "default", "first" or "even" (<see cref="DocxHeaderFooterKinds"/>).
    /// </summary>
    public IReadOnlyList<string> Headers
    {
        get;
        init => field = ValueList<string>.Of(value);
    } = ValueList<string>.Empty;

    /// <summary>
    /// The kinds of footer the section has: "default", "first" or "even" (<see cref="DocxHeaderFooterKinds"/>).
    /// </summary>
    public IReadOnlyList<string> Footers
    {
        get;
        init => field = ValueList<string>.Of(value);
    } = ValueList<string>.Empty;

    /// <summary>
    /// A tracked change to the section's settings (<see cref="DocxRevisionKind.SectionFormat"/>), or null.
    /// </summary>
    public DocxRevision? Revision { get; init; }

    /// <summary>
    /// A hash of the section's own settings PDiffy doesn't read, such as w:pgBorders or w:lnNumType, so changing it
    /// changes the snapshot; null when it has none. Only its own formatting counts, not what it inherits, and noise
    /// Word writes everywhere is left out.
    /// </summary>
    public string? Other { get; init; }
}

/// <summary>A style the document uses. <see cref="Run"/> and <see cref="Paragraph"/> are resolved through its based-on chain.</summary>
public sealed record DocxStyle
{
    internal DocxStyle()
    {
    }

    /// <summary>The style's id, such as "Heading1", which paragraphs, runs and tables refer to it by.</summary>
    public required string Id { get; init; }

    /// <summary>"paragraph", "character", "table" or "numbering", as in <see cref="DocxStyleTypes"/>.</summary>
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

    /// <summary>
    /// A hash of the style's own run, paragraph and table formatting PDiffy doesn't read, such as w:kern or
    /// w:widowControl, so changing it changes the snapshot; null when it has none. Only its own formatting counts, not
    /// what it inherits, and noise Word writes everywhere is left out.
    /// </summary>
    public string? Other { get; init; }
}

/// <summary>How text looks. Sizes are in points; null means not set.</summary>
public sealed record RunFormat
{
    internal RunFormat()
    {
    }

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

    /// <summary>The underline kind, such as "single" or "double"; <see cref="DocxUnderlines"/> lists them.</summary>
    public string? Underline { get; init; }

    /// <summary>A hex color such as "FF0000"; null means automatic or not set.</summary>
    public string? Color { get; init; }

    /// <summary>A highlight color name, such as "yellow"; <see cref="DocxHighlights"/> lists them.</summary>
    public string? Highlight { get; init; }

    /// <summary>Text shown in capitals, whatever case it's typed in.</summary>
    public bool Caps { get; init; }

    /// <summary>Text shown in small capitals, whatever case it's typed in.</summary>
    public bool SmallCaps { get; init; }

    /// <summary>Text struck through with one line.</summary>
    public bool Strike { get; init; }

    /// <summary>"superscript" or "subscript", as in <see cref="DocxRunVerticalAligns"/>.</summary>
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

    /// <summary>
    /// A hash of the run's own direct formatting PDiffy doesn't read, such as character spacing (w:spacing) or w:shd,
    /// so changing it changes the snapshot; null when it has none. Only the run's own formatting counts, not its
    /// styles', and noise Word writes everywhere is left out.
    /// </summary>
    public string? Other { get; init; }
}

/// <summary>How a paragraph is laid out. Lengths are in points; null means not set.</summary>
public sealed record ParagraphFormat
{
    internal ParagraphFormat()
    {
    }

    /// <summary>
    /// "left", "center", "right", "both" (justified) and so on, as Word writes it; <see cref="DocxAlignments"/> lists
    /// them.
    /// </summary>
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

    /// <summary>Line spacing: a multiple such as 1.15, or a length with its rule, such as exactly 12 points.</summary>
    public DocxLineSpacing? LineSpacing { get; init; }

    /// <summary>Whether the paragraph is kept on the same page as the one after it.</summary>
    public bool KeepNext { get; init; }

    /// <summary>Whether all the paragraph's lines are kept on one page.</summary>
    public bool KeepLines { get; init; }

    /// <summary>Whether the paragraph starts on a new page.</summary>
    public bool PageBreakBefore { get; init; }

    /// <summary>The heading level from 1, when the paragraph is a heading.</summary>
    public int? HeadingLevel { get; init; }

    /// <summary>Custom tab stops, such as a right tab at 468 points with dots before it.</summary>
    public IReadOnlyList<DocxTabStop> Tabs
    {
        get;
        init => field = ValueList<DocxTabStop>.Of(value);
    } = ValueList<DocxTabStop>.Empty;

    /// <summary>A paragraph set right to left, such as one in Arabic or Hebrew.</summary>
    public bool RightToLeft { get; init; }

    /// <summary>Borders around the paragraph, one per side it has.</summary>
    public IReadOnlyList<DocxBorder> Borders
    {
        get;
        init => field = ValueList<DocxBorder>.Of(value);
    } = ValueList<DocxBorder>.Empty;

    /// <summary>The paragraph's fill color in hex.</summary>
    public string? Shading { get; init; }

    /// <summary>
    /// A hash of the paragraph's own formatting PDiffy doesn't read, such as w:widowControl or w:contextualSpacing, so
    /// changing it changes the snapshot; null when it has none. Only its own formatting counts, not what it inherits,
    /// and noise Word writes everywhere is left out.
    /// </summary>
    public string? Other { get; init; }
}

/// <summary>
/// Something in the body, a table cell, a header or footer, a note or a text box: a paragraph, a table, a content
/// control, an inserted document, a bookmark between them, or content PDiffy can't read.
/// </summary>
public abstract record DocxBlock
{
    private protected DocxBlock()
    {
    }
}
/// <summary>A paragraph: its style, list item, layout, text and what sits in the text.</summary>
public sealed record DocxParagraph : DocxBlock
{
    internal DocxParagraph()
    {
    }

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
    public IReadOnlyList<DocxSpan> Spans
    {
        get;
        init => field = ValueList<DocxSpan>.Of(value);
    } = ValueList<DocxSpan>.Empty;

    /// <summary>
    /// What sits in the text, in reading order: images, links, bookmarks, symbols, fields, page or column breaks, and
    /// stray text from runs in the paragraph's properties.
    /// </summary>
    public IReadOnlyList<DocxInline> Inlines
    {
        get;
        init => field = ValueList<DocxInline>.Of(value);
    } = ValueList<DocxInline>.Empty;

    /// <summary>The section this paragraph ends, numbered from 1, or null.</summary>
    public int? EndsSection { get; init; }
}

/// <summary>
/// A paragraph's place in a list: the list's id (w:numId), its level from 0, and the label Word shows, such as "A." or
/// "1.2", counted from the items before it. The label is null when the list or its level isn't defined.
/// </summary>
public sealed record DocxListItem
{
    internal DocxListItem()
    {
    }

    /// <summary>
    /// The list's id (w:numId), the <see cref="DocxList.Id"/> of its entry in <see cref="DocxDocument.Lists"/> when the
    /// list is defined. Never 0, since numId 0 takes a paragraph out of its list.
    /// </summary>
    public required int List { get; init; }

    /// <summary>The level from 0, the outermost: the paragraph's own w:ilvl, else its style's, else 0.</summary>
    public required int Level { get; init; }

    /// <summary>The label as Word shows it, such as "A." or "1.2", or the bullet character for a bullet.</summary>
    public required string? Label { get; init; }
}

/// <summary>
/// A list the document uses: its id, the definition it numbers from (w:abstractNumId, or the definition of the list style
/// it links to) and the levels used.
/// </summary>
public sealed record DocxList
{
    internal DocxList()
    {
    }

    /// <summary>The list's id (w:numId), which <see cref="DocxListItem.List"/> refers to it by.</summary>
    public required int Id { get; init; }

    /// <summary>
    /// The id of the definition the list numbers from, such as "3". Lists that share a definition share its counters,
    /// unless one restarts its numbering with a start override.
    /// </summary>
    public required string Definition { get; init; }

    /// <summary>The levels the document's paragraphs use, outermost first, not every level defined.</summary>
    public required IReadOnlyList<DocxListLevel> Levels { get; init => field = ValueList<DocxListLevel>.Of(value); }
}

/// <summary>
/// How one level of a list is numbered: its format (such as "decimal", "upperLetter" or "bullet"), its label pattern
/// (such as "%1." or a bullet character), the number it starts at, its indents in points and its bullet font. Format,
/// Text and Start are null when the list's definition has no such level.
/// </summary>
public sealed record DocxListLevel
{
    internal DocxListLevel()
    {
    }

    /// <summary>The level from 0, the outermost, to 8.</summary>
    public required int Level { get; init; }

    /// <summary>The number format, such as "lowerRoman"; "decimal" when the level doesn't set one.</summary>
    public required string? Format { get; init; }

    /// <summary>
    /// The label pattern, in which %1 to %9 stand for the counters of levels 0 to 8, so "%1.%2." prints as "2.1." for
    /// the first item under the second; a bullet's character for a bullet, and empty when the level doesn't set one.
    /// </summary>
    public required string? Text { get; init; }

    /// <summary>The number the level starts at: the list's own override, else the level's start, else 1.</summary>
    public required int? Start { get; init; }

    /// <summary>The left indent of the level's paragraphs in points, or null when the level sets none.</summary>
    public required double? Indent { get; init; }

    /// <summary>
    /// How far the label hangs out to the left of the text after it, in points, or null when the level doesn't set it.
    /// </summary>
    public required double? Hanging { get; init; }

    /// <summary>The label's font, such as "Symbol" for a bullet, or null when the level doesn't set one.</summary>
    public required string? Font { get; init; }
}

/// <summary>Part of a paragraph's text and how it looks.</summary>
public sealed record DocxSpan
{
    internal DocxSpan()
    {
    }

    /// <summary>
    /// The text, never empty. The spans of a paragraph, in order, make up its <see cref="DocxParagraph.Text"/>, and two
    /// next to each other always look different.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// The full formatting: the document defaults and the paragraph style, with the character style and the run's own
    /// formatting on top.
    /// </summary>
    public required RunFormat Format { get; init; }
}

/// <summary>Something in a paragraph's text. <see cref="Position"/> is where, counted in characters of its text.</summary>
public abstract record DocxInline
{
    private protected DocxInline()
    {
    }

    /// <summary>
    /// How many characters of <see cref="DocxParagraph.Text"/> come before it, so 0 is the start and the text's length
    /// is the end.
    /// </summary>
    public required int Position { get; init; }
}

/// <summary>
/// A field: its code, such as "PAGE" or "TOC \o \"1-3\"", and its result, the text Word last showed for it. The result
/// is also part of the paragraph's text, unless the field sits in another field's code, whose code it becomes part of.
/// </summary>
public sealed record DocxField : DocxInline
{
    internal DocxField()
    {
    }

    /// <summary>
    /// The field's code with each stretch of white space made one space and the ends trimmed. A field nested in it is
    /// replaced by that field's result, so { IF { PAGE } = 1 ... } on page 1 reads "IF 1 = 1 ...".
    /// </summary>
    public required string Code { get; init; }

    /// <summary>The text Word last showed for the field, or empty when it showed none.</summary>
    public required string Result { get; init; }
}

/// <summary>
/// The text, tabs and breaks of a run where WordprocessingML doesn't allow one: inside the paragraph's properties (w:pPr). Word may
/// drop or move it, so it isn't part of the paragraph's text, but it's recorded so the text isn't lost and the problem
/// shows. Its <see cref="DocxInline.Position"/> is always 0, before the paragraph's text.
/// </summary>
public sealed record DocxStrayText : DocxInline
{
    internal DocxStrayText()
    {
    }

    /// <summary>
    /// The run's text, with tabs as \t, line breaks as \n, page breaks as \f and column breaks as \u000E, the
    /// characters Word's own plain text uses; empty when the run holds none of these.
    /// </summary>
    public required string Text { get; init; }
}

/// <summary>
/// A tracked change and who made it. An insertion, deletion, move or run format change covers text; the paragraph
/// kinds are about the paragraph itself, with no text; table, row and cell kinds are on
/// <see cref="DocxTable.Revisions"/>, <see cref="DocxRow.Revisions"/> and <see cref="DocxCell.Revisions"/>, and
/// <see cref="DocxRevisionKind.SectionFormat"/> on <see cref="PageSetup.Revision"/>. Tables, rows, cells and sections
/// have no text and no position.
/// </summary>
public sealed record DocxRevision : DocxInline
{
    internal DocxRevision()
    {
    }

    /// <summary>What changed, such as an insertion or a deleted row.</summary>
    public required DocxRevisionKind Kind { get; init; }

    /// <summary>
    /// The text inserted, deleted, moved or reformatted, empty for the other kinds. Deleted and moved-away text isn't
    /// part of the paragraph's text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>Who made the change, as Word recorded it, or null when it didn't record anyone.</summary>
    public required string? Author { get; init; }
}

/// <summary>Where a footnote or endnote is referenced. <see cref="DocxDocument.Notes"/> has what the note says.</summary>
public sealed record DocxNoteReference : DocxInline
{
    internal DocxNoteReference()
    {
    }

    /// <summary>Whether it refers to a footnote or an endnote.</summary>
    public required DocxNoteKind Kind { get; init; }

    /// <summary>The <see cref="DocxNote.Id"/> of the note of the same kind that this refers to.</summary>
    public required int Id { get; init; }
}

/// <summary>Where a comment is attached. <see cref="DocxDocument.Notes"/> has what the comment says.</summary>
public sealed record DocxCommentReference : DocxInline
{
    internal DocxCommentReference()
    {
    }

    /// <summary>
    /// The <see cref="DocxNote.Id"/> of the <see cref="DocxNoteKind.Comment"/> note that this refers to.
    /// </summary>
    public required int Id { get; init; }
}

/// <summary>
/// A form control in the text: a content control or a legacy form field, as its <see cref="Kind"/> says. Name is a
/// content control's tag or a legacy field's name; a check box's Value is "yes" or "no".
/// </summary>
public sealed record DocxForm : DocxInline
{
    internal DocxForm()
    {
    }

    /// <summary>The kind of content control, or of legacy form field.</summary>
    public required DocxFormKind Kind { get; init; }

    /// <summary>A content control's tag or a legacy form field's name, or null when it has none.</summary>
    public string? Name { get; init; }

    /// <summary>A content control's title, which Word shows on it.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// What the control holds: the text it shows, "yes" or "no" for a check box, or a legacy drop-down's chosen entry.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>The choices of a drop-down or combo box.</summary>
    public IReadOnlyList<string> Items { get; init => field = ValueList<string>.Of(value); } = ValueList<string>.Empty;
}

/// <summary>A page or column break.</summary>
public sealed record DocxBreak : DocxInline
{
    internal DocxBreak()
    {
    }

    /// <summary>
    /// "page" or "column", as in <see cref="DocxBreakKinds"/>. A line break isn't a DocxBreak but \n in the paragraph's
    /// text.
    /// </summary>
    public required string Kind { get; init; }
}

/// <summary>A link: the text it covers and where it goes, a URL or "#bookmark" for a place in the document.</summary>
public sealed record DocxLink : DocxInline
{
    internal DocxLink()
    {
    }

    /// <summary>The text the link covers, as it is in the paragraph's text; empty when it covers none.</summary>
    public required string Text { get; init; }

    /// <summary>
    /// Where the link goes, such as "https://example.com/trails", with "#bookmark" on the end when it also names a
    /// place; just "#bookmark" for a place in this document. Null when the link's address can't be found or it has
    /// none.
    /// </summary>
    public required string? Target { get; init; }
}

/// <summary>
/// A named place in the document that links and cross-references can point to. A bookmark in a table can cover only
/// some columns: <see cref="FirstColumn"/> and <see cref="LastColumn"/> are then the first and last, counting from 0.
/// </summary>
public sealed record DocxBookmark : DocxInline
{
    internal DocxBookmark()
    {
    }

    /// <summary>
    /// The bookmark's name, such as "Ridge_Loop", which a link's "#Ridge_Loop" points to. Word's own "_GoBack" bookmark
    /// for the last edit is left out.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>The first table column covered; null unless it's a table bookmark covering only some columns.</summary>
    public int? FirstColumn { get; init; }

    /// <summary>The last table column covered; null unless it's a table bookmark covering only some columns.</summary>
    public int? LastColumn { get; init; }
}

/// <summary>A character from a symbol font, such as Wingdings: the font and the character's code in hex.</summary>
public sealed record DocxSymbol : DocxInline
{
    internal DocxSymbol()
    {
    }

    /// <summary>The symbol font, such as "Wingdings", or null when the symbol doesn't name one.</summary>
    public required string? Font { get; init; }

    /// <summary>
    /// The character's code in upper-case hex, such as "F0FC", and then the character is also in the paragraph's text.
    /// A code that isn't hex is kept as the file writes it and isn't in the text; empty when the symbol has no code.
    /// </summary>
    public required string Code { get; init; }
}

/// <summary>
/// An image or other graphic. Width and height are in points. For a picture, <see cref="Hash"/> is the first 16 hex
/// digits of the image's SHA-256, which catches a swapped image, and <see cref="Type"/> its file type, such as "png".
/// For an embedded object, Type is the program it belongs to, such as "Excel.Sheet.12", and Hash is of what it holds;
/// for an ActiveX control, Type is the control's name.
/// </summary>
public sealed record DocxImage : DocxInline
{
    internal DocxImage()
    {
    }
    /// <summary>
    /// "image" for a picture, "object" for an embedded or linked object such as a spreadsheet, "control" for an ActiveX
    /// control; otherwise what the graphic is, such as "chart", "diagram" or "shape".
    /// <see cref="DocxGraphicKinds"/> lists them.
    /// </summary>
    public string Kind { get; init; } = DocxGraphicKinds.Image;

    /// <summary>The file a linked object shows, which the document doesn't contain; null when it's embedded.</summary>
    public string? Source { get; init; }

    /// <summary>What a text box, or a shape with text in it, says.</summary>
    public IReadOnlyList<DocxBlock> TextBox
    {
        get;
        init => field = ValueList<DocxBlock>.Of(value);
    } = ValueList<DocxBlock>.Empty;

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
public sealed record DocxMath : DocxInline
{
    internal DocxMath()
    {
    }

    /// <summary>The characters of the equation's text runs (m:t), in order; empty when it has none.</summary>
    public required string Text { get; init; }

    /// <summary>
    /// The first 16 hex digits of a SHA-256 of the equation's XML, leaving out editing-session ids, relationship ids,
    /// namespace prefixes and the indenting between elements, which change without the equation changing.
    /// </summary>
    public required string Hash { get; init; }
}

/// <summary>
/// Something in a paragraph PDiffy doesn't read yet, such as a phonetic guide (w:ruby): the element's name and a hash of
/// its contents, so a change to it still shows.
/// </summary>
public sealed record DocxUnsupported : DocxInline
{
    internal DocxUnsupported()
    {
    }

    /// <summary>
    /// The element's name with its usual prefix, "w:" or "m:", whatever prefix the file uses, such as "w:ruby"; an
    /// element from another namespace is named in full, as "{namespace}name".
    /// </summary>
    public required string Element { get; init; }

    /// <summary>
    /// The first 16 hex digits of a SHA-256 of the element's XML, leaving out what <see cref="DocxMath.Hash"/> leaves
    /// out.
    /// </summary>
    public required string Hash { get; init; }
}

/// <summary>
/// A bookmark between paragraphs, tables, rows or cells, such as one that starts at a table; links can point to it.
/// </summary>
public sealed record DocxBookmarkBlock : DocxBlock
{
    internal DocxBookmarkBlock()
    {
    }

    /// <summary>The bookmark's name, as for <see cref="DocxBookmark.Name"/>.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The first column a table-column bookmark covers, as for <see cref="DocxBookmark.FirstColumn"/>.
    /// </summary>
    public int? FirstColumn { get; init; }

    /// <summary>The last column a table-column bookmark covers, as for <see cref="DocxBookmark.LastColumn"/>.</summary>
    public int? LastColumn { get; init; }
}

/// <summary>
/// Something between paragraphs, tables, rows or cells that PDiffy doesn't read yet: the element's name and a hash of
/// its contents, so a change to it still shows.
/// </summary>
public sealed record DocxUnsupportedBlock : DocxBlock
{
    internal DocxUnsupportedBlock()
    {
    }

    /// <summary>The element's name, written as for <see cref="DocxUnsupported.Element"/>.</summary>
    public required string Element { get; init; }

    /// <summary>The element's hash, worked out as for <see cref="DocxUnsupported.Hash"/>.</summary>
    public required string Hash { get; init; }
}

/// <summary>A footnote, endnote or comment: its kind, id, a comment's author and what it says.</summary>
public sealed record DocxNote
{
    internal DocxNote()
    {
    }

    /// <summary>Whether it's a footnote, an endnote or a comment.</summary>
    public required DocxNoteKind Kind { get; init; }

    /// <summary>
    /// The note's id, which <see cref="DocxNoteReference.Id"/> or <see cref="DocxCommentReference.Id"/> refers to it
    /// by; ids are only unique within a kind. 0 when the file gives none.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>A comment's author, or null for a footnote, an endnote or a comment that names no one.</summary>
    public required string? Author { get; init; }

    /// <summary>What the note says: its paragraphs, tables and other blocks.</summary>
    public required IReadOnlyList<DocxBlock> Blocks { get; init => field = ValueList<DocxBlock>.Of(value); }
}

/// <summary>A content control around paragraphs or tables, such as a repeating section or a rich-text box.</summary>
public sealed record DocxControl : DocxBlock
{
    internal DocxControl()
    {
    }

    /// <summary>The kind of control: <see cref="DocxFormKind.RichText"/> when the control sets no other kind.</summary>
    public required DocxFormKind Kind { get; init; }

    /// <summary>The control's tag.</summary>
    public string? Name { get; init; }

    /// <summary>The control's title, which Word shows on it.</summary>
    public string? Title { get; init; }

    /// <summary>The paragraphs, tables and other blocks inside the control.</summary>
    public IReadOnlyList<DocxBlock> Blocks
    {
        get;
        init => field = ValueList<DocxBlock>.Of(value);
    } = ValueList<DocxBlock>.Empty;
}

/// <summary>A section's header or footer of one kind: "default", "first" or "even". Blocks is null when its part can't be found.</summary>
public sealed record DocxHeaderFooter
{
    internal DocxHeaderFooter()
    {
    }

    /// <summary>
    /// The section it belongs to, numbered from 1 in document order, so section 1 is the first of <see
    /// cref="DocxDocument.PageSetups"/>.
    /// </summary>
    public required int Section { get; init; }

    /// <summary>
    /// "first" for the section's first page, "even" for even pages, or "default" for the rest, as in
    /// <see cref="DocxHeaderFooterKinds"/>.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>Its paragraphs, tables and other blocks, or null when its part can't be found.</summary>
    public required IReadOnlyList<DocxBlock>? Blocks
    {
        get;
        init => field = value is null ? null : ValueList<DocxBlock>.Of(value);
    }
}

/// <summary>
/// A document inserted into this one (w:altChunk). A DOCX is read in place into <see cref="Document"/>, with its own
/// styles and lists; other formats, such as HTML or RTF, and a DOCX that can't be opened (<see cref="Unreadable"/>) are
/// recorded by <see cref="Hash"/>, which is null when the inserted file is missing.
/// </summary>
public sealed record DocxInserted : DocxBlock
{
    internal DocxInserted()
    {
    }

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

/// <summary>A table: its grid, rows and cells, and the widths, margins and borders it sets.</summary>
public sealed record DocxTable : DocxBlock
{
    internal DocxTable()
    {
    }

    /// <summary>The table style's id, or null when the table names none.</summary>
    public string? Style { get; init; }

    /// <summary>The width of each column of the table's grid, in points.</summary>
    public IReadOnlyList<double> Widths { get; init => field = ValueList<double>.Of(value); } = ValueList<double>.Empty;

    /// <summary>The width of the whole table, or null when it doesn't set one.</summary>
    public DocxWidth? Width { get; init; }

    /// <summary>The space between each cell's edge and its text, unless a cell sets its own.</summary>
    public IReadOnlyList<DocxMargin> CellMargins
    {
        get;
        init => field = ValueList<DocxMargin>.Of(value);
    } = ValueList<DocxMargin>.Empty;

    /// <summary>The table's own borders, one per side the table sets.</summary>
    public IReadOnlyList<DocxBorder> Borders
    {
        get;
        init => field = ValueList<DocxBorder>.Of(value);
    } = ValueList<DocxBorder>.Empty;

    /// <summary>The table's title, the first part of its alt text in Word.</summary>
    public string? Caption { get; init; }

    /// <summary>The table's description, the rest of its alt text in Word.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// A hash of the table's own formatting PDiffy doesn't read, such as w:tblLook or w:tblInd, so changing it changes
    /// the snapshot; null when it has none. Only its own formatting counts, not what it inherits, and noise Word writes
    /// everywhere is left out.
    /// </summary>
    public string? Other { get; init; }

    /// <summary>The rows in order, including those inside content controls such as a repeating section.</summary>
    public IReadOnlyList<DocxRow> Rows
    {
        get;
        init => field = ValueList<DocxRow>.Of(value);
    } = ValueList<DocxRow>.Empty;

    /// <summary>Bookmarks and content PDiffy can't read between the rows.</summary>
    public IReadOnlyList<DocxBetween> Between
    {
        get;
        init => field = ValueList<DocxBetween>.Of(value);
    } = ValueList<DocxBetween>.Empty;

    /// <summary>
    /// Tracked changes to the table's properties (<see cref="DocxRevisionKind.TableFormat"/>) or its columns
    /// (<see cref="DocxRevisionKind.TableGrid"/>).
    /// </summary>
    public IReadOnlyList<DocxRevision> Revisions
    {
        get;
        init => field = ValueList<DocxRevision>.Of(value);
    } = ValueList<DocxRevision>.Empty;
}

/// <summary>A table row. A header row repeats at the top of each page the table runs onto.</summary>
public sealed record DocxRow
{
    internal DocxRow()
    {
    }

    /// <summary>The cells in order, including those inside content controls around cells.</summary>
    public required IReadOnlyList<DocxCell> Cells { get; init => field = ValueList<DocxCell>.Of(value); }

    /// <summary>Whether the row is a header row (w:tblHeader).</summary>
    public bool IsHeader { get; init; }

    /// <summary>The row's height, or null when the row doesn't set one.</summary>
    public DocxRowHeight? Height { get; init; }

    /// <summary>Whether the row is kept on one page rather than split across two.</summary>
    public bool CantSplit { get; init; }

    /// <summary>
    /// A hash of the row's own formatting PDiffy doesn't read, such as w:jc, so changing it changes the snapshot; null
    /// when it has none. Only its own formatting counts, not what it inherits, and noise Word writes everywhere is left
    /// out.
    /// </summary>
    public string? Other { get; init; }

    /// <summary>
    /// Tracked changes to the row: <see cref="DocxRevisionKind.RowInsert"/>, <see cref="DocxRevisionKind.RowDelete"/>,
    /// <see cref="DocxRevisionKind.RowFormat"/>, or <see cref="DocxRevisionKind.RowTableFormat"/> for the table
    /// properties the row sets for itself.
    /// </summary>
    public IReadOnlyList<DocxRevision> Revisions
    {
        get;
        init => field = ValueList<DocxRevision>.Of(value);
    } = ValueList<DocxRevision>.Empty;

    /// <summary>The content controls around the row, outermost first, such as a repeating section and its item.</summary>
    public IReadOnlyList<DocxControl> Controls
    {
        get;
        init => field = ValueList<DocxControl>.Of(value);
    } = ValueList<DocxControl>.Empty;

    /// <summary>Bookmarks and content PDiffy can't read between the cells.</summary>
    public IReadOnlyList<DocxBetween> Between
    {
        get;
        init => field = ValueList<DocxBetween>.Of(value);
    } = ValueList<DocxBetween>.Empty;
}

/// <summary>
/// A <see cref="DocxBookmarkBlock"/> or <see cref="DocxUnsupportedBlock"/> between a table's rows or a row's cells.
/// <see cref="Before"/> is the index of the row or cell that comes after it, or the count when it comes last.
/// </summary>
public sealed record DocxBetween
{
    internal DocxBetween()
    {
    }

    /// <summary>The index, counting from 0, of the row or cell after it.</summary>
    public required int Before { get; init; }

    /// <summary>The bookmark or unread content.</summary>
    public required DocxBlock Block { get; init; }
}

/// <summary>
/// A table cell. <see cref="Span"/> is how many grid columns it covers; <see cref="Merge"/> is "restart" for the top of
/// cells merged down a column and "continue" for those below it; <see cref="Shading"/> is its fill color in hex. Width,
/// margins and borders are written as for <see cref="DocxTable"/>.
/// </summary>
public sealed record DocxCell
{
    internal DocxCell()
    {
    }

    /// <summary>The paragraphs, tables and other blocks in the cell.</summary>
    public required IReadOnlyList<DocxBlock> Blocks { get; init => field = ValueList<DocxBlock>.Of(value); }

    /// <summary>How many columns of the table's grid the cell covers, 1 unless it's merged across.</summary>
    public int Span { get; init; } = 1;

    /// <summary>
    /// "restart" or "continue" (<see cref="DocxCellMerges"/>), or null when it isn't merged with the cells above or
    /// below it.
    /// </summary>
    public string? Merge { get; init; }

    /// <summary>The fill color in upper-case hex, such as "D9E2F3", or null when the cell has no fill.</summary>
    public string? Shading { get; init; }

    /// <summary>The cell's width, such as 120 points or 25%, or null when the cell doesn't set one.</summary>
    public DocxWidth? Width { get; init; }

    /// <summary>
    /// The cell's own margins, used instead of <see cref="DocxTable.CellMargins"/> on those sides.
    /// </summary>
    public IReadOnlyList<DocxMargin> Margins
    {
        get;
        init => field = ValueList<DocxMargin>.Of(value);
    } = ValueList<DocxMargin>.Empty;

    /// <summary>The cell's own borders, one per side the cell sets.</summary>
    public IReadOnlyList<DocxBorder> Borders
    {
        get;
        init => field = ValueList<DocxBorder>.Of(value);
    } = ValueList<DocxBorder>.Empty;

    /// <summary>
    /// Where the text sits in the cell as Word writes it, such as "center" or "bottom";
    /// <see cref="DocxCellVerticalAligns"/> lists them.
    /// </summary>
    public string? VerticalAlign { get; init; }

    /// <summary>
    /// The direction of the cell's text when the cell sets one, as Word writes it, such as "btLr";
    /// <see cref="DocxTextDirections"/> lists them.
    /// </summary>
    public string? TextDirection { get; init; }

    /// <summary>
    /// A hash of the cell's own formatting PDiffy doesn't read, such as w:noWrap, so changing it changes the snapshot;
    /// null when it has none. Only its own formatting counts, not what it inherits, and noise Word writes everywhere is
    /// left out.
    /// </summary>
    public string? Other { get; init; }

    /// <summary>
    /// Tracked changes to the cell: <see cref="DocxRevisionKind.CellInsert"/>,
    /// <see cref="DocxRevisionKind.CellDelete"/>, <see cref="DocxRevisionKind.CellMerge"/> or
    /// <see cref="DocxRevisionKind.CellFormat"/>.
    /// </summary>
    public IReadOnlyList<DocxRevision> Revisions
    {
        get;
        init => field = ValueList<DocxRevision>.Of(value);
    } = ValueList<DocxRevision>.Empty;

    /// <summary>The content controls around the cell, outermost first.</summary>
    public IReadOnlyList<DocxControl> Controls
    {
        get;
        init => field = ValueList<DocxControl>.Of(value);
    } = ValueList<DocxControl>.Empty;
}
