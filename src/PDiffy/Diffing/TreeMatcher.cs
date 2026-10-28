using System.Text;

namespace PDiffy.Diffing;

/// <summary>
/// Compares one section of two snapshots. Siblings that didn't change at all are matched first. Between them, a removed
/// and an added line with the same key and mostly the same words are treated as one edited line. Whatever is left is
/// removed or added, unless the same subtree turns up on the other side, which makes it a move.
/// </summary>
internal sealed class TreeMatcher(string name, Section? section)
{
    private const double SameLineSimilarity = 0.5;

    private readonly List<Entry> _entries = [];
    private Dictionary<SnapshotLine, int> _beforePositions = [];
    private Dictionary<SnapshotLine, int> _afterPositions = [];

    public IReadOnlyList<SnapshotChange> Compare(List<SnapshotLine> before, List<SnapshotLine> after)
    {
        _beforePositions = Positions(before);
        _afterPositions = Positions(after);
        CompareChildren(before, after, name);
        FindMoves();
        return _entries.Select(ToChange).ToList();
    }

    private void CompareChildren(List<SnapshotLine> before, List<SnapshotLine> after, string parentPath)
    {
        var steps = LineDiff.Compare(before.ConvertAll(line => line.Signature), after.ConvertAll(line => line.Signature));
        var (b, a) = (0, 0);
        var removed = new List<SnapshotLine>();
        var added = new List<SnapshotLine>();
        foreach (var step in steps.Append(new LineChange(' ', "")))
        {
            if (step.Kind == '-') removed.Add(before[b++]);
            else if (step.Kind == '+') added.Add(after[a++]);
            else
            {
                CompareGap(removed, added, before, after, parentPath);
                removed.Clear();
                added.Clear();
                b++;
                a++;
            }
        }
    }

    // Pairs removed and added lines in order when they look like the same line edited; the rest wait for FindMoves.
    private void CompareGap(List<SnapshotLine> removed, List<SnapshotLine> added, List<SnapshotLine> beforeSiblings,
        List<SnapshotLine> afterSiblings, string parentPath)
    {
        var next = 0;
        foreach (var old in removed)
        {
            var match = added.FindIndex(next, line => IsSameLine(old, line));
            if (match < 0)
            {
                _entries.Add(new Entry(Removed: old, Path: PathOf(old, beforeSiblings, parentPath)));
                continue;
            }

            foreach (var skipped in added.Skip(next).Take(match - next)) AddAdded(skipped, afterSiblings, parentPath);
            var edited = added[match];
            var path = PathOf(edited, afterSiblings, parentPath);
            CompareLines(old, edited, path);
            CompareChildren(old.Children, edited.Children, path);
            next = match + 1;
        }

        foreach (var line in added.Skip(next)) AddAdded(line, afterSiblings, parentPath);
    }

    private void AddAdded(SnapshotLine line, List<SnapshotLine> siblings, string parentPath) =>
        _entries.Add(new Entry(Added: line, Path: PathOf(line, siblings, parentPath)));

    private void CompareLines(SnapshotLine old, SnapshotLine edited, string path)
    {
        if (old.Text == edited.Text) return;

        var page = edited.Page ?? old.Page;
        var found = _entries.Count;
        // A key can repeat, as annot= does once per annotation, so values are compared occurrence by occurrence.
        foreach (var key in old.Attributes.Select(pair => pair.Key).Union(edited.Attributes.Select(pair => pair.Key)))
        {
            var before = Values(old, key);
            var after = Values(edited, key);
            for (var i = 0; i < Math.Max(before.Count, after.Count); i++)
            {
                var from = i < before.Count ? before[i] : "(missing)";
                var to = i < after.Count ? after[i] : "(missing)";
                if (from != to) Add(ChangeKind.AttributeChanged, path, page, $"{key}: {from} -> {to}");
            }
        }

        if (old.QuotedText != edited.QuotedText)
        {
            Add(ChangeKind.TextChanged, path, page, $"\"{WordDiff.Show(old.QuotedText ?? "", edited.QuotedText ?? "")}\"");
        }

        // Other words (such as the pages) changed, or something no part above covers, such as keys in a new order.
        if (!old.Words.SequenceEqual(edited.Words) || _entries.Count == found)
        {
            Add(ChangeKind.Changed, path, page, $"{old.Text.TrimStart()} -> {edited.Text.TrimStart()}");
        }
    }

    private void Add(ChangeKind kind, string path, int? page, string detail) =>
        _entries.Add(new Entry(Change: new SnapshotChange(kind, section, path, page, detail)));

    // A removed subtree that reappears, unchanged, somewhere else on the other side was moved, not removed and added.
    private void FindMoves()
    {
        foreach (var removed in _entries.Where(entry => entry.Removed is not null).ToList())
        {
            var index = _entries.FindIndex(entry => entry.Added?.Signature == removed.Removed!.Signature);
            if (index < 0) continue;

            var added = _entries[index];
            var detail = $"reading position {_beforePositions[removed.Removed!]} -> {_afterPositions[added.Added!]}";
            var moved = new SnapshotChange(ChangeKind.Moved, section, added.Path!, added.Added!.Page, detail);
            _entries[index] = new Entry(Change: moved);
            _entries.Remove(removed);
        }
    }

    private SnapshotChange ToChange(Entry entry) => entry.Change
        ?? (entry.Removed is { } removed
            ? new SnapshotChange(ChangeKind.Removed, section, entry.Path!, removed.Page, removed.Text.Trim())
            : new SnapshotChange(ChangeKind.Added, section, entry.Path!, entry.Added!.Page, entry.Added.Text.Trim()));

    // The key, numbered when siblings share it: "TR[2]".
    private static string PathOf(SnapshotLine line, List<SnapshotLine> siblings, string parentPath)
    {
        var sameKey = siblings.Where(sibling => sibling.Key == line.Key).ToList();
        var label = sameKey.Count > 1 ? $"{line.Key}[{sameKey.IndexOf(line) + 1}]" : line.Key;
        return $"{parentPath} > {label}";
    }

    // The same key, and either the same text (a bookmark that gained a page) or mostly the same words.
    internal static bool IsSameLine(SnapshotLine old, SnapshotLine line) =>
        line.Key == old.Key
        && (old.QuotedText is not null && line.QuotedText == old.QuotedText || Similarity(old, line) >= SameLineSimilarity);

    // The share of words, attributes and text words two lines have in common.
    private static double Similarity(SnapshotLine first, SnapshotLine second)
    {
        var firstTokens = Tokens(first);
        var secondTokens = Tokens(second);
        var common = firstTokens.Intersect(secondTokens).Count();
        return (double)common / Math.Max(1, Math.Max(firstTokens.Count, secondTokens.Count));
    }

    private static HashSet<string> Tokens(SnapshotLine line) =>
    [
        .. line.Words, .. line.Attributes.Select(pair => $"{pair.Key}={pair.Value}"),
        .. (line.QuotedText ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries),
    ];

    private static List<string> Values(SnapshotLine line, string key) =>
        line.Attributes.Where(pair => pair.Key == key).Select(pair => pair.Value).ToList();

    // Each line's place in reading order: depth first, counting from 1.
    private static Dictionary<SnapshotLine, int> Positions(List<SnapshotLine> roots)
    {
        var positions = new Dictionary<SnapshotLine, int>();
        void Visit(SnapshotLine line)
        {
            positions[line] = positions.Count + 1;
            foreach (var child in line.Children) Visit(child);
        }

        foreach (var root in roots) Visit(root);
        return positions;
    }

    // A finished change, or a removed or added line that may still turn out to be half of a move.
    private sealed record Entry(
        SnapshotChange? Change = null, SnapshotLine? Removed = null, SnapshotLine? Added = null, string? Path = null);
}

/// <summary>A word-level diff of two texts, marking removed words [-like this-] and added words {+like this+}.</summary>
internal static class WordDiff
{
    public static string Show(string before, string after)
    {
        var steps = LineDiff.Compare(Words(before), Words(after));
        var text = new StringBuilder();
        for (var i = 0; i < steps.Count;)
        {
            var kind = steps[i].Kind;
            var words = new List<string>();
            while (i < steps.Count && steps[i].Kind == kind) words.Add(steps[i++].Line);
            var joined = string.Join(" ", words);
            if (text.Length > 0) text.Append(' ');
            text.Append(kind switch { '-' => $"[-{joined}-]", '+' => $"{{+{joined}+}}", _ => joined });
        }

        return text.ToString();
    }

    private static string[] Words(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
