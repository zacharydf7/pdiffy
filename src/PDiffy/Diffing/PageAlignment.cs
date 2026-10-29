namespace PDiffy.Diffing;

/// <summary>
/// How the pages of two PDF snapshots line up when pages were only inserted or only removed, worked out from the text on
/// each page. Renumbering the earlier snapshot's pages to match lets the matcher compare old page 5 with new page 6,
/// instead of reporting every later element as moved to another page.
/// </summary>
internal sealed class PageAlignment
{
    private readonly Dictionary<int, int> _newNumbers;
    private readonly List<int> _inserted;
    private readonly List<int> _removed;

    private PageAlignment(Dictionary<int, int> newNumbers, List<int> inserted, List<int> removed)
    {
        _newNumbers = newNumbers;
        _inserted = inserted;
        _removed = removed;
    }

    /// <summary>
    /// The alignment, or null unless the page count changed and every page of the shorter document lines up with one of
    /// the longer: a mix of inserted, removed and rewritten pages is left to the line-by-line comparison.
    /// </summary>
    public static PageAlignment? Find(Dictionary<string, List<SnapshotLine>> before,
        Dictionary<string, List<SnapshotLine>> after)
    {
        var (oldCount, newCount) = (PageCount(before), PageCount(after));
        if (oldCount == newCount) return null;

        var pairs = Align(WordsByPage(before, oldCount), WordsByPage(after, newCount));
        var onlyInserted = pairs.Count == oldCount;
        var onlyRemoved = pairs.Count == newCount;
        if (!onlyInserted && !onlyRemoved || pairs.All(pair => pair.Old == pair.New)) return null;

        var inserted = Enumerable.Range(1, newCount).Except(pairs.Select(pair => pair.New)).ToList();
        var removed = Enumerable.Range(1, oldCount).Except(pairs.Select(pair => pair.Old)).ToList();
        return new PageAlignment(pairs.ToDictionary(pair => pair.Old, pair => pair.New), inserted, removed);
    }

    /// <summary>The one change that explains the shift, such as "p2 inserted; p2-10 -> p3-11".</summary>
    public SnapshotChange Change()
    {
        var parts = new List<string>();
        if (_inserted.Count > 0) parts.Add($"{Ranges(_inserted)} inserted");
        if (_removed.Count > 0) parts.Add($"{Ranges(_removed)} removed");
        parts.AddRange(Shifts().Select(shift => $"{shift.From} -> {shift.To}"));
        var page = _inserted.Concat(_removed).Min();
        return new SnapshotChange(ChangeKind.Changed, Section.Pages, "pages", page, string.Join("; ", parts));
    }

    /// <summary>
    /// The snapshot with each page word (p3, p2-4) renumbered to the page it lines up with. Text in quotes, the header
    /// lines and a removed page's number stay as they are.
    /// </summary>
    public string Renumber(string snapshot) => string.Join('\n', snapshot.Split('\n').Select(line =>
    {
        if (line.StartsWith('#')) return line;
        var tokens = SnapshotLine.Tokens(line.Trim()).ToList();
        var renumbered = tokens.ConvertAll(RenumberWord);
        if (renumbered.SequenceEqual(tokens)) return line;

        // Only a line with a page to renumber is rebuilt, so every other line keeps its spacing.
        var indent = line.Length - line.TrimStart().Length;
        return line[..indent] + string.Join(' ', renumbered);
    }));

    private string RenumberWord(string word)
    {
        if (PageRange(word) is not (var first, var last)) return word;
        var (from, to) = (NewNumber(first), NewNumber(last ?? first));
        return from == to ? $"p{from}" : $"p{from}-{to}";
    }

    private int NewNumber(int page) => _newNumbers.GetValueOrDefault(page, page);

    // A word such as p3 or p2-4: its first page and, for a range, its last.
    private static (int First, int? Last)? PageRange(string word)
    {
        if (word.Length < 2 || word[0] != 'p') return null;
        var parts = word[1..].Split('-');
        if (parts.Length > 2 || parts.Any(part => part.Length == 0 || !part.All(char.IsAsciiDigit))) return null;
        return (int.Parse(parts[0]), parts.Length == 2 ? int.Parse(parts[1]) : null);
    }

    // The runs of pages that moved by the same amount, as "p2-10" -> "p3-11".
    private IEnumerable<(string From, string To)> Shifts()
    {
        var moved = _newNumbers.Where(pair => pair.Key != pair.Value).OrderBy(pair => pair.Key).ToList();
        for (var i = 0; i < moved.Count; i++)
        {
            var start = i;
            var offset = moved[i].Value - moved[i].Key;
            while (i + 1 < moved.Count && moved[i + 1].Key == moved[i].Key + 1
                && moved[i + 1].Value == moved[i + 1].Key + offset)
            {
                i++;
            }

            yield return (Range(moved[start].Key, moved[i].Key), Range(moved[start].Value, moved[i].Value));
        }
    }

    private static string Ranges(List<int> pages)
    {
        var ranges = new List<string>();
        for (var i = 0; i < pages.Count; i++)
        {
            var start = i;
            while (i + 1 < pages.Count && pages[i + 1] == pages[i] + 1) i++;
            ranges.Add(Range(pages[start], pages[i]));
        }

        return string.Join(", ", ranges);
    }

    private static string Range(int first, int last) => first == last ? $"p{first}" : $"p{first}-{last}";

    // The document line's pages= when the document section is there, else the highest page any line names.
    private static int PageCount(Dictionary<string, List<SnapshotLine>> sections)
    {
        var document = sections.GetValueOrDefault(SnapshotWriter.Name(Section.Document)) ?? [];
        var count = document.SelectMany(line => line.Attributes).Where(pair => pair.Key == "pages")
            .Select(pair => int.TryParse(pair.Value, out var pages) ? pages : 0).FirstOrDefault();
        return count > 0 ? count : AllLines(sections).Select(line => line.Page ?? 0).DefaultIfEmpty().Max();
    }

    // The words of the text on each page, from every line that names one; index 0 is unused.
    private static HashSet<string>[] WordsByPage(Dictionary<string, List<SnapshotLine>> sections, int count)
    {
        var words = Enumerable.Range(0, count + 1).Select(_ => new HashSet<string>()).ToArray();
        foreach (var line in AllLines(sections))
        {
            if (line.Page is int page && page >= 1 && page <= count) words[page].UnionWith(line.TextWords);
        }

        return words;
    }

    private static IEnumerable<SnapshotLine> AllLines(Dictionary<string, List<SnapshotLine>> sections)
    {
        IEnumerable<SnapshotLine> Flatten(SnapshotLine line) => line.Children.SelectMany(Flatten).Prepend(line);
        return sections.Values.SelectMany(lines => lines.SelectMany(Flatten));
    }

    // Identical pages (blank ones too) line up first; between them, pages with mostly the same words pair in order.
    private static List<(int Old, int New)> Align(HashSet<string>[] before, HashSet<string>[] after)
    {
        static string Key(HashSet<string> words) => string.Join(' ', words.Order(StringComparer.Ordinal));

        var steps = LineDiff.Compare(before.Skip(1).Select(Key).ToList(), after.Skip(1).Select(Key).ToList());
        var pairs = new List<(int Old, int New)>();
        var (removed, added) = (new List<int>(), new List<int>());
        var (oldPage, newPage) = (1, 1);
        foreach (var step in steps.Append(new LineChange(' ', "")))
        {
            if (step.Kind == '-') removed.Add(oldPage++);
            else if (step.Kind == '+') added.Add(newPage++);
            else
            {
                var next = 0;
                foreach (var old in removed)
                {
                    var match = added.FindIndex(next, page => IsSamePage(before[old], after[page]));
                    if (match < 0) continue;
                    pairs.Add((old, added[match]));
                    next = match + 1;
                }

                pairs.Add((oldPage++, newPage++));
                removed.Clear();
                added.Clear();
            }
        }

        pairs.RemoveAt(pairs.Count - 1);
        return pairs;
    }

    private static bool IsSamePage(HashSet<string> first, HashSet<string> second) =>
        first.Count > 0 && second.Count > 0 && TreeMatcher.Overlap(first, second) >= TreeMatcher.SameLineSimilarity;
}
