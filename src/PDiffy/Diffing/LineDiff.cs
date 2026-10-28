namespace PDiffy.Diffing;

/// <summary>A diff of two lists of lines (or words): which were removed from the first and added in the second.</summary>
internal static class LineDiff
{
    // Beyond this many removals and additions, the middle of the diff is reported as removed and added without matching,
    // to keep time and memory bounded. Lists that differ that much need reading in full anyway.
    private const int MaxEdits = 2000;

    public static IReadOnlyList<LineChange> Compare(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        var start = 0;
        while (start < before.Count && start < after.Count && before[start] == after[start]) start++;
        var end = 0;
        while (end < before.Count - start && end < after.Count - start && before[^(end + 1)] == after[^(end + 1)]) end++;

        var changes = new List<LineChange>();
        for (var i = 0; i < start; i++) changes.Add(new LineChange(' ', before[i]));
        changes.AddRange(Middle(before.Skip(start).SkipLast(end).ToList(), after.Skip(start).SkipLast(end).ToList()));
        for (var i = before.Count - end; i < before.Count; i++) changes.Add(new LineChange(' ', before[i]));
        return changes;
    }

    // Myers' algorithm: finds the fewest removals and additions, in time and memory that grow with how much changed
    // rather than with the length of the lists. Round d records how far each diagonal k (x - y) gets with d edits.
    private static List<LineChange> Middle(List<string> before, List<string> after)
    {
        var offset = before.Count + after.Count + 1;
        var furthest = new int[2 * offset + 1];
        var rounds = new List<int[]>();
        for (var d = 0; d <= Math.Min(MaxEdits, before.Count + after.Count); d++)
        {
            for (var k = -d; k <= d; k += 2)
            {
                var x = k == -d || k != d && furthest[offset + k - 1] < furthest[offset + k + 1]
                    ? furthest[offset + k + 1]
                    : furthest[offset + k - 1] + 1;
                var y = x - k;
                while (x < before.Count && y < after.Count && before[x] == after[y]) (x, y) = (x + 1, y + 1);
                furthest[offset + k] = x;
                if (x >= before.Count && y >= after.Count)
                {
                    rounds.Add(furthest[(offset - d)..(offset + d + 1)]);
                    return Backtrack(before, after, rounds);
                }
            }

            rounds.Add(furthest[(offset - d)..(offset + d + 1)]);
        }

        return [.. before.Select(line => new LineChange('-', line)), .. after.Select(line => new LineChange('+', line))];
    }

    // Walks back from the end through each round's diagonals, emitting the kept, removed and added lines in reverse.
    private static List<LineChange> Backtrack(List<string> before, List<string> after, List<int[]> rounds)
    {
        var changes = new List<LineChange>();
        var (x, y) = (before.Count, after.Count);
        for (var d = rounds.Count - 1; d > 0; d--)
        {
            var previous = rounds[d - 1];
            var k = x - y;
            var fromAbove = k == -d || k != d && previous[k - 1 + d - 1] < previous[k + 1 + d - 1];
            var previousK = fromAbove ? k + 1 : k - 1;
            var previousX = previous[previousK + d - 1];
            var previousY = previousX - previousK;
            while (x > previousX && y > previousY)
            {
                (x, y) = (x - 1, y - 1);
                changes.Add(new LineChange(' ', before[x]));
            }

            if (fromAbove) changes.Add(new LineChange('+', after[--y]));
            else changes.Add(new LineChange('-', before[--x]));
        }

        while (x > 0 && y > 0)
        {
            (x, y) = (x - 1, y - 1);
            changes.Add(new LineChange(' ', before[x]));
        }

        changes.Reverse();
        return changes;
    }
}

/// <summary>A line and whether it was kept (' '), removed ('-') or added ('+').</summary>
internal sealed record LineChange(char Kind, string Line);
