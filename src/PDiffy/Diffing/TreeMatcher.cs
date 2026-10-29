using System.Text;

namespace PDiffy.Diffing;

/// <summary>
/// Compares one section of two snapshots. Siblings that didn't change at all are matched first. Between them, a removed
/// and an added line with the same key and mostly the same words are treated as one edited line; failing that, so are
/// two without text with the same key and leading words, such as a table cell that gained attributes, and, in the
/// structure, two with the same text and a different type. Whatever is left is removed or added, unless it turns up
/// elsewhere on the other side, unchanged or edited, which makes it a move; found again in its own place, it was only
/// edited. When the earlier snapshot's pages were renumbered to line up with the later one's,
/// <paramref name="originals"/> gives each line as it was written, for showing. A number that moved no more than
/// <paramref name="tolerances"/> allow isn't a change.
/// </summary>
internal sealed class TreeMatcher(string name, Section? section,
    IReadOnlyDictionary<SnapshotLine, SnapshotLine>? originals = null, Tolerances? tolerances = null)
{
    internal const double SameLineSimilarity = 0.5;

    private readonly List<Entry> _entries = [];
    private Dictionary<SnapshotLine, int> _beforePositions = [];
    private Dictionary<SnapshotLine, int> _afterPositions = [];

    /// <summary>Whether the tolerances allowed any difference, which <see cref="Compare"/> then didn't report.</summary>
    public bool Tolerated { get; private set; }

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
        // An empty or ignored section's placeholder isn't an element, so content taking its place is only added or removed.
        (before, after) = (WithoutPlaceholder(before), WithoutPlaceholder(after));
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
            var match = MatchOf(old, added, next, name);
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

    // A placeholder is the only line under its header and has nothing under it; an element with no type also prints as
    // "(none)", but among other lines or with children of its own.
    private static List<SnapshotLine> WithoutPlaceholder(List<SnapshotLine> lines) =>
        lines is [{ Children.Count: 0 } only] && only.Text.Trim() is SnapshotWriter.None or SnapshotWriter.Ignored
            ? []
            : lines;

    private void AddAdded(SnapshotLine line, List<SnapshotLine> siblings, string parentPath) =>
        _entries.Add(new Entry(Added: line, Path: PathOf(line, siblings, parentPath)));

    private void CompareLines(SnapshotLine old, SnapshotLine edited, string path)
    {
        // A line moved to another depth differs only in its indent.
        if (old.Text.TrimStart() == edited.Text.TrimStart()) return;

        var page = edited.Page ?? old.Page;
        var found = _entries.Count;
        var retyped = old.Key != edited.Key;
        var tolerated = false;
        if (retyped) Add(ChangeKind.Changed, path, page, $"type: {old.Key} -> {edited.Key}");
        // A key can repeat, as annot= does once per annotation, so values are compared occurrence by occurrence.
        foreach (var key in old.Attributes.Select(pair => pair.Key).Union(edited.Attributes.Select(pair => pair.Key)))
        {
            var before = Values(old, key);
            var after = Values(edited, key);
            for (var i = 0; i < Math.Max(before.Count, after.Count); i++)
            {
                var from = i < before.Count ? before[i] : "(missing)";
                var to = i < after.Count ? after[i] : "(missing)";
                if (from == to) continue;
                if (tolerances?.Allow(edited, key, from, to) == true) tolerated = Tolerated = true;
                else Add(ChangeKind.AttributeChanged, path, page, $"{key}: {from} -> {to}");
            }
        }

        if (old.QuotedText != edited.QuotedText)
        {
            Add(ChangeKind.TextChanged, path, page, $"\"{WordDiff.Show(old.QuotedText ?? "", edited.QuotedText ?? "")}\"");
        }

        // Other words (such as pages) changed, or nothing above caught or tolerated it; a retyped line's first word is
        // its type.
        var skip = retyped ? 1 : 0;
        if (!old.Words.Skip(skip).SequenceEqual(edited.Words.Skip(skip)) || (_entries.Count == found && !tolerated))
        {
            Add(ChangeKind.Changed, path, page, $"{Original(old).Text.TrimStart()} -> {edited.Text.TrimStart()}");
        }
    }

    private void Add(ChangeKind kind, string path, int? page, string detail) =>
        _entries.Add(new Entry(Change: new SnapshotChange(kind, section, path, page, detail)));

    // A removed subtree found elsewhere, as is or edited, was moved; leftover children of a move are checked too.
    private void FindMoves()
    {
        var pending = new Queue<Entry>(_entries.Where(entry => entry.Removed is not null));
        while (pending.TryDequeue(out var removed))
        {
            var old = removed.Removed!;
            var match = Nearest(old, _entries.Where(entry => entry.Added?.Signature == old.Signature))
                ?? Nearest(old, _entries.Where(entry => entry.Added is { } line && IsEditedCopy(old, line)));
            if (match is null) continue;

            // The edits to a moved line go right after its move, not at the end of the list.
            var (line, path, start) = (match.Added!, match.Path!, _entries.Count);
            CompareLines(old, line, path);
            CompareChildren(old.Children, line.Children, path);
            var edits = _entries.GetRange(start, _entries.Count - start);
            _entries.RemoveRange(start, edits.Count);
            var index = _entries.IndexOf(match);
            _entries.RemoveAt(index);
            // A line found again in its own place, at the same path and reading position, was edited, not moved.
            if (path != removed.Path || _beforePositions[old] != _afterPositions[line])
            {
                var detail = $"reading position {_beforePositions[old]} -> {_afterPositions[line]}";
                var moved = new SnapshotChange(ChangeKind.Moved, section, path, line.Page, detail);
                _entries.Insert(index++, new Entry(Change: moved));
            }

            _entries.InsertRange(index, edits);
            _entries.Remove(removed);
            foreach (var child in edits.Where(entry => entry.Removed is not null)) pending.Enqueue(child);
        }
    }

    // When the same line was added in several places, the one closest to where it was removed is its move.
    private Entry? Nearest(SnapshotLine old, IEnumerable<Entry> candidates) =>
        candidates.MinBy(entry => Math.Abs(_afterPositions[entry.Added!] - _beforePositions[old]));

    private SnapshotChange ToChange(Entry entry) => entry.Change
        ?? (entry.Removed is { } removed
            ? new SnapshotChange(ChangeKind.Removed, section, entry.Path!, Original(removed).Page,
                Original(removed).Text.Trim())
            : new SnapshotChange(ChangeKind.Added, section, entry.Path!, entry.Added!.Page, entry.Added.Text.Trim()));

    private SnapshotLine Original(SnapshotLine line) => originals?.GetValueOrDefault(line) ?? line;

    // The key, numbered when siblings share it: "TR[2]".
    private static string PathOf(SnapshotLine line, List<SnapshotLine> siblings, string parentPath)
    {
        var sameKey = siblings.Where(sibling => sibling.Key == line.Key).ToList();
        var label = sameKey.Count > 1 ? $"{line.Key}[{sameKey.IndexOf(line) + 1}]" : line.Key;
        return $"{parentPath} > {label}";
    }

    // Same key, and the same text (a bookmark that gained a page), mostly the same words, or a changed one-word value.
    internal static bool IsSameLine(SnapshotLine old, SnapshotLine line) =>
        line.Key == old.Key && (old.QuotedText is not null && line.QuotedText == old.QuotedText
            || Similarity(old, line) >= SameLineSimilarity || IsValueChange(old, line));

    // A structure element whose type changed but whose text didn't, such as H2 to H3; tried after same-type lines.
    internal static bool IsRetyped(SnapshotLine old, SnapshotLine line) =>
        line.Key != old.Key && !string.IsNullOrEmpty(old.QuotedText) && line.QuotedText == old.QuotedText;

    // A line without text, of the same key, whose attributes alone changed, however much: a table cell that gained
    // margins and borders, or a page's counts of untagged images and paths ("p1 images=2 paths=3").
    private static bool IsReattributed(SnapshotLine old, SnapshotLine line) =>
        line.Key == old.Key && old.QuotedText is null && line.QuotedText is null && old.Words.SequenceEqual(line.Words);

    // The same line edited, else one without text that only changed attributes, else a retype in the structure.
    internal static int MatchOf(SnapshotLine old, List<SnapshotLine> added, int start, string sectionName)
    {
        var match = added.FindIndex(start, line => IsSameLine(old, line));
        if (match < 0) match = added.FindIndex(start, line => IsReattributed(old, line));
        return match >= 0 || sectionName != SnapshotWriter.Name(Section.Structure) ? match
            : added.FindIndex(start, line => IsRetyped(old, line));
    }

    // Only text words count when both lines have text, so type, page and style alone never make two lines the same.
    private static double Similarity(SnapshotLine first, SnapshotLine second) =>
        first.TextWords.Count > 0 && second.TextWords.Count > 0
            ? Overlap(first.TextWords, second.TextWords)
            : Overlap(first.AllWords, second.AllWords);

    // A one-word value, such as a number in a table cell, replaced on a line that is otherwise the same.
    private static bool IsValueChange(SnapshotLine old, SnapshotLine line) =>
        old.TextWords.Count == 1 && line.TextWords.Count == 1 && old.Words.SequenceEqual(line.Words)
        && old.Attributes.SequenceEqual(line.Attributes);

    // An edited copy elsewhere: when both subtrees have text, their words decide, so unrelated rows never pair.
    private static bool IsEditedCopy(SnapshotLine old, SnapshotLine line)
    {
        if (line.Key != old.Key) return false;

        var (before, after) = (old.SubtreeWords, line.SubtreeWords);
        var similarity = before.Count > 0 && after.Count > 0 ? Overlap(before, after) : Similarity(old, line);
        return similarity >= SameLineSimilarity;
    }

    // The share of words two sets have in common, out of the larger set. A plain loop, since a diff with nothing in
    // common runs it for every pair of lines.
    internal static double Overlap(HashSet<string> first, HashSet<string> second)
    {
        var shared = 0;
        foreach (var word in first)
        {
            if (second.Contains(word)) shared++;
        }

        return (double)shared / Math.Max(1, Math.Max(first.Count, second.Count));
    }

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
