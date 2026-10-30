using System.Globalization;
using System.Xml.Linq;
using static PDiffy.Docx.Reading.DocxXml;

namespace PDiffy.Docx.Reading;

/// <summary>
/// Reads the widths, margins, borders and shading of tables, cells and paragraphs, and the heights of rows (w:tblW, w:tcW,
/// w:tblCellMar, w:tcMar, w:tblBorders, w:tcBorders, w:pBdr, w:shd, w:trHeight) into the short forms
/// <see cref="DocxTable"/> describes.
/// </summary>
internal static class TableFormat
{
    // Word writes start and end for left and right in newer files.
    private static readonly Dictionary<string, string> SideNames = new() { ["start"] = "left", ["end"] = "right" };

    // Points for twips, a percentage (in fiftieths of a percent, or written with %), or "auto".
    public static DocxWidth? Width(XElement? width) => Attr(width, "type") switch
    {
        _ when width is null => null,
        "auto" => new DocxWidth { Value = 0, Unit = DocxWidthUnit.Auto },
        "nil" => new DocxWidth { Value = 0, Unit = DocxWidthUnit.Points },
        "pct" when Attr(width, "w") is { } value && value.EndsWith('%')
            && double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) =>
            new DocxWidth { Value = percent, Unit = DocxWidthUnit.Percent },
        "pct" => Integer(width, "w") is int fiftieths
            ? new DocxWidth { Value = Math.Round(fiftieths / 50.0, 1), Unit = DocxWidthUnit.Percent } : null,
        _ => Twips(width, "w") is double points ? new DocxWidth { Value = points, Unit = DocxWidthUnit.Points } : null,
    };

    public static IReadOnlyList<DocxMargin> Margins(XElement? margins) => margins?.Elements()
        .Select(side => new DocxMargin { Side = Side(side), Width = Twips(side, "w") ?? 0 }).ToList() ?? [];

    public static IReadOnlyList<DocxBorder> Borders(XElement? borders) =>
        borders?.Elements().Select(Border).ToList() ?? [];

    // Border sizes are in eighths of a point.
    private static DocxBorder Border(XElement side) => new()
    {
        Side = Side(side), Style = Attr(side, "val") ?? "none", Width = (Integer(side, "sz") ?? 0) / 8.0,
        Color = Attr(side, "color") is { } color and not "auto" ? color.ToUpperInvariant() : null,
    };

    // A row height is at least its value unless the rule says "exact", or "auto" (sized by Word, the value ignored).
    public static DocxRowHeight? Height(XElement? height) => height is null ? null : new DocxRowHeight
    {
        Value = Twips(height, "val") ?? 0, Rule = Attr(height, "hRule") ?? DocxSizeRules.AtLeast,
    };

    // A fill color in hex; "auto" is no fill.
    public static string? Shading(XElement? shading) =>
        NonEmpty(Attr(shading, "fill")) is { } fill and not "auto" ? fill.ToUpperInvariant() : null;

    private static string Side(XElement side) => SideNames.GetValueOrDefault(side.Name.LocalName, side.Name.LocalName);
}
