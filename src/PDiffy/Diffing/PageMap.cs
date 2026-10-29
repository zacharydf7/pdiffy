using System.Net;
using System.Text;

namespace PDiffy.Diffing;

/// <summary>
/// A schematic SVG of one page for the HTML report, drawn from two snapshots' layout lines alone: the page's box and
/// every text run, image and path as a box. What's the same is gray; where something was is a dashed outline, and
/// where it is now is filled. It shows where things are, not what they look like.
/// </summary>
internal static class PageMap
{
    /// <summary>A snapshot whose pages section was left out doesn't give a page's size, so letter stands in.</summary>
    public static readonly (double Width, double Height) DefaultSize = PageSize.Dimensions("letter")!.Value;

    // How wide a map is shown; its height follows the page's shape.
    private const int ShownWidth = 320;

    // Text runs have a position and a font size but no width, so their box is estimated from the text, and most of a
    // line sits above its baseline. A run whose font size was left out is drawn at a typical size.
    private const double CharacterWidth = 0.5;
    private const double AboveBaseline = 0.8;
    private const double DefaultFontSize = 10;
    private const int MaxLabelLength = 40;

    /// <summary>
    /// The map, or null when no line on the page gives a position, as when <see cref="Field.Positions"/> is ignored.
    /// </summary>
    public static string? Draw(int page, IReadOnlySet<string> was, IReadOnlySet<string> now,
        (double Width, double Height) box)
    {
        var shapes = new StringBuilder();
        foreach (var line in now) Element(shapes, line, was.Contains(line) ? "same" : "now", box.Height);
        foreach (var line in was.Where(line => !now.Contains(line))) Element(shapes, line, "was", box.Height);
        if (shapes.Length == 0) return null;

        return new StringBuilder($"<figure class=\"map\"><figcaption>p{page}</figcaption>")
            .Append($"<svg viewBox=\"0 0 {Number(box.Width)} {Number(box.Height)}\" width=\"{ShownWidth}\" role=\"img\" ")
            .Append($"aria-label=\"Where things are on page {page}\">")
            .Append($"<rect class=\"page\" width=\"{Number(box.Width)}\" height=\"{Number(box.Height)}\"/>")
            .Append(shapes).Append("</svg></figure>").ToString();
    }

    /// <summary>The lines of a section, trimmed and grouped by the page each is on.</summary>
    public static Dictionary<int, HashSet<string>> ByPage(IEnumerable<string> lines) =>
        lines.Select(line => line.Trim()).GroupBy(line => SnapshotLine.Parse(line, 0).Page)
            .Where(group => group.Key is not null).ToDictionary(group => group.Key!.Value, group => group.ToHashSet());

    /// <summary>
    /// Each page's size from the pages section's lines, such as "p1 letter rotate=0" or "p2 500x700 rotate=0".
    /// </summary>
    public static Dictionary<int, (double Width, double Height)> Sizes(IEnumerable<string> pageLines)
    {
        var sizes = new Dictionary<int, (double Width, double Height)>();
        foreach (var text in pageLines)
        {
            var line = SnapshotLine.Parse(text.Trim(), 0);
            var size = line.Words.ElementAtOrDefault(1) is { } words ? PageSize.Dimensions(words) : null;
            if (line.Page is int page && size is { } box) sizes[page] = box;
        }

        return sizes;
    }

    // PDF positions count up from the page's bottom edge; SVG's count down from the top.
    private static void Element(StringBuilder svg, string text, string state, double pageHeight)
    {
        var line = SnapshotLine.Parse(text, 0);
        if (Numbers(line, "at") is not [var x, var y]) return;

        var title = $"<title>{WebUtility.HtmlEncode(text)}</title>";
        if (line.Words.ElementAtOrDefault(1) == "text")
        {
            var fontSize = Numbers(line, "size") is [var size] ? size : DefaultFontSize;
            var label = line.QuotedText ?? "";
            var width = Math.Max(fontSize, label.Length * fontSize * CharacterWidth);
            var shown = label.Length > MaxLabelLength ? label[..MaxLabelLength] + "..." : label;
            svg.Append($"<g class=\"{state}\">{title}")
                .Append(Box(x, pageHeight - y - fontSize * AboveBaseline, width, fontSize))
                .Append($"<text x=\"{Number(x)}\" y=\"{Number(pageHeight - y)}\" font-size=\"{Number(fontSize)}\">")
                .Append(WebUtility.HtmlEncode(shown)).Append("</text></g>");
        }
        else if (Numbers(line, "size") is [var w, var h])
        {
            svg.Append($"<g class=\"{state}\">{title}{Box(x, pageHeight - y - h, w, h)}</g>");
        }
    }

    // At least a point each way, so an empty run or a hairline still shows.
    private static string Box(double x, double y, double width, double height) =>
        $"<rect x=\"{Number(x)}\" y=\"{Number(y)}\" width=\"{Number(Math.Max(width, 1))}\" "
        + $"height=\"{Number(Math.Max(height, 1))}\"/>";

    private static double[]? Numbers(SnapshotLine line, string key) =>
        line.Attributes.FirstOrDefault(pair => pair.Key == key).Value is { } value ? SnapshotLine.Numbers(value) : null;

    private static string Number(double value) =>
        Math.Round(value, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
