using System.Text.RegularExpressions;

namespace PDiffy.Diffing;

/// <summary>Splits a snapshot into its sections, each a tree of lines built from the two-space indentation.</summary>
internal static class SnapshotTree
{
    private static readonly Regex FormatLine = new(@"^# [a-z]+-snapshot v\d+(\n|$)");

    // PDiffy's own formats start with a line such as "# pdfua-snapshot v1"; anything else is plain text.
    public static bool IsSectioned(string text) => FormatLine.IsMatch(text);

    // Starts, when given, gets where each line starts in the snapshot, with CRLF read as LF.
    public static Dictionary<string, List<SnapshotLine>> Parse(string snapshot,
        Dictionary<SnapshotLine, int>? starts = null)
    {
        var sections = new Dictionary<string, List<SnapshotLine>>();
        List<SnapshotLine>? roots = null;
        var open = new Stack<SnapshotLine>();
        var next = 0;
        foreach (var text in snapshot.Replace("\r\n", "\n").Split('\n'))
        {
            var start = next;
            next += text.Length + 1;
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
            starts?.Add(line, start);
            while (open.Count > 0 && open.Peek().Depth >= depth) open.Pop();
            if (open.Count == 0) roots.Add(line);
            else open.Peek().Children.Add(line);
            open.Push(line);
        }

        return sections;
    }

    /// <summary>
    /// The path a diff gives each line, such as "body > Table > Row[2] > Cell > P", by where the line starts in a
    /// snapshot with LF line endings.
    /// </summary>
    public static Dictionary<int, string> PathsByStart(string snapshot)
    {
        var starts = new Dictionary<SnapshotLine, int>();
        var paths = new Dictionary<int, string>();
        foreach (var (name, roots) in Parse(snapshot, starts)) AddPaths(roots, name, starts, paths);
        return paths;
    }

    private static void AddPaths(List<SnapshotLine> siblings, string parentPath, Dictionary<SnapshotLine, int> starts,
        Dictionary<int, string> paths)
    {
        foreach (var (line, path) in StructurePaths.WithPaths(siblings, line => line.Key, parentPath))
        {
            paths[starts[line]] = path;
            AddPaths(line.Children, path, starts, paths);
        }
    }
}
