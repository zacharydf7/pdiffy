using System.Net;
using System.Text;
using PDiffy.Diffing;

namespace PDiffy.Cli;

/// <summary>
/// One row per received snapshot: whether it's new or changed, and its changes by kind and by section, as text, as
/// Markdown for a CI job summary, or as an HTML page that links to each report.
/// </summary>
internal sealed class PendingSummary
{
    private readonly List<Row> _rows;

    public PendingSummary(IEnumerable<PendingSnapshot> pending) => _rows = pending.Select(RowOf).ToList();

    public string Headline => _rows.Count == 0 ? "No received snapshots."
        : $"{_rows.Count} received snapshot(s): {_rows.Count(row => row.Diff is not null)} changed, "
            + $"{_rows.Count(row => row.Diff is null)} new";

    public string ToText()
    {
        var text = new StringBuilder(Headline).Append('\n');
        foreach (var row in _rows)
        {
            text.Append($"{row.Status} {row.Name}");
            if (row.Diff is not null) text.Append($": {row.Changes}");
            if (row.Sections.Length > 0) text.Append($"; by section: {row.Sections}");
            if (row.Accessibility > 0) text.Append($"; {row.Accessibility} can affect accessibility");
            text.Append('\n');
        }

        return text.ToString();
    }

    public string ToMarkdown()
    {
        var text = new StringBuilder($"### PDiffy: {Headline}\n");
        if (_rows.Count == 0) return text.ToString();

        text.Append("\n| Snapshot | Status | Changes | By section | Can affect accessibility |\n")
            .Append("|---|---|---|---|---|\n");
        foreach (var row in _rows)
        {
            text.Append($"| `{row.Name.Replace("|", "\\|")}` | {row.Status} | {row.Changes} | {row.Sections} | ")
                .Append(row.Diff is null ? "" : row.Accessibility.ToString()).Append(" |\n");
        }

        return text.ToString();
    }

    /// <summary>The page links each report relative to <paramref name="folder"/>, where the page is saved.</summary>
    public string ToHtml(string folder)
    {
        var html = new StringBuilder(Head).Append($"<h1>{Encode(Headline)}</h1>\n");
        if (_rows.Count > 0)
        {
            html.Append("<table><tr><th>Snapshot</th><th>Status</th><th>Changes</th><th>By section</th>")
                .Append("<th>Can affect accessibility</th></tr>\n");
        }

        foreach (var row in _rows)
        {
            var name = File.Exists(row.Snapshot.Report)
                ? $"<a href=\"{Link(folder, row.Snapshot.Report)}\">{Encode(row.Name)}</a>"
                : Encode(row.Name);
            html.Append($"<tr><td>{name}</td><td>{row.Status}</td><td>{Encode(row.Changes)}</td>")
                .Append($"<td>{Encode(row.Sections)}</td><td>{(row.Diff is null ? "" : row.Accessibility)}</td></tr>\n");
        }

        if (_rows.Count > 0) html.Append("</table>\n");
        return html.Append("</body></html>\n").ToString();
    }

    // A new snapshot has no verified file, so it has nothing to compare with.
    private static Row RowOf(PendingSnapshot snapshot)
    {
        var name = Path.GetRelativePath(Directory.GetCurrentDirectory(), snapshot.Received);
        if (!File.Exists(snapshot.Verified)) return new Row(snapshot, name, null);

        var diff = SnapshotDiff.Compare(File.ReadAllText(snapshot.Verified), File.ReadAllText(snapshot.Received));
        return new Row(snapshot, name, diff);
    }

    // A report on another drive has no relative path, so it gets a file:// link.
    private static string Link(string folder, string file)
    {
        var relative = Path.GetRelativePath(folder, file);
        if (Path.IsPathRooted(relative)) return new Uri(file).AbsoluteUri;
        return string.Join('/', relative.Split(Path.DirectorySeparatorChar).Select(Uri.EscapeDataString));
    }

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    private sealed record Row(PendingSnapshot Snapshot, string Name, SnapshotDiff? Diff)
    {
        public string Status => Diff is null ? "new" : "changed";

        public string Changes => Diff is null ? "" : $"{Diff.Changes.Count}: {string.Join(", ", Diff.CountsByKind())}";

        public string Sections => Diff is { IsSectioned: true } ? string.Join(", ", Diff.CountsBySection()) : "";

        public int Accessibility => Diff?.Changes.Count(change => change.AffectsAccessibility) ?? 0;
    }

    private const string Head = $$"""
        <!doctype html>
        <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Received snapshots</title>
        <style>
        {{DiffReport.BaseStyle}}
        table { border-collapse: collapse; }
        th, td { text-align: left; padding: 4px 8px; border-top: 1px solid var(--line); vertical-align: top; }
        th { color: var(--muted); font-weight: 600; }
        </style></head><body>
        """;
}
