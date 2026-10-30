using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace PDiffy.Reading;

internal static partial class TextJoiner
{
    /// <summary>The widest gap between two letters, as a share of the font size, that still counts as no space.</summary>
    public const double SpaceGap = 0.2;

    /// <summary>The widest letter spacing, as a share of the font size; wider gaps are letters set apart on purpose.</summary>
    public const double MaxLetterSpacing = 0.25;

    // Fewer gaps than this can't tell letter spacing from short words set apart, so they aren't taken as spacing.
    private const int MinGapsForLetterSpacing = 3;

    // Points past MaxLetterSpacing still taken as spacing: writers round Tc to two decimals, which can land a hair past
    // the cap (2.63 at 10.5 pt, where the cap is 2.625).
    private const double WrittenRounding = 0.01;

    // A space goes between two letters when the baseline changes or the gap is wider than SpaceGap em beyond the line's
    // letter spacing. Gaps are measured along the text's own baseline, so text drawn at an angle joins as it does across
    // the page. Space glyphs in the PDF come through as letters themselves.
    public static string Join(IReadOnlyList<Letter> letters)
    {
        var sb = new StringBuilder();
        var spacing = 0.0;
        for (var i = 0; i < letters.Count; i++)
        {
            if (i == 0 || !OnSameLine(letters[i - 1], letters[i]))
            {
                if (i > 0) sb.Append(' ');
                spacing = LetterSpacing(letters, i);
            }
            else if (Gap(letters[i - 1], letters[i]) - spacing > letters[i - 1].PointSize * SpaceGap)
            {
                sb.Append(' ');
            }

            sb.Append(letters[i].Value);
        }

        return Normalize(sb.ToString());
    }

    /// <summary>Each line's text, in content order, leaving out lines with no text.</summary>
    public static List<string> JoinLines(IReadOnlyList<Letter> letters)
    {
        var lines = new List<string>();
        var start = 0;
        for (var i = 1; i <= letters.Count; i++)
        {
            if (i < letters.Count && OnSameLine(letters[i - 1], letters[i])) continue;
            if (Join(letters.Skip(start).Take(i - start).ToList()) is { Length: > 0 } line) lines.Add(line);
            start = i;
        }

        return lines;
    }

    // Letter spacing widens every gap on a line, word gaps and gaps inside words alike, so the low end of the line's gaps
    // stands for it; word gaps only widen some of them.
    private static double LetterSpacing(IReadOnlyList<Letter> letters, int start)
    {
        var gaps = new List<double>();
        for (var i = start + 1; i < letters.Count && OnSameLine(letters[i - 1], letters[i]); i++)
        {
            gaps.Add(Gap(letters[i - 1], letters[i]));
        }

        if (gaps.Count < MinGapsForLetterSpacing) return 0;
        gaps.Sort();
        var low = gaps[gaps.Count / 4];
        return low > 0 && low <= letters[start].PointSize * MaxLetterSpacing + WrittenRounding ? low : 0;
    }

    /// <summary>
    /// Two letters are on the same line when they run the same way and their baselines are within half the font size of
    /// each other.
    /// </summary>
    public static bool OnSameLine(Letter previous, Letter next)
    {
        var (x, y) = Direction(previous);
        var (nextX, nextY) = Direction(next);
        var (dx, dy) = (next.StartBaseLine.X - previous.StartBaseLine.X, next.StartBaseLine.Y - previous.StartBaseLine.Y);
        return x * nextX + y * nextY > 0.99 && Math.Abs(dy * x - dx * y) <= previous.PointSize * 0.5;
    }

    /// <summary>How far the next letter starts past where the previous one ends, along the previous one's baseline.</summary>
    public static double Gap(Letter previous, Letter next) => Along(previous, previous.EndBaseLine, next.StartBaseLine);

    /// <summary>How far the next letter starts ahead of the previous one's start; negative when the text jumps back.</summary>
    public static double Advance(Letter previous, Letter next) => Along(previous, previous.StartBaseLine, next.StartBaseLine);

    /// <summary>The angle a letter's baseline runs at, in whole degrees counterclockwise from across the page (0 to 359).</summary>
    public static int Angle(Letter letter)
    {
        var (x, y) = Direction(letter);
        var degrees = (int)Math.Round(Math.Atan2(y, x) * 180 / Math.PI, MidpointRounding.AwayFromZero);
        return (degrees + 360) % 360;
    }

    private static double Along(Letter letter, PdfPoint from, PdfPoint to)
    {
        var (x, y) = Direction(letter);
        return (to.X - from.X) * x + (to.Y - from.Y) * y;
    }

    // A letter of no width, such as some spaces, has no baseline to measure, so the angle of its box stands in.
    private static (double X, double Y) Direction(Letter letter)
    {
        var (dx, dy) = (letter.EndBaseLine.X - letter.StartBaseLine.X, letter.EndBaseLine.Y - letter.StartBaseLine.Y);
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length > 0) return (dx / length, dy / length);
        var radians = letter.BoundingBox.Rotation * Math.PI / 180;
        return (Math.Cos(radians), Math.Sin(radians));
    }

    /// <summary>Unicode NFC, whitespace runs collapsed to one space, trimmed.</summary>
    // Normalizing throws on U+FFFE, which a broken ToUnicode map can give, so U+FFFD replaces it.
    public static string Normalize(string text) =>
        Whitespace().Replace(text.Replace('\uFFFE', '\uFFFD').Normalize(NormalizationForm.FormC), " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
