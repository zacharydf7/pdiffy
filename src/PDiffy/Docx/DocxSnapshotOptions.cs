using System.Text.RegularExpressions;

namespace PDiffy;

/// <summary>The sections of a DOCX snapshot, in the order it prints them.</summary>
public enum DocxSection
{
    /// <summary>Title, subject, language, compatibility mode, tracked changes, protection and header settings.</summary>
    Document,

    /// <summary>Each section's page size, margins, columns, page numbering and which headers and footers it has.</summary>
    Sections,

    /// <summary>The styles the document uses, with their properties resolved.</summary>
    Styles,

    /// <summary>The lists the document uses and how each level is numbered.</summary>
    Numbering,

    /// <summary>Paragraphs, tables and inserted documents, with everything inside them.</summary>
    Body,

    Headers,

    Footers,

    /// <summary>Footnotes, endnotes and comments.</summary>
    Notes,
}

/// <summary>
/// A detail that <see cref="DocxSnapshotOptions.Ignore(DocxDetail[])"/> leaves out of a DOCX snapshot wherever it
/// appears.
/// </summary>
public enum DocxDetail
{
    /// <summary>The hash of each image, embedded object and control's data.</summary>
    ImageHashes,

    /// <summary>
    /// The result of each field, such as the page number a PAGE field last showed: the field's result= and the result's
    /// text in the paragraph, also inside links, formatted text, tracked changes and content controls' values.
    /// </summary>
    FieldResults,

    /// <summary>
    /// The font of styles and of text that differs from its style. A symbol's or a bullet's font stays, since it
    /// decides which character shows.
    /// </summary>
    Fonts,

    /// <summary>The font size of styles and of text that differs from its style.</summary>
    FontSizes,

    /// <summary>Text color, highlight color, cell shading and border colors.</summary>
    Colors,

    /// <summary>Space before and after paragraphs, and line spacing.</summary>
    Spacing,

    /// <summary>Left, right, first-line and hanging indents of paragraphs, styles and list levels.</summary>
    Indents,

    /// <summary>Who made each tracked change.</summary>
    RevisionAuthors,

    /// <summary>Who wrote each comment.</summary>
    CommentAuthors,
}

/// <summary>
/// What to leave out of a DOCX snapshot, or change in it, for content a test doesn't care about. Ignored sections still
/// print their header with "(ignored)" under it, and the snapshot's first lines list what was ignored.
/// </summary>
public sealed class DocxSnapshotOptions
{
    private readonly HashSet<DocxSection> _ignoredSections = [];
    private readonly HashSet<DocxDetail> _ignoredDetails = [];
    private readonly List<Scrubber> _scrubbers = [];

    /// <summary>Leaves whole sections out, such as <see cref="DocxSection.Styles"/>.</summary>
    public DocxSnapshotOptions Ignore(params DocxSection[] sections)
    {
        _ignoredSections.UnionWith(sections);
        return this;
    }

    /// <summary>
    /// Leaves one detail out wherever it appears, such as <see cref="DocxDetail.ImageHashes"/>; the rest of each line
    /// stays.
    /// </summary>
    public DocxSnapshotOptions Ignore(params DocxDetail[] details)
    {
        _ignoredDetails.UnionWith(details);
        return this;
    }

    /// <summary>
    /// Replaces matches in quoted text (paragraph text, alt text, titles, field results and so on) and link addresses.
    /// The replacement can refer to groups, such as "Order $1".
    /// </summary>
    public DocxSnapshotOptions ScrubText(Regex pattern, string replacement)
    {
        _scrubbers.Add(new Scrubber(pattern, replacement, Numbered: false));
        return this;
    }

    /// <summary>
    /// Replaces dates in quoted text and link addresses with Date_1, Date_2 and so on. The same text always gets the same token. Recognizes
    /// the same dates as <see cref="SnapshotOptions.ScrubDates"/>.
    /// </summary>
    public DocxSnapshotOptions ScrubDates()
    {
        _scrubbers.Add(Scrubber.Dates);
        return this;
    }

    /// <summary>
    /// Replaces GUIDs in quoted text and link addresses with Guid_1, Guid_2 and so on. The same text always gets the same token.
    /// </summary>
    public DocxSnapshotOptions ScrubGuids()
    {
        _scrubbers.Add(Scrubber.Guids);
        return this;
    }

    internal IReadOnlyList<Scrubber> Scrubbers => _scrubbers;

    internal bool Ignores(DocxSection section) => _ignoredSections.Contains(section);

    internal IEnumerable<DocxSection> IgnoredSections => _ignoredSections.Order();

    internal bool Ignores(DocxDetail detail) => _ignoredDetails.Contains(detail);

    internal IEnumerable<DocxDetail> IgnoredDetails => _ignoredDetails.Order();
}
