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
internal sealed partial class InlineReader(StyleResolver styles, DocxPackage package, string part, HashSet<string> usedStyles,
    Func<XElement, IReadOnlyList<DocxBlock>> readBlocks)
{
    private readonly DrawingReader _drawings = new(package, part, readBlocks);

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

    // Changes to the paragraph itself: its mark inserted, deleted, moved (pressing Enter, or joining two paragraphs, with
    // tracking on) or reformatted, recorded at its end, and its formatting changed.
    private static void ParagraphChanges(XElement? pPr, ParagraphContent content)
    {
        foreach (var mark in pPr?.Element(W + "rPr")?.Elements() ?? [])
        {
            if (MarkKinds.TryGetValue(mark.Name.LocalName, out var kind) && mark.Name.Namespace == W)
            {
                content.Inlines.Add(new DocxRevision
                {
                    Kind = kind, Text = "", Author = Attr(mark, "author"), Position = content.Text.Length,
                });
            }
        }

        if (pPr?.Element(W + "pPrChange") is { } change) content.Inlines.Add(new DocxRevision
        {
            Kind = DocxRevisionKind.ParagraphFormat, Text = "", Author = Attr(change, "author"), Position = 0,
        });
    }

    private static readonly Dictionary<string, DocxRevisionKind> MarkKinds = new()
    {
        ["ins"] = DocxRevisionKind.ParagraphInsert, ["del"] = DocxRevisionKind.ParagraphDelete,
        ["moveTo"] = DocxRevisionKind.ParagraphMoveTo, ["moveFrom"] = DocxRevisionKind.ParagraphMoveFrom,
        ["rPrChange"] = DocxRevisionKind.ParagraphMarkFormat,
    };

    // A run inside the paragraph's properties (w:pPr) isn't allowed there, and Word may drop or move it; every such run
    // is recorded, with its text, tabs and breaks, apart from the paragraph's text, so the misplaced run shows.
    private static void StrayText(XElement paragraph, ParagraphContent content)
    {
        foreach (var run in paragraph.Element(W + "pPr")?.Elements(W + "r") ?? [])
        {
            content.Inlines.Add(new DocxStrayText
            {
                Text = string.Concat(run.Elements().Select(StrayPiece)), Position = 0,
            });
        }
    }

    // Page and column breaks become the characters Word's own plain text uses for them.
    private static string StrayPiece(XElement item) =>
        TextOf(item) ?? (item.Name.LocalName == "br" ? Attr(item, "type") == "page" ? "\f" : "\u000E" : "");

    // The text a run's item stands for (w:ptab is a tab to a fixed place); null for anything else, such as a page break.
    private static string? TextOf(XElement item) => item.Name.LocalName switch
    {
        "t" => item.Value,
        "tab" or "ptab" => "\t",
        "cr" => "\n",
        "br" when Attr(item, "type") is not ("page" or "column") => "\n",
        "noBreakHyphen" => "\u2011",
        "softHyphen" => "\u00AD",
        _ => null,
    };

    // Paragraph content left out on purpose: properties read elsewhere, and markers that only bracket a range.
    private static readonly HashSet<string> SkippedInParagraph = [.. RangeMarkers, "pPr", "customXmlPr", "smartTagPr"];

    private void ReadContent(XElement container, ParagraphContent content)
    {
        foreach (var element in container.Elements())
        {
            var name = element.Name.LocalName;
            if (element.Name == Mc + "AlternateContent")
            {
                if (Chosen(element) is { } choice) ReadContent(choice, content);
            }
            else if (element.Name.Namespace == M && name is "oMath" or "oMathPara") ReadMath(element, content);
            else if (element.Name.Namespace != W) content.Inlines.Add(Unsupported(element, content.Text.Length));
            else if (name == "r") ReadRun(element, content);
            else if (name == "hyperlink") ReadLink(element, content);
            else if (name == "fldSimple") ReadSimpleField(element, content);
            else if (name is "ins" or "moveTo") ReadInsertion(element, content);
            else if (name is "del" or "moveFrom") ReadDeletion(element, content);
            else if (name == "sdt") ReadControl(element, content);
            else if (name == "bookmarkStart")
            {
                if (Attr(element, "name") is { } bookmark and not LastEditBookmark)
                {
                    var columns = BookmarkColumns(element);
                    content.Inlines.Add(new DocxBookmark
                    {
                        Name = bookmark, Position = content.Text.Length, FirstColumn = columns?.First,
                        LastColumn = columns?.Last,
                    });
                }
            }
            else if (IsWrapper(element)) ReadContent(element, content);
            else if (!SkippedInParagraph.Contains(name)) content.Inlines.Add(Unsupported(element, content.Text.Length));
        }
    }

    // Left out on purpose: formatting read elsewhere, Word's layout cache, note marks and separators, and deleted text.
    private static readonly HashSet<string> SkippedInRun =
    [
        "rPr", "lastRenderedPageBreak", "annotationRef", "footnoteRef", "endnoteRef", "separator", "continuationSeparator",
        "delText", "delInstrText",
    ];

    // A run's items, with each mc:AlternateContent replaced by what its first choice holds.
    private static IEnumerable<XElement> RunContent(XElement container) => container.Elements().SelectMany(item =>
        item.Name != Mc + "AlternateContent" ? [item] : Chosen(item) is { } choice ? RunContent(choice) : []);

    // An equation's text is the m:t of its runs; how it's built (fractions, powers, roots) goes into its hash.
    private static void ReadMath(XElement math, ParagraphContent content)
    {
        var text = string.Concat(math.Descendants(M + "t").Select(t => t.Value));
        content.Inlines.Add(new DocxMath { Text = text, Hash = Hash(math), Position = content.Text.Length });
    }

    private static DocxUnsupported Unsupported(XElement element, int position) => new()
    {
        Element = NameOf(element), Hash = Hash(element), Position = position,
    };

    // A link's text is the text of the runs inside it, so it's read first and the link placed before what it contains.
    // A link whose address can't be found has no target, even when it also names a bookmark.
    private void ReadLink(XElement link, ParagraphContent content)
    {
        var (start, index) = (content.Text.Length, content.Inlines.Count);
        ReadContent(link, content);
        var bookmark = Attr(link, "anchor") is { } anchor ? $"#{anchor}" : null;
        var target = (string?)link.Attribute(R + "id") is { } id ? package.Target(part, id) is { } address ? address + bookmark : null : bookmark;
        content.Inlines.Insert(index, new DocxLink
        {
            Text = content.Text.ToString(start, content.Text.Length - start), Target = target, Position = start,
        });
    }

    // Inserted text is part of the paragraph and is also recorded as a tracked change, with who made it.
    private void ReadInsertion(XElement insertion, ParagraphContent content)
    {
        var (start, index) = (content.Text.Length, content.Inlines.Count);
        ReadContent(insertion, content);
        var kind = insertion.Name.LocalName == "ins" ? DocxRevisionKind.Insert : DocxRevisionKind.MoveTo;
        var text = content.Text.ToString(start, content.Text.Length - start);
        content.Inlines.Insert(index, new DocxRevision
        {
            Kind = kind, Text = text, Author = Attr(insertion, "author"), Position = start,
        });
    }

    // Deleted text (tracked deletions and moves away) isn't part of the paragraph; it's recorded where it was.
    private static void ReadDeletion(XElement deletion, ParagraphContent content)
    {
        var text = string.Concat(deletion.Descendants().Where(item => item.Name == W + "delText" || item.Name == W + "t").Select(item => item.Value));
        var kind = deletion.Name.LocalName == "del" ? DocxRevisionKind.Delete : DocxRevisionKind.MoveFrom;
        content.Inlines.Add(new DocxRevision
        {
            Kind = kind, Text = text, Author = Attr(deletion, "author"), Position = content.Text.Length,
        });
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
        content.Inlines.Insert(index, new DocxField
        {
            Code = FieldCode(Attr(field, "instr")), Result = result, Position = start,
        });
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
        content.Inlines.Insert(field.Index, new DocxField
        {
            Code = FieldCode(field.Code.ToString()), Result = field.Result.ToString(), Position = field.Position,
        });
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

        foreach (var item in RunContent(run))
        {
            var position = content.Text.Length + text.Length;
            if (TextOf(item) is { } piece)
            {
                Append(piece);
                continue;
            }

            switch (item.Name.LocalName)
            {
                // What's left of w:br once TextOf has taken line breaks: page and column breaks.
                case "br":
                    content.Inlines.Add(new DocxBreak { Kind = Attr(item, "type")!, Position = position });
                    break;

                // A symbol is a character from a symbol font, such as a Wingdings check mark: it's text, and its font
                // decides what it looks like.
                case "sym" when Attr(item, "char") is { } code && int.TryParse(code, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value):
                    Append(((char)value).ToString());
                    content.Inlines.Add(new DocxSymbol
                    {
                        Font = Attr(item, "font"), Code = code.ToUpperInvariant(), Position = position,
                    });
                    break;
                case "sym":
                    content.Inlines.Add(new DocxSymbol
                    {
                        Font = Attr(item, "font"), Code = Attr(item, "char") ?? "", Position = position,
                    });
                    break;
                case "fldChar":
                    ReadFieldCharacter(item, position, content);
                    break;
                case "instrText" when content.Fields.TryPeek(out var field):
                    field.Code.Append(item.Value);
                    break;
                case "footnoteReference" or "endnoteReference" when Integer(item, "id") is int id:
                    content.Inlines.Add(new DocxNoteReference
                    {
                        Kind = item.Name.LocalName == "footnoteReference" ? DocxNoteKind.Footnote
                            : DocxNoteKind.Endnote,
                        Id = id, Position = position,
                    });
                    break;
                case "commentReference" when Integer(item, "id") is int id:
                    content.Inlines.Add(new DocxCommentReference { Id = id, Position = position });
                    break;
                case "drawing" when _drawings.Drawing(item, position) is { } image:
                    content.Inlines.Add(image);
                    break;
                case "pict" when _drawings.Picture(item, position) is { } picture:
                    content.Inlines.Add(picture);
                    break;
                case "object":
                    content.Inlines.Add(_drawings.Object(item, position));
                    break;
                default:
                    if (!SkippedInRun.Contains(item.Name.LocalName)) content.Inlines.Add(Unsupported(item, position));
                    break;
            }
        }

        // A run whose formatting changed with tracking on is a tracked change of its text.
        if (rPr?.Element(W + "rPrChange") is { } change) content.Inlines.Insert(index, new DocxRevision
        {
            Kind = DocxRevisionKind.Format, Text = text.ToString(), Author = Attr(change, "author"), Position = start,
        });

        if (text.Length == 0) return;
        content.Text.Append(text);
        var spans = content.Spans;
        if (spans.Count > 0 && spans[^1].Format == format) spans[^1] = spans[^1] with { Text = spans[^1].Text + text };
        else spans.Add(new DocxSpan { Text = text.ToString(), Format = format });
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
