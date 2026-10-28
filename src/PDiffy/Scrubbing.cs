using System.Text.RegularExpressions;

namespace PDiffy;

/// <summary>A pattern and its replacement. A numbered scrubber uses the replacement as a token name: Date_1, Date_2.</summary>
internal sealed record Scrubber(Regex Pattern, string Replacement, bool Numbered)
{
    private const string Month = @"(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?";
    private const string Time = @"([ T]\d{1,2}:\d{2}(:\d{2}(\.\d+)?)?( ?[AaPp][Mm])?(Z|[+-]\d{2}:?\d{2})?)?";

    // 2026-10-27, 10/27/2026, October 27, 2026 and 27 Oct 2026, each with an optional time; and PDF dates (D:20261027...).
    public static readonly Scrubber Dates = new(new Regex(
        @"\b(\d{4}-\d{2}-\d{2}" + Time + @"|\d{1,2}/\d{1,2}/\d{2,4}" + Time + "|" + Month + @" \d{1,2},? \d{4}" + Time
        + @"|\d{1,2} " + Month + @" \d{4}" + Time + @"|D:\d{8,14}(Z|[+-]\d{2}'?\d{2}'?)?)(?!\d)", RegexOptions.IgnoreCase),
        "Date", Numbered: true);

    public static readonly Scrubber Guids = new(
        new Regex(@"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", RegexOptions.IgnoreCase),
        "Guid", Numbered: true);
}

/// <summary>Applies scrubbers to text, keeping numbered tokens consistent across every call on one instance.</summary>
internal sealed class Scrubbing(IReadOnlyList<Scrubber> scrubbers)
{
    private readonly Dictionary<string, Dictionary<string, string>> _tokens = [];

    public string Scrub(string text)
    {
        foreach (var scrubber in scrubbers)
        {
            text = scrubber.Numbered
                ? scrubber.Pattern.Replace(text, match => Token(scrubber.Replacement, match.Value))
                : scrubber.Pattern.Replace(text, scrubber.Replacement);
        }

        return text;
    }

    // The same value always gets the same token: the first date in the snapshot is Date_1 wherever it appears.
    private string Token(string name, string value)
    {
        if (!_tokens.TryGetValue(name, out var seen)) _tokens[name] = seen = [];
        if (!seen.TryGetValue(value, out var token)) seen[value] = token = $"{name}_{seen.Count + 1}";
        return token;
    }
}
