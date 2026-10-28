using PDiffy.Reading;

namespace PDiffy;

/// <summary>Everything PDiffy reads from a PDF, in the order a snapshot prints it.</summary>
public sealed record PdfUaDocument
{
    public required DocumentInfo Document { get; init; }
    public required Metadata Metadata { get; init; }
    public DocumentSettings Settings { get; init; } = new();
    public IReadOnlyList<RoleMapping> RoleMap { get; init; } = [];
    public IReadOnlyList<PageInfo> Pages { get; init; } = [];
    public IReadOnlyList<PageLabelRange> PageLabels { get; init; } = [];
    public IReadOnlyList<Bookmark> Bookmarks { get; init; } = [];
    public IReadOnlyList<StructureElement> Structure { get; init; } = [];
    public IReadOnlyList<PageText> Artifacts { get; init; } = [];
    public IReadOnlyList<PageText> Untagged { get; init; } = [];
    public IReadOnlyList<FontInfo> Fonts { get; init; } = [];
    public IReadOnlyList<Annotation> Annotations { get; init; } = [];
    public IReadOnlyList<FormField> FormFields { get; init; } = [];
    public IReadOnlyList<TextRun> TextRuns { get; init; } = [];
    public IReadOnlyList<ImageInfo> Images { get; init; } = [];
    public IReadOnlyList<PathInfo> Paths { get; init; } = [];

    /// <summary>Reads the PDF in a stream. The stream is left open.</summary>
    /// <remarks>A page that can't be loaded comes back as <see cref="PageInfo.Unreadable"/>; a file that can't be opened throws.</remarks>
    public static PdfUaDocument Load(Stream pdf) => PdfPigReader.Read(pdf);

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

    /// <summary>Structure elements of a type, matched on the type or its role-mapped type, in reading order.</summary>
    public IEnumerable<StructureElement> Elements(string type)
    {
        foreach (var root in Structure)
        {
            foreach (var element in SelfAndDescendants(root))
            {
                if (element.Type == type || element.MappedType == type)
                {
                    yield return element;
                }
            }
        }
    }

    private static IEnumerable<StructureElement> SelfAndDescendants(StructureElement element)
    {
        yield return element;
        foreach (var child in element.Children)
        {
            foreach (var descendant in SelfAndDescendants(child))
            {
                yield return descendant;
            }
        }
    }
}

public sealed record DocumentInfo(bool Marked, string? Lang, bool DisplayDocTitle, int PageCount);

/// <summary>
/// Document metadata from the Info dictionary and XMP. <see cref="OtherInfo"/> holds every Info entry not covered by the
/// other properties, such as a company name or a classification label.
/// </summary>
public sealed record Metadata(
    string? Title, string? Author, string? Subject, string? Keywords, bool HasCreationDate, bool HasModDate,
    string? Producer, string? Creator, string? PdfUaPart, string? XmpTitle)
{
    public IReadOnlyList<Setting> OtherInfo { get; init; } = [];
    public string? XmpCreator { get; init; }
    public string? XmpDescription { get; init; }
    public string? XmpLanguage { get; init; }

    /// <summary>The PDF/A part and conformance level the XMP claims, such as "2B".</summary>
    public string? PdfAConformance { get; init; }
}

public sealed record RoleMapping(string Custom, string Standard);

/// <summary>
/// A page. <see cref="Unreadable"/> is set when the PDF library could not load it; its other values are then empty.
/// The boxes are set only when the page declares them (or, for the crop box, inherits one).
/// </summary>
public sealed record PageInfo(int Number, double Width, double Height, int Rotation, string? Tabs, bool HasAnnotations, bool Unreadable = false)
{
    public PageBox? CropBox { get; init; }
    public PageBox? BleedBox { get; init; }
    public PageBox? TrimBox { get; init; }
    public PageBox? ArtBox { get; init; }
    public double? UserUnit { get; init; }

    /// <summary>The page's transparency group, which changes how overlapping content blends.</summary>
    public PageGroup? Group { get; init; }
}

/// <summary>A page group: its type (usually "Transparency"), the color space it blends in, and its isolated and knockout options.</summary>
public sealed record PageGroup(string? Type, string? ColorSpace, bool Isolated, bool Knockout);

/// <summary>A rectangle in whole points from the page's bottom-left corner.</summary>
public sealed record PageBox(int X, int Y, int Width, int Height);

public sealed record PageLabelRange(int FirstPage, string? Style, string? Prefix, int Start);

public sealed record Bookmark(string Title, int? Page, IReadOnlyList<Bookmark> Children);

/// <summary>A structure element. <see cref="Name"/> replaces the type in the snapshot when other elements refer to it (TH1, TH2).</summary>
public sealed record StructureElement
{
    public required string Type { get; init; }
    public IReadOnlyList<StructureElement> Children { get; init; } = [];
    public string? MappedType { get; init; }
    public string? Name { get; init; }
    public int? FirstPage { get; init; }
    public int? LastPage { get; init; }
    public string? Lang { get; init; }
    public string? Alt { get; init; }
    public string? ActualText { get; init; }
    public string? Expansion { get; init; }
    public string? Title { get; init; }
    public string? Scope { get; init; }
    public IReadOnlyList<string>? Headers { get; init; }
    public int? RowSpan { get; init; }
    public int? ColSpan { get; init; }
    public string? ListNumbering { get; init; }
    public string? Summary { get; init; }
    public IReadOnlyList<Annotation> Annotations { get; init; } = [];
    public string? Text { get; init; }
}

/// <summary>
/// An annotation: a link, comment, form widget and so on. Its rectangle is in whole points from the page's bottom-left
/// corner. <see cref="Role"/> is <see cref="ContentRole.Tagged"/> when a structure element points to it.
/// </summary>
public sealed record Annotation
{
    public required string Subtype { get; init; }
    public int? Page { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public AnnotationFlags Flags { get; init; }
    public ContentRole Role { get; init; }
    public string? Uri { get; init; }
    public string? Action { get; init; }
    public bool HasDestination { get; init; }
    public string? Contents { get; init; }
    public string? AccessibleName { get; init; }
}

public sealed record PageText(int Page, string Text);

/// <summary>
/// A font. <see cref="UnmappedCodes"/> lists the character codes its text uses that don't map to Unicode: that text can't
/// be copied, searched or read aloud correctly.
/// </summary>
public sealed record FontInfo(string Name, string? Subtype, bool Embedded, bool HasToUnicode)
{
    public IReadOnlyList<int> UnmappedCodes { get; init; } = [];
}
