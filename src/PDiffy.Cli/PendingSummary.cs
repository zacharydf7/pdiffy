using System.Text;

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

    public string ToText() =>
        string.Concat(_rows.Select(row => ChangeSummary.TextLine(row.Summary) + "\n").Prepend(Headline + "\n"));

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
    public string ToHtml(string folder) =>
        ChangeSummary.Page("Received snapshots", Headline, "Snapshot", _rows.ConvertAll(row => row.Summary), folder);

    // A new snapshot has no verified file, so it has nothing to compare with.
    private static Row RowOf(PendingSnapshot snapshot)
    {
        var name = Path.GetRelativePath(Directory.GetCurrentDirectory(), snapshot.Received);
        if (!File.Exists(snapshot.Verified)) return new Row(snapshot, name, null);

        var diff = SnapshotDiff.Compare(File.ReadAllText(snapshot.Verified), File.ReadAllText(snapshot.Received));
        return new Row(snapshot, name, diff);
    }

    private sealed record Row(PendingSnapshot Snapshot, string Name, SnapshotDiff? Diff)
    {
        public string Status => Diff is null ? "new" : "changed";

        public SummaryRow Summary =>
            new(Status, Name, Diff, Report: File.Exists(Snapshot.Report) ? Snapshot.Report : null);

        public string Changes => Diff is null ? "" : ChangeSummary.Changes(Diff);

        public string Sections => Diff is null ? "" : ChangeSummary.Sections(Diff);

        public int Accessibility => Diff is null ? 0 : ChangeSummary.Accessibility(Diff);
    }
}
