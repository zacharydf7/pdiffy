using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace PDiffy.Reading;

internal static partial class TextJoiner
{
    /// <summary>The widest gap between two letters, as a share of the font size, that still counts as no space.</summary>
    public const double SpaceGap = 0.2;

    // A space goes between two letters when the baseline changes or the gap is wider than SpaceGap em. Gaps are measured
    // along the text's own baseline, so text drawn at an angle joins as it does across the page. Space glyphs in the PDF
    // come through as letters themselves.
    public static string Join(IEnumerable<Letter> letters)
    {
        var sb = new StringBuilder();
        Letter? previous = null;
        foreach (var letter in letters)
        {
            if (previous is not null)
            {
                if (!OnSameLine(previous, letter) || Gap(previous, letter) > previous.PointSize * SpaceGap)
                {
                    sb.Append(' ');
                }
            }

            sb.Append(letter.Value);
            previous = letter;
        }

        return Normalize(sb.ToString());
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
    public static string Normalize(string text) =>
        Whitespace().Replace(text.Normalize(NormalizationForm.FormC), " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
