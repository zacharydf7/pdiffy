using System.Text;
using PDiffy.Diffing;

namespace PDiffy;

/// <summary>What kind of difference a <see cref="SnapshotChange"/> is.</summary>
public enum ChangeKind
{
    /// <summary>A line, with everything under it, that only the later snapshot has, such as a new paragraph.</summary>
    Added,

    /// <summary>A line, with everything under it, that only the earlier snapshot has.</summary>
    Removed,

    /// <summary>The same element, with everything under it, at a new position in reading order.</summary>
    Moved,

    /// <summary>One key's value changed, such as alt, scope or color.</summary>
    AttributeChanged,

    /// <summary>
    /// The text changed. The detail shows the words removed [-like this-] and added {+like this+}, or both lines in quotes
    /// when only spaces changed.
    /// </summary>
    TextChanged,

    /// <summary>Anything else about the line changed, such as its pages or a structure element's type ("type: H2 -> H3").</summary>
    Changed,
}

/// <summary>
/// One difference between two snapshots. <see cref="Path"/> locates it, such as
/// "structure > Document > Table > TR[2] > TD[1]", or "line 12" in text without PDiffy's sections; <see cref="Detail"/>
/// says what changed. <see cref="SectionName"/> names the section it's in, and <see cref="PdfSection"/> or
/// <see cref="DocxSection"/> gives that section as an enum.
/// </summary>
public sealed record SnapshotChange
{
    internal SnapshotChange()
    {
    }

    /// <summary>What kind of difference it is.</summary>
    public required ChangeKind Kind { get; init; }

    /// <summary>
    /// The section the change is in, as the snapshot names it, such as "structure" or "body"; "header" for the
    /// snapshot's first lines (format version and options), "whitespace" when only blank lines or line endings differ,
    /// and null for every change in text without sections. The JSON output's "section" holds the same string.
    /// </summary>
    public required string? SectionName { get; init; }

    /// <summary>
    /// The section of a PDF snapshot the change is in; null for a Word or text snapshot, "header" and "whitespace".
    /// </summary>
    public PdfSection? PdfSection => Format == SnapshotFormat.Pdf ? Named<PdfSection>(SectionName) : null;

    /// <summary>
    /// The section of a Word snapshot the change is in; null for a PDF or text snapshot, "header" and "whitespace".
    /// </summary>
    public DocxSection? DocxSection => Format == SnapshotFormat.Docx ? Named<DocxSection>(SectionName) : null;

    /// <summary>
    /// Where the change is, as described above; "header" for the snapshot's first lines, "pages" for pages inserted or
    /// removed, and "whitespace" when only blank lines or line endings differ. For a new or fixed problem in the checks
    /// section, the rule, such as "FigureAltText", with the finding's own path as the <see cref="Detail"/>.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The page the changed line is on, counted from 1, as its first "p3" word says (the later snapshot's, for a line
    /// both have); null when the line names no page. For pages inserted or removed, the first such page.
    /// </summary>
    public required int? Page { get; init; }

    /// <summary>
    /// What changed, such as <c>type: H2 -> H3</c>, <c>alt: "Trail map" -> (missing)</c> or <c>reading position 3 ->
    /// 5</c> and <c>parent: Document > P -> Document</c> for a move; for a line added or removed, the line itself. It
    /// can run over several lines.
    /// </summary>
    public required string Detail { get; init; }

    /// <summary>
    /// Whether the change can affect what assistive technology gets, by the rules the README lists under "Changes
    /// that affect accessibility", such as a heading that became a paragraph or a figure that lost its alt text. Null
    /// when the change isn't assessed: every change in a Word or text snapshot.
    /// </summary>
    public bool? AffectsAccessibility => Format == SnapshotFormat.Pdf ? AccessibilityRules.Affects(this) : null;

    // Which kind of snapshot the change is in, which says what its section name means.
    internal SnapshotFormat Format { get; init; }

    // A new or fixed problem in a PDF's or a Word document's checks section.
    internal bool IsFinding => PdfSection == PDiffy.PdfSection.Checks || DocxSection == PDiffy.DocxSection.Checks;

    private static T? Named<T>(string? name) where T : struct, Enum =>
        Enum.GetValues<T>().Where(value => SnapshotWriter.Name(value) == name).Select(value => (T?)value)
            .FirstOrDefault();
}

/// <summary>The kind of snapshot two texts are, which decides what their sections mean.</summary>
internal enum SnapshotFormat
{
    Text,
    Pdf,
    Docx,
}

/// <summary>
/// The differences between two snapshots. PDF snapshots are matched element by element, so an edited paragraph is one
/// change instead of a removed line and an added one, and an element that moved is reported as moved. Other text, such
/// as JSON, is compared line by line.
/// </summary>
public sealed class SnapshotDiff
{
    private readonly string _before;
    private readonly string _after;
    private readonly string _matchedBefore;

    private SnapshotDiff(string before, string after, IReadOnlyList<SnapshotChange> changes, string matchedBefore)
    {
        _before = before;
        _after = after;
        _matchedBefore = matchedBefore;
        Changes = changes;
    }

    /// <summary>
    /// Every change, in snapshot order; <see cref="Describe"/> lists those that can affect accessibility first. A
    /// number that differs by no more than <see cref="PdfSnapshotOptions.Tolerate"/> allows isn't a change.
    /// </summary>
    public IReadOnlyList<SnapshotChange> Changes { get; }

    /// <summary>Whether there are no changes: the snapshots match, or differ only within their tolerances.</summary>
    public bool IsEmpty => Changes.Count == 0;

    /// <summary>
    /// The differences between two PDFs' snapshots, element by element: what was added, removed, moved or changed, and
    /// where, such as before and after a library upgrade.
    /// </summary>
    /// <param name="before">The earlier PDF, whose content counts as removed when it's missing from the other.</param>
    /// <param name="after">The later PDF, whose new content counts as added.</param>
    /// <param name="configure">
    /// What to leave out or scrub from both, as for <see cref="PdfUaDocument.ToSnapshot"/>.
    /// </param>
    public static SnapshotDiff Compare(PdfUaDocument before, PdfUaDocument after,
        Action<PdfSnapshotOptions>? configure = null) =>
        CompareSnapshots(before.ToSnapshot(configure), after.ToSnapshot(configure));

    /// <summary>The differences between two Word documents' snapshots, element by element.</summary>
    /// <param name="before">
    /// The earlier document, whose content counts as removed when it's missing from the other.
    /// </param>
    /// <param name="after">The later document, whose new content counts as added.</param>
    /// <param name="configure">
    /// What to leave out or scrub from both, as for <see cref="DocxDocument.ToSnapshot"/>.
    /// </param>
    public static SnapshotDiff Compare(DocxDocument before, DocxDocument after,
        Action<DocxSnapshotOptions>? configure = null) =>
        CompareSnapshots(before.ToSnapshot(configure), after.ToSnapshot(configure));

    /// <summary>
    /// Compares two files the way <c>pdiffy before after</c> does: a PDF or a Word document is read and snapshotted,
    /// and anything else, such as a <c>.verified.pdfua.txt</c> file or JSON, is compared as a snapshot already.
    /// </summary>
    /// <param name="beforePath">The earlier file.</param>
    /// <param name="afterPath">The later file.</param>
    /// <param name="configurePdf">What to leave out or scrub from a PDF.</param>
    /// <param name="configureDocx">What to leave out or scrub from a Word document.</param>
    public static SnapshotDiff CompareFiles(string beforePath, string afterPath,
        Action<PdfSnapshotOptions>? configurePdf = null, Action<DocxSnapshotOptions>? configureDocx = null) =>
        CompareSnapshots(SnapshotOf(beforePath, configurePdf, configureDocx), SnapshotOf(afterPath, configurePdf,
            configureDocx));

    /// <summary>
    /// Compares two snapshot texts, such as a verified file's contents and a received one's. PDF and Word snapshots are
    /// matched element by element; any other text, such as JSON, line by line. To compare files, use
    /// <see cref="CompareFiles"/>.
    /// </summary>
    /// <param name="before">The earlier snapshot text.</param>
    /// <param name="after">The later snapshot text.</param>
    public static SnapshotDiff CompareSnapshots(string before, string after)
    {
        before = before.Replace("\r\n", "\n");
        after = after.Replace("\r\n", "\n");
        var sectioned = SnapshotTree.IsSectioned(before) && SnapshotTree.IsSectioned(after);
        var format = FormatOf(before, after);
        var (changes, matchedBefore, tolerated) = sectioned
            ? CompareSections(before, after, format)
            : (TextDiff.Compare(before, after), before, false);

        // The texts can still differ in what no line shows: blank lines, a missing newline at the end, or an indent.
        // When the snapshot's tolerances allowed a difference, that difference is what made the texts differ.
        if (changes.Count == 0 && before != after && !tolerated)
        {
            changes.Add(new SnapshotChange
            {
                Kind = ChangeKind.Changed, SectionName = sectioned ? "whitespace" : null, Path = "whitespace",
                Page = null, Detail = "Blank lines or line endings differ.", Format = format,
            });
        }

        return new SnapshotDiff(before, after, changes, matchedBefore);
    }

    // Also returns the earlier snapshot as compared, its pages renumbered when pages were inserted or removed, and
    // whether a tolerance allowed any difference.
    private static (List<SnapshotChange> Changes, string MatchedBefore, bool Tolerated) CompareSections(string before,
        string after, SnapshotFormat format)
    {
        var changes = new List<SnapshotChange>();
        var beforeHeader = before.Split('\n').Where(line => line.StartsWith('#')).ToList();
        var afterHeader = after.Split('\n').Where(line => line.StartsWith('#')).ToList();
        if (!beforeHeader.SequenceEqual(afterHeader))
        {
            var removed = beforeHeader.Except(afterHeader).Select(line => $"- {line}");
            var detail = removed.Concat(afterHeader.Except(beforeHeader).Select(line => $"+ {line}"));
            changes.Add(new SnapshotChange
            {
                Kind = ChangeKind.Changed, SectionName = "header", Path = "header", Page = null, Format = format,
                Detail = string.Join("\n", detail),
            });
        }

        var pdf = format == SnapshotFormat.Pdf;
        var beforeSections = SnapshotTree.Parse(before);
        var afterSections = SnapshotTree.Parse(after);

        var matchedBefore = before;
        Dictionary<SnapshotLine, SnapshotLine>? originals = null;

        // An inserted or removed page renumbers every later one, so the pages are lined up first.
        if (pdf && PageAlignment.Find(beforeSections, afterSections) is { } alignment)
        {
            changes.Add(alignment.Change());
            matchedBefore = alignment.Renumber(before);
            var written = beforeSections;
            beforeSections = SnapshotTree.Parse(matchedBefore);
            originals = Originals(beforeSections, written);
        }

        // A difference in the tolerances themselves is a header change, so either side's are the ones to apply.
        var tolerances = pdf ? Tolerances.Read(after) : null;
        var tolerated = false;
        foreach (var name in beforeSections.Keys.Union(afterSections.Keys))
        {
            if (format != SnapshotFormat.Text && name == SnapshotWriter.Name(PdfSection.Checks))
            {
                changes.AddRange(CheckChanges.Compare(beforeSections.GetValueOrDefault(name) ?? [],
                    afterSections.GetValueOrDefault(name) ?? [], originals, format));
                continue;
            }

            var matcher = new TreeMatcher(name, format, originals, tolerances);
            changes.AddRange(matcher.Compare(beforeSections.GetValueOrDefault(name) ?? [],
                afterSections.GetValueOrDefault(name) ?? []));
            tolerated |= matcher.Tolerated;
        }

        return (changes, matchedBefore, tolerated);
    }

    // Renumbering keeps every line in its place, so each renumbered line pairs with the written one at the same spot.
    private static Dictionary<SnapshotLine, SnapshotLine> Originals(Dictionary<string, List<SnapshotLine>> renumbered,
        Dictionary<string, List<SnapshotLine>> written)
    {
        static IEnumerable<SnapshotLine> Flatten(SnapshotLine line) => line.Children.SelectMany(Flatten).Prepend(line);
        return renumbered.SelectMany(section => section.Value.SelectMany(Flatten)
                .Zip(written[section.Key].SelectMany(Flatten)))
            .ToDictionary(pair => pair.First, pair => pair.Second);
    }

    private static string SnapshotOf(string path, Action<PdfSnapshotOptions>? configurePdf,
        Action<DocxSnapshotOptions>? configureDocx) =>
        DocumentFiles.IsPdf(path) ? PdfUaDocument.Load(path).ToSnapshot(configurePdf)
        : DocumentFiles.IsDocx(path) ? DocxDocument.Load(path).ToSnapshot(configureDocx)
        : File.ReadAllText(path);

    // Both texts must be the same kind of snapshot for their section names to mean that kind's sections.
    private static SnapshotFormat FormatOf(string before, string after)
    {
        bool Both(string prefix) =>
            before.StartsWith(prefix, StringComparison.Ordinal) && after.StartsWith(prefix, StringComparison.Ordinal);
        return Both(SnapshotWriter.FormatPrefix) ? SnapshotFormat.Pdf
            : Both(DocxSnapshotWriter.FormatPrefix) ? SnapshotFormat.Docx
            : SnapshotFormat.Text;
    }

    /// <summary>
    /// A summary for a test failure: counts by kind and by section, then up to <paramref name="maxChanges"/> changes,
    /// those that can affect accessibility first. Layout lines one after another that moved the same way count as one
    /// change, a shift.
    /// </summary>
    public string Describe(int maxChanges = 20)
    {
        if (IsEmpty) return "No changes.";

        var text = new StringBuilder($"{Changes.Count} change(s): {string.Join(", ", CountsByKind())}");
        if (IsSectioned)
        {
            text.Append($"\nby section: {string.Join(", ", CountsBySection())}");
        }

        if (CheckRules(_after) - CheckRules(_before) is > 0 and var added)
        {
            text.Append($"\n{added} rule(s) added to the checks, so a new problem can come from a new rule");
        }

        var flagged = Changes.Count(change => change.AffectsAccessibility == true);
        if (flagged > 0) text.Append($"\n{flagged} can affect accessibility, listed first");
        // A reflow's run of lines that all moved the same way is one item, so the edit that caused it isn't buried.
        var shown = 0;
        foreach (var group in LayoutShifts.Group(AccessibilityFirst()).Take(maxChanges))
        {
            var change = group[0];
            var page = change.Page is int number ? $" (p{number})" : "";
            if (LayoutShifts.IsRun(group))
            {
                text.Append($"\n~ shifted {change.Path} to {group[^1].Path.Split(" > ")[^1]}{page}");
                text.Append($"\n    {LayoutShifts.Describe(group)}");
            }
            else
            {
                var tag = change.AffectsAccessibility == true ? " [accessibility]" : "";
                text.Append($"\n{Symbol(change.Kind)} {Label(change)} {change.Path}{page}{tag}");
                foreach (var line in change.Detail.Split('\n')) text.Append($"\n    {line}");
            }

            shown += group.Count;
        }

        if (Changes.Count > shown) text.Append($"\n... and {Changes.Count - shown} more");
        return text.ToString();
    }

    /// <summary>
    /// A self-contained HTML page with the two snapshots side by side and the changed words highlighted, for reading a
    /// failure in a browser or keeping it as a build artifact.
    /// </summary>
    public string ToHtml() => DiffReport.Write(_before, _matchedBefore, _after, this);

    /// <summary>
    /// The changes as JSON, for scripts and tools outside .NET: a version number and, for each change, its kind,
    /// section, path, page, detail and whether it can affect accessibility. The README describes the shape.
    /// </summary>
    public string ToJson() => DiffJson.Write(this);

    /// <summary>The same summary as <see cref="Describe"/>, with nothing left out.</summary>
    public override string ToString() => Describe(int.MaxValue);

    internal bool IsSectioned => SnapshotTree.IsSectioned(_before) && SnapshotTree.IsSectioned(_after);

    // Findings count apart from content, as new problems and fixed ones.
    internal IEnumerable<string> CountsByKind() => Changes
        .GroupBy(change => (change.Kind, Finding: change.IsFinding))
        .OrderBy(group => group.Key.Kind).ThenBy(group => group.Key.Finding)
        .Select(group => $"{group.Count()} {Label(group.First())}");

    // In the order sections first change.
    internal IEnumerable<string> CountsBySection() =>
        Changes.GroupBy(change => change.SectionName).Select(group => $"{group.Key} {group.Count()}");

    // Order is kept within each group, so changes still read in snapshot order.
    internal IEnumerable<SnapshotChange> AccessibilityFirst() =>
        Changes.OrderBy(change => change.AffectsAccessibility != true);

    private static string Symbol(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => "+",
        ChangeKind.Removed => "-",
        ChangeKind.Moved => "↕",
        _ => "~",
    };

    private static string Label(SnapshotChange change) => (change.Kind, change.IsFinding) switch
    {
        (ChangeKind.Added, true) => "new problem",
        (ChangeKind.Removed, true) => "fixed",
        (ChangeKind.AttributeChanged, _) => "attribute",
        (ChangeKind.TextChanged, _) => "text",
        _ => change.Kind.ToString().ToLowerInvariant(),
    };

    // How many rules a snapshot's checks section applies, from its "# checks: latest, 12 rules" header line; 0 when it
    // has none.
    private static int CheckRules(string snapshot)
    {
        var header = snapshot.Split('\n').TakeWhile(line => line.StartsWith('#'))
            .FirstOrDefault(line => line.StartsWith(SnapshotWriter.ChecksHeader, StringComparison.Ordinal));
        var count = header?[(header.LastIndexOf(", ", StringComparison.Ordinal) + 2)..].Split(' ')[0];
        return int.TryParse(count, out var rules) ? rules : 0;
    }
}
