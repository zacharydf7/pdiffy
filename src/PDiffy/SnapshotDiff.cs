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
/// says what changed. <see cref="Section"/> is null for the snapshot's first lines (format version and options), for blank
/// lines, and for every change in text without sections.
/// </summary>
/// <param name="Kind">What kind of difference it is.</param>
/// <param name="Section">The PDF snapshot section the change is in. Besides the cases listed above, it's null for every
/// change in a DOCX snapshot.</param>
/// <param name="Path">Where the change is, as described above; "header" for the snapshot's first lines, "pages" for
/// pages inserted or removed, and "whitespace" when only blank lines or line endings differ.</param>
/// <param name="Page">The page the changed line is on, counted from 1, as its first "p3" word says (the later
/// snapshot's, for a line both have); null when the line names no page. For pages inserted or removed, the first such
/// page.</param>
/// <param name="Detail">What changed, such as <c>type: H2 -> H3</c>, <c>alt: "Trail map" -> (missing)</c> or
/// <c>reading position 3 -> 5</c> and <c>parent: Document > P -> Document</c> for a move; for a line added or removed,
/// the line itself. It can run over several lines.</param>
public sealed record SnapshotChange(ChangeKind Kind, Section? Section, string Path, int? Page, string Detail)
{
    /// <summary>
    /// Whether the change can affect what assistive technology gets, by the rules the README lists under "Changes
    /// that affect accessibility", such as a heading that became a paragraph or a figure that lost its alt text.
    /// </summary>
    public bool AffectsAccessibility => AccessibilityRules.Affects(this);
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
    /// number that differs by no more than <see cref="SnapshotOptions.Tolerate"/> allows isn't a change.
    /// </summary>
    public IReadOnlyList<SnapshotChange> Changes { get; }

    /// <summary>Whether there are no changes: the snapshots match, or differ only within their tolerances.</summary>
    public bool IsEmpty => Changes.Count == 0;

    /// <summary>
    /// Compares two snapshot texts, such as a verified file and a received one. PDF snapshots are matched element by
    /// element; any other text, such as JSON, line by line.
    /// </summary>
    public static SnapshotDiff Compare(string before, string after)
    {
        before = before.Replace("\r\n", "\n");
        after = after.Replace("\r\n", "\n");
        var (changes, matchedBefore, tolerated) = SnapshotTree.IsSectioned(before) && SnapshotTree.IsSectioned(after)
            ? CompareSections(before, after)
            : (TextDiff.Compare(before, after), before, false);

        // The texts can still differ in what no line shows: blank lines, a missing newline at the end, or an indent.
        // When the snapshot's tolerances allowed a difference, that difference is what made the texts differ.
        if (changes.Count == 0 && before != after && !tolerated)
        {
            changes.Add(new SnapshotChange(ChangeKind.Changed, null, "whitespace", null, "Blank lines or line endings differ."));
        }

        return new SnapshotDiff(before, after, changes, matchedBefore);
    }

    // Also returns the earlier snapshot as compared, its pages renumbered when pages were inserted or removed, and
    // whether a tolerance allowed any difference.
    private static (List<SnapshotChange> Changes, string MatchedBefore, bool Tolerated) CompareSections(string before,
        string after)
    {
        var changes = new List<SnapshotChange>();
        var beforeHeader = before.Split('\n').Where(line => line.StartsWith('#')).ToList();
        var afterHeader = after.Split('\n').Where(line => line.StartsWith('#')).ToList();
        if (!beforeHeader.SequenceEqual(afterHeader))
        {
            var removed = beforeHeader.Except(afterHeader).Select(line => $"- {line}");
            var detail = removed.Concat(afterHeader.Except(beforeHeader).Select(line => $"+ {line}"));
            changes.Add(new SnapshotChange(ChangeKind.Changed, null, "header", null, string.Join("\n", detail)));
        }

        // Section names a PDF snapshot's sections; other formats, such as DOCX, have sections of their own.
        var pdf = before.StartsWith(SnapshotWriter.FormatPrefix, StringComparison.Ordinal)
            && after.StartsWith(SnapshotWriter.FormatPrefix, StringComparison.Ordinal);
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
            var matcher = new TreeMatcher(name, pdf ? SectionNamed(name) : null, originals, tolerances);
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

    private static Section? SectionNamed(string name) =>
        Enum.GetValues<Section>().Where(section => SnapshotWriter.Name(section) == name)
            .Select(section => (Section?)section).FirstOrDefault();

    /// <summary>
    /// A summary for a test failure: counts by kind and by section, then up to <paramref name="maxChanges"/> changes,
    /// those that can affect accessibility first.
    /// </summary>
    public string Describe(int maxChanges = 20)
    {
        if (IsEmpty) return "No changes.";

        var text = new StringBuilder($"{Changes.Count} change(s): {string.Join(", ", CountsByKind())}");
        if (IsSectioned)
        {
            text.Append($"\nby section: {string.Join(", ", CountsBySection())}");
        }

        var flagged = Changes.Count(change => change.AffectsAccessibility);
        if (flagged > 0) text.Append($"\n{flagged} can affect accessibility, listed first");
        foreach (var change in AccessibilityFirst().Take(maxChanges))
        {
            var page = change.Page is int number ? $" (p{number})" : "";
            var tag = change.AffectsAccessibility ? " [accessibility]" : "";
            text.Append($"\n{Symbol(change.Kind)} {Label(change.Kind)} {change.Path}{page}{tag}");
            foreach (var line in change.Detail.Split('\n')) text.Append($"\n    {line}");
        }

        if (Changes.Count > maxChanges) text.Append($"\n... and {Changes.Count - maxChanges} more");
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

    /// <summary>The same summary as <see cref="Describe"/>, with every change listed.</summary>
    public override string ToString() => Describe(int.MaxValue);

    internal bool IsSectioned => SnapshotTree.IsSectioned(_before) && SnapshotTree.IsSectioned(_after);

    internal IEnumerable<string> CountsByKind() => Changes.GroupBy(change => change.Kind).OrderBy(group => group.Key)
        .Select(group => $"{group.Count()} {Label(group.Key)}");

    // The section is the path's first part, such as "structure" or "body"; text without sections has none.
    internal string? SectionOf(SnapshotChange change) => IsSectioned ? change.Path.Split(" > ")[0] : null;

    // In the order sections first change.
    internal IEnumerable<string> CountsBySection() =>
        Changes.GroupBy(SectionOf).Select(group => $"{group.Key} {group.Count()}");

    // Order is kept within each group, so changes still read in snapshot order.
    internal IEnumerable<SnapshotChange> AccessibilityFirst() => Changes.OrderBy(change => !change.AffectsAccessibility);

    private static string Symbol(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => "+",
        ChangeKind.Removed => "-",
        ChangeKind.Moved => "↕",
        _ => "~",
    };

    private static string Label(ChangeKind kind) => kind switch
    {
        ChangeKind.AttributeChanged => "attribute",
        ChangeKind.TextChanged => "text",
        _ => kind.ToString().ToLowerInvariant(),
    };
}
