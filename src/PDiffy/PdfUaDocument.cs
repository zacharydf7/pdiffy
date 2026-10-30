using PDiffy.Reading;

namespace PDiffy;

/// <summary>Everything PDiffy reads from a PDF, in the order a snapshot prints it.</summary>
public sealed record PdfUaDocument
{
    internal PdfUaDocument()
    {
    }

    /// <summary>Whether it's tagged, its language and page count, and whether a viewer shows its title.</summary>
    public required PdfDocumentInfo Document { get; init; }

    /// <summary>The title, author and other metadata from the Info dictionary and XMP.</summary>
    public required PdfMetadata Metadata { get; init; }

    /// <summary>
    /// How a viewer opens, shows or restricts the file, such as its layout, encryption or attachments.
    /// </summary>
    public PdfSettings Settings { get; init; } = new();

    /// <summary>The custom structure types and the type each maps to; empty when the PDF has no role map.</summary>
    public IReadOnlyList<PdfRoleMapping> RoleMap
    {
        get;
        init => field = ValueList<PdfRoleMapping>.Of(value);
    } = ValueList<PdfRoleMapping>.Empty;

    /// <summary>
    /// Every page in order, including any that couldn't be loaded (<see cref="PdfPage.Unreadable"/>).
    /// </summary>
    public IReadOnlyList<PdfPage> Pages
    {
        get;
        init => field = ValueList<PdfPage>.Of(value);
    } = ValueList<PdfPage>.Empty;

    /// <summary>
    /// The page label ranges, ordered by their first page; empty when the PDF doesn't label its pages.
    /// </summary>
    public IReadOnlyList<PdfPageLabelRange> PageLabels
    {
        get;
        init => field = ValueList<PdfPageLabelRange>.Of(value);
    } = ValueList<PdfPageLabelRange>.Empty;

    /// <summary>The top-level bookmarks, with nested ones in their <see cref="PdfBookmark.Children"/>.</summary>
    public IReadOnlyList<PdfBookmark> Bookmarks
    {
        get;
        init => field = ValueList<PdfBookmark>.Of(value);
    } = ValueList<PdfBookmark>.Empty;

    /// <summary>
    /// Set when the bookmarks nest more than 256 deep or loop back on themselves, so they can't be read safely;
    /// <see cref="Bookmarks"/> is then empty.
    /// </summary>
    public bool BookmarksUnreadable { get; init; }

    /// <summary>
    /// The structure tree's top-level elements, with the rest in their <see cref="PdfStructureElement.Children"/>; empty
    /// for an untagged PDF. <see cref="AllElements"/> lists every element.
    /// </summary>
    public IReadOnlyList<PdfStructureElement> Structure
    {
        get;
        init => field = ValueList<PdfStructureElement>.Of(value);
    } = ValueList<PdfStructureElement>.Empty;

    /// <summary>Artifact text, such as page numbers, headers and footers, page by page.</summary>
    public IReadOnlyList<PdfPageText> Artifacts
    {
        get;
        init => field = ValueList<PdfPageText>.Of(value);
    } = ValueList<PdfPageText>.Empty;

    /// <summary>
    /// Text assistive technology can't reach, page by page: text outside any tag, one entry per line, and tagged text that
    /// no structure element points to, one entry per marked-content ID.
    /// </summary>
    public IReadOnlyList<PdfPageText> Untagged
    {
        get;
        init => field = ValueList<PdfPageText>.Of(value);
    } = ValueList<PdfPageText>.Empty;

    /// <summary>Every font the pages use, once each.</summary>
    public IReadOnlyList<PdfFont> Fonts
    {
        get;
        init => field = ValueList<PdfFont>.Of(value);
    } = ValueList<PdfFont>.Empty;

    /// <summary>
    /// Every annotation on every page, in each page's own order. Those a structure element points to are also in its
    /// <see cref="PdfStructureElement.Annotations"/>.
    /// </summary>
    public IReadOnlyList<PdfAnnotation> Annotations
    {
        get;
        init => field = ValueList<PdfAnnotation>.Of(value);
    } = ValueList<PdfAnnotation>.Empty;

    /// <summary>Every field in the form; empty when the document has no form.</summary>
    public IReadOnlyList<PdfFormField> FormFields
    {
        get;
        init => field = ValueList<PdfFormField>.Of(value);
    } = ValueList<PdfFormField>.Empty;

    /// <summary>Settings for the whole form; null when the document has no form.</summary>
    public PdfFormSettings? Form { get; init; }

    /// <summary>The text, page by page, in runs that share a font, size, color, render mode and role.</summary>
    public IReadOnlyList<PdfTextRun> TextRuns
    {
        get;
        init => field = ValueList<PdfTextRun>.Of(value);
    } = ValueList<PdfTextRun>.Empty;

    /// <summary>Every image drawn on every page, page by page.</summary>
    public IReadOnlyList<PdfImage> Images
    {
        get;
        init => field = ValueList<PdfImage>.Of(value);
    } = ValueList<PdfImage>.Empty;

    /// <summary>Every painted vector path, such as a line, box or filled shape, page by page.</summary>
    public IReadOnlyList<PdfVectorPath> Paths
    {
        get;
        init => field = ValueList<PdfVectorPath>.Of(value);
    } = ValueList<PdfVectorPath>.Empty;

    /// <summary>
    /// Reads the PDF in a stream from its start, wherever the stream was left, and puts its position back afterwards. The
    /// stream is left open.
    /// </summary>
    /// <remarks>
    /// A page that can't be loaded comes back as <see cref="PdfPage.Unreadable"/>. An empty stream, a ZIP file, a web
    /// page, or a PDF too damaged to open or encrypted with a password, throws <see cref="InvalidDataException"/> saying
    /// so.
    /// </remarks>
    public static PdfUaDocument Load(Stream pdf) => InputStream.Read(pdf, DocumentFormat.Pdf, PdfPigReader.Read);

    /// <summary>Reads the PDF in a byte array.</summary>
    public static PdfUaDocument Load(byte[] pdf) => Load(new MemoryStream(pdf, writable: false));

    /// <summary>Reads the PDF file at a path.</summary>
    /// <remarks>
    /// A page that can't be loaded comes back as <see cref="PdfPage.Unreadable"/>; a file that isn't a PDF, is too
    /// damaged to open or is encrypted with a password throws <see cref="InvalidDataException"/>.
    /// </remarks>
    public static PdfUaDocument Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>The canonical snapshot text: one element per line, LF endings.</summary>
    /// <param name="configure">What to leave out, e.g. <c>o => o.Ignore(PdfSection.Metadata).Ignore(PdfDetail.Positions)</c>.</param>
    public string ToSnapshot(Action<PdfSnapshotOptions>? configure = null)
    {
        var options = new PdfSnapshotOptions();
        configure?.Invoke(options);
        return SnapshotWriter.Write(this, options);
    }

    /// <summary>
    /// The differences between this document's snapshot and a later one's, element by element, as
    /// <see cref="SnapshotDiff.Compare(PdfUaDocument, PdfUaDocument, Action{PdfSnapshotOptions}?)"/> gives them.
    /// </summary>
    /// <param name="after">The later PDF, whose new content counts as added.</param>
    /// <param name="configure">What to leave out or scrub from both, as for <see cref="ToSnapshot"/>.</param>
    public SnapshotDiff CompareTo(PdfUaDocument after, Action<PdfSnapshotOptions>? configure = null) =>
        SnapshotDiff.Compare(this, after, configure);

    /// <summary>
    /// Applies the checks and lists what they find, grouped by check in the order <see cref="PdfCheck"/> declares them;
    /// an empty list means every check passed. With no checks given, applies every check.
    /// </summary>
    public IReadOnlyList<PdfCheckFinding> Check(params PdfCheck[] checks) =>
        PdfChecker.Run(this, checks.Length == 0 ? PdfCheckSet.Latest.Checks : checks);

    /// <summary>
    /// Applies the rules in a set, such as <see cref="PdfCheckSet.Latest"/> or a release's own set that never changes,
    /// and lists what they find as <see cref="Check(PdfCheck[])"/> does.
    /// </summary>
    public IReadOnlyList<PdfCheckFinding> Check(PdfCheckSet set) => PdfChecker.Run(this, set.Checks);

    /// <summary>Every structure element, in reading order.</summary>
    public IEnumerable<PdfStructureElement> AllElements() => Structure.SelectMany(root => root.Descendants().Prepend(root));

    /// <summary>
    /// Structure elements of a type, such as <see cref="StructureType.Figure"/>, matched on the type or its role-mapped
    /// type, in reading order.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The type is neither a standard type, nor in the role map, nor used in the document, so a typo such as "figure"
    /// can't make an assertion over no elements pass.
    /// </exception>
    public IEnumerable<PdfStructureElement> Elements(string type)
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
    public IReadOnlyList<PdfHeading> Headings() =>
        AllElements().Select(element => (Element: element, Type: element.TypeOrMapped))
            .Where(pair => pair.Type == StructureType.Heading || StructureTypes.HeadingLevel(pair.Type) is not null)
            .Select(pair => new PdfHeading { Level = StructureTypes.HeadingLevel(pair.Type), Element = pair.Element })
            .ToList();
}

/// <summary>What the PDF's catalog says about the document as a whole, and its page count.</summary>
public sealed record PdfDocumentInfo
{
    internal PdfDocumentInfo()
    {
    }

    /// <summary>Whether the PDF says it's tagged (MarkInfo's Marked).</summary>
    public required bool Marked { get; init; }

    /// <summary>
    /// Whether MarkInfo says the tags may not be reliable (Suspects), as a tool that guessed them sets it.
    /// </summary>
    public bool Suspects { get; init; }

    /// <summary>
    /// Whether the catalog has a structure tree root (StructTreeRoot). A snapshot shows a missing root as an empty
    /// structure section, as it does an empty tree.
    /// </summary>
    public bool HasStructureTree { get; init; }

    /// <summary>The document's default language, such as "en-US"; null when it doesn't set one.</summary>
    public required string? Language { get; init; }

    /// <summary>Whether a viewer shows the metadata's title in its title bar, not the file name.</summary>
    public required bool DisplayDocTitle { get; init; }

    /// <summary>How many pages the document has, including any that couldn't be loaded.</summary>
    public required int PageCount { get; init; }
}

/// <summary>
/// Document metadata from the Info dictionary and XMP. <see cref="OtherInfo"/> holds every Info entry not covered by the
/// other properties, such as a company name or a classification label.
/// </summary>
public sealed record PdfMetadata
{
    internal PdfMetadata()
    {
    }

    /// <summary>The Info dictionary's title; null when it has none. <see cref="XmpTitle"/> is the XMP's.</summary>
    public required string? Title { get; init; }

    /// <summary>The Info dictionary's author; null when it has none.</summary>
    public required string? Author { get; init; }

    /// <summary>The Info dictionary's subject; null when it has none.</summary>
    public required string? Subject { get; init; }

    /// <summary>
    /// The Info dictionary's keywords, as one string the way the PDF writes them; null when it has none.
    /// </summary>
    public required string? Keywords { get; init; }

    /// <summary>
    /// Whether the Info dictionary has a creation date. The date itself is left out, since it changes every time the
    /// file is made.
    /// </summary>
    public required bool HasCreatedDate { get; init; }

    /// <summary>Whether the Info dictionary has a modification date, which is left out for the same reason.</summary>
    public required bool HasModifiedDate { get; init; }

    /// <summary>
    /// The program that wrote the PDF, such as a PDF library; null when the Info dictionary doesn't say. Snapshots
    /// print only whether it's there unless <see cref="PdfSnapshotOptions.ShowProducerAndCreator"/> is used.
    /// </summary>
    public required string? Producer { get; init; }

    /// <summary>
    /// The program the document was made in before it became a PDF, such as a word processor; null when the Info
    /// dictionary doesn't say. Printed like <see cref="Producer"/>.
    /// </summary>
    public required string? Creator { get; init; }

    /// <summary>The PDF/UA part the XMP claims (pdfuaid:part), such as "1" or "2"; null when it claims none.</summary>
    public required string? PdfUaPart { get; init; }

    /// <summary>
    /// The XMP title (dc:title), not the Info dictionary's <see cref="Title"/>; the last language version when it
    /// has several, and null when the XMP has none.
    /// </summary>
    public required string? XmpTitle { get; init; }

    /// <summary>The Info entries the other properties don't cover, sorted by name.</summary>
    public IReadOnlyList<PdfSetting> OtherInfo
    {
        get;
        init => field = ValueList<PdfSetting>.Of(value);
    } = ValueList<PdfSetting>.Empty;

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

    /// <summary>
    /// Whether the catalog has an XMP metadata stream. A snapshot can't tell a missing one from one with none of the
    /// values it prints.
    /// </summary>
    public bool HasXmp { get; init; }

    /// <summary>
    /// The prefix the XMP writes its PDF/UA identification properties with, when it isn't the "pdfuaid" PDF/UA names;
    /// null when it is, or when there are none. Snapshots leave it out, as it's how the XML is spelled.
    /// </summary>
    public string? PdfUaIdPrefix { get; init; }

    /// <summary>
    /// Whether the XMP title or description has text for the default language (xml:lang "x-default", or none), which
    /// takes the document's language, rather than naming a language of its own. Snapshots leave it out.
    /// </summary>
    public bool XmpHasDefaultLanguageText { get; init; }
}

/// <summary>One role map entry: a custom structure type and the type it maps to.</summary>
public sealed record PdfRoleMapping
{
    internal PdfRoleMapping()
    {
    }

    /// <summary>The custom type, as elements use it.</summary>
    public required string Custom { get; init; }

    /// <summary>
    /// The type it maps to, usually a standard one such as <see cref="StructureType.Paragraph"/>. It can be another
    /// custom type, which maps on in turn; <see cref="PdfStructureElement.MappedType"/> gives the end of the chain.
    /// </summary>
    public required string Standard { get; init; }
}

/// <summary>
/// A page. <see cref="Unreadable"/> is set when the PDF library could not load it; its other values are then empty.
/// The boxes are set only when the page declares them (or, for the crop box, inherits one).
/// </summary>
public sealed record PdfPage
{
    internal PdfPage()
    {
    }

    /// <summary>The page number, counted from 1.</summary>
    public required int Number { get; init; }

    /// <summary>
    /// The media box's width in points, before <see cref="Rotation"/> turns the page, so a portrait page turned 90
    /// degrees still has its portrait width; 0 for an unreadable page.
    /// </summary>
    public required double Width { get; init; }

    /// <summary>
    /// The media box's height in points, before <see cref="Rotation"/> turns the page; 0 for an unreadable page.
    /// </summary>
    public required double Height { get; init; }

    /// <summary>How far a viewer turns the page clockwise, in degrees: 0, 90, 180 or 270.</summary>
    public required int Rotation { get; init; }

    /// <summary>
    /// The tab order of the page's links and form fields: "R" (rows), "C" (columns) or "S" (structure order, which
    /// PDF/UA requires on a page with annotations), or another name the PDF writes; null when the page doesn't set one.
    /// <see cref="PdfTabOrders"/> lists the standard ones.
    /// </summary>
    public required string? Tabs { get; init; }

    /// <summary>
    /// How many artifacts on the page sit inside tagged content, which PDF/UA doesn't allow, since a screen reader
    /// can't tell whether to read them.
    /// </summary>
    public int ArtifactsInTaggedContent { get; init; }

    /// <summary>
    /// How many artifacts on the page hold tagged content (marked content with an MCID), which PDF/UA doesn't allow.
    /// </summary>
    public int ArtifactsHoldingTaggedContent { get; init; }

    /// <summary>Whether the page has annotations, such as links or form fields.</summary>
    public required bool HasAnnotations { get; init; }

    /// <summary>Whether the PDF library couldn't load the page.</summary>
    public bool Unreadable { get; init; }

    /// <summary>
    /// The area a viewer shows; null when neither the page nor a parent sets one, so all of it shows.
    /// </summary>
    public PdfBox? CropBox { get; init; }

    /// <summary>The area to keep when printing with bleed; null when the page doesn't set one.</summary>
    public PdfBox? BleedBox { get; init; }

    /// <summary>The finished page after trimming; null when the page doesn't set one.</summary>
    public PdfBox? TrimBox { get; init; }

    /// <summary>The area of the page's meaningful content; null when the page doesn't set one.</summary>
    public PdfBox? ArtBox { get; init; }

    /// <summary>
    /// The size of one unit in points, for pages larger than PDF's usual limit; null when the page doesn't set it,
    /// which means 1.
    /// </summary>
    public double? UserUnit { get; init; }

    /// <summary>The page's transparency group, which changes how overlapping content blends.</summary>
    public PdfPageGroup? Group { get; init; }
}

/// <summary>A page group: its type (usually "Transparency"), the color space it blends in, and its isolated and knockout options.</summary>
public sealed record PdfPageGroup
{
    internal PdfPageGroup()
    {
    }

    /// <summary>
    /// The group's subtype; "Transparency" is the only one PDF defines. Null when the PDF leaves it out.
    /// </summary>
    public required string? Type { get; init; }

    /// <summary>
    /// The color space content blends in, such as "DeviceRGB", or an array in brackets for one such as ICCBased; null
    /// when the group doesn't set one.
    /// </summary>
    public required string? ColorSpace { get; init; }

    /// <summary>Whether the group blends against a transparent backdrop instead of what's under it.</summary>
    public required bool Isolated { get; init; }

    /// <summary>
    /// Whether each object in the group covers the group's earlier ones instead of blending with them.
    /// </summary>
    public required bool Knockout { get; init; }
}

/// <summary>A rectangle in whole points from the page's bottom-left corner.</summary>
public sealed record PdfBox
{
    internal PdfBox()
    {
    }

    /// <summary>The left edge.</summary>
    public required int X { get; init; }

    /// <summary>The bottom edge.</summary>
    public required int Y { get; init; }

    /// <summary>The width; never negative, whichever corners the PDF gives.</summary>
    public required int Width { get; init; }

    /// <summary>The height; never negative, whichever corners the PDF gives.</summary>
    public required int Height { get; init; }
}

/// <summary>
/// A range of page labels, such as roman numerals for the front pages, from <see cref="FirstPage"/> until the next
/// range starts.
/// </summary>
public sealed record PdfPageLabelRange
{
    internal PdfPageLabelRange()
    {
    }

    /// <summary>The range's first page, counted from 1.</summary>
    public required int FirstPage { get; init; }

    /// <summary>
    /// The numbering: "D" (1, 2, 3), "R" (I, II, III), "r" (i, ii, iii), "A" (A, B, C) or "a" (a, b, c); null for
    /// labels that are only the prefix. <see cref="PdfPageLabelStyles"/> lists them.
    /// </summary>
    public required string? Style { get; init; }

    /// <summary>Text before each number, such as "Map-"; null when there is none.</summary>
    public required string? Prefix { get; init; }

    /// <summary>The number of the range's first page; 1 when the PDF doesn't say.</summary>
    public required int Start { get; init; }
}

/// <summary>
/// A bookmark. <see cref="Page"/> is set when it goes to a page of this document, <see cref="Uri"/> when it opens a web
/// address and <see cref="File"/> when it opens another file.
/// </summary>
public sealed record PdfBookmark
{
    internal PdfBookmark()
    {
    }

    /// <summary>The text the bookmark shows, with whitespace runs collapsed to one space.</summary>
    public required string Title { get; init; }

    /// <summary>
    /// The page it goes to in this document, counted from 1; null when it goes anywhere else or nowhere.
    /// </summary>
    public required int? Page { get; init; }

    /// <summary>The bookmarks nested under it, in order; empty when there are none.</summary>
    public required IReadOnlyList<PdfBookmark> Children { get; init => field = ValueList<PdfBookmark>.Of(value); }

    /// <summary>The page and view it opens: in this document, or in <see cref="File"/>. Its zoom isn't read.</summary>
    public PdfDestination? Destination { get; init; }

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
public sealed record PdfDestination
{
    internal PdfDestination()
    {
    }

    /// <summary>The page, counted from 1; null when the PDF points at something that isn't a page.</summary>
    public int? Page { get; init; }

    /// <summary>
    /// The view type, such as XYZ (a position), Fit (the whole page) or FitH (the page's width).
    /// <see cref="PdfDestinationViews"/> lists them.
    /// </summary>
    public string? View { get; init; }

    /// <summary>The view's positions, such as the left and top of an XYZ view.</summary>
    public IReadOnlyList<int?> Positions { get; init => field = ValueList<int?>.Of(value); } = ValueList<int?>.Empty;

    /// <summary>An XYZ view's zoom, where 1 is 100%; null when the viewer keeps its zoom.</summary>
    public double? Zoom { get; init; }

    /// <summary>The name of a destination the PDF doesn't define, or one in another file.</summary>
    public string? Name { get; init; }
}

/// <summary>A heading in the structure tree. <see cref="Level"/> is 1 to 6, or null for a plain H.</summary>
public sealed record PdfHeading
{
    internal PdfHeading()
    {
    }

    /// <summary>1 for H1 through 6 for H6; null for H, whose level comes from how it's nested.</summary>
    public required int? Level { get; init; }

    /// <summary>The heading's structure element, with its text and children.</summary>
    public required PdfStructureElement Element { get; init; }
}

/// <summary>A structure element. <see cref="Name"/> replaces the type in the snapshot when other elements refer to it (TH1, TH2).</summary>
public sealed record PdfStructureElement
{
    internal PdfStructureElement()
    {
    }

    /// <summary>
    /// The type as the PDF writes it: a standard type (see <see cref="StructureType"/>) or a custom one the role map
    /// maps; "(none)" when the element has no type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>The child elements, in the order the element lists them, which is reading order.</summary>
    public IReadOnlyList<PdfStructureElement> Children
    {
        get;
        init => field = ValueList<PdfStructureElement>.Of(value);
    } = ValueList<PdfStructureElement>.Empty;

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
    /// The element's ID (/ID), as the PDF writes it; null when it has none. Snapshots leave it out, since generators
    /// often make it up, and print a header's <see cref="Name"/> instead.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// The first page the element's own marked content or annotations are on; null when it has none on a page. Its
    /// children's pages don't count.
    /// </summary>
    public int? FirstPage { get; init; }

    /// <summary>The last page, found the same way as <see cref="FirstPage"/>.</summary>
    public int? LastPage { get; init; }

    /// <summary>The language the element sets for itself, such as "fr"; null when it uses its parent's.</summary>
    public string? Language { get; init; }

    /// <summary>The alt text, such as a figure's description of a trail map; null when it has none.</summary>
    public string? AltText { get; init; }

    /// <summary>
    /// Text a screen reader reads instead of the content, such as the words on a photo of a trail sign.
    /// </summary>
    public string? ActualText { get; init; }

    /// <summary>The full form of an abbreviation, such as "Mount" for "Mt."; null when it has none.</summary>
    public string? Expansion { get; init; }

    /// <summary>The element's title, a name for it that isn't part of the page's text; null when it has none.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// A table header cell's scope: "Row", "Column" or "Both", as in <see cref="PdfTableScopes"/>; null when it doesn't
    /// set one.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// The header cells a table cell names, by their <see cref="Name"/>, or "?id" for an ID no element has; null when
    /// the cell has no Headers attribute.
    /// </summary>
    public IReadOnlyList<string>? Headers
    {
        get;
        init => field = value is null ? null : ValueList<string>.Of(value);
    }

    /// <summary>How many rows a table cell spans; null when it doesn't say, which means 1.</summary>
    public int? RowSpan { get; init; }

    /// <summary>How many columns a table cell spans; null when it doesn't say, which means 1.</summary>
    public int? ColSpan { get; init; }

    /// <summary>
    /// A list's numbering, such as "Decimal", "Disc" or "UpperRoman"; null when it doesn't set one.
    /// <see cref="PdfListNumberings"/> lists them.
    /// </summary>
    public string? ListNumbering { get; init; }

    /// <summary>
    /// What kind of field a Form element tags, from its PrintField Role attribute, such as "cb" (check box), "rb"
    /// (radio button), "pb" (push button) or "tv" (text); null when it doesn't set one.
    /// </summary>
    public string? FieldRole { get; init; }

    /// <summary>A table's summary of its purpose and layout; null when it has none.</summary>
    public string? Summary { get; init; }

    /// <summary>
    /// The annotations the element points to, such as links and form widgets, in the order it lists them.
    /// </summary>
    public IReadOnlyList<PdfAnnotation> Annotations
    {
        get;
        init => field = ValueList<PdfAnnotation>.Of(value);
    } = ValueList<PdfAnnotation>.Empty;

    /// <summary>How many of the element's marked-content references name no page, so their text can't be found.</summary>
    public int McidsWithoutPage { get; init; }

    /// <summary>
    /// How many of the element's marked-content references point into a form XObject, such as a stamp the page draws.
    /// Their text is read from the form's own content, which numbers its MCIDs apart from the page's.
    /// </summary>
    public int McidsInXObject { get; init; }

    /// <summary>
    /// How many child elements are left out because they're nested more than 256 deep. Their content then counts as
    /// untagged.
    /// </summary>
    public int UnreadChildren { get; init; }

    /// <summary>
    /// Set when the element has no parent entry (/P), which PDF/UA requires, so a tool that walks up the tree from it
    /// can't place it.
    /// </summary>
    public bool ParentMissing { get; init; }

    /// <summary>Marked content in the element that sets its own language, replacement text, alt text or expansion.</summary>
    public IReadOnlyList<PdfMarkedSpan> MarkedSpans
    {
        get;
        init => field = ValueList<PdfMarkedSpan>.Of(value);
    } = ValueList<PdfMarkedSpan>.Empty;

    /// <summary>
    /// The hashes of the images the element's own marked content draws, in the order the element lists its marked
    /// content, as <see cref="PdfImage.Hash"/> gives them: the image a figure's alt text describes.
    /// </summary>
    public IReadOnlyList<string> ImageHashes
    {
        get;
        init => field = ValueList<string>.Of(value);
    } = ValueList<string>.Empty;

    /// <summary>
    /// The text runs the element's own marked content draws, in the order the element lists its marked content and
    /// each in drawing order, with the font, size and color each uses: the same runs
    /// <see cref="PdfUaDocument.TextRuns"/> lists. Runs drawn by child elements are in each child's own list.
    /// </summary>
    public IReadOnlyList<PdfTextRun> TextRuns
    {
        get;
        init => field = ValueList<PdfTextRun>.Of(value);
    } = ValueList<PdfTextRun>.Empty;

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
    public IEnumerable<PdfStructureElement> Descendants() =>
        Children.SelectMany(child => child.Descendants().Prepend(child));

    /// <summary>
    /// Descendants of a type, such as <see cref="StructureType.Paragraph"/>, matched on the type or its role-mapped
    /// type. Unlike <see cref="PdfUaDocument.Elements(string)"/>, it doesn't check the type, so use the
    /// <see cref="StructureType"/> constants.
    /// </summary>
    public IEnumerable<PdfStructureElement> Elements(string type) => Descendants().Where(element => element.Is(type));

    internal bool Is(string type) => Type == type || MappedType == type;

    internal string TypeOrMapped => MappedType ?? Type;
}

/// <summary>
/// An annotation: a link, comment, form widget and so on. Its rectangle is in whole points from the page's bottom-left
/// corner. <see cref="Role"/> is <see cref="PdfContentRole.Tagged"/> when a structure element points to it.
/// </summary>
public sealed record PdfAnnotation
{
    internal PdfAnnotation()
    {
    }

    /// <summary>
    /// The kind of annotation, such as "Link", "Widget" or "Text"; "(none)" (<see cref="PdfAnnotationTypes.None"/>)
    /// when the PDF doesn't say. <see cref="PdfAnnotationTypes"/> lists the standard ones.
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
    public PdfAnnotationFlags Flags { get; init; }

    /// <summary>
    /// <see cref="PdfContentRole.Tagged"/> when a structure element points to it, otherwise <see
    /// cref="PdfContentRole.Untagged"/>.
    /// </summary>
    public PdfContentRole Role { get; init; }

    /// <summary>The web address it opens; null when it opens none.</summary>
    public string? Uri { get; init; }

    /// <summary>
    /// The type of action it runs, such as "GoTo", "Launch" or "JavaScript"; null when it has no action or its action
    /// opens a web address (<see cref="Uri"/>), and "URI" for a web-address action that names no address.
    /// <see cref="PdfActionTypes"/> lists the standard ones.
    /// </summary>
    public string? Action { get; init; }

    /// <summary>Where the link goes, from its own destination or a GoTo or GoToR action.</summary>
    public PdfDestination? Destination { get; init; }

    /// <summary>The file a GoToR or Launch action opens.</summary>
    public string? File { get; init; }

    /// <summary>
    /// The annotation's text, which for a link is its description for assistive technology; null when it has none.
    /// </summary>
    public string? Contents { get; init; }

    /// <summary>A form widget's tooltip, which a screen reader announces as its name; null when it has none.</summary>
    public string? AccessibleName { get; init; }

    /// <summary>
    /// The media clip a Screen annotation's first Rendition action plays, on click or on an event such as the page
    /// opening; null when it plays none.
    /// </summary>
    public PdfMediaClip? MediaClip { get; init; }
}

/// <summary>A media clip, such as a video, that a Screen annotation plays.</summary>
public sealed record PdfMediaClip
{
    internal PdfMediaClip()
    {
    }

    /// <summary>Its content type, such as "video/mp4"; null when it doesn't say.</summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Its alternate text, several languages' joined with "; "; null when it has none. A screen reader reads it in
    /// place of the clip.
    /// </summary>
    public string? AltText { get; init; }
}

/// <summary>
/// Text on a page that no structure element holds: an artifact or untagged text. An artifact can say what kind it is,
/// in <see cref="Type"/> (Pagination, Layout, Page or Background) and <see cref="Subtype"/> (such as Header or Footer).
/// </summary>
public sealed record PdfPageText
{
    internal PdfPageText()
    {
    }

    /// <summary>The page number, counted from 1.</summary>
    public required int Page { get; init; }

    /// <summary>The text, with whitespace runs collapsed to one space, such as "Page 4 of 12".</summary>
    public required string Text { get; init; }

    /// <summary>
    /// The artifact's kind: "Pagination", "Layout", "Page" or "Background", as in <see cref="PdfArtifactTypes"/>; null
    /// for untagged text and when it doesn't say.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// The artifact's subtype, such as "Header", "Footer" or "Watermark"; null for untagged text and when it doesn't
    /// say. <see cref="PdfArtifactSubtypes"/> lists the standard ones.
    /// </summary>
    public string? Subtype { get; init; }
}

/// <summary>
/// What marked content inside a structure element says about its own text: its language, replacement text, alt text
/// or expansion, which a screen reader uses instead of the element's.
/// </summary>
public sealed record PdfMarkedSpan
{
    internal PdfMarkedSpan()
    {
    }

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
public sealed record PdfFont
{
    internal PdfFont()
    {
    }

    /// <summary>
    /// The font's name (BaseFont) without a subset prefix, such as "Helvetica-Bold" for "ABCDEF+Helvetica-Bold";
    /// "(none)" when the PDF doesn't name it.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The font's type, such as "Type1", "TrueType", "Type0" or "Type3"; null when the PDF leaves it out.
    /// <see cref="PdfFontTypes"/> lists them.
    /// </summary>
    public required string? Subtype { get; init; }

    /// <summary>
    /// Whether the PDF holds the font's file, so the text doesn't depend on the fonts installed where it's opened.
    /// </summary>
    public required bool Embedded { get; init; }

    /// <summary>
    /// Whether the font has a ToUnicode map from its character codes to Unicode. A font without one can still map its
    /// codes through its encoding; <see cref="UnmappedCodes"/> lists the ones nothing maps.
    /// </summary>
    public required bool HasToUnicode { get; init; }

    /// <summary>
    /// The character codes the font's text uses that don't map to Unicode, smallest first; empty when every code maps.
    /// A code mapped to U+0000, U+FEFF or U+FFFE counts as unmapped, since it gives no text.
    /// </summary>
    public IReadOnlyList<int> UnmappedCodes { get; init => field = ValueList<int>.Of(value); } = ValueList<int>.Empty;

    /// <summary>
    /// The character codes the font's text uses that show its .notdef glyph, the empty box a font draws for a character
    /// it lacks, smallest first. Only codes the font's encoding or CID-to-glyph map send there are found, not glyphs
    /// missing from the font file.
    /// </summary>
    public IReadOnlyList<int> MissingGlyphCodes { get; init => field = ValueList<int>.Of(value); } = ValueList<int>.Empty;

    /// <summary>
    /// What PDF/UA forbids in the font's dictionaries and CMaps, in the order of <see cref="PdfFontProblem"/>.
    /// </summary>
    public IReadOnlyList<PdfFontProblem> Problems
    {
        get;
        init => field = ValueList<PdfFontProblem>.Of(value);
    } = ValueList<PdfFontProblem>.Empty;
}

/// <summary>
/// Something PDF/UA forbids in a Type 0 or TrueType font's dictionaries or CMaps, found without reading the font file.
/// </summary>
public enum PdfFontProblem
{
    /// <summary>A Type 0 font's CIDSystemInfo names a different character collection from its CMap's.</summary>
    CidSystemInfo,

    /// <summary>An embedded TrueType-based CID font has no CIDToGIDMap from its CIDs to its glyphs.</summary>
    CidToGidMap,

    /// <summary>A Type 0 font's CMap is neither embedded nor one of the CMaps every reader has built in.</summary>
    CmapNotEmbedded,

    /// <summary>An embedded CMap's dictionary and data give different writing modes (WMode).</summary>
    CmapWritingMode,

    /// <summary>An embedded CMap uses another CMap that isn't one of the built-in ones.</summary>
    CmapReference,

    /// <summary>A non-symbolic TrueType font's encoding isn't MacRomanEncoding or WinAnsiEncoding.</summary>
    TrueTypeEncoding,

    /// <summary>A symbolic TrueType font has an Encoding entry.</summary>
    SymbolicEncoding,
}
