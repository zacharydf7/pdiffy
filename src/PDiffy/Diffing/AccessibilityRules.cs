using System.Globalization;

namespace PDiffy.Diffing;

/// <summary>The rules behind <see cref="SnapshotChange.AffectsAccessibility"/>; the README lists them.</summary>
internal static class AccessibilityRules
{
    // Keys whose value going missing or empty takes something away from a screen reader.
    private static readonly HashSet<string> RemovableKeys = ["alt", "actual", "expansion", "scope", "headers", "tu"];

    public static bool Affects(SnapshotChange change)
    {
        // Only PDF changes have a section; DOCX keys can mean other things, such as a section's headers.
        if (change.Section is not { } section) return false;
        var (before, after) = Sides(change.Detail);
        if (section == Section.Untagged) return AddsUntagged(change.Kind, change.Detail, before, after);

        return change.Kind switch
        {
            ChangeKind.Moved => section == Section.Structure,
            ChangeKind.Changed => IsTypeChange(before, after) || HidesTagged(section, Role(before), Role(after)),
            ChangeKind.AttributeChanged => KeyOf(before) is var key
                && (key == "lang" || TakesAway(key, after) || Loses(section, key, ValueOf(before), after)),
            _ => false,
        };
    }

    // A value gone or emptied, or a cell's headers pointing at a header that isn't there ("?id").
    private static bool TakesAway(string key, string after) =>
        RemovableKeys.Contains(key) && IsGone(after) || key == "headers" && after.Contains('?');

    private static bool IsGone(string value) => value is "(missing)" or "\"\"";

    // Tagged content that a screen reader no longer reaches: an annotation that lost its tag, or text, an image or a
    // path now marked as an artifact (decoration) or left untagged.
    private static bool HidesTagged(Section section, string? before, string? after) => before == "tagged" && section switch
    {
        Section.Annotations => after == "untagged",
        Section.Layout => after is "artifact" or "untagged",
        _ => false,
    };

    // Keys whose change takes something away, matched by section since some names repeat elsewhere.
    private static bool Loses(Section section, string key, string from, string to) => (section, key) switch
    {
        (Section.Document, "marked" or "display-doc-title") => from == "yes" && to != "yes",
        (Section.Metadata, "title" or "xmp-title") => IsGone(to),
        (Section.Pages, "tabs") => from == "S" && to != "S",
        (Section.Fonts, "to-unicode") => from == "yes" && to != "yes",
        (Section.Fonts, "unmapped") => AddsCodes(from, to),
        (Section.Settings, "permissions") => AllowsExtraction(from) && !AllowsExtraction(to),
        _ => false,
    };

    // Only an encrypted document lists permissions; one that isn't encrypted allows everything.
    private static bool AllowsExtraction(string permissions) =>
        permissions == "(missing)" || permissions.Split(',').Contains("extract-for-accessibility");

    // Character codes that no longer map to Unicode, written as "[41-43,50]": more of them is worse, fewer isn't. Runs of
    // codes are written as one range, so a range is old news only when a single earlier range holds all of it.
    private static bool AddsCodes(string from, string to)
    {
        var known = CodeRanges(from).ToList();
        return CodeRanges(to).Any(range => !known.Exists(old => old.First <= range.First && range.Last <= old.Last));
    }

    private static IEnumerable<(int First, int Last)> CodeRanges(string value)
    {
        if (!value.StartsWith('[')) yield break;
        foreach (var range in value.Trim('[', ']').Split(','))
        {
            var ends = range.Split('-');
            if (int.TryParse(ends[0], NumberStyles.HexNumber, null, out var first)
                && int.TryParse(ends[^1], NumberStyles.HexNumber, null, out var last))
            {
                yield return (first, last);
            }
        }
    }

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
