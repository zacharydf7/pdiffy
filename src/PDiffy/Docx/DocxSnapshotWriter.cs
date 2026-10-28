using System.Text;
using static PDiffy.Docx.Reading.DocxXml;

namespace PDiffy;

/// <summary>Writes a <see cref="DocxDocument"/> in the canonical snapshot format: one item per line, LF endings.</summary>
internal sealed class DocxSnapshotWriter
{
    public const string FormatHeader = "# docx-snapshot v1";

    private const string None = "(none)";
    private const string Missing = "(missing)";

    private readonly StringBuilder _snapshot = new();
    private readonly Dictionary<string, ParagraphFormat> _styleLayouts;

    private DocxSnapshotWriter(DocxDocument document) =>
        _styleLayouts = document.Styles.Where(style => style.Type == "paragraph").ToDictionary(style => style.Id, style => style.Paragraph);

    public static string Write(DocxDocument document) => new DocxSnapshotWriter(document).Build(document);

    private string Build(DocxDocument document)
    {
        Line(0, FormatHeader);
        Line(0, "document");
        WriteDocument(document.Document);
        Line(0, "sections");
        WriteSections(document.PageSetups);
        Line(0, "styles");
        WriteStyles(document.Styles);
        Line(0, "body");
        if (document.Body.Count == 0) Line(1, None);
        WriteBlocks(document.Body, 1);
        return _snapshot.ToString();
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

    private void WriteStyles(IReadOnlyList<DocxStyle> styles)
    {
        if (styles.Count == 0) Line(1, None);
        foreach (var style in styles)
        {
            var line = new StringBuilder($"{style.Id} {style.Type}");
            Key(line, null, Quote(style.Name));
            if (style.IsDefault) line.Append(" default");
            Key(line, "based-on", style.BasedOn);
            WriteRun(line, style.Run, new RunFormat());
            WriteLayout(line, style.Paragraph, new ParagraphFormat());
            Line(1, line.ToString());
        }
    }

    private void WriteBlocks(IReadOnlyList<DocxBlock> blocks, int depth)
    {
        foreach (var block in blocks)
        {
            if (block is DocxParagraph paragraph) WriteParagraph(paragraph, depth);
            else if (block is DocxTable table) WriteTable(table, depth);
        }
    }

    // A paragraph lists how its layout differs from its style's, as a span lists how its look differs from the paragraph's.
    private void WriteParagraph(DocxParagraph paragraph, int depth)
    {
        var line = new StringBuilder("P");
        Key(line, "style", paragraph.Style);
        var styleLayout = paragraph.Style is null ? null : _styleLayouts.GetValueOrDefault(paragraph.Style);
        WriteLayout(line, paragraph.Layout, styleLayout ?? new ParagraphFormat());
        if (paragraph.Text.Length > 0) line.Append(' ').Append(Quote(paragraph.Text));
        Line(depth, line.ToString());

        foreach (var span in paragraph.Spans.Where(span => span.Format != paragraph.Run))
        {
            var spanLine = new StringBuilder($"span {Quote(span.Text)}");
            WriteRun(spanLine, span.Format, paragraph.Run);
            Line(depth + 1, spanLine.ToString());
        }

        foreach (var pageBreak in paragraph.Breaks) Line(depth + 1, $"break {pageBreak.Kind} at={pageBreak.Position}");
        if (paragraph.EndsSection is int section) Line(depth + 1, $"ends-section s{section}");
    }

    private void WriteTable(DocxTable table, int depth)
    {
        Line(depth, "Table");
        foreach (var row in table.Rows)
        {
            Line(depth + 1, "Row");
            foreach (var cell in row.Cells)
            {
                Line(depth + 2, "Cell");
                WriteBlocks(cell.Blocks, depth + 3);
            }
        }
    }

    private static void WriteRun(StringBuilder line, RunFormat format, RunFormat baseFormat)
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

    // Text is exact: backslashes and quotes are escaped, tabs and line breaks print as \t and \n, and invisible characters
    // as \uXXXX, so none can hide in a snapshot. Control characters are escaped by Line.
    private static string? Quote(string? text)
    {
        if (text is null) return null;
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
    // byte-order mark, and the replacement character that stands in for text lost on the way.
    private static bool IsInvisible(char c) =>
        c is '\u00A0' or '\u00AD' or '\u2007' or '\u202F' or '\uFEFF' or '\uFFFD'
        || c is >= '\u200B' and <= '\u200F' || c is >= '\u2060' and <= '\u2064';

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
