namespace PDiffy.Diffing;

/// <summary>The fixed rules behind <see cref="SnapshotChange.AffectsAccessibility"/>; the README lists them.</summary>
internal static class AccessibilityRules
{
    // Keys whose value going missing or empty takes something away from a screen reader.
    private static readonly HashSet<string> RemovableKeys = ["alt", "scope", "headers", "tu"];

    public static bool Affects(SnapshotChange change)
    {
        // Only PDF changes have a section; DOCX keys can mean other things, such as a section's headers.
        if (change.Section is null) return false;
        var (before, after) = Sides(change.Detail);
        if (change.Section == Section.Untagged) return AddsUntagged(change.Kind, change.Detail, before, after);

        return change.Kind switch
        {
            ChangeKind.Moved => change.Section == Section.Structure,
            ChangeKind.Changed => IsTypeChange(before, after)
                || change.Section == Section.Annotations && Role(before) == "tagged" && Role(after) == "untagged",
            ChangeKind.AttributeChanged => KeyOf(before) is var key && (key == "lang" || TakesAway(key, after)),
            _ => false,
        };
    }

    // A value gone or emptied, or a cell's headers pointing at a header that isn't there ("?id").
    private static bool TakesAway(string key, string after) =>
        RemovableKeys.Contains(key) && after is "(missing)" or "\"\"" || key == "headers" && after.Contains('?');

    // Something new a screen reader can't reach: a line, words added to one, or more images or paths. Less isn't.
    private static bool AddsUntagged(ChangeKind kind, string detail, string before, string after) => kind switch
    {
        ChangeKind.Added => true,
        ChangeKind.TextChanged => detail.Contains("{+", StringComparison.Ordinal),
        ChangeKind.AttributeChanged => int.TryParse(ValueOf(before), out var from) && int.TryParse(after, out var to)
            && to > from,
        _ => false,
    };

    // A header cell's generated name (TH1, TH2) can change alone; only headings, mapped or not, end in a number.
    private static bool IsTypeChange(string before, string after)
    {
        if (!before.StartsWith("type: ", StringComparison.Ordinal)) return false;
        var (from, to) = (before["type: ".Length..].TrimEnd(Digits), after.TrimEnd(Digits));
        return from != to || from == "H" || from.EndsWith("->H", StringComparison.Ordinal);
    }

    private static readonly char[] Digits = "0123456789".ToCharArray();

    // An attribute change's before side reads "key: value".
    private static string KeyOf(string before) => before[..Math.Max(0, before.IndexOf(": ", StringComparison.Ordinal))];

    private static string ValueOf(string before) => before[(before.IndexOf(": ", StringComparison.Ordinal) + 2)..];

    // An annotation line reads "p1 Link tagged ...": its third word is its role.
    private static string? Role(string line) => SnapshotLine.Parse(line, 0).Words.ElementAtOrDefault(2);

    // "before -> after", split at the first arrow outside quoted text, since quoted text can hold arrows of its own.
    private static (string Before, string After) Sides(string detail)
    {
        var quoted = false;
        for (var i = 0; i < detail.Length; i++)
        {
            if (quoted && detail[i] == '\\') i++;
            else if (detail[i] == '"') quoted = !quoted;
            else if (!quoted && string.CompareOrdinal(detail, i, " -> ", 0, 4) == 0)
            {
                return (detail[..i], detail[(i + 4)..]);
            }
        }

        return (detail, "");
    }
}
