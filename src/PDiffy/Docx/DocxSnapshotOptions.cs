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
}

/// <summary>
/// What to leave out of a DOCX snapshot, or change in it, for content a test doesn't care about. Ignored sections still
/// print their header with "(ignored)" under it, and the snapshot's first lines list what was ignored.
/// </summary>
public sealed class DocxSnapshotOptions
{
    private readonly HashSet<DocxSection> _ignoredSections = [];
    private readonly List<Scrubber> _scrubbers = [];

    /// <summary>Leaves whole sections out, such as <see cref="DocxSection.Styles"/>.</summary>
    public DocxSnapshotOptions Ignore(params DocxSection[] sections)
    {
        _ignoredSections.UnionWith(sections);
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
}
