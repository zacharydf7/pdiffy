using System.Globalization;
using System.Xml.Linq;

namespace PDiffy.Docx.Reading;

/// <summary>Reading WordprocessingML values: attributes, on/off properties and lengths in twentieths of a point.</summary>
internal static class DocxXml
{
    public static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    // Elements that only wrap content: content controls, custom XML, smart tags and tracked insertions.
    private static readonly HashSet<string> Wrappers = ["sdt", "sdtContent", "customXml", "smartTag", "ins", "moveTo"];

    public static string? Attr(XElement? element, string name) => (string?)element?.Attribute(W + name);

    // An on/off property: present means on, unless its val says otherwise.
    public static bool IsOn(XElement? element) => element is not null && Attr(element, "val") is not ("0" or "false" or "off");

    public static bool IsTrue(string? value) => value is "1" or "true" or "on";

    public static double? Twips(XElement? element, string attribute) =>
        double.TryParse(Attr(element, attribute), CultureInfo.InvariantCulture, out var twips) ? twips / 20 : null;

    public static int? Integer(XElement? element, string attribute) =>
        int.TryParse(Attr(element, attribute), CultureInfo.InvariantCulture, out var value) ? value : null;

    public static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    public static bool IsWrapper(XElement element) => element.Name.Namespace == W && Wrappers.Contains(element.Name.LocalName);

    public static string Points(double value) => Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);
}
