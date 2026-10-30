using System.Text.RegularExpressions;
using PDiffy.Diffing;

namespace PDiffy;

/// <summary>
/// What to leave out of a snapshot, or change in it, for content a test doesn't care about. Ignored sections still print
/// their header with "(ignored)" under it, and the snapshot's first lines list what was ignored and which pages were kept.
/// </summary>
public sealed class PdfSnapshotOptions
{
    private readonly HashSet<PdfSection> _ignoredSections = [];
    private readonly HashSet<PdfDetail> _ignoredFields = [];
    private readonly List<Scrubber> _scrubbers = [];
    private readonly SortedSet<int> _pages = [];
    private readonly SortedDictionary<PdfDetail, double> _tolerances = [];

    /// <summary>Leaves whole sections out, such as <see cref="PdfSection.Metadata"/>.</summary>
    public PdfSnapshotOptions Ignore(params PdfSection[] sections)
    {
        _ignoredSections.UnionWith(sections);
        return this;
    }

    /// <summary>Leaves a detail out wherever it appears, such as <see cref="PdfDetail.Positions"/> or <see cref="PdfDetail.Colors"/>.</summary>
    public PdfSnapshotOptions Ignore(params PdfDetail[] details)
    {
        _ignoredFields.UnionWith(details);
        return this;
    }

    /// <summary>
    /// Leaves out what a profile leaves out, such as <see cref="PdfProfile.Accessibility"/>. Further
    /// <c>Ignore</c> calls leave out more.
    /// </summary>
    public PdfSnapshotOptions Use(PdfProfile profile) => profile switch
    {
        PdfProfile.Strict => this,
        PdfProfile.Content => Ignore(PdfSection.Settings, PdfSection.Metadata, PdfSection.Fonts, PdfSection.Layout)
            .Ignore(PdfDetail.PageBoxes, PdfDetail.Positions),
        PdfProfile.Accessibility => Ignore(PdfDetail.PdfVersion, PdfDetail.OpenAction, PdfDetail.EmbeddedFileContents,
            PdfDetail.Author, PdfDetail.Subject, PdfDetail.Keywords, PdfDetail.Dates, PdfDetail.Producer, PdfDetail.Creator,
            PdfDetail.CustomInfo, PdfDetail.PageBoxes, PdfDetail.Positions, PdfDetail.Colors, PdfDetail.FontNames,
            PdfDetail.FontSizes, PdfDetail.LineWidths, PdfDetail.ImageContents),
        PdfProfile.Layout => Ignore(PdfSection.Settings, PdfSection.Metadata),
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Not a PdfProfile value."),
    };

    /// <summary>
    /// Lets a number differ by up to <paramref name="points"/> without failing the test, so a value that rounds the
    /// other way after a library update still passes while a real move still fails. A run within the tolerance passes
    /// and leaves the verified file as it is. The snapshot's header lists each tolerance, such as
    /// "# tolerances: positions 1pt".
    /// </summary>
    public PdfSnapshotOptions Tolerate(PdfTolerance tolerance, double points)
    {
        var field = tolerance switch
        {
            PdfTolerance.Positions => PdfDetail.Positions,
            PdfTolerance.FontSizes => PdfDetail.FontSizes,
            PdfTolerance.LineWidths => PdfDetail.LineWidths,
            _ => throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Not a PdfTolerance value."),
        };

        if (!double.IsFinite(points) || points <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), points, "A tolerance is a positive number of points.");
        }

        _tolerances[field] = points;
        return this;
    }

    /// <summary>
    /// Replaces matches in quoted text (element text, alt text, titles, link addresses, form values and so on).
    /// The replacement can refer to groups, such as "Permit $1".
    /// </summary>
    public PdfSnapshotOptions ScrubText(Regex pattern, string replacement)
    {
        _scrubbers.Add(new Scrubber(pattern, replacement, Numbered: false));
        return this;
    }

    /// <summary>
    /// Replaces dates in quoted text with Date_1, Date_2 and so on. The same text always gets the same token, so a test
    /// can still see that two dates match. Recognizes 2026-10-27, 10/27/2026, October 27, 2026 and 27 Oct 2026, each
    /// with an optional time, and PDF dates such as D:20261027120000.
    /// </summary>
    public PdfSnapshotOptions ScrubDates()
    {
        _scrubbers.Add(Scrubber.Dates);
        return this;
    }

    /// <summary>Replaces GUIDs in quoted text with Guid_1, Guid_2 and so on. The same text always gets the same token.</summary>
    public PdfSnapshotOptions ScrubGuids()
    {
        _scrubbers.Add(Scrubber.Guids);
        return this;
    }

    /// <summary>
    /// Prints the producer and creator (the libraries and application that wrote the PDF), which a snapshot normally
    /// records only as present or missing because they change with every upgrade.
    /// </summary>
    public PdfSnapshotOptions ShowProducerAndCreator()
    {
        ShowsProducerAndCreator = true;
        return this;
    }

    /// <summary>
    /// Keeps only what is on these pages, numbered from 1. A structure element or bookmark is kept when it, or anything
    /// under it, is on one of them. The document's page count, page labels and fonts are still printed for the whole file.
    /// </summary>
    public PdfSnapshotOptions OnlyPages(params int[] pages)
    {
        if (pages.Length == 0) throw new ArgumentException("Pass at least one page.", nameof(pages));
        if (pages.Any(page => page < 1)) throw new ArgumentOutOfRangeException(nameof(pages), "Pages are numbered from 1.");
        _pages.UnionWith(pages);
        return this;
    }

    internal bool ShowsProducerAndCreator { get; private set; }

    internal IReadOnlyList<Scrubber> Scrubbers => _scrubbers;

    internal IReadOnlySet<int> Pages => _pages;

    internal IReadOnlyDictionary<PdfDetail, double> ToleratedFields => _tolerances;

    internal bool Ignores(PdfSection section) => _ignoredSections.Contains(section);

    internal bool Ignores(PdfDetail field) => _ignoredFields.Contains(field);

    internal IEnumerable<PdfSection> IgnoredSections => _ignoredSections.Order();

    internal IEnumerable<PdfDetail> IgnoredFields => _ignoredFields.Order();
}
