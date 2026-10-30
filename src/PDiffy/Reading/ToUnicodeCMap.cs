using System.Text;
using System.Text.RegularExpressions;

namespace PDiffy.Reading;

/// <summary>
/// The character codes a ToUnicode CMap maps to text, and those it maps to no text: to nothing, U+0000, U+FEFF or
/// U+FFFE. A reader uses the map's value over any other, so a code mapped to no text has none. Parsing is forgiving: a
/// map cut off part way still counts the entries before the cut, and the codes after it come out unmapped, which is
/// what a reader sees.
/// </summary>
internal sealed class ToUnicodeCMap
{
    private static readonly Regex Block = new(@"begin(bfchar|bfrange)(.*?)(end\1|\z)", RegexOptions.Singleline);
    private static readonly Regex Token = new(@"<([0-9A-Fa-f\s]*)>|\[|\]");
    private static readonly long[] NotText = [0, 0xFEFF, 0xFFFE];

    private readonly HashSet<long> _codes = [];
    private readonly List<(long First, long Last)> _ranges = [];
    private readonly HashSet<long> _codesWithoutText = [];
    private readonly List<(long First, long Last)> _rangesWithoutText = [];

    public bool Maps(int code) => !GivesNoText(code)
        && (_codes.Contains(code) || _ranges.Any(range => code >= range.First && code <= range.Last));

    public bool GivesNoText(int code) => _codesWithoutText.Contains(code)
        || _rangesWithoutText.Any(range => code >= range.First && code <= range.Last);

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
            if (Hex(tokens[i]) is long code) Add(code, tokens[i + 1]);
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
                    if (code is long value) Add(value, tokens[i]);
                }

                i++;
                continue;
            }

            if (first is long from && last is long to && tokens[i + 2].StartsWith('<'))
            {
                if (Digits(tokens[i + 2]).Length == 0)
                {
                    _rangesWithoutText.Add((from, to));
                }
                else
                {
                    // Each code maps one past the code before it, so any code in the range can land on a value without
                    // text.
                    _ranges.Add((from, to));
                    var start = Hex(tokens[i + 2]) ?? long.MaxValue;
                    foreach (var value in NotText.Where(value => value >= start && value - start <= to - from))
                    {
                        _codesWithoutText.Add(from + value - start);
                    }
                }
            }

            i += 3;
        }
    }

    // A destination of nothing, or of only U+0000, U+FEFF or U+FFFE, gives a reader no text.
    private void Add(long code, string destination)
    {
        if (!destination.StartsWith('<')) return;

        var digits = Digits(destination);
        var noText = digits.All(c => c == '0') || Hex(destination) is long value && NotText.Contains(value);
        if (noText) _codesWithoutText.Add(code);
        else _codes.Add(code);
    }

    private static string Digits(string token) => string.Concat(token.Trim('<', '>').Where(char.IsAsciiHexDigit));

    private static long? Hex(string token)
    {
        var digits = Digits(token);
        return digits.Length is > 0 and <= 8 ? Convert.ToInt64(digits, 16) : null;
    }
}
