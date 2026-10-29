using System.Text.RegularExpressions;

namespace PDiffy;

/// <summary>
/// What to leave out of a snapshot, or change in it, for content a test doesn't care about. Ignored sections still print
/// their header with "(ignored)" under it, and the snapshot's first lines list what was ignored and which pages were kept.
/// </summary>
public sealed class SnapshotOptions
{
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
    /// Leaves out what a profile leaves out, such as <see cref="SnapshotProfile.Accessibility"/>. Further
    /// <c>Ignore</c> calls leave out more.
    /// </summary>
    public SnapshotOptions Use(SnapshotProfile profile) => profile switch
    {
        SnapshotProfile.Strict => this,
        SnapshotProfile.Content => Ignore(Section.Settings, Section.Metadata, Section.Fonts, Section.Layout)
            .Ignore(Field.PageBoxes, Field.Positions),
        SnapshotProfile.Accessibility => Ignore(Field.PdfVersion, Field.OpenAction, Field.EmbeddedFileContents,
            Field.Author, Field.Subject, Field.Keywords, Field.Dates, Field.Producer, Field.Creator, Field.CustomInfo,
            Field.PageBoxes, Field.Positions, Field.Colors, Field.FontNames, Field.FontSizes, Field.LineWidths,
            Field.ImageContents),
        SnapshotProfile.Layout => Ignore(Section.Settings, Section.Metadata),
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Not a SnapshotProfile value."),
    };

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
        _scrubbers.Add(Scrubber.Dates);
        return this;
    }

    /// <summary>Replaces GUIDs in quoted text with Guid_1, Guid_2 and so on. The same text always gets the same token.</summary>
    public SnapshotOptions ScrubGuids()
    {
        _scrubbers.Add(Scrubber.Guids);
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
