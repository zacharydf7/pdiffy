using System.Text;
using PDiffy.Diffing;

namespace PDiffy;

/// <summary>What kind of difference a <see cref="SnapshotChange"/> is.</summary>
public enum ChangeKind
{
    Added,
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
public sealed record SnapshotChange(ChangeKind Kind, Section? Section, string Path, int? Page, string Detail)
{
    /// <summary>
    /// Whether the change can affect what assistive technology gets, by the fixed rules the README lists under "Changes
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

    private SnapshotDiff(string before, string after, IReadOnlyList<SnapshotChange> changes)
    {
        _before = before;
        _after = after;
        Changes = changes;
    }

    public IReadOnlyList<SnapshotChange> Changes { get; }

    public bool IsEmpty => Changes.Count == 0;

    /// <summary>
    /// Compares two snapshot texts, such as a verified file and a received one. PDF snapshots are matched element by
    /// element; any other text, such as JSON, line by line.
    /// </summary>
    public static SnapshotDiff Compare(string before, string after)
    {
        before = before.Replace("\r\n", "\n");
        after = after.Replace("\r\n", "\n");
        var changes = SnapshotTree.IsSectioned(before) && SnapshotTree.IsSectioned(after)
            ? CompareSections(before, after)
            : TextDiff.Compare(before, after);

        // The texts can still differ in what no line shows: blank lines, or a missing newline at the end.
        if (changes.Count == 0 && before != after)
        {
            changes.Add(new SnapshotChange(ChangeKind.Changed, null, "whitespace", null, "Blank lines or line endings differ."));
        }

        return new SnapshotDiff(before, after, changes);
    }

    private static List<SnapshotChange> CompareSections(string before, string after)
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
        var pdf = before.StartsWith(SnapshotWriter.FormatHeader, StringComparison.Ordinal);
        var beforeSections = SnapshotTree.Parse(before);
        var afterSections = SnapshotTree.Parse(after);
        foreach (var name in beforeSections.Keys.Union(afterSections.Keys))
        {
            var matcher = new TreeMatcher(name, pdf ? SectionNamed(name) : null);
            changes.AddRange(matcher.Compare(beforeSections.GetValueOrDefault(name) ?? [],
                afterSections.GetValueOrDefault(name) ?? []));
        }

        return changes;
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
    public string ToHtml() => DiffReport.Write(_before, _after, this);

    public override string ToString() => Describe(int.MaxValue);

    internal bool IsSectioned => SnapshotTree.IsSectioned(_before) && SnapshotTree.IsSectioned(_after);

    internal IEnumerable<string> CountsByKind() => Changes.GroupBy(change => change.Kind).OrderBy(group => group.Key)
        .Select(group => $"{group.Count()} {Label(group.Key)}");

    // The section is the path's first part, such as "structure" or "body", in the order sections first change.
    internal IEnumerable<string> CountsBySection() =>
        Changes.GroupBy(change => change.Path.Split(" > ")[0]).Select(group => $"{group.Key} {group.Count()}");

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
