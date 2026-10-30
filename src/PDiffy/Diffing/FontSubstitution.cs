namespace PDiffy.Diffing;

/// <summary>
/// Spots the usual reason a PDF snapshot changes everywhere at once on a build agent: the generator couldn't find a
/// font there and used another, so one font is removed, another added, and the layout lines switch from one to the
/// other and shift a little.
/// </summary>
internal static class FontSubstitution
{
    // Most of the other changes must be the switch itself or the positions it moves, so a real edit that also changes a
    // font, such as a new font size, isn't explained away.
    private const double ShareThatMustBeFontOrPosition = 0.8;

    public static string? Hint(SnapshotDiff diff)
    {
        var fonts = diff.Changes.Where(change => change.Section == Section.Fonts).ToList();
        var removed = fonts.Where(change => change.Kind == ChangeKind.Removed).ToList();
        var added = fonts.Where(change => change.Kind == ChangeKind.Added).ToList();
        if (removed.Count != 1 || added.Count != 1) return null;

        var (from, to) = (FontName(removed[0]), FontName(added[0]));
        var others = diff.Changes.Where(change => change.Section != Section.Fonts).ToList();
        var switched = others.Count(change => change.Detail == $"font: {from} -> {to}");
        var explained = others.Count(change => change.Detail == $"font: {from} -> {to}" || IsPositionChange(change));
        if (switched == 0 || explained < others.Count * ShareThatMustBeFontOrPosition) return null;

        return $"{from} was replaced by {to} in {switched} line(s): the machine running this test probably doesn't have "
            + $"{from}. Embed the font in the PDF, or install it on the build agent.";
    }

    // A font's path is "fonts > Arial".
    private static string FontName(SnapshotChange change) =>
        change.Path[(change.Path.LastIndexOf("> ", StringComparison.Ordinal) + 2)..];

    private static bool IsPositionChange(SnapshotChange change) =>
        change is { Section: Section.Layout, Kind: ChangeKind.AttributeChanged }
        && change.Detail.StartsWith("at: ", StringComparison.Ordinal);
}
