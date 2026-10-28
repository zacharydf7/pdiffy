using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;

namespace PDiffy.Reading;

internal static partial class TextJoiner
{
    // A space goes between two letters when the baseline changes or the gap is wider than 0.2 em.
    // Space glyphs in the PDF come through as letters themselves.
    public static string Join(IEnumerable<Letter> letters)
    {
        var sb = new StringBuilder();
        Letter? previous = null;
        foreach (var letter in letters)
        {
            if (previous is not null)
            {
                var gap = letter.StartBaseLine.X - previous.EndBaseLine.X;
                if (!OnSameLine(previous, letter) || gap > previous.PointSize * 0.2)
                {
                    sb.Append(' ');
                }
            }

            sb.Append(letter.Value);
            previous = letter;
        }

        return Normalize(sb.ToString());
    }

    /// <summary>Two letters are on the same line when their baselines are within half the font size of each other.</summary>
    public static bool OnSameLine(Letter previous, Letter next) =>
        Math.Abs(next.StartBaseLine.Y - previous.StartBaseLine.Y) <= previous.PointSize * 0.5;

    /// <summary>Unicode NFC, whitespace runs collapsed to one space, trimmed.</summary>
    public static string Normalize(string text) =>
        Whitespace().Replace(text.Normalize(NormalizationForm.FormC), " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
