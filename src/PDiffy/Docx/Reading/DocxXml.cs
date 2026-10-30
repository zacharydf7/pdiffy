using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace PDiffy.Docx.Reading;

/// <summary>Reading WordprocessingML values: attributes, on/off properties and lengths in twentieths of a point.</summary>
internal static class DocxXml
{
    public static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public static readonly XNamespace Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    public static readonly XNamespace M = "http://schemas.openxmlformats.org/officeDocument/2006/math";

    // Word's own bookmark for where the document was last edited, which changes without the content changing.
    public const string LastEditBookmark = "_GoBack";

    // Markers that only bracket a range, read nowhere: bookmark ends, comment and move ranges, permissions, spelling.
    public static readonly HashSet<string> RangeMarkers =
    [
        "bookmarkEnd", "proofErr", "permStart", "permEnd", "commentRangeStart", "commentRangeEnd", "moveFromRangeStart",
        "moveFromRangeEnd", "moveToRangeStart", "moveToRangeEnd", "customXmlInsRangeStart", "customXmlInsRangeEnd",
        "customXmlDelRangeStart", "customXmlDelRangeEnd", "customXmlMoveFromRangeStart", "customXmlMoveFromRangeEnd",
        "customXmlMoveToRangeStart", "customXmlMoveToRangeEnd",
    ];

    // Elements that only wrap content: content controls, custom XML, smart tags and right-to-left text.
    private static readonly HashSet<string> Wrappers = ["sdt", "sdtContent", "customXml", "smartTag", "dir", "bdo"];

    public static string? Attr(XElement? element, string name) => (string?)element?.Attribute(W + name);

    // An on/off property: present means on, unless its val says otherwise.
    public static bool IsOn(XElement? element) => element is not null && Attr(element, "val") is not ("0" or "false" or "off");

    public static bool IsTrue(string? value) => value is "1" or "true" or "on";

    public static double? Twips(XElement? element, string attribute) =>
        double.TryParse(Attr(element, attribute), CultureInfo.InvariantCulture, out var twips) ? twips / 20 : null;

    public static int? Integer(XElement? element, string attribute) =>
        int.TryParse(Attr(element, attribute), CultureInfo.InvariantCulture, out var value) ? value : null;

    public static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    // A bookmark in a table can cover only some columns, from w:colFirst to w:colLast.
    public static (int First, int Last)? BookmarkColumns(XElement bookmarkStart) =>
        Integer(bookmarkStart, "colFirst") is int first && Integer(bookmarkStart, "colLast") is int last
            ? (first, last)
            : null;

    public static bool IsWrapper(XElement element) => element.Name.Namespace == W && Wrappers.Contains(element.Name.LocalName);

    public static string Points(double value) => Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);

    // The element's name with its usual prefix, such as "w:ruby", whatever prefix the file uses; other namespaces in full.
    public static string NameOf(XElement element)
    {
        var prefix = element.Name.Namespace == W ? "w" : element.Name.Namespace == M ? "m" : null;
        return prefix is null ? element.Name.ToString() : $"{prefix}:{element.Name.LocalName}";
    }

    // mc:AlternateContent offers the same content in several forms, newest first; the first choice is the one read.
    public static XElement? Chosen(XElement alternateContent) =>
        alternateContent.Element(Mc + "Choice") ?? alternateContent.Element(Mc + "Fallback");

    /// <summary>
    /// The first 16 hex digits of the SHA-256 of an element's XML, leaving out what changes without the content changing:
    /// editing-session ids (w:rsid...), relationship ids, namespace prefixes and the indenting between elements.
    /// </summary>
    public static string Hash(XElement element)
    {
        var copy = new XElement(element);
        foreach (var item in copy.DescendantsAndSelf())
        {
            item.Attributes().Where(attribute => attribute.IsNamespaceDeclaration || attribute.Name.Namespace == R
                || attribute.Name.LocalName.StartsWith("rsid", StringComparison.Ordinal)).Remove();
        }

        copy.DescendantNodes().OfType<XText>()
            .Where(text => text.Parent!.HasElements && string.IsNullOrWhiteSpace(text.Value)).Remove();
        var xml = copy.ToString(SaveOptions.DisableFormatting);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(xml)))[..16];
    }
}
