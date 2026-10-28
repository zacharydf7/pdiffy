using System.Text;
using static PDiffy.Docx.Reading.DocxXml;

namespace PDiffy;

/// <summary>Writes a <see cref="DocxDocument"/> in the canonical snapshot format: one item per line, LF endings.</summary>
internal sealed class DocxSnapshotWriter
{
    public const string FormatHeader = "# docx-snapshot v1";

    private const string None = "(none)";
    private const string Missing = "(missing)";

    private readonly StringBuilder _snapshot;
    private readonly DocxSnapshotOptions _options;
    private readonly Scrubbing _scrubbing;
    private readonly Dictionary<string, ParagraphFormat> _styleLayouts;

    private DocxSnapshotWriter(DocxDocument document, StringBuilder snapshot, DocxSnapshotOptions options, Scrubbing scrubbing)
    {
        (_snapshot, _options, _scrubbing) = (snapshot, options, scrubbing);
        _styleLayouts = document.Styles.Where(style => style.Type == "paragraph").ToDictionary(style => style.Id, style => style.Paragraph);
    }

    public static string Write(DocxDocument document, DocxSnapshotOptions options) =>
        new DocxSnapshotWriter(document, new StringBuilder(), options, new Scrubbing(options.Scrubbers)).Build(document);

    private string Build(DocxDocument document)
    {
        Line(0, FormatHeader);
        if (_options.IgnoredSections.Any()) Line(0, $"# ignored sections: {string.Join(", ", _options.IgnoredSections.Select(SnapshotWriter.Name))}");
        WriteSection(DocxSection.Document, () => WriteDocument(document.Document));
        WriteSection(DocxSection.Sections, () => WriteSections(document.PageSetups));
        WriteSection(DocxSection.Styles, () => WriteStyles(document.Styles, 1));
        WriteSection(DocxSection.Numbering, () => WriteLists(document.Lists, 1));
        WriteSection(DocxSection.Body, () =>
        {
            if (document.Body.Count == 0) Line(1, None);
            WriteBlocks(document.Body, 1);
        });
        WriteSection(DocxSection.Headers, () => WriteHeadersAndFooters(document.Headers));
        WriteSection(DocxSection.Footers, () => WriteHeadersAndFooters(document.Footers));
        return _snapshot.ToString();
    }

    private void WriteSection(DocxSection section, Action write)
    {
        Line(0, SnapshotWriter.Name(section));
        if (_options.Ignores(section)) Line(1, "(ignored)");
        else write();
    }

    private void WriteDocument(DocxInfo info)
    {
        var line = new StringBuilder();
        Key(line, "title", Quote(info.Title));
        Key(line, "subject", Quote(info.Subject));
        Key(line, "lang", info.Language ?? Missing);
        Key(line, "compat", info.CompatibilityMode?.ToString() ?? Missing);
        Key(line, "track-changes", YesNo(info.TrackChanges));
        Key(line, "protection", info.Protection ?? "none");
        Key(line, "even-odd-headers", YesNo(info.EvenAndOddHeaders));
        Key(line, "created", Present(info.HasCreatedDate));
        Key(line, "modified", Present(info.HasModifiedDate));
        Line(1, line.ToString().TrimStart());
        if (info.UnreadableParts.Count > 0) Line(1, $"unreadable {string.Join(", ", info.UnreadableParts)}");
    }

    private void WriteSections(IReadOnlyList<PageSetup> setups)
    {
        if (setups.Count == 0) Line(1, None);
        for (var i = 0; i < setups.Count; i++)
        {
            var setup = setups[i];
            var size = setup.Width > 0 && setup.Height > 0 ? PageSize.Describe(setup.Width, setup.Height) : Missing;
            var line = new StringBuilder($"s{i + 1} {size}");
            var margins = new[] { setup.MarginTop, setup.MarginRight, setup.MarginBottom, setup.MarginLeft };
            Key(line, "margins", string.Join(",", margins.Select(Points)));
            Key(line, "header", Points(setup.HeaderDistance));
            Key(line, "footer", Points(setup.FooterDistance));
            if (setup.Gutter != 0) Key(line, "gutter", Points(setup.Gutter));
            Key(line, "columns", setup.Columns.ToString());
            Key(line, "break", setup.Break);
            Key(line, "page-start", setup.PageNumberStart?.ToString());
            Key(line, "page-format", setup.PageNumberFormat);
            if (setup.TitlePage) line.Append(" title-page");
            Key(line, "headers", setup.Headers.Count > 0 ? string.Join(",", setup.Headers) : null);
            Key(line, "footers", setup.Footers.Count > 0 ? string.Join(",", setup.Footers) : null);
            Line(1, line.ToString());
        }
    }

    private void WriteStyles(IReadOnlyList<DocxStyle> styles, int depth)
    {
        if (styles.Count == 0) Line(depth, None);
        foreach (var style in styles)
        {
            var line = new StringBuilder($"{style.Id} {style.Type}");
            Key(line, null, Quote(style.Name));
            if (style.IsDefault) line.Append(" default");
            Key(line, "based-on", style.BasedOn);
            WriteRun(line, style.Run, new RunFormat());
            WriteLayout(line, style.Paragraph, new ParagraphFormat());
            Line(depth, line.ToString());
        }
    }

    private void WriteLists(IReadOnlyList<DocxList> lists, int depth)
    {
        if (lists.Count == 0) Line(depth, None);
        foreach (var list in lists)
        {
            Line(depth, $"list {list.Id} definition={list.Definition}");
            foreach (var level in list.Levels)
            {
                if (level.Format is null)
                {
                    Line(depth + 1, $"level {level.Level} {Missing}");
                    continue;
                }

                var line = new StringBuilder($"level {level.Level} format={level.Format}");
                Key(line, "text", Quote(level.Text));
                Key(line, "start", level.Start?.ToString());
                Key(line, "indent", Length(level.Indent));
                Key(line, "hanging", Length(level.Hanging));
                Key(line, "font", Quote(level.Font));
                Line(depth + 1, line.ToString());
            }
        }
    }

    private void WriteBlocks(IReadOnlyList<DocxBlock> blocks, int depth)
    {
        foreach (var block in blocks)
        {
            if (block is DocxParagraph paragraph) WriteParagraph(paragraph, depth);
            else if (block is DocxTable table) WriteTable(table, depth);
            else if (block is DocxInserted inserted) WriteInserted(inserted, depth);
        }
    }

    private void WriteHeadersAndFooters(IReadOnlyList<DocxHeaderFooter> items)
    {
        if (items.Count == 0) Line(1, None);
        foreach (var item in items)
        {
            Line(1, $"s{item.Section} {item.Kind}{(item.Blocks is null ? $" {Missing}" : "")}");
            WriteBlocks(item.Blocks ?? [], 2);
        }
    }

    // An inserted DOCX is read with its own styles and lists, so its body is written against them, under its own styles
    // and numbering; anything else is written by hash.
    private void WriteInserted(DocxInserted inserted, int depth)
    {
        var line = new StringBuilder($"Inserted {inserted.Kind ?? "(unknown)"}");
        if (inserted.Unreadable) line.Append(" unreadable");
        if (inserted.Document is null) Key(line, "hash", inserted.Hash ?? Missing);
        Line(depth, line.ToString());
        if (inserted.Document is not { } document) return;

        var writer = new DocxSnapshotWriter(document, _snapshot, _options, _scrubbing);
        writer.Line(depth + 1, "styles");
        if (_options.Ignores(DocxSection.Styles)) writer.Line(depth + 2, "(ignored)");
        else writer.WriteStyles(document.Styles, depth + 2);
        writer.Line(depth + 1, "numbering");
        if (_options.Ignores(DocxSection.Numbering)) writer.Line(depth + 2, "(ignored)");
        else writer.WriteLists(document.Lists, depth + 2);
        writer.Line(depth + 1, "body");
        if (document.Body.Count == 0) writer.Line(depth + 2, None);
        writer.WriteBlocks(document.Body, depth + 2);
    }

    // A paragraph lists how its layout differs from its style's, as a span lists how its look differs from the paragraph's.
    private void WriteParagraph(DocxParagraph paragraph, int depth)
    {
        var line = new StringBuilder("P");
        Key(line, "style", paragraph.Style);
        Key(line, "list", paragraph.List is { } item ? $"{item.List}:{item.Level}" : null);
        Key(line, "label", Quote(paragraph.List?.Label));
        var styleLayout = paragraph.Style is null ? null : _styleLayouts.GetValueOrDefault(paragraph.Style);
        WriteLayout(line, paragraph.Layout, styleLayout ?? new ParagraphFormat());
        // The text is scrubbed once, with the positions of what sits in it, so those still point into the printed text.
        var positions = paragraph.Inlines.Select(inline => inline.Position).ToArray();
        var text = _scrubbing.Scrub(paragraph.Text, positions);
        if (text.Length > 0) line.Append(' ').Append(Escaped(text));
        Line(depth, line.ToString());

        foreach (var span in paragraph.Spans.Where(span => span.Format != paragraph.Run))
        {
            var spanLine = new StringBuilder($"span {Quote(span.Text)}");
            WriteRun(spanLine, span.Format, paragraph.Run);
            Line(depth + 1, spanLine.ToString());
        }

        for (var i = 0; i < positions.Length; i++) Line(depth + 1, InlineLine(paragraph.Inlines[i], positions[i]));
        if (paragraph.EndsSection is int section) Line(depth + 1, $"ends-section s{section}");
    }

    // An image always says whether it has alt text, since a missing description is what an accessibility check looks for.
    private string InlineLine(DocxInline inline, int position)
    {
        var line = new StringBuilder();
        switch (inline)
        {
            case DocxBreak pageBreak:
                line.Append($"break {pageBreak.Kind}");
                break;
            case DocxLink link:
                line.Append($"link {Quote(link.Text)} -> {(link.Target is null ? Missing : _scrubbing.Scrub(link.Target))}");
                break;
            case DocxField field:
                line.Append($"field {Quote(field.Code)}");
                Key(line, "result", Quote(field.Result));
                break;
            case DocxBookmark bookmark:
                line.Append($"bookmark {Quote(bookmark.Name)}");
                break;
            case DocxSymbol symbol:
                line.Append("symbol");
                Key(line, "font", Quote(symbol.Font) ?? Missing);
                Key(line, "code", symbol.Code);
                break;
            case DocxImage image:
                line.Append(image.Kind).Append(image.Floating ? " floating" : " inline");
                Key(line, "size", $"{Points(image.Width)}x{Points(image.Height)}");
                Key(line, "alt", Quote(image.AltText) ?? Missing);
                Key(line, "title", Quote(image.Title));
                if (image.Decorative) line.Append(" decorative");
                Key(line, "type", image.Type);
                if (image.Kind == "image") Key(line, "hash", image.Hash ?? Missing);
                break;
        }

        return line.Append($" at={position}").ToString();
    }

    private void WriteTable(DocxTable table, int depth)
    {
        var line = new StringBuilder("Table");
        Key(line, "style", table.Style);
        Key(line, "widths", table.Widths.Count > 0 ? string.Join(",", table.Widths.Select(Points)) : null);
        Line(depth, line.ToString());
        foreach (var row in table.Rows)
        {
            Line(depth + 1, row.IsHeader ? "Row header" : "Row");
            foreach (var cell in row.Cells)
            {
                var cellLine = new StringBuilder("Cell");
                if (cell.Span != 1) Key(cellLine, "span", cell.Span.ToString());
                Key(cellLine, "merge", cell.Merge);
                Key(cellLine, "shading", cell.Shading);
                Line(depth + 2, cellLine.ToString());
                WriteBlocks(cell.Blocks, depth + 3);
            }
        }
    }

    private void WriteRun(StringBuilder line, RunFormat format, RunFormat baseFormat)
    {
        Changed(line, "char-style", format.Style, baseFormat.Style, Missing);
        if (format.Font != baseFormat.Font) Key(line, "font", Quote(format.Font) ?? Missing);
        Changed(line, "size", Length(format.Size), Length(baseFormat.Size), Missing);
        Flag(line, "bold", format.Bold, baseFormat.Bold);
        Flag(line, "italic", format.Italic, baseFormat.Italic);
        Changed(line, "underline", format.Underline, baseFormat.Underline, "none");
        Changed(line, "color", format.Color, baseFormat.Color, "auto");
        Changed(line, "highlight", format.Highlight, baseFormat.Highlight, "none");
        Flag(line, "caps", format.Caps, baseFormat.Caps);
        Flag(line, "small-caps", format.SmallCaps, baseFormat.SmallCaps);
        Flag(line, "strike", format.Strike, baseFormat.Strike);
        Changed(line, "vertical", format.VerticalAlign, baseFormat.VerticalAlign, "baseline");
    }

    private static void WriteLayout(StringBuilder line, ParagraphFormat format, ParagraphFormat baseFormat)
    {
        Changed(line, "heading", format.HeadingLevel?.ToString(), baseFormat.HeadingLevel?.ToString(), "none");
        Changed(line, "align", format.Align, baseFormat.Align, Missing);
        Changed(line, "indent-left", Length(format.IndentLeft), Length(baseFormat.IndentLeft), Missing);
        Changed(line, "indent-right", Length(format.IndentRight), Length(baseFormat.IndentRight), Missing);
        Changed(line, "first-line", Length(format.FirstLine), Length(baseFormat.FirstLine), "none");
        Changed(line, "hanging", Length(format.Hanging), Length(baseFormat.Hanging), "none");
        Changed(line, "spacing-before", Length(format.SpacingBefore), Length(baseFormat.SpacingBefore), Missing);
        Changed(line, "spacing-after", Length(format.SpacingAfter), Length(baseFormat.SpacingAfter), Missing);
        Changed(line, "line", format.LineSpacing?.Replace(' ', '-'), baseFormat.LineSpacing?.Replace(' ', '-'), Missing);
        Flag(line, "keep-next", format.KeepNext, baseFormat.KeepNext);
        Flag(line, "keep-lines", format.KeepLines, baseFormat.KeepLines);
        Flag(line, "page-break-before", format.PageBreakBefore, baseFormat.PageBreakBefore);
        if (!format.Tabs.SequenceEqual(baseFormat.Tabs)) Key(line, "tabs", format.Tabs.Count > 0 ? string.Join(",", format.Tabs) : "none");
    }

    // A value prints when it differs from the base's; one that was cleared prints what clearing means, such as "none".
    private static void Changed(StringBuilder line, string key, string? value, string? baseValue, string cleared)
    {
        if (value != baseValue) Key(line, key, value ?? cleared);
    }

    // A flag prints bare when it turns on and as "=no" when it turns off what the base had on.
    private static void Flag(StringBuilder line, string name, bool value, bool baseValue)
    {
        if (value != baseValue) line.Append(' ').Append(value ? name : $"{name}=no");
    }

    private static void Key(StringBuilder line, string? key, string? value)
    {
        if (value is null) return;
        line.Append(' ');
        if (key is not null) line.Append(key).Append('=');
        line.Append(value);
    }

    private static string? Length(double? points) => points is double value ? Points(value) : null;

    // Text is exact apart from what the options scrub: backslashes and quotes are escaped, tabs and line breaks print as
    // \t and \n, and invisible characters as \uXXXX, so none can hide in a snapshot. Control characters are escaped by Line.
    private string? Quote(string? text) => text is null ? null : Escaped(_scrubbing.Scrub(text));

    private static string Escaped(string text)
    {
        var quoted = new StringBuilder("\"");
        foreach (var c in text)
        {
            quoted.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\t' => "\\t",
                '\n' => "\\n",
                _ when IsInvisible(c) => Escape(c),
                _ => c.ToString(),
            });
        }

        return quoted.Append('"').ToString();
    }

    // Spaces and marks a reader can't see: no-break and narrow spaces, the soft hyphen, zero-width characters, the
    // byte-order mark, the replacement character that stands in for text lost on the way, and private-use characters,
    // such as Symbol font bullets, which show only in the font they were made for.
    private static bool IsInvisible(char c) =>
        c is '\u00A0' or '\u00AD' or '\u2007' or '\u202F' or '\uFEFF' or '\uFFFD'
        || c is >= '\u200B' and <= '\u200F' || c is >= '\u2060' and <= '\u2064' || c is >= '\uE000' and <= '\uF8FF';

    private static string Escape(char c) => $"\\u{(int)c:x4}";

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static string Present(bool value) => value ? "<present>" : "<missing>";

    // Control characters are escaped here, for quoted and unquoted values alike, so a value can never start a new line.
    private void Line(int depth, string text)
    {
        _snapshot.Append(' ', depth * 2);
        foreach (var c in text) _snapshot.Append(char.IsControl(c) ? Escape(c) : c.ToString());
        _snapshot.Append('\n');
    }
}
