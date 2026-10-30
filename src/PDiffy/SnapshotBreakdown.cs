using System.Globalization;
using PDiffy.Diffing;

namespace PDiffy;

/// <summary>
/// How big a new PDF or DOCX snapshot is, section by section, so a reviewer knows what they're about to accept:
/// <c>4 pages; 1,362 lines: layout 1,240 (91%), structure 58, fonts 3</c>.
/// </summary>
internal static class SnapshotBreakdown
{
    public static string? Of(string snapshot)
    {
        if (!SnapshotTree.IsSectioned(snapshot)) return null;

        var sections = SnapshotTree.Parse(snapshot);
        var counts = sections.ToDictionary(section => section.Key, section => section.Value.Sum(Count));
        var total = counts.Values.Sum();
        if (total == 0) return null;

        var layout = SnapshotWriter.Name(Section.Layout);
        var parts = counts.Where(pair => pair.Value > 0).OrderByDescending(pair => pair.Value).Select(pair =>
            $"{pair.Key} {Number(pair.Value)}" + (pair.Key == layout ? $" ({pair.Value * 100 / total}%)" : ""));
        var pageCount = PageAlignment.PageCount(sections);
        var pages = pageCount > 0 ? $"{pageCount} page{(pageCount == 1 ? "" : "s")}; " : "";
        var breakdown = $"{pages}{Number(total)} lines: {string.Join(", ", parts)}.";

        // Layout is most of a PDF snapshot with nothing left out, which makes it easy to accept without reading the rest.
        // A snapshot that already leaves something out lists it on a "# ignored" line, and has chosen what to compare.
        var leavesOut = snapshot.Split('\n').Any(line => line.StartsWith("# ignored", StringComparison.Ordinal));
        if (!leavesOut && counts.GetValueOrDefault(layout) * 2 >= total)
        {
            breakdown += Environment.NewLine + "Most of it is layout: where things are drawn and how they look. If the "
                + "test guards the content or its accessibility, Use(SnapshotProfile.Content) leaves layout out, and "
                + "Use(SnapshotProfile.Accessibility) keeps only what assistive technology uses.";
        }

        return breakdown;
    }

    // A section with nothing in it, or left out, prints a placeholder line, which isn't content.
    private static int Count(SnapshotLine line) =>
        (line.Text.Trim() is SnapshotWriter.None or SnapshotWriter.Ignored ? 0 : 1) + line.Children.Sum(Count);

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
