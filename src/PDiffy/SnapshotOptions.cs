using System.Text.RegularExpressions;

namespace PDiffy;

/// <summary>
/// What to leave out of a snapshot, or change in it, for content a test doesn't care about. Ignored sections still print
/// their header with "(ignored)" under it, and the snapshot's first lines list what was ignored and which pages were kept.
/// </summary>
public sealed class SnapshotOptions
{
    private const string Month = @"(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?";
    private const string Time = @"([ T]\d{1,2}:\d{2}(:\d{2}(\.\d+)?)?( ?[AaPp][Mm])?(Z|[+-]\d{2}:?\d{2})?)?";

    // 2026-10-27, 10/27/2026, October 27, 2026 and 27 Oct 2026, each with an optional time; and PDF dates (D:20261027...).
    private static readonly Regex DatePattern = new(
        @"\b(\d{4}-\d{2}-\d{2}" + Time + @"|\d{1,2}/\d{1,2}/\d{2,4}" + Time + "|" + Month + @" \d{1,2},? \d{4}" + Time
        + @"|\d{1,2} " + Month + @" \d{4}" + Time + @"|D:\d{8,14}(Z|[+-]\d{2}'?\d{2}'?)?)(?!\d)", RegexOptions.IgnoreCase);

    private static readonly Regex GuidPattern =
        new(@"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", RegexOptions.IgnoreCase);

    private readonly HashSet<Section> _ignoredSections = [];
    private readonly HashSet<Field> _ignoredFields = [];
    private readonly List<Scrubber> _scrubbers = [];
    private readonly SortedSet<int> _pages = [];

    /// <summary>Leaves whole sections out, such as <see cref="Section.Metadata"/>.</summary>
    public SnapshotOptions Ignore(params Section[] sections)
    {
        _ignoredSections.UnionWith(sections);
        return this;
    }

    /// <summary>Leaves a detail out wherever it appears, such as <see cref="Field.Positions"/> or <see cref="Field.Colors"/>.</summary>
    public SnapshotOptions Ignore(params Field[] fields)
    {
        _ignoredFields.UnionWith(fields);
        return this;
    }

    /// <summary>
    /// Replaces matches in quoted text (element text, alt text, titles, link addresses, form values and so on).
    /// The replacement can refer to groups, such as "Order $1".
    /// </summary>
    public SnapshotOptions ScrubText(Regex pattern, string replacement)
    {
        _scrubbers.Add(new Scrubber(pattern, replacement, Numbered: false));
        return this;
    }

    /// <summary>
    /// Replaces dates in quoted text with Date_1, Date_2 and so on. The same text always gets the same token, so a test
    /// can still see that two dates match. Recognizes 2026-10-27, 10/27/2026, October 27, 2026 and 27 Oct 2026, each
    /// with an optional time, and PDF dates such as D:20261027120000.
    /// </summary>
    public SnapshotOptions ScrubDates()
    {
        _scrubbers.Add(new Scrubber(DatePattern, "Date", Numbered: true));
        return this;
    }

    /// <summary>Replaces GUIDs in quoted text with Guid_1, Guid_2 and so on. The same text always gets the same token.</summary>
    public SnapshotOptions ScrubGuids()
    {
        _scrubbers.Add(new Scrubber(GuidPattern, "Guid", Numbered: true));
        return this;
    }

    /// <summary>
    /// Prints the producer and creator (the libraries and application that wrote the PDF), which a snapshot normally
    /// records only as present or missing because they change with every upgrade.
    /// </summary>
    public SnapshotOptions ShowProducerAndCreator()
    {
        ShowsProducerAndCreator = true;
        return this;
    }

    /// <summary>
    /// Keeps only what is on these pages, numbered from 1. A structure element or bookmark is kept when it, or anything
    /// under it, is on one of them. The document's page count, page labels and fonts are still printed for the whole file.
    /// </summary>
    public SnapshotOptions OnlyPages(params int[] pages)
    {
        if (pages.Length == 0) throw new ArgumentException("Pass at least one page.", nameof(pages));
        if (pages.Any(page => page < 1)) throw new ArgumentOutOfRangeException(nameof(pages), "Pages are numbered from 1.");
        _pages.UnionWith(pages);
        return this;
    }

    internal bool ShowsProducerAndCreator { get; private set; }

    internal IReadOnlyList<Scrubber> Scrubbers => _scrubbers;

    internal IReadOnlySet<int> Pages => _pages;

    internal bool Ignores(Section section) => _ignoredSections.Contains(section);

    internal bool Ignores(Field field) => _ignoredFields.Contains(field);

    internal IEnumerable<Section> IgnoredSections => _ignoredSections.Order();

    internal IEnumerable<Field> IgnoredFields => _ignoredFields.Order();
}

/// <summary>A pattern and its replacement. A numbered scrubber uses the replacement as a token name: Date_1, Date_2.</summary>
internal sealed record Scrubber(Regex Pattern, string Replacement, bool Numbered);
