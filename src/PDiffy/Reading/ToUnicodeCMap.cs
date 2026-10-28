using System.Text;
using System.Text.RegularExpressions;

namespace PDiffy.Reading;

/// <summary>
/// The character codes a ToUnicode CMap maps. Parsing is forgiving: a map cut off part way still counts the entries
/// before the cut, and the codes after it come out unmapped, which is what a reader sees.
/// </summary>
internal sealed class ToUnicodeCMap
{
    private static readonly Regex Block = new(@"begin(bfchar|bfrange)(.*?)(end\1|\z)", RegexOptions.Singleline);
    private static readonly Regex Token = new(@"<([0-9A-Fa-f\s]*)>|\[|\]");

    private readonly HashSet<long> _codes = [];
    private readonly List<(long First, long Last)> _ranges = [];

    public bool Maps(int code) => _codes.Contains(code) || _ranges.Any(range => code >= range.First && code <= range.Last);

    public static ToUnicodeCMap Parse(byte[]? data)
    {
        var map = new ToUnicodeCMap();
        if (data is null) return map;

        foreach (Match block in Block.Matches(Encoding.Latin1.GetString(data)))
        {
            var tokens = Token.Matches(block.Groups[2].Value).Select(match => match.Value).ToList();
            if (block.Groups[1].Value == "bfchar") map.ReadChars(tokens);
            else map.ReadRanges(tokens);
        }

        return map;
    }

    // Pairs of <code> <unicode>.
    private void ReadChars(List<string> tokens)
    {
        for (var i = 0; i + 1 < tokens.Count; i += 2)
        {
            if (Hex(tokens[i]) is long code && MapsToText(tokens[i + 1])) _codes.Add(code);
        }
    }

    // <first> <last> <unicode of first>, or <first> <last> [<unicode> <unicode> ...] with one entry per code.
    private void ReadRanges(List<string> tokens)
    {
        var i = 0;
        while (i + 2 < tokens.Count)
        {
            var first = Hex(tokens[i]);
            var last = Hex(tokens[i + 1]);
            if (tokens[i + 2] == "[")
            {
                i += 3;
                for (var code = first; i < tokens.Count && tokens[i] != "]"; i++, code++)
                {
                    if (code is long value && MapsToText(tokens[i])) _codes.Add(value);
                }

                i++;
                continue;
            }

            if (first is long from && last is long to && MapsToText(tokens[i + 2])) _ranges.Add((from, to));
            i += 3;
        }
    }

    // A destination of nothing, or of only U+0000, gives a reader no text.
    private static bool MapsToText(string token) =>
        token.StartsWith('<') && token.Trim('<', '>').Any(c => c is not ('0' or ' '));

    private static long? Hex(string token)
    {
        var digits = string.Concat(token.Trim('<', '>').Where(char.IsAsciiHexDigit));
        return digits.Length is > 0 and <= 8 ? Convert.ToInt64(digits, 16) : null;
    }
}
