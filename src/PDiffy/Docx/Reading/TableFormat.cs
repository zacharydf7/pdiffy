using System.Globalization;
using System.Xml.Linq;
using static PDiffy.Docx.Reading.DocxXml;

namespace PDiffy.Docx.Reading;

/// <summary>
/// Reads the widths, margins and borders of tables and cells (w:tblW, w:tcW, w:tblCellMar, w:tcMar, w:tblBorders,
/// w:tcBorders) into the short forms <see cref="DocxTable"/> describes.
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

    private static string Side(XElement side) => SideNames.GetValueOrDefault(side.Name.LocalName, side.Name.LocalName);
}
