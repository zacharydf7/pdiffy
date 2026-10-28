using System.Text.RegularExpressions;

namespace PDiffy.Diffing;

/// <summary>Splits a snapshot into its sections, each a tree of lines built from the two-space indentation.</summary>
internal static class SnapshotTree
{
    private static readonly Regex FormatLine = new(@"^# [a-z]+-snapshot v\d+(\n|$)");

    // PDiffy's own formats start with a line such as "# pdfua-snapshot v1"; anything else is plain text.
    public static bool IsSectioned(string text) => FormatLine.IsMatch(text);

    public static Dictionary<string, List<SnapshotLine>> Parse(string snapshot)
    {
        var sections = new Dictionary<string, List<SnapshotLine>>();
        List<SnapshotLine>? roots = null;
        var open = new Stack<SnapshotLine>();
        foreach (var text in snapshot.Replace("\r\n", "\n").Split('\n'))
        {
            if (text.Length == 0 || text.StartsWith('#')) continue;

            var depth = text.TakeWhile(c => c == ' ').Count() / 2;
            if (depth == 0)
            {
                roots = [];
                sections[text] = roots;
                open.Clear();
                continue;
            }

            if (roots is null) continue;

            var line = SnapshotLine.Parse(text, depth);
            while (open.Count > 0 && open.Peek().Depth >= depth) open.Pop();
            if (open.Count == 0) roots.Add(line);
            else open.Peek().Children.Add(line);
            open.Push(line);
        }

        return sections;
    }
}
