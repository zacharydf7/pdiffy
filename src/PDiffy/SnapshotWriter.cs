using System.Text;

namespace PDiffy;

/// <summary>Writes a <see cref="PdfUaDocument"/> in the canonical snapshot format: one element per line, LF endings.</summary>
internal sealed partial class SnapshotWriter(SnapshotOptions options)
{
    // Every format version starts with this, so an older verified file is still known as a PDF snapshot.
    public const string FormatPrefix = "# pdfua-snapshot v";

    public const string FormatHeader = FormatPrefix + "1";

    private const string None = "(none)";
    private const string Missing = "(missing)";

    private readonly StringBuilder _snapshot = new();
    private readonly Scrubbing _scrubbing = new(options.Scrubbers);

    public static string Write(PdfUaDocument document, SnapshotOptions options)
    {
        var scoped = options.Pages.Count == 0 ? document : PageScope.Keep(document, options.Pages);
        return new SnapshotWriter(options).Build(scoped);
    }

    private string Build(PdfUaDocument document)
    {
        Line(0, FormatHeader);
        WriteOptions();
        WriteSection(Section.Document, () => WriteDocument(document.Document));
        WriteSection(Section.Settings, () => WriteSettings(document.Settings));
        WriteSection(Section.Metadata, () => WriteMetadata(document.Metadata));
        WriteSection(Section.RoleMap, () => WriteRoleMap(document.RoleMap));
        WriteSection(Section.Pages, () => WritePages(document.Pages, document.PageLabels));
        WriteSection(Section.Bookmarks, () => WriteBookmarks(document.Bookmarks));
        WriteSection(Section.Structure, () => WriteStructure(document.Structure));
        WriteSection(Section.Artifacts, () => WritePageTexts(document.Artifacts));
        WriteSection(Section.Untagged, () => WriteUntagged(document));
        WriteSection(Section.Fonts, () => WriteFonts(document.Fonts));
        WriteSection(Section.Annotations, () => WriteAnnotations(document.Annotations));
        WriteSection(Section.Forms, () => WriteFormFields(document.FormFields));
        WriteSection(Section.Layout, () => WriteLayout(document));
        return _snapshot.ToString();
    }

    private void WriteOptions()
    {
        if (options.IgnoredSections.Any()) Line(0, $"# ignored sections: {string.Join(", ", options.IgnoredSections.Select(Name))}");
        if (options.IgnoredFields.Any()) Line(0, $"# ignored fields: {string.Join(", ", options.IgnoredFields.Select(Name))}");
        if (options.Pages.Count > 0) Line(0, $"# only pages: {string.Join(", ", options.Pages)}");
    }

    private void WriteSection(Section section, Action write)
    {
        Line(0, Name(section));
        if (options.Ignores(section)) Line(1, "(ignored)");
        else write();
    }

    private void WriteDocument(DocumentInfo info)
    {
        var line = new StringBuilder($"marked={YesNo(info.Marked)}");
        Key(line, Field.Language, "lang", info.Lang ?? Missing);
        line.Append($" display-doc-title={YesNo(info.DisplayDocTitle)} pages={info.PageCount}");
        Line(1, line.ToString());
    }

    private void WriteMetadata(Metadata metadata)
    {
        var names = new StringBuilder();
        QuotedKey(names, Field.Title, "title", metadata.Title, showMissing: true);
        QuotedKey(names, Field.Author, "author", metadata.Author, showMissing: true);
        QuotedKey(names, Field.Subject, "subject", metadata.Subject, showMissing: true);
        QuotedKey(names, Field.Keywords, "keywords", metadata.Keywords, showMissing: true);
        WriteKeys(names);
        var presence = new StringBuilder();
        Key(presence, Field.Dates, "creation-date", Present(metadata.HasCreationDate));
        Key(presence, Field.Dates, "mod-date", Present(metadata.HasModDate));
        Key(presence, Field.Producer, "producer", ProducerText(Field.Producer, metadata.Producer));
        Key(presence, Field.Creator, "creator", ProducerText(Field.Creator, metadata.Creator));
        WriteKeys(presence);
        var xmp = new StringBuilder($"pdfuaid-part={metadata.PdfUaPart ?? Missing}");
        QuotedKey(xmp, Field.Title, "xmp-title", metadata.XmpTitle, showMissing: true);
        QuotedKey(xmp, Field.Author, "xmp-creator", metadata.XmpCreator);
        QuotedKey(xmp, Field.Subject, "xmp-description", metadata.XmpDescription);
        Key(xmp, Field.Language, "xmp-language", metadata.XmpLanguage);
        Key(xmp, "pdfa", metadata.PdfAConformance);
        Line(1, xmp.ToString());
        if (options.Ignores(Field.CustomInfo)) return;
        foreach (var entry in metadata.OtherInfo) Line(1, $"info {entry.Name}={Quote(entry.Value)}");
    }

    // The role map is a dictionary in the PDF, so its order means nothing; sort it so the snapshot is stable.
    private void WriteRoleMap(IReadOnlyList<RoleMapping> roleMap)
    {
        if (roleMap.Count == 0) Line(1, None);
        foreach (var mapping in roleMap.OrderBy(r => r.Custom, StringComparer.Ordinal))
        {
            Line(1, $"{mapping.Custom} -> {mapping.Standard}");
        }
    }

    private void WritePages(IReadOnlyList<PageInfo> pages, IReadOnlyList<PageLabelRange> labels)
    {
        if (pages.Count == 0 && labels.Count == 0) Line(1, None);
        foreach (var page in pages)
        {
            if (page.Unreadable)
            {
                Line(1, $"p{page.Number} unreadable");
                continue;
            }

            var line = new StringBuilder($"p{page.Number} {PageSize.Describe(page.Width, page.Height)} rotate={page.Rotation}");
            if (page.Tabs is not null || page.HasAnnotations) line.Append($" tabs={page.Tabs ?? Missing}");
            Key(line, Field.PageBoxes, "crop", BoxText(page.CropBox));
            Key(line, Field.PageBoxes, "bleed", BoxText(page.BleedBox));
            Key(line, Field.PageBoxes, "trim", BoxText(page.TrimBox));
            Key(line, Field.PageBoxes, "art", BoxText(page.ArtBox));
            Key(line, Field.PageBoxes, "user-unit", page.UserUnit?.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (page.Group is { } group)
            {
                Key(line, "group", group.Type ?? Missing);
                Key(line, "group-color-space", group.ColorSpace);
                if (group.Isolated) line.Append(" isolated=yes");
                if (group.Knockout) line.Append(" knockout=yes");
            }

            Line(1, line.ToString());
        }

        foreach (var label in labels)
        {
            Line(1, $"label from p{label.FirstPage} style={label.Style ?? "none"} prefix={Quote(label.Prefix)} start={label.Start}");
        }
    }

    private static string? BoxText(PageBox? box) => box is null ? null : $"{box.X},{box.Y},{box.Width},{box.Height}";

    private void WriteBookmarks(IReadOnlyList<Bookmark> bookmarks)
    {
        if (bookmarks.Count == 0) Line(1, None);
        foreach (var bookmark in bookmarks) WriteBookmark(bookmark, 1);
    }

    private void WriteBookmark(Bookmark bookmark, int depth)
    {
        var line = bookmark.Page is int page ? $"{Quote(bookmark.Title)} -> p{page}" : Quote(bookmark.Title);
        Line(depth, line);
        foreach (var child in bookmark.Children) WriteBookmark(child, depth + 1);
    }

    private void WriteStructure(IReadOnlyList<StructureElement> structure)
    {
        if (structure.Count == 0) Line(1, None);
        foreach (var element in structure) WriteElement(element, 1);
    }

    private void WriteElement(StructureElement element, int depth)
    {
        var line = new StringBuilder(element.Name ?? element.Type);
        if (element.MappedType is not null) line.Append("->").Append(element.MappedType);
        if (element.FirstPage is int first)
        {
            line.Append(" p").Append(first);
            if (element.LastPage is int last && last != first) line.Append('-').Append(last);
        }

        Key(line, Field.Language, "lang", element.Lang);
        QuotedKey(line, Field.AltText, "alt", element.Alt);
        QuotedKey(line, Field.ActualText, "actual", element.ActualText);
        Key(line, "expansion", QuoteIfPresent(element.Expansion));
        Key(line, "title", QuoteIfPresent(element.Title));
        Key(line, "scope", element.Scope);
        Key(line, "headers", element.Headers is null ? null : $"[{string.Join(",", element.Headers)}]");
        Key(line, "rowspan", element.RowSpan?.ToString());
        Key(line, "colspan", element.ColSpan?.ToString());
        Key(line, "numbering", element.ListNumbering);
        Key(line, "summary", QuoteIfPresent(element.Summary));
        foreach (var annotation in element.Annotations)
        {
            line.Append(" annot=").Append(annotation.Subtype);
            AppendAnnotationDetails(line, annotation);
        }

        Key(line, "mcids-without-page", element.McidsWithoutPage > 0 ? element.McidsWithoutPage.ToString() : null);
        if (element.Text is not null) line.Append(' ').Append(Quote(element.Text));
        Line(depth, line.ToString());
        foreach (var child in element.Children) WriteElement(child, depth + 1);
    }

    private void WritePageTexts(IReadOnlyList<PageText> texts)
    {
        if (texts.Count == 0) Line(1, None);
        foreach (var text in texts) Line(1, $"p{text.Page} {Quote(text.Text)}");
    }

    // Untagged text, then a count of untagged images and paths, page by page. The layout section lists each graphic.
    private void WriteUntagged(PdfUaDocument document)
    {
        var images = document.Images.Where(image => image.Role == ContentRole.Untagged).ToList();
        var paths = document.Paths.Where(path => path.Role == ContentRole.Untagged).ToList();
        var pages = document.Untagged.Select(text => text.Page).Concat(images.Select(image => image.Page))
            .Concat(paths.Select(path => path.Page)).Distinct().Order().ToList();
        if (pages.Count == 0) Line(1, None);
        foreach (var page in pages)
        {
            foreach (var text in document.Untagged.Where(text => text.Page == page)) Line(1, $"p{page} {Quote(text.Text)}");
            var imageCount = images.Count(image => image.Page == page);
            var pathCount = paths.Count(path => path.Page == page);
            if (imageCount > 0 || pathCount > 0) Line(1, $"p{page} images={imageCount} paths={pathCount}");
        }
    }

    // Two font objects can print the same line (two subsets of Arial), in no useful order; print each line once, sorted.
    private void WriteFonts(IReadOnlyList<FontInfo> fonts)
    {
        var lines = fonts.Select(FormatFont).Distinct().Order(StringComparer.Ordinal).ToList();
        if (lines.Count == 0) Line(1, None);
        foreach (var line in lines) Line(1, line);
    }

    private static string FormatFont(FontInfo font)
    {
        var subtype = font.Subtype is null ? "" : " " + font.Subtype;
        var line = $"{font.Name}{subtype} embedded={YesNo(font.Embedded)} to-unicode={YesNo(font.HasToUnicode)}";
        return font.UnmappedCodes.Count == 0 ? line : $"{line} unmapped=[{CodeRanges(font.UnmappedCodes, font.Subtype == "Type0" ? 4 : 2)}]";
    }

    // Hex codes, with runs of consecutive codes shortened to first-last: [0041-0043,0050].
    private static string CodeRanges(IReadOnlyList<int> codes, int digits)
    {
        var ranges = new List<string>();
        for (var i = 0; i < codes.Count; i++)
        {
            var first = codes[i];
            while (i + 1 < codes.Count && codes[i + 1] == codes[i] + 1) i++;
            var hex = (int code) => code.ToString($"x{digits}");
            ranges.Add(codes[i] == first ? hex(first) : $"{hex(first)}-{hex(codes[i])}");
        }

        return string.Join(",", ranges);
    }

    // Control characters are escaped here, for quoted and unquoted values alike, so a value can never start a new line.
    private void Line(int depth, string text)
    {
        _snapshot.Append(' ', depth * 2);
        foreach (var c in text)
        {
            if (char.IsControl(c)) _snapshot.Append($"\\u{(int)c:x4}");
            else _snapshot.Append(c);
        }

        _snapshot.Append('\n');
    }

    private static void Key(StringBuilder line, string key, string? value)
    {
        if (value is not null) line.Append(' ').Append(key).Append('=').Append(value);
    }

    private void Key(StringBuilder line, Field field, string key, string? value)
    {
        if (!options.Ignores(field)) Key(line, key, value);
    }

    // Quotes only what is printed, so an ignored value never uses up a Date_N or Guid_N token.
    private void QuotedKey(StringBuilder line, Field field, string key, string? value, bool showMissing = false)
    {
        if (!options.Ignores(field)) Key(line, key, showMissing ? Quote(value) : QuoteIfPresent(value));
    }

    // Writes a line made only of keys, which is empty when every key was ignored.
    private void WriteKeys(StringBuilder keys)
    {
        if (keys.Length > 0) Line(1, keys.ToString().TrimStart());
    }

    // Section and field names as the snapshot prints them, e.g. "role-map" or "pdf-version".
    internal static string Name<T>(T value) where T : struct, Enum => KebabCase(value.ToString());

    private string? QuoteIfPresent(string? s) => s is null ? null : Quote(s);

    private string Quote(string? s)
    {
        if (s is null) return Missing;
        return "\"" + _scrubbing.Scrub(s).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    // The producer and creator change with every upgrade, so only their presence prints unless the test asks for them.
    private string ProducerText(Field field, string? value)
    {
        var show = options.ShowsProducerAndCreator && value is not null && !options.Ignores(field);
        return show ? Quote(value) : Present(value is not null);
    }

    private static string YesNo(bool b) => b ? "yes" : "no";

    private static string Present(bool b) => b ? "<present>" : "<missing>";
}
