using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static PDiffy.Docx.Reading.DocxXml;

namespace PDiffy.Docx.Reading;

/// <summary>
/// Reads images and other graphics: DrawingML pictures, charts, diagrams and shapes (w:drawing), inline or floating,
/// legacy VML pictures (w:pict) and embedded objects (w:object), with the text of any text box in them. Sizes are in
/// points.
/// </summary>
internal sealed partial class DrawingReader(DocxPackage package, string part,
    Func<XElement, IReadOnlyList<DocxBlock>> readBlocks)
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
        var blip = frame.Descendants(A + "blip").FirstOrDefault(blip => !InTextBox(blip, frame));
        var embed = (string?)blip?.Attribute(R + "embed");
        var imagePart = embed is null ? null : package.Target(part, embed);
        return new DocxImage
        {
            Position = position, Kind = blip is null ? GraphicKind(frame) : DocxGraphicKinds.Image,
            Floating = frame.Name.LocalName == "anchor", Width = Emus(extent, "cx"), Height = Emus(extent, "cy"),
            AltText = NonEmpty((string?)properties?.Attribute("descr")),
            Title = NonEmpty((string?)properties?.Attribute("title")),
            Decorative = properties?.Descendants(Decorative + "decorative").Any(mark => IsTrue((string?)mark.Attribute("val"))) ?? false,
            Type = TypeOf(imagePart), Hash = package.Hash(imagePart), TextBox = TextBoxes(frame),
        };
    }

    // VML gives its size and placement in a CSS-like style attribute, such as "position:absolute;width:72pt;height:36pt".
    public DocxImage? Picture(XElement pict, int position)
    {
        if (pict.Descendants(V + "shape").FirstOrDefault(shape => !InTextBox(shape, pict)) is not { } shape) return null;
        var data = shape.Element(V + "imagedata");
        var id = (string?)data?.Attribute(R + "id");
        var imagePart = id is null ? null : package.Target(part, id);
        var style = (string?)shape.Attribute("style") ?? "";
        return new DocxImage
        {
            Position = position, Kind = data is null ? DocxGraphicKinds.Shape : DocxGraphicKinds.Image,
            Floating = style.Contains("position:absolute", StringComparison.Ordinal),
            Width = StylePoints(style, "width"), Height = StylePoints(style, "height"),
            AltText = NonEmpty((string?)shape.Attribute("alt")),
            Title = NonEmpty((string?)data?.Attribute(O + "title")), Type = TypeOf(imagePart),
            Hash = package.Hash(imagePart), TextBox = TextBoxes(pict),
        };
    }

    // An embedded or linked object (o:OLEObject) or an ActiveX control (w:control), with its data's hash or linked file.
    public DocxImage Object(XElement item, int position)
    {
        var shape = item.Descendants(V + "shape").FirstOrDefault();
        var ole = item.Element(O + "OLEObject");
        var control = item.Element(W + "control");
        var id = (string?)ole?.Attribute(R + "id") ?? (string?)control?.Attribute(R + "id");
        var target = id is null ? null : package.Target(part, id);
        var linked = (string?)ole?.Attribute("Type") == "Link";
        var isControl = ole is null && control is not null;
        var style = (string?)shape?.Attribute("style") ?? "";
        return new DocxImage
        {
            Position = position, Kind = isControl ? DocxGraphicKinds.Control : DocxGraphicKinds.Object,
            Width = StylePoints(style, "width"),
            Height = StylePoints(style, "height"), AltText = NonEmpty((string?)shape?.Attribute("alt")),
            Type = NonEmpty((string?)ole?.Attribute("ProgID") ?? Attr(control, "name")),
            Source = linked ? target : null,
            Hash = linked ? null : package.Hash(target, isControl ? StatePart(target) : null),
        };
    }

    // An ActiveX control's state, such as whether a check box is ticked, is in the binary part its XML part points to.
    private string? StatePart(string? controlPart) =>
        controlPart is not null && (string?)package.Xml(controlPart)?.Root?.Attribute(R + "id") is { } id
            ? package.Target(controlPart, id)
            : null;

    // The paragraphs of each text box in a graphic, in order; a text box inside one of them is read with its paragraph.
    private IReadOnlyList<DocxBlock> TextBoxes(XElement graphic) =>
        graphic.Descendants(W + "txbxContent").Where(box => !InTextBox(box, graphic)).SelectMany(readBlocks).ToList();

    // What's inside a text box belongs to the text box's paragraphs, not to the graphic around it.
    private static bool InTextBox(XElement item, XElement graphic) =>
        item.Ancestors().TakeWhile(ancestor => ancestor != graphic).Any(ancestor => ancestor.Name == W + "txbxContent");

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
