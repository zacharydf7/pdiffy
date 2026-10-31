namespace PDiffy.Diffing;

/// <summary>
/// Compares two checks sections as lists of findings: a finding only the later snapshot has is a new problem, and one
/// only the earlier snapshot has is fixed. Findings are never paired as edits, since a finding on another element is
/// a different problem.
/// </summary>
internal static class CheckChanges
{
    // Originals maps the earlier snapshot's lines, renumbered when pages were inserted or removed, back to the lines as
    // written, so a fixed problem keeps the page it was on.
    public static IEnumerable<SnapshotChange> Compare(List<SnapshotLine> before, List<SnapshotLine> after,
        Dictionary<SnapshotLine, SnapshotLine>? originals, SnapshotFormat format)
    {
        var beforeFindings = Findings(before);
        var afterFindings = Findings(after);
        foreach (var line in Without(beforeFindings, afterFindings))
        {
            yield return Change(ChangeKind.Removed, (originals?.GetValueOrDefault(line) ?? line).Text.Trim(), format);
        }

        foreach (var line in Without(afterFindings, beforeFindings))
        {
            yield return Change(ChangeKind.Added, line.Text.Trim(), format);
        }
    }

    // The same finding can appear more than once, such as two links without a description on one page, so each copy
    // counts on its own.
    private static List<SnapshotLine> Without(List<SnapshotLine> findings, List<SnapshotLine> others)
    {
        var left = others.Select(line => line.Text).ToList();
        return findings.Where(line => !left.Remove(line.Text)).ToList();
    }

    private static List<SnapshotLine> Findings(List<SnapshotLine> lines) =>
        lines.Where(line => line.Text.Trim() is not (SnapshotWriter.None or SnapshotWriter.Ignored)).ToList();

    // A line is the rule, the page when there is one, and the path: "FigureAltText p2 structure > Document > Figure".
    // A Word finding has no page: "ImageAltText body > P > image".
    private static SnapshotChange Change(ChangeKind kind, string finding, SnapshotFormat format)
    {
        var words = finding.Split(' ', 3);
        var page = words.Length == 3 && words[1] is ['p', .. var digits] && int.TryParse(digits, out var number)
            ? number : (int?)null;
        var path = page is not null ? words[2] : words.Length > 1 ? finding[(words[0].Length + 1)..] : "";
        return new SnapshotChange
        {
            Kind = kind, SectionName = SnapshotWriter.Name(PdfSection.Checks), Path = words[0], Page = page,
            Detail = path, Format = format,
        };
    }
}
