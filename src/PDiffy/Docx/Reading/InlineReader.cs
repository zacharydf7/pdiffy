using System.Globalization;
using System.Text;
using System.Xml.Linq;
using static PDiffy.Docx.Reading.DocxXml;

namespace PDiffy.Docx.Reading;

/// <summary>
/// Reads what's inside a paragraph: its text with runs merged, the parts where its look changes, and what sits in the
/// text (images, links, bookmarks, symbols and page or column breaks), each at its position.
/// </summary>
internal sealed class InlineReader(StyleResolver styles, DocxPackage package, string part, HashSet<string> usedStyles)
{
    private readonly DrawingReader _drawings = new(package, part);

    public ParagraphContent Read(XElement paragraph, RunFormat paragraphRun)
    {
        var content = new ParagraphContent(paragraphRun);
        ReadContent(paragraph, content);
        return content;
    }

    // Deleted text (tracked deletions, moves away) isn't part of the paragraph, so w:del and w:moveFrom are skipped.
    private void ReadContent(XElement container, ParagraphContent content)
    {
        foreach (var element in container.Elements().Where(element => element.Name.Namespace == W))
        {
            if (element.Name.LocalName == "r") ReadRun(element, content);
            else if (element.Name.LocalName == "hyperlink") ReadLink(element, content);
            else if (element.Name.LocalName == "bookmarkStart" && Attr(element, "name") is { } name and not "_GoBack")
            {
                content.Inlines.Add(new DocxBookmark(name, content.Text.Length));
            }
            else if (IsWrapper(element)) ReadContent(element, content);
        }
    }

    // A link's text is the text of the runs inside it, so it's read first and the link placed before what it contains.
    // A link whose address can't be found has no target, even when it also names a bookmark.
    private void ReadLink(XElement link, ParagraphContent content)
    {
        var (start, index) = (content.Text.Length, content.Inlines.Count);
        ReadContent(link, content);
        var bookmark = Attr(link, "anchor") is { } anchor ? $"#{anchor}" : null;
        var target = (string?)link.Attribute(R + "id") is { } id ? package.Target(part, id) is { } address ? address + bookmark : null : bookmark;
        content.Inlines.Insert(index, new DocxLink(content.Text.ToString(start, content.Text.Length - start), target, start));
    }

    private void ReadRun(XElement run, ParagraphContent content)
    {
        var rPr = run.Element(W + "rPr");
        var characterStyle = Attr(rPr?.Element(W + "rStyle"), "val");
        if (characterStyle is not null) usedStyles.Add(characterStyle);
        var format = styles.Run(content.ParagraphRun, characterStyle, rPr);

        var text = new StringBuilder();
        foreach (var item in run.Elements())
        {
            var position = content.Text.Length + text.Length;
            switch (item.Name.LocalName)
            {
                case "t":
                    text.Append(item.Value);
                    break;
                case "tab":
                    text.Append('\t');
                    break;
                case "cr":
                    text.Append('\n');
                    break;
                case "noBreakHyphen":
                    text.Append('\u2011');
                    break;
                case "softHyphen":
                    text.Append('\u00AD');
                    break;
                case "br" when Attr(item, "type") is "page" or "column":
                    content.Inlines.Add(new DocxBreak(Attr(item, "type")!, position));
                    break;
                case "br":
                    text.Append('\n');
                    break;

                // A symbol is a character from a symbol font, such as a Wingdings check mark: it's text, and its font
                // decides what it looks like.
                case "sym" when Attr(item, "char") is { } code && int.TryParse(code, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value):
                    text.Append((char)value);
                    content.Inlines.Add(new DocxSymbol(Attr(item, "font"), code.ToUpperInvariant(), position));
                    break;
                case "drawing" when _drawings.Drawing(item, position) is { } image:
                    content.Inlines.Add(image);
                    break;
                case "pict" when _drawings.Picture(item, position) is { } picture:
                    content.Inlines.Add(picture);
                    break;
            }
        }

        if (text.Length == 0) return;
        content.Text.Append(text);
        var spans = content.Spans;
        if (spans.Count > 0 && spans[^1].Format == format) spans[^1] = spans[^1] with { Text = spans[^1].Text + text };
        else spans.Add(new DocxSpan(text.ToString(), format));
    }
}

/// <summary>A paragraph's content as it's read.</summary>
internal sealed class ParagraphContent(RunFormat paragraphRun)
{
    public RunFormat ParagraphRun { get; } = paragraphRun;
    public StringBuilder Text { get; } = new();
    public List<DocxSpan> Spans { get; } = [];
    public List<DocxInline> Inlines { get; } = [];
}
