using PDiffy.Reading;

namespace PDiffy;

/// <summary>Everything PDiffy reads from a PDF, in the order a snapshot prints it.</summary>
public sealed record PdfUaDocument
{
    /// <summary>Whether it's tagged, its language and page count, and whether a viewer shows its title.</summary>
    public required DocumentInfo Document { get; init; }

    /// <summary>The title, author and other metadata from the Info dictionary and XMP.</summary>
    public required Metadata Metadata { get; init; }

    /// <summary>
    /// How a viewer opens, shows or restricts the file, such as its layout, encryption or attachments.
    /// </summary>
    public DocumentSettings Settings { get; init; } = new();

    /// <summary>The custom structure types and the type each maps to; empty when the PDF has no role map.</summary>
    public IReadOnlyList<RoleMapping> RoleMap { get; init; } = [];

    /// <summary>
    /// Every page in order, including any that couldn't be loaded (<see cref="PageInfo.Unreadable"/>).
    /// </summary>
    public IReadOnlyList<PageInfo> Pages { get; init; } = [];

    /// <summary>
    /// The page label ranges, ordered by their first page; empty when the PDF doesn't label its pages.
    /// </summary>
    public IReadOnlyList<PageLabelRange> PageLabels { get; init; } = [];

    /// <summary>The top-level bookmarks, with nested ones in their <see cref="Bookmark.Children"/>.</summary>
    public IReadOnlyList<Bookmark> Bookmarks { get; init; } = [];

    /// <summary>
    /// Set when the bookmarks nest more than 256 deep or loop back on themselves, so they can't be read safely;
    /// <see cref="Bookmarks"/> is then empty.
    /// </summary>
    public bool BookmarksUnreadable { get; init; }

    /// <summary>
    /// The structure tree's top-level elements, with the rest in their <see cref="StructureElement.Children"/>; empty
    /// for an untagged PDF. <see cref="AllElements"/> lists every element.
    /// </summary>
    public IReadOnlyList<StructureElement> Structure { get; init; } = [];

    /// <summary>Artifact text, such as page numbers, headers and footers, page by page.</summary>
    public IReadOnlyList<PageText> Artifacts { get; init; } = [];

    /// <summary>
    /// Text assistive technology can't reach, page by page: text outside any tag, one entry per line, and tagged text that
    /// no structure element points to, one entry per marked-content ID.
    /// </summary>
    public IReadOnlyList<PageText> Untagged { get; init; } = [];

    /// <summary>Every font the pages use, once each.</summary>
    public IReadOnlyList<FontInfo> Fonts { get; init; } = [];

    /// <summary>
    /// Every annotation on every page, in each page's own order. Those a structure element points to are also in its
    /// <see cref="StructureElement.Annotations"/>.
    /// </summary>
    public IReadOnlyList<Annotation> Annotations { get; init; } = [];

    /// <summary>Every field in the form; empty when the document has no form.</summary>
    public IReadOnlyList<FormField> FormFields { get; init; } = [];

    /// <summary>Settings for the whole form; null when the document has no form.</summary>
    public FormSettings? Form { get; init; }

    /// <summary>The text, page by page, in runs that share a font, size, color, render mode and role.</summary>
    public IReadOnlyList<TextRun> TextRuns { get; init; } = [];

    /// <summary>Every image drawn on every page, page by page.</summary>
    public IReadOnlyList<ImageInfo> Images { get; init; } = [];

    /// <summary>Every painted vector path, such as a line, box or filled shape, page by page.</summary>
    public IReadOnlyList<PathInfo> Paths { get; init; } = [];

    /// <summary>
    /// Reads the PDF in a stream from its start, wherever the stream was left, and puts its position back afterwards. The
    /// stream is left open.
    /// </summary>
    /// <remarks>
    /// A page that can't be loaded comes back as <see cref="PageInfo.Unreadable"/>; a file that can't be opened throws.
    /// An empty stream, a ZIP file or a web page throws <see cref="InvalidDataException"/> saying so.
    /// </remarks>
    public static PdfUaDocument Load(Stream pdf) => InputStream.Read(pdf, DocumentFormat.Pdf, PdfPigReader.Read);

    /// <summary>Reads the PDF in a byte array.</summary>
    public static PdfUaDocument Load(byte[] pdf) => Load(new MemoryStream(pdf, writable: false));

    /// <summary>Reads the PDF file at a path.</summary>
    /// <remarks>A page that can't be loaded comes back as <see cref="PageInfo.Unreadable"/>; a file that can't be opened throws.</remarks>
    public static PdfUaDocument Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>The canonical snapshot text: one element per line, LF endings.</summary>
    /// <param name="configure">What to leave out, e.g. <c>o => o.Ignore(Section.Metadata).Ignore(Field.Positions)</c>.</param>
    public string ToSnapshot(Action<SnapshotOptions>? configure = null)
    {
        var options = new SnapshotOptions();
        configure?.Invoke(options);
        return SnapshotWriter.Write(this, options);
    }

    /// <summary>
    /// The differences between this document's snapshot and another's, element by element: what was added, removed,
    /// moved or changed, and where. Use it to compare two PDFs directly, such as before and after a library upgrade.
    /// </summary>
    public SnapshotDiff CompareTo(PdfUaDocument other, Action<SnapshotOptions>? configure = null) =>
        SnapshotDiff.Compare(ToSnapshot(configure), other.ToSnapshot(configure));

    /// <summary>
    /// Applies the checks and lists what they find, grouped by check in the order <see cref="PdfCheck"/> declares them;
    /// an empty list means every check passed. With no checks given, applies every check.
    /// </summary>
    public IReadOnlyList<CheckFinding> Check(params PdfCheck[] checks) =>
        PdfChecker.Run(this, checks.Length == 0 ? Enum.GetValues<PdfCheck>() : checks);

    /// <summary>Every structure element, in reading order.</summary>
    public IEnumerable<StructureElement> AllElements() => Structure.SelectMany(root => root.Descendants().Prepend(root));

    /// <summary>
    /// Structure elements of a type, such as <see cref="StructureType.Figure"/>, matched on the type or its role-mapped
    /// type, in reading order.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The type is neither a standard type, nor in the role map, nor used in the document, so a typo such as "figure"
    /// can't make an assertion over no elements pass.
    /// </exception>
    public IEnumerable<StructureElement> Elements(string type)
    {
        var elements = AllElements().Where(element => element.Is(type)).ToList();
        if (elements.Count == 0 && !StructureTypes.IsKnown(type, RoleMap))
        {
            var used = AllElements().Select(element => element.Type);
            throw new ArgumentException(StructureTypes.Unknown(type, RoleMap, used), nameof(type));
        }

        return elements;
    }

    /// <summary>Every heading, H1 to H6 and H, with its level, role-mapped types included, in reading order.</summary>
    public IReadOnlyList<Heading> Headings() =>
        AllElements().Select(element => (Element: element, Type: element.TypeOrMapped))
            .Where(pair => pair.Type == StructureType.Heading || StructureTypes.HeadingLevel(pair.Type) is not null)
            .Select(pair => new Heading(StructureTypes.HeadingLevel(pair.Type), pair.Element))
            .ToList();
}

/// <summary>What the PDF's catalog says about the document as a whole, and its page count.</summary>
/// <param name="Marked">Whether the PDF says it's tagged (MarkInfo's Marked).</param>
/// <param name="Lang">The document's default language, such as "en-US"; null when it doesn't set one.</param>
/// <param name="DisplayDocTitle">Whether a viewer shows the metadata's title in its title bar, not the file
/// name.</param>
/// <param name="PageCount">How many pages the document has, including any that couldn't be loaded.</param>
public sealed record DocumentInfo(bool Marked, string? Lang, bool DisplayDocTitle, int PageCount);

/// <summary>
/// Document metadata from the Info dictionary and XMP. <see cref="OtherInfo"/> holds every Info entry not covered by the
/// other properties, such as a company name or a classification label.
/// </summary>
/// <param name="Title">The Info dictionary's title; null when it has none. <paramref name="XmpTitle"/> is the
/// XMP's.</param>
/// <param name="Author">The Info dictionary's author; null when it has none.</param>
/// <param name="Subject">The Info dictionary's subject; null when it has none.</param>
/// <param name="Keywords">The Info dictionary's keywords, as one string the way the PDF writes them; null when it has
/// none.</param>
/// <param name="HasCreationDate">Whether the Info dictionary has a creation date. The date itself is left out, since it
/// changes every time the file is made.</param>
/// <param name="HasModDate">Whether the Info dictionary has a modification date, which is left out for the same
/// reason.</param>
/// <param name="Producer">The program that wrote the PDF, such as a PDF library; null when the Info dictionary doesn't
/// say. Snapshots print only whether it's there unless <see cref="SnapshotOptions.ShowProducerAndCreator"/> is
/// used.</param>
/// <param name="Creator">The program the document was made in before it became a PDF, such as a word processor; null
/// when the Info dictionary doesn't say. Printed like <paramref name="Producer"/>.</param>
/// <param name="PdfUaPart">The PDF/UA part the XMP claims (pdfuaid:part), such as "1" or "2"; null when it claims
/// none.</param>
/// <param name="XmpTitle">The XMP title (dc:title), not the Info dictionary's <paramref name="Title"/>; the last
/// language version when it has several, and null when the XMP has none.</param>
public sealed record Metadata(
    string? Title, string? Author, string? Subject, string? Keywords, bool HasCreationDate, bool HasModDate,
    string? Producer, string? Creator, string? PdfUaPart, string? XmpTitle)
{
    /// <summary>The Info entries the other properties don't cover, sorted by name.</summary>
    public IReadOnlyList<Setting> OtherInfo { get; init; } = [];

    /// <summary>The XMP creator (dc:creator), several joined with "; "; null when the XMP has none.</summary>
    public string? XmpCreator { get; init; }

    /// <summary>The XMP description (dc:description), several joined with "; "; null when the XMP has none.</summary>
    public string? XmpDescription { get; init; }

    /// <summary>The XMP language (dc:language), several joined with "; "; null when the XMP has none.</summary>
    public string? XmpLanguage { get; init; }

    /// <summary>The PDF/A part and conformance level the XMP claims, such as "2B".</summary>
    public string? PdfAConformance { get; init; }

    /// <summary>Set when the XMP isn't well-formed XML, has a DTD or nests more than 256 deep; the XMP values are then null.</summary>
    public bool XmpUnreadable { get; init; }
}

/// <summary>One role map entry: a custom structure type and the type it maps to.</summary>
/// <param name="Custom">The custom type, as elements use it.</param>
/// <param name="Standard">
/// The type it maps to, usually a standard one such as <see cref="StructureType.Paragraph"/>. It can be another custom
/// type, which maps on in turn; <see cref="StructureElement.MappedType"/> gives the end of the chain.
/// </param>
public sealed record RoleMapping(string Custom, string Standard);

/// <summary>
/// A page. <see cref="Unreadable"/> is set when the PDF library could not load it; its other values are then empty.
/// The boxes are set only when the page declares them (or, for the crop box, inherits one).
/// </summary>
/// <param name="Number">The page number, counted from 1.</param>
/// <param name="Width">The media box's width in points, before <paramref name="Rotation"/> turns the page, so a
/// portrait page turned 90 degrees still has its portrait width; 0 for an unreadable page.</param>
/// <param name="Height">The media box's height in points, before <paramref name="Rotation"/> turns the page; 0 for an
/// unreadable page.</param>
/// <param name="Rotation">How far a viewer turns the page clockwise, in degrees: 0, 90, 180 or 270.</param>
/// <param name="Tabs">The tab order of the page's links and form fields: "R" (rows), "C" (columns) or "S" (structure
/// order, which PDF/UA requires on a page with annotations), or another name the PDF writes; null when the page doesn't
/// set one.</param>
/// <param name="HasAnnotations">Whether the page has annotations, such as links or form fields.</param>
/// <param name="Unreadable">Whether the PDF library couldn't load the page.</param>
public sealed record PageInfo(int Number, double Width, double Height, int Rotation, string? Tabs, bool HasAnnotations, bool Unreadable = false)
{
    /// <summary>
    /// The area a viewer shows; null when neither the page nor a parent sets one, so all of it shows.
    /// </summary>
    public PageBox? CropBox { get; init; }

    /// <summary>The area to keep when printing with bleed; null when the page doesn't set one.</summary>
    public PageBox? BleedBox { get; init; }

    /// <summary>The finished page after trimming; null when the page doesn't set one.</summary>
    public PageBox? TrimBox { get; init; }

    /// <summary>The area of the page's meaningful content; null when the page doesn't set one.</summary>
    public PageBox? ArtBox { get; init; }

    /// <summary>
    /// The size of one unit in points, for pages larger than PDF's usual limit; null when the page doesn't set it,
    /// which means 1.
    /// </summary>
    public double? UserUnit { get; init; }

    /// <summary>The page's transparency group, which changes how overlapping content blends.</summary>
    public PageGroup? Group { get; init; }
}

/// <summary>A page group: its type (usually "Transparency"), the color space it blends in, and its isolated and knockout options.</summary>
/// <param name="Type">The group's subtype; "Transparency" is the only one PDF defines. Null when the PDF leaves it
/// out.</param>
/// <param name="ColorSpace">The color space content blends in, such as "DeviceRGB", or an array in brackets for one
/// such as ICCBased; null when the group doesn't set one.</param>
/// <param name="Isolated">Whether the group blends against a transparent backdrop instead of what's under it.</param>
/// <param name="Knockout">Whether each object in the group covers the group's earlier ones instead of blending with
/// them.</param>
public sealed record PageGroup(string? Type, string? ColorSpace, bool Isolated, bool Knockout);

/// <summary>A rectangle in whole points from the page's bottom-left corner.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The bottom edge.</param>
/// <param name="Width">The width; never negative, whichever corners the PDF gives.</param>
/// <param name="Height">The height; never negative, whichever corners the PDF gives.</param>
public sealed record PageBox(int X, int Y, int Width, int Height);

/// <summary>
/// A range of page labels, such as roman numerals for the front pages, from <paramref name="FirstPage"/> until the next
/// range starts.
/// </summary>
/// <param name="FirstPage">The range's first page, counted from 1.</param>
/// <param name="Style">
/// The numbering: "D" (1, 2, 3), "R" (I, II, III), "r" (i, ii, iii), "A" (A, B, C) or "a" (a, b, c); null for labels
/// that are only the prefix.
/// </param>
/// <param name="Prefix">Text before each number, such as "Map-"; null when there is none.</param>
/// <param name="Start">The number of the range's first page; 1 when the PDF doesn't say.</param>
public sealed record PageLabelRange(int FirstPage, string? Style, string? Prefix, int Start);

/// <summary>
/// A bookmark. <see cref="Page"/> is set when it goes to a page of this document, <see cref="Uri"/> when it opens a web
/// address and <see cref="File"/> when it opens another file.
/// </summary>
/// <param name="Title">The text the bookmark shows, with whitespace runs collapsed to one space.</param>
/// <param name="Page">The page it goes to in this document, counted from 1; null when it goes anywhere else or
/// nowhere.</param>
/// <param name="Children">The bookmarks nested under it, in order; empty when there are none.</param>
public sealed record Bookmark(string Title, int? Page, IReadOnlyList<Bookmark> Children)
{
    /// <summary>The page and view it opens: in this document, or in <see cref="File"/>. Its zoom isn't read.</summary>
    public Destination? Destination { get; init; }

    /// <summary>The web address it opens.</summary>
    public string? Uri { get; init; }

    /// <summary>The other file it opens.</summary>
    public string? File { get; init; }
}

/// <summary>
/// Where a link, bookmark or action goes: a page, the view a viewer opens it at (a PDF view type such as XYZ, Fit or FitH)
/// and that view's positions in whole points, with null for a position the viewer keeps as it is. <see cref="Name"/> is
/// set, and the rest empty, when the PDF names a destination it doesn't define.
/// </summary>
public sealed record Destination
{
    /// <summary>The page, counted from 1; null when the PDF points at something that isn't a page.</summary>
    public int? Page { get; init; }

    /// <summary>The view type, such as XYZ (a position), Fit (the whole page) or FitH (the page's width).</summary>
    public string? View { get; init; }

    /// <summary>The view's positions, such as the left and top of an XYZ view.</summary>
    public IReadOnlyList<int?> Positions { get; init; } = [];

    /// <summary>An XYZ view's zoom, where 1 is 100%; null when the viewer keeps its zoom.</summary>
    public double? Zoom { get; init; }

    /// <summary>The name of a destination the PDF doesn't define, or one in another file.</summary>
    public string? Name { get; init; }
}

/// <summary>A heading in the structure tree. <see cref="Level"/> is 1 to 6, or null for a plain H.</summary>
/// <param name="Level">1 for H1 through 6 for H6; null for H, whose level comes from how it's nested.</param>
/// <param name="Element">The heading's structure element, with its text and children.</param>
public sealed record Heading(int? Level, StructureElement Element);

/// <summary>A structure element. <see cref="Name"/> replaces the type in the snapshot when other elements refer to it (TH1, TH2).</summary>
public sealed record StructureElement
{
    /// <summary>
    /// The type as the PDF writes it: a standard type (see <see cref="StructureType"/>) or a custom one the role map
    /// maps; "(none)" when the element has no type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>The child elements, in the order the element lists them, which is reading order.</summary>
    public IReadOnlyList<StructureElement> Children { get; init; } = [];

    /// <summary>
    /// The type the role map maps a custom <see cref="Type"/> to, at the end of the chain when one custom type maps to
    /// another; null when the type isn't mapped.
    /// </summary>
    public string? MappedType { get; init; }

    /// <summary>
    /// The name, such as TH1, that other elements' <see cref="Headers"/> use for it; null when none does.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The first page the element's own marked content or annotations are on; null when it has none on a page. Its
    /// children's pages don't count.
    /// </summary>
    public int? FirstPage { get; init; }

    /// <summary>The last page, found the same way as <see cref="FirstPage"/>.</summary>
    public int? LastPage { get; init; }

    /// <summary>The language the element sets for itself, such as "fr"; null when it uses its parent's.</summary>
    public string? Lang { get; init; }

    /// <summary>The alt text, such as a figure's description of a trail map; null when it has none.</summary>
    public string? Alt { get; init; }

    /// <summary>
    /// Text a screen reader reads instead of the content, such as the words on a photo of a trail sign.
    /// </summary>
    public string? ActualText { get; init; }

    /// <summary>The full form of an abbreviation, such as "Mount" for "Mt."; null when it has none.</summary>
    public string? Expansion { get; init; }

    /// <summary>The element's title, a name for it that isn't part of the page's text; null when it has none.</summary>
    public string? Title { get; init; }

    /// <summary>A table header cell's scope: "Row", "Column" or "Both"; null when it doesn't set one.</summary>
    public string? Scope { get; init; }

    /// <summary>
    /// The header cells a table cell names, by their <see cref="Name"/>, or "?id" for an ID no element has; null when
    /// the cell has no Headers attribute.
    /// </summary>
    public IReadOnlyList<string>? Headers { get; init; }

    /// <summary>How many rows a table cell spans; null when it doesn't say, which means 1.</summary>
    public int? RowSpan { get; init; }

    /// <summary>How many columns a table cell spans; null when it doesn't say, which means 1.</summary>
    public int? ColSpan { get; init; }

    /// <summary>A list's numbering, such as "Decimal", "Disc" or "UpperRoman"; null when it doesn't set one.</summary>
    public string? ListNumbering { get; init; }

    /// <summary>A table's summary of its purpose and layout; null when it has none.</summary>
    public string? Summary { get; init; }

    /// <summary>
    /// The annotations the element points to, such as links and form widgets, in the order it lists them.
    /// </summary>
    public IReadOnlyList<Annotation> Annotations { get; init; } = [];

    /// <summary>How many of the element's marked-content references name no page, so their text can't be found.</summary>
    public int McidsWithoutPage { get; init; }

    /// <summary>
    /// How many of the element's marked-content references point into a form XObject. Their text is read, but a form's
    /// MCIDs can't be told from the page's own, so it includes the page's content for the same MCID, if any.
    /// </summary>
    public int McidsInXObject { get; init; }

    /// <summary>
    /// How many child elements are left out because they're nested more than 256 deep. Their content then counts as
    /// untagged.
    /// </summary>
    public int UnreadChildren { get; init; }

    /// <summary>Marked content in the element that sets its own language, replacement text, alt text or expansion.</summary>
    public IReadOnlyList<MarkedSpan> MarkedSpans { get; init; } = [];

    /// <summary>
    /// The hashes of the images the element's own marked content draws, in the order the element lists its marked
    /// content, as <see cref="ImageInfo.Hash"/> gives them: the image a figure's alt text describes.
    /// </summary>
    public IReadOnlyList<string> ImageHashes { get; init; } = [];

    /// <summary>
    /// The text of the element's own marked content. Text in child elements, such as a link in a paragraph or a
    /// paragraph in a table cell, is in <see cref="AllText"/>.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>The element's text with its descendants', in the order the element lists them.</summary>
    public string? AllText { get; init; }

    /// <summary>
    /// Where the element is, as a check finding and a diff of whole-document snapshots show it:
    /// <c>structure > Document > Table > TR[2]</c>. With <c>OnlyPages</c>, a diff numbers only the siblings it kept.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>The element's children, their children and so on, in reading order.</summary>
    public IEnumerable<StructureElement> Descendants() =>
        Children.SelectMany(child => child.Descendants().Prepend(child));

    /// <summary>
    /// Descendants of a type, such as <see cref="StructureType.Paragraph"/>, matched on the type or its role-mapped
    /// type. Unlike <see cref="PdfUaDocument.Elements(string)"/>, it doesn't check the type, so use the
    /// <see cref="StructureType"/> constants.
    /// </summary>
    public IEnumerable<StructureElement> Elements(string type) => Descendants().Where(element => element.Is(type));

    internal bool Is(string type) => Type == type || MappedType == type;

    internal string TypeOrMapped => MappedType ?? Type;
}

/// <summary>
/// An annotation: a link, comment, form widget and so on. Its rectangle is in whole points from the page's bottom-left
/// corner. <see cref="Role"/> is <see cref="ContentRole.Tagged"/> when a structure element points to it.
/// </summary>
public sealed record Annotation
{
    /// <summary>
    /// The kind of annotation, such as "Link", "Widget" or "Text"; "(none)" when the PDF doesn't say.
    /// </summary>
    public required string Subtype { get; init; }

    /// <summary>
    /// The page it's on, counted from 1; null only for one a structure element points to when neither names its page.
    /// </summary>
    public int? Page { get; init; }

    /// <summary>The rectangle's left edge, in whole points from the page's left edge.</summary>
    public int X { get; init; }

    /// <summary>The rectangle's bottom edge, in whole points from the page's bottom edge.</summary>
    public int Y { get; init; }

    /// <summary>The rectangle's width in whole points.</summary>
    public int Width { get; init; }

    /// <summary>The rectangle's height in whole points.</summary>
    public int Height { get; init; }

    /// <summary>How a viewer treats it: whether it is shown, printed, editable and so on.</summary>
    public AnnotationFlags Flags { get; init; }

    /// <summary>
    /// <see cref="ContentRole.Tagged"/> when a structure element points to it, otherwise <see
    /// cref="ContentRole.Untagged"/>.
    /// </summary>
    public ContentRole Role { get; init; }

    /// <summary>The web address it opens; null when it opens none.</summary>
    public string? Uri { get; init; }

    /// <summary>
    /// The type of action it runs, such as "GoTo", "Launch" or "JavaScript"; null when it has no action or its action
    /// opens a web address (<see cref="Uri"/>), and "URI" for a web-address action that names no address.
    /// </summary>
    public string? Action { get; init; }

    /// <summary>Whether the link has its own destination entry. <see cref="Destination"/> says where it goes.</summary>
    public bool HasDestination { get; init; }

    /// <summary>Where the link goes, from its own destination or a GoTo or GoToR action.</summary>
    public Destination? Destination { get; init; }

    /// <summary>The file a GoToR or Launch action opens.</summary>
    public string? File { get; init; }

    /// <summary>
    /// The annotation's text, which for a link is its description for assistive technology; null when it has none.
    /// </summary>
    public string? Contents { get; init; }

    /// <summary>A form widget's tooltip, which a screen reader announces as its name; null when it has none.</summary>
    public string? AccessibleName { get; init; }
}

/// <summary>
/// Text on a page that no structure element holds: an artifact or untagged text. An artifact can say what kind it is,
/// in <see cref="Type"/> (Pagination, Layout, Page or Background) and <see cref="Subtype"/> (such as Header or Footer).
/// </summary>
/// <param name="Page">The page number, counted from 1.</param>
/// <param name="Text">The text, with whitespace runs collapsed to one space, such as "Page 4 of 12".</param>
public sealed record PageText(int Page, string Text)
{
    /// <summary>
    /// The artifact's kind: "Pagination", "Layout", "Page" or "Background"; null for untagged text and when it doesn't
    /// say.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// The artifact's subtype, such as "Header", "Footer" or "Watermark"; null for untagged text and when it doesn't
    /// say.
    /// </summary>
    public string? Subtype { get; init; }
}

/// <summary>
/// What marked content inside a structure element says about its own text: its language, replacement text, alt text
/// or expansion, which a screen reader uses instead of the element's.
/// </summary>
public sealed record MarkedSpan
{
    /// <summary>The language of the text, such as "fr"; null when it doesn't set one.</summary>
    public string? Language { get; init; }

    /// <summary>
    /// Text a screen reader reads instead of the content, such as the words on a photo of a trail sign.
    /// </summary>
    public string? ActualText { get; init; }

    /// <summary>Alt text that describes the content; null when it has none.</summary>
    public string? AltText { get; init; }

    /// <summary>The full form of an abbreviation, such as "Mount" for "Mt."; null when it has none.</summary>
    public string? Expansion { get; init; }

    /// <summary>
    /// The text the marked content covers, as it's drawn, such as "bonjour"; null when it covers no text, such as an
    /// image with alt text.
    /// </summary>
    public string? Text { get; init; }
}

/// <summary>
/// A font. <see cref="UnmappedCodes"/> lists the character codes its text uses that don't map to Unicode: that text can't
/// be copied, searched or read aloud correctly.
/// </summary>
/// <param name="Name">The font's name (BaseFont) without a subset prefix, such as "Helvetica-Bold" for
/// "ABCDEF+Helvetica-Bold"; "(none)" when the PDF doesn't name it.</param>
/// <param name="Subtype">The font's type, such as "Type1", "TrueType", "Type0" or "Type3"; null when the PDF leaves it
/// out.</param>
/// <param name="Embedded">Whether the PDF holds the font's file, so the text doesn't depend on the fonts installed
/// where it's opened.</param>
/// <param name="HasToUnicode">Whether the font has a ToUnicode map from its character codes to Unicode. A font without
/// one can still map its codes through its encoding; <see cref="UnmappedCodes"/> lists the ones nothing maps.</param>
public sealed record FontInfo(string Name, string? Subtype, bool Embedded, bool HasToUnicode)
{
    /// <summary>
    /// The character codes the font's text uses that don't map to Unicode, smallest first; empty when every code maps.
    /// </summary>
    public IReadOnlyList<int> UnmappedCodes { get; init; } = [];
}
