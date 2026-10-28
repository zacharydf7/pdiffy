using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static PDiffy.Docx.Reading.DocxXml;

namespace PDiffy.Docx.Reading;

/// <summary>
/// Reads what's inside a paragraph: its text with runs merged, the parts where its look changes, and what sits in the
/// text (images, links, bookmarks, symbols, fields, form controls, note and comment references, tracked changes and page
/// or column breaks), each at its position.
/// </summary>
internal sealed partial class InlineReader(StyleResolver styles, DocxPackage package, string part, HashSet<string> usedStyles)
{
    private readonly DrawingReader _drawings = new(package, part);

    public ParagraphContent Read(XElement paragraph, RunFormat paragraphRun)
    {
        var content = new ParagraphContent(paragraphRun);
        StrayText(paragraph, content);
        ReadContent(paragraph, content);

        // A field can run on into the next paragraphs, as a table of contents does; it's recorded with what it has here.
        while (content.Fields.TryPop(out var field)) EndField(field, content);
        ParagraphChanges(paragraph.Element(W + "pPr"), content);
        return content;
    }

    // Changes to the paragraph itself: its mark inserted, deleted or moved (pressing Enter, or joining two paragraphs,
    // with tracking on), recorded at its end, and its formatting changed.
    private static void ParagraphChanges(XElement? pPr, ParagraphContent content)
    {
        foreach (var mark in pPr?.Element(W + "rPr")?.Elements() ?? [])
        {
            if (MarkKinds.TryGetValue(mark.Name.LocalName, out var kind) && mark.Name.Namespace == W)
            {
                content.Inlines.Add(new DocxRevision(kind, "", Attr(mark, "author"), content.Text.Length));
            }
        }

        if (pPr?.Element(W + "pPrChange") is { } change) content.Inlines.Add(new DocxRevision("paragraph-format", "", Attr(change, "author"), 0));
    }

    private static readonly Dictionary<string, string> MarkKinds = new()
    {
        ["ins"] = "paragraph-insert", ["del"] = "paragraph-delete", ["moveTo"] = "paragraph-move-to", ["moveFrom"] = "paragraph-move-from",
    };

    // A run inside the paragraph's properties (w:pPr) isn't allowed there, and Word may drop or move it; every such run
    // is recorded, with its w:t text, apart from the paragraph's text, so the misplaced run shows.
    private static void StrayText(XElement paragraph, ParagraphContent content)
    {
        foreach (var run in paragraph.Element(W + "pPr")?.Elements(W + "r") ?? [])
        {
            content.Inlines.Add(new DocxStrayText(string.Concat(run.Elements(W + "t").Select(t => t.Value)), 0));
        }
    }

    private void ReadContent(XElement container, ParagraphContent content)
    {
        foreach (var element in container.Elements().Where(element => element.Name.Namespace == W))
        {
            if (element.Name.LocalName == "r") ReadRun(element, content);
            else if (element.Name.LocalName == "hyperlink") ReadLink(element, content);
            else if (element.Name.LocalName == "fldSimple") ReadSimpleField(element, content);
            else if (element.Name.LocalName is "ins" or "moveTo") ReadInsertion(element, content);
            else if (element.Name.LocalName is "del" or "moveFrom") ReadDeletion(element, content);
            else if (element.Name.LocalName == "sdt") ReadControl(element, content);
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

    // Inserted text is part of the paragraph and is also recorded as a tracked change, with who made it.
    private void ReadInsertion(XElement insertion, ParagraphContent content)
    {
        var (start, index) = (content.Text.Length, content.Inlines.Count);
        ReadContent(insertion, content);
        var kind = insertion.Name.LocalName == "ins" ? "insert" : "move-to";
        var text = content.Text.ToString(start, content.Text.Length - start);
        content.Inlines.Insert(index, new DocxRevision(kind, text, Attr(insertion, "author"), start));
    }

    // Deleted text (tracked deletions and moves away) isn't part of the paragraph; it's recorded where it was.
    private static void ReadDeletion(XElement deletion, ParagraphContent content)
    {
        var text = string.Concat(deletion.Descendants().Where(item => item.Name == W + "delText" || item.Name == W + "t").Select(item => item.Value));
        var kind = deletion.Name.LocalName == "del" ? "delete" : "move-from";
        content.Inlines.Add(new DocxRevision(kind, text, Attr(deletion, "author"), content.Text.Length));
    }

    // A content control in a paragraph: its kind, tag and title, and the text it shows as its value.
    private void ReadControl(XElement control, ParagraphContent content)
    {
        var (start, index) = (content.Text.Length, content.Inlines.Count);
        if (control.Element(W + "sdtContent") is { } inner) ReadContent(inner, content);
        var value = content.Text.ToString(start, content.Text.Length - start);
        content.Inlines.Insert(index, FormReader.Control(control.Element(W + "sdtPr"), value, start));
    }

    // A simple field is one element: its code and its result, the text of the runs inside it.
    private void ReadSimpleField(XElement field, ParagraphContent content)
    {
        var (start, index) = (content.Text.Length, content.Inlines.Count);
        ReadContent(field, content);
        var result = content.Text.ToString(start, content.Text.Length - start);
        content.Inlines.Insert(index, new DocxField(FieldCode(Attr(field, "instr")), result, start));
    }

    // A complex field spans runs: fldChar begin, its code in instrText, fldChar separate, the result as ordinary text,
    // then fldChar end. Fields can nest, so the open ones are kept on a stack.
    private static void ReadFieldCharacter(XElement character, int position, ParagraphContent content)
    {
        switch (Attr(character, "fldCharType"))
        {
            case "begin":
                content.Fields.Push(new OpenField(position, content.Inlines.Count, character.Element(W + "ffData")));
                break;
            case "separate" when content.Fields.TryPeek(out var field):
                field.InResult = true;
                break;
            case "end" when content.Fields.TryPop(out var field):
                EndField(field, content);
                break;
        }
    }

    // A field nested in another's code, such as PAGE in { IF { PAGE } = 1 ... }, gives that code its result. A legacy
    // form field (FORMTEXT, FORMCHECKBOX, FORMDROPDOWN) is also recorded as a form control, from its w:ffData.
    private static void EndField(OpenField field, ParagraphContent content)
    {
        if (field.FormData is { } data) content.Inlines.Insert(field.Index, FormReader.LegacyField(data, field.Result.ToString(), field.Position));
        content.Inlines.Insert(field.Index, new DocxField(FieldCode(field.Code.ToString()), field.Result.ToString(), field.Position));
        if (content.Fields.TryPeek(out var outer) && !outer.InResult) outer.Code.Append(field.Result);
    }

    private static string FieldCode(string? code) => Whitespace().Replace(code ?? "", " ").Trim();

    private void ReadRun(XElement run, ParagraphContent content)
    {
        var rPr = run.Element(W + "rPr");
        var characterStyle = Attr(rPr?.Element(W + "rStyle"), "val");
        if (characterStyle is not null) usedStyles.Add(characterStyle);
        var format = styles.Run(content.ParagraphRun, characterStyle, rPr);
        var (start, index) = (content.Text.Length, content.Inlines.Count);

        var text = new StringBuilder();

        // Text shows when no field around it is still in its code: inside a code, such as the result of a field nested
        // there, it belongs only to the innermost field. Shown text is also every open field's result.
        void Append(string piece)
        {
            if (content.Fields.Any(field => !field.InResult))
            {
                if (content.Fields.TryPeek(out var innermost) && innermost.InResult) innermost.Result.Append(piece);
                return;
            }

            text.Append(piece);
            foreach (var field in content.Fields) field.Result.Append(piece);
        }

        foreach (var item in run.Elements())
        {
            var position = content.Text.Length + text.Length;
            switch (item.Name.LocalName)
            {
                case "t":
                    Append(item.Value);
                    break;
                case "tab":
                    Append("\t");
                    break;
                case "cr":
                    Append("\n");
                    break;
                case "noBreakHyphen":
                    Append("\u2011");
                    break;
                case "softHyphen":
                    Append("\u00AD");
                    break;
                case "br" when Attr(item, "type") is "page" or "column":
                    content.Inlines.Add(new DocxBreak(Attr(item, "type")!, position));
                    break;
                case "br":
                    Append("\n");
                    break;

                // A symbol is a character from a symbol font, such as a Wingdings check mark: it's text, and its font
                // decides what it looks like.
                case "sym" when Attr(item, "char") is { } code && int.TryParse(code, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value):
                    Append(((char)value).ToString());
                    content.Inlines.Add(new DocxSymbol(Attr(item, "font"), code.ToUpperInvariant(), position));
                    break;
                case "fldChar":
                    ReadFieldCharacter(item, position, content);
                    break;
                case "instrText" when content.Fields.TryPeek(out var field):
                    field.Code.Append(item.Value);
                    break;
                case "footnoteReference" or "endnoteReference" when Integer(item, "id") is int id:
                    content.Inlines.Add(new DocxNoteReference(item.Name.LocalName == "footnoteReference" ? "footnote" : "endnote", id, position));
                    break;
                case "commentReference" when Integer(item, "id") is int id:
                    content.Inlines.Add(new DocxCommentReference(id, position));
                    break;
                case "drawing" when _drawings.Drawing(item, position) is { } image:
                    content.Inlines.Add(image);
                    break;
                case "pict" when _drawings.Picture(item, position) is { } picture:
                    content.Inlines.Add(picture);
                    break;
            }
        }

        // A run whose formatting changed with tracking on is a tracked change of its text.
        if (rPr?.Element(W + "rPrChange") is { } change) content.Inlines.Insert(index, new DocxRevision("format", text.ToString(), Attr(change, "author"), start));

        if (text.Length == 0) return;
        content.Text.Append(text);
        var spans = content.Spans;
        if (spans.Count > 0 && spans[^1].Format == format) spans[^1] = spans[^1] with { Text = spans[^1].Text + text };
        else spans.Add(new DocxSpan(text.ToString(), format));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

/// <summary>
/// A complex field that has begun and not yet ended: where it starts, its code and result so far, and the settings of a
/// legacy form field (w:ffData) when it is one.
/// </summary>
internal sealed class OpenField(int position, int index, XElement? formData)
{
    public int Position { get; } = position;
    public int Index { get; } = index;
    public XElement? FormData { get; } = formData;
    public StringBuilder Code { get; } = new();
    public StringBuilder Result { get; } = new();
    public bool InResult { get; set; }
}

/// <summary>A paragraph's content as it's read.</summary>
internal sealed class ParagraphContent(RunFormat paragraphRun)
{
    public RunFormat ParagraphRun { get; } = paragraphRun;
    public StringBuilder Text { get; } = new();
    public List<DocxSpan> Spans { get; } = [];
    public List<DocxInline> Inlines { get; } = [];
    public Stack<OpenField> Fields { get; } = [];
}
