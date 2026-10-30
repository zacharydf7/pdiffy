using System.Net;
using System.Text;
using PDiffy.Diffing;

namespace PDiffy.Cli;

/// <summary>
/// One file in a summary: its status, its name as shown, and its changes, or why it couldn't be read.
/// <see cref="Report"/> is the report its name links to, if there is one.
/// </summary>
internal sealed record SummaryRow(string Status, string Name, SnapshotDiff? Diff, string? Problem = null,
    string? Report = null);

/// <summary>
/// A diff's counts as the summaries print them: by kind, by section, and how many can affect accessibility.
/// </summary>
internal static class ChangeSummary
{
    /// <summary>Such as "3: 1 moved, 2 text".</summary>
    public static string Changes(SnapshotDiff diff) => $"{diff.Changes.Count}: {string.Join(", ", diff.CountsByKind())}";

    /// <summary>Such as "structure 2, layout 1", or nothing for text that isn't a sectioned snapshot.</summary>
    public static string Sections(SnapshotDiff diff) => diff.IsSectioned ? string.Join(", ", diff.CountsBySection()) : "";

    public static int Accessibility(SnapshotDiff diff) =>
        diff.Changes.Count(change => change.AffectsAccessibility == true);

    /// <summary>
    /// Such as "changed guide.pdf: 3: 1 moved, 2 text; by section: structure 3; 1 can affect accessibility", or
    /// "unreadable broken.pdf: " and the problem.
    /// </summary>
    public static string TextLine(SummaryRow row)
    {
        var line = $"{row.Status} {row.Name}";
        if (row.Problem is not null) line += $": {row.Problem}";
        if (row.Diff is not { } diff) return line;

        line += $": {Changes(diff)}";
        if (Sections(diff) is { Length: > 0 } sections) line += $"; by section: {sections}";
        if (Accessibility(diff) is > 0 and var count) line += $"; {count} can affect accessibility";
        return line;
    }

    /// <summary>
    /// A page with the headline and a table of the rows, each name linked to its report when it has one.
    /// </summary>
    public static string Page(string title, string headline, string nameHeader, IReadOnlyList<SummaryRow> rows,
        string folder)
    {
        var html = new StringBuilder(PageHead(title)).Append($"<h1>{Encode(headline)}</h1>\n");
        if (rows.Count == 0) return html.Append("</body></html>\n").ToString();

        html.Append($"<table><tr><th>{nameHeader}</th><th>Status</th><th>Changes</th><th>By section</th>")
            .Append("<th>Can affect accessibility</th></tr>\n");
        foreach (var row in rows)
        {
            var name = row.Report is null
                ? Encode(row.Name)
                : $"<a href=\"{Link(folder, row.Report)}\">{Encode(row.Name)}</a>";
            var changes = row.Diff is null ? row.Problem ?? "" : Changes(row.Diff);
            var sections = row.Diff is null ? "" : Sections(row.Diff);
            var accessibility = row.Diff is null ? "" : Accessibility(row.Diff).ToString();
            html.Append($"<tr><td>{name}</td><td>{row.Status}</td><td>{Encode(changes)}</td>")
                .Append($"<td>{Encode(sections)}</td><td>{accessibility}</td></tr>\n");
        }

        return html.Append("</table>\n</body></html>\n").ToString();
    }

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    // The start of a summary page, up to and including the body tag, in the reports' style.
    internal static string PageHead(string title) => $$"""
        <!doctype html>
        <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{{title}}</title>
        <style>
        {{DiffReport.BaseStyle}}
        table { border-collapse: collapse; }
        th, td { text-align: left; padding: 4px 8px; border-top: 1px solid var(--line); vertical-align: top; }
        th { color: var(--muted); font-weight: 600; }
        </style></head><body>
        """;

    /// <summary>
    /// A link from a page saved in <paramref name="folder"/> to <paramref name="file"/>; a file on another drive gets a
    /// file:// link.
    /// </summary>
    private static string Link(string folder, string file)
    {
        var relative = Path.GetRelativePath(folder, file);
        if (Path.IsPathRooted(relative)) return new Uri(file).AbsoluteUri;
        return string.Join('/', relative.Split(Path.DirectorySeparatorChar).Select(Uri.EscapeDataString));
    }
}
