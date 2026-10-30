using System.Net;
using System.Text;

namespace PDiffy.Cli;

/// <summary>
/// How many check findings the verified snapshots hold, by rule and by snapshot, read from their checks sections, so a
/// team sees how many problems its documents have without regenerating any of them.
/// </summary>
internal sealed class FindingCounts
{
    private readonly List<Snapshot> _snapshots;
    private readonly int _withoutChecks;

    private FindingCounts(List<Snapshot> snapshots, int withoutChecks) =>
        (_snapshots, _withoutChecks) = (snapshots, withoutChecks);

    public static FindingCounts Of(IEnumerable<string> verifiedFiles) =>
        Of(verifiedFiles.Select(file => (Path.GetFullPath(file), File.ReadAllLines(file))));

    /// <summary>The counts of snapshots already read, each by its full path and its lines.</summary>
    public static FindingCounts Of(IEnumerable<(string File, string[] Lines)> verified)
    {
        var snapshots = new List<Snapshot>();
        var withoutChecks = 0;
        foreach (var (file, lines) in verified)
        {
            if (lines.FirstOrDefault()?.StartsWith(SnapshotWriter.FormatPrefix) != true) continue;

            var findings = Findings(lines);
            if (findings is null) withoutChecks++;
            else snapshots.Add(new Snapshot(file, findings));
        }

        return new FindingCounts(snapshots, withoutChecks);
    }

    /// <summary>A line printed under the headline, such as how the counts compare with an earlier commit.</summary>
    public string? Note { get; set; }

    public int Problems => _snapshots.Sum(snapshot => snapshot.Findings.Count);

    public string Headline => _snapshots.Count == 0 ? "No verified snapshots record checks."
        : $"{Problems} problem(s) in {_snapshots.Count} snapshot(s)"
            + (_withoutChecks > 0 ? $" ({_withoutChecks} snapshot(s) don't record checks)" : "");

    // A finding is fixed when only the earlier snapshot of the same file has it, and new when only the later one does,
    // so a renamed snapshot's findings count as fixed and new.
    public string ChangeSince(FindingCounts earlier, string reference)
    {
        var now = Keys().CountBy(key => key).ToDictionary();
        var then = earlier.Keys().CountBy(key => key).ToDictionary();
        var fixedOnes = then.Sum(pair => Math.Max(0, pair.Value - now.GetValueOrDefault(pair.Key)));
        var newOnes = now.Sum(pair => Math.Max(0, pair.Value - then.GetValueOrDefault(pair.Key)));
        var difference = Problems - earlier.Problems;
        var amount = difference < 0 ? $"{-difference} fewer than {reference}"
            : difference > 0 ? $"{difference} more than {reference}" : $"as many as {reference}";
        return $"{amount} ({fixedOnes} fixed, {newOnes} new)";
    }

    public string ToText()
    {
        var text = new StringBuilder($"{Headline}\n");
        if (Note is not null) text.Append($"{Note}\n");
        var byRule = ByRule().Select(pair => $"{pair.Key} {pair.Value}").ToList();
        if (byRule.Count > 0) text.Append($"by rule: {string.Join(", ", byRule)}\n");
        return text.ToString();
    }

    public string ToMarkdown()
    {
        var text = new StringBuilder($"### PDiffy: {Headline}\n");
        if (Note is not null) text.Append($"\n{Note}\n");
        if (Problems == 0) return text.ToString();

        text.Append("\n| Rule | Problems |\n|---|---|\n");
        foreach (var (rule, count) in ByRule()) text.Append($"| `{rule}` | {count} |\n");
        text.Append("\n| Snapshot | Problems |\n|---|---|\n");
        foreach (var snapshot in MostFirst())
        {
            text.Append($"| `{snapshot.Name.Replace("|", "\\|")}` | {snapshot.Findings.Count} |\n");
        }

        return text.ToString();
    }

    // Each rule links to its help page, so the page says how to fix what it counts.
    public string ToHtml()
    {
        var html = new StringBuilder(ChangeSummary.PageHead("Check findings"))
            .Append($"<h1>{WebUtility.HtmlEncode(Headline)}</h1>\n");
        if (Note is not null) html.Append($"<p>{WebUtility.HtmlEncode(Note)}</p>\n");
        if (Problems > 0)
        {
            html.Append("<h2>By rule</h2><table><thead><tr><th>Rule</th><th>Problems</th></tr></thead><tbody>");
            foreach (var (rule, count) in ByRule())
            {
                var name = WebUtility.HtmlEncode(rule);
                var cell = Enum.TryParse<PdfCheck>(rule, out var check)
                    ? $"<a href=\"{PdfCheckFinding.HelpUrlOf(check)}\">{name}</a>" : name;
                html.Append($"<tr><td>{cell}</td><td>{count}</td></tr>");
            }

            html.Append("</tbody></table><h2>By snapshot</h2><table><thead><tr><th>Snapshot</th><th>Problems</th></tr>")
                .Append("</thead><tbody>");
            foreach (var snapshot in MostFirst())
            {
                html.Append($"<tr><td>{WebUtility.HtmlEncode(snapshot.Name)}</td>")
                    .Append($"<td>{snapshot.Findings.Count}</td></tr>");
            }

            html.Append("</tbody></table>");
        }

        return html.Append("</body></html>\n").ToString();
    }

    private IEnumerable<string> Keys() =>
        _snapshots.SelectMany(snapshot => snapshot.Findings.Select(finding => $"{snapshot.File}\n{finding}"));

    // Most found first, such as "UntaggedContent 61, FigureAltText 30".
    private List<KeyValuePair<string, int>> ByRule() => _snapshots
        .SelectMany(snapshot => snapshot.Findings.Select(finding => finding.Split(' ')[0])).CountBy(rule => rule)
        .OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).ToList();

    private IEnumerable<Snapshot> MostFirst() => _snapshots.Where(snapshot => snapshot.Findings.Count > 0)
        .OrderByDescending(snapshot => snapshot.Findings.Count)
        .ThenBy(snapshot => snapshot.Name, StringComparer.Ordinal);

    // Each finding in the checks section, such as "FigureAltText p2 structure > Figure", or null when the snapshot
    // doesn't record checks: it has no check set in its header, or it ignores the section.
    private static List<string>? Findings(string[] lines)
    {
        if (!lines.Any(line => line.StartsWith(SnapshotWriter.ChecksHeader))) return null;

        var section = lines.SkipWhile(line => line != SnapshotWriter.Name(PdfSection.Checks)).Skip(1)
            .TakeWhile(line => line.StartsWith(' ')).Select(line => line.Trim()).ToList();
        if (section.Contains(SnapshotWriter.Ignored)) return null;

        return section.Where(line => line != SnapshotWriter.None).ToList();
    }

    private sealed record Snapshot(string File, List<string> Findings)
    {
        public string Name => Path.GetRelativePath(Directory.GetCurrentDirectory(), File);
    }
}
