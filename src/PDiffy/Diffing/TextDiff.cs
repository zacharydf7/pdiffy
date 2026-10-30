namespace PDiffy.Diffing;

/// <summary>
/// A line-by-line diff for text without PDiffy's sections, such as JSON or CSV. Each changed line is one change,
/// located by its line number: in the new text, or in the old one for a removed line.
/// </summary>
internal static class TextDiff
{
    public static List<SnapshotChange> Compare(string before, string after)
    {
        var changes = new List<SnapshotChange>();
        var removed = new List<(int Number, string Text)>();
        var added = new List<(int Number, string Text)>();
        var (b, a) = (0, 0);
        foreach (var step in LineDiff.Compare(before.Split('\n'), after.Split('\n')).Append(new LineChange(' ', "")))
        {
            if (step.Kind == '-') removed.Add((++b, step.Line));
            else if (step.Kind == '+') added.Add((++a, step.Line));
            else
            {
                AddGap(changes, removed, added);
                removed.Clear();
                added.Clear();
                (b, a) = (b + 1, a + 1);
            }
        }

        return changes;
    }

    // Between unchanged lines, removed and added lines pair up in order as edited lines; the rest were removed or added.
    private static void AddGap(List<SnapshotChange> changes, List<(int Number, string Text)> removed,
        List<(int Number, string Text)> added)
    {
        var paired = Math.Min(removed.Count, added.Count);
        for (var i = 0; i < paired; i++)
        {
            changes.Add(Change(ChangeKind.TextChanged, added[i].Number, Show(removed[i].Text, added[i].Text)));
        }

        changes.AddRange(removed.Skip(paired).Select(line => Change(ChangeKind.Removed, line.Number, line.Text)));
        changes.AddRange(added.Skip(paired).Select(line => Change(ChangeKind.Added, line.Number, line.Text)));
    }

    private static SnapshotChange Change(ChangeKind kind, int line, string detail) =>
        new() { Kind = kind, SectionName = null, Path = $"line {line}", Page = null, Detail = detail };

    // A change made only of spaces has no word to mark, so both lines are shown in quotes.
    private static string Show(string before, string after)
    {
        var sameWords = before.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .SequenceEqual(after.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return sameWords ? $"- \"{before}\"\n+ \"{after}\"" : WordDiff.Show(before, after);
    }
}
