using System.Xml.Linq;
using static PDiffy.Docx.Reading.DocxXml;

namespace PDiffy.Docx.Reading;

/// <summary>
/// Reads form controls: content controls (w:sdt), whose kind comes from their properties, and legacy form fields,
/// whose settings sit in a field's w:ffData.
/// </summary>
internal static class FormReader
{
    private static readonly XNamespace W14 = "http://schemas.microsoft.com/office/word/2010/wordml";
    private static readonly XNamespace W15 = "http://schemas.microsoft.com/office/word/2012/wordml";

    // The property that names a content control's kind; a control with none of them holds rich text.
    private static readonly (string Element, string Kind)[] Kinds =
    [
        ("text", "text"), ("dropDownList", "dropdown"), ("comboBox", "combobox"), ("date", "date"), ("picture", "picture"),
        ("docPartObj", "gallery"), ("docPartList", "gallery"), ("group", "group"), ("citation", "citation"),
        ("bibliography", "bibliography"), ("equation", "equation"),
    ];

    public static (string Kind, string? Name, string? Title) Describe(XElement? sdtPr)
    {
        var kind = sdtPr?.Element(W14 + "checkbox") is not null ? "checkbox"
            : sdtPr?.Element(W15 + "repeatingSection") is not null ? "repeating-section"
            : sdtPr?.Element(W15 + "repeatingSectionItem") is not null ? "repeating-section-item"
            : Kinds.FirstOrDefault(entry => sdtPr?.Element(W + entry.Element) is not null).Kind ?? "rich-text";
        return (kind, Attr(sdtPr?.Element(W + "tag"), "val"), Attr(sdtPr?.Element(W + "alias"), "val"));
    }

    /// <summary>A content control inside a paragraph, with the text it shows as its value.</summary>
    public static DocxForm Control(XElement? sdtPr, string value, int position)
    {
        var (kind, name, title) = Describe(sdtPr);
        var list = sdtPr?.Element(W + "dropDownList") ?? sdtPr?.Element(W + "comboBox");
        var checkbox = sdtPr?.Element(W14 + "checkbox");
        return new DocxForm
        {
            Kind = kind, Position = position, Name = name, Title = title,
            Value = checkbox is null ? value : YesNo(IsTrue((string?)checkbox.Element(W14 + "checked")?.Attribute(W14 + "val"))),
            Items = list?.Elements(W + "listItem").Select(item => Attr(item, "displayText") ?? Attr(item, "value") ?? "").ToList() ?? [],
        };
    }

    /// <summary>
    /// A legacy form field from its w:ffData: a check box's value is whether it's checked, a drop-down's the chosen entry,
    /// and a text field's its result.
    /// </summary>
    public static DocxForm LegacyField(XElement data, string result, int position)
    {
        var checkBox = data.Element(W + "checkBox");
        var dropDown = data.Element(W + "ddList");
        var entries = dropDown?.Elements(W + "listEntry").Select(entry => Attr(entry, "val") ?? "").ToList() ?? [];
        var (kind, value) = checkBox is not null
            ? ("legacy-checkbox", YesNo(IsOn(checkBox.Element(W + "checked") ?? checkBox.Element(W + "default"))))
            : dropDown is not null
                ? ("legacy-dropdown", entries.ElementAtOrDefault(Integer(dropDown.Element(W + "result"), "val") ?? 0))
                : ("legacy-text", result);
        return new DocxForm
        {
            Kind = kind, Position = position, Name = Attr(data.Element(W + "name"), "val"), Value = value,
            Items = entries,
        };
    }

    private static string YesNo(bool value) => value ? "yes" : "no";
}
