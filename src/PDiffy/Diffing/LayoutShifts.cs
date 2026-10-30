using System.Globalization;

namespace PDiffy.Diffing;

/// <summary>
/// Spots a reflow in a list of changes: layout lines one after another on a page whose only change is the same move,
/// such as every line below an inserted paragraph moving down 16 pt. A failure summary shows each run as one change, so
/// the edit that caused it isn't buried.
/// </summary>
internal static class LayoutShifts
{
    // Two lines moving together could be a coincidence; three is a reflow.
    private const int ShortestRun = 3;

    /// <summary>
    /// The changes in order, with each run of the same move on a page as one group and every other change alone.
    /// </summary>
    public static IEnumerable<IReadOnlyList<SnapshotChange>> Group(IEnumerable<SnapshotChange> changes)
    {
        var run = new List<SnapshotChange>();
        foreach (var change in changes)
        {
            if (run.Count > 0 && Continues(run[^1], change))
            {
                run.Add(change);
                continue;
            }

            foreach (var group in Flush(run)) yield return group;
            run = [change];
        }

        foreach (var group in Flush(run)) yield return group;
    }

    /// <summary>What a run did, such as "27 lines down 16 pt" or "4 lines down 12 pt and right 2 pt".</summary>
    public static string Describe(IReadOnlyList<SnapshotChange> run)
    {
        var (x, y) = MoveOf(run[0])!.Value;
        var directions = new List<string>();
        if (y != 0) directions.Add($"{(y < 0 ? "down" : "up")} {Number(Math.Abs(y))} pt");
        if (x != 0) directions.Add($"{(x < 0 ? "left" : "right")} {Number(Math.Abs(x))} pt");
        return $"{run.Count} lines {string.Join(" and ", directions)}";
    }

    public static bool IsRun(IReadOnlyList<SnapshotChange> group) => group.Count >= ShortestRun;

    // A run too short to count is its changes one by one.
    private static IEnumerable<IReadOnlyList<SnapshotChange>> Flush(List<SnapshotChange> run) =>
        run.Count >= ShortestRun ? [run] : run.Select(change => (IReadOnlyList<SnapshotChange>)[change]);

    // The next line down the same page moved the same way, so an unchanged line in between ends the run.
    private static bool Continues(SnapshotChange last, SnapshotChange next) =>
        MoveOf(next) is { } move && move == MoveOf(last) && next.Page == last.Page
        && LineNumber(last) is int number && LineNumber(next) == number + 1;

    // Only a layout line's position counts, so a run never hides a change of color, font or text.
    private static (double X, double Y)? MoveOf(SnapshotChange change)
    {
        const string prefix = "at: ";
        if (change is not { PdfSection: PdfSection.Layout, Kind: ChangeKind.AttributeChanged }
            || !change.Detail.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        if (change.Detail[prefix.Length..].Split(" -> ") is not [var from, var to]
            || SnapshotLine.Numbers(from) is not [var x1, var y1] || SnapshotLine.Numbers(to) is not [var x2, var y2])
        {
            return null;
        }

        var move = (X: x2 - x1, Y: y2 - y1);
        return move == (0, 0) ? null : move;
    }

    // A layout line's place among the page's lines: 5 for "layout > p1[5]".
    private static int? LineNumber(SnapshotChange change)
    {
        var label = change.Path[(change.Path.LastIndexOf(' ') + 1)..];
        var open = label.IndexOf('[');
        return open >= 0 && int.TryParse(label[(open + 1)..^1], out var number) ? number : null;
    }

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);
}
