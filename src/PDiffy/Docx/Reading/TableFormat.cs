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
    public static string? Width(XElement? width) => Attr(width, "type") switch
    {
        _ when width is null => null,
        "auto" => "auto",
        "nil" => "0",
        "pct" when Attr(width, "w") is { } value && value.EndsWith('%') => value,
        "pct" => Integer(width, "w") is int fiftieths ? (fiftieths / 50.0).ToString("0.#", CultureInfo.InvariantCulture) + "%"
            : null,
        _ => Twips(width, "w") is double points ? Points(points) : null,
    };

    public static IReadOnlyList<string> Margins(XElement? margins) =>
        margins?.Elements().Select(side => $"{Side(side)}:{Points(Twips(side, "w") ?? 0)}").ToList() ?? [];

    // Border sizes are in eighths of a point.
    public static IReadOnlyList<string> Borders(XElement? borders) => borders?.Elements().Select(side =>
    {
        var line = $"{Side(side)}:{Attr(side, "val") ?? "none"}:{Points((Integer(side, "sz") ?? 0) / 8.0)}";
        return Attr(side, "color") is { } color and not "auto" ? $"{line}:{color.ToUpperInvariant()}" : line;
    }).ToList() ?? [];

    // A row height is at least its value unless the rule says "exact", or "auto" (sized by Word, the value ignored).
    public static string? Height(XElement? height) => Attr(height, "hRule") switch
    {
        _ when height is null => null,
        "auto" => "auto",
        "exact" => $"{Points(Twips(height, "val") ?? 0)}-exact",
        _ => Points(Twips(height, "val") ?? 0),
    };

    // A fill color in hex; "auto" is no fill.
    public static string? Shading(XElement? shading) =>
        NonEmpty(Attr(shading, "fill")) is { } fill and not "auto" ? fill.ToUpperInvariant() : null;

    private static string Side(XElement side) => SideNames.GetValueOrDefault(side.Name.LocalName, side.Name.LocalName);
}
