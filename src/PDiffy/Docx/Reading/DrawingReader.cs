using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static PDiffy.Docx.Reading.DocxXml;

namespace PDiffy.Docx.Reading;

/// <summary>
/// Reads images and other graphics: DrawingML pictures, charts, diagrams and shapes (w:drawing), inline or floating,
/// and legacy VML pictures (w:pict). Sizes are in points.
/// </summary>
internal sealed partial class DrawingReader(DocxPackage package, string part)
{
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Decorative = "http://schemas.microsoft.com/office/drawing/2017/decorative";
    private static readonly XNamespace V = "urn:schemas-microsoft-com:vml";
    private static readonly XNamespace O = "urn:schemas-microsoft-com:office:office";

    private const double EmusPerPoint = 12700;

    public DocxImage? Drawing(XElement drawing, int position)
    {
        if ((drawing.Element(Wp + "inline") ?? drawing.Element(Wp + "anchor")) is not { } frame) return null;
        var extent = frame.Element(Wp + "extent");
        var properties = frame.Element(Wp + "docPr");
        var embed = (string?)frame.Descendants(A + "blip").FirstOrDefault()?.Attribute(R + "embed");
        var imagePart = embed is null ? null : package.Target(part, embed);
        return new DocxImage(position)
        {
            Kind = embed is null ? GraphicKind(frame) : "image",
            Floating = frame.Name.LocalName == "anchor",
            Width = Emus(extent, "cx"),
            Height = Emus(extent, "cy"),
            AltText = NonEmpty((string?)properties?.Attribute("descr")),
            Title = NonEmpty((string?)properties?.Attribute("title")),
            Decorative = properties?.Descendants(Decorative + "decorative").Any(mark => IsTrue((string?)mark.Attribute("val"))) ?? false,
            Type = TypeOf(imagePart),
            Hash = package.Hash(imagePart),
        };
    }

    // VML gives its size and placement in a CSS-like style attribute, such as "position:absolute;width:72pt;height:36pt".
    public DocxImage? Picture(XElement pict, int position)
    {
        if (pict.Descendants(V + "shape").FirstOrDefault() is not { } shape) return null;
        var data = shape.Element(V + "imagedata");
        var id = (string?)data?.Attribute(R + "id");
        var imagePart = id is null ? null : package.Target(part, id);
        var style = (string?)shape.Attribute("style") ?? "";
        return new DocxImage(position)
        {
            Floating = style.Contains("position:absolute", StringComparison.Ordinal),
            Width = StylePoints(style, "width"),
            Height = StylePoints(style, "height"),
            AltText = NonEmpty((string?)shape.Attribute("alt")),
            Title = NonEmpty((string?)data?.Attribute(O + "title")),
            Type = TypeOf(imagePart),
            Hash = package.Hash(imagePart),
        };
    }

    private static double Emus(XElement? extent, string attribute) =>
        long.TryParse((string?)extent?.Attribute(attribute), CultureInfo.InvariantCulture, out var emus) ? emus / EmusPerPoint : 0;

    private static double StylePoints(string style, string name) =>
        StyleLength().Matches(style).FirstOrDefault(match => match.Groups[1].Value == name) is { } match
            ? double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
            : 0;

    private static string? TypeOf(string? imagePart) =>
        imagePart is null ? null : NonEmpty(Path.GetExtension(imagePart).TrimStart('.').ToLowerInvariant());

    // What a graphic without a picture is, from the end of its graphicData uri: "chart", "diagram", "shape" and so on.
    private static string GraphicKind(XElement frame)
    {
        var uri = (string?)frame.Descendants(A + "graphicData").FirstOrDefault()?.Attribute("uri") ?? "";
        var kind = uri[(uri.LastIndexOf('/') + 1)..];
        return kind.StartsWith("wordprocessing", StringComparison.Ordinal) ? kind["wordprocessing".Length..].ToLowerInvariant()
            : NonEmpty(kind) ?? "graphic";
    }

    [GeneratedRegex(@"(width|height)\s*:\s*([\d.]+)pt")]
    private static partial Regex StyleLength();
}
