using System.Text;
using PDiffy.Diffing;

namespace PDiffy;

/// <summary>Writes a <see cref="PdfUaDocument"/> in the canonical snapshot format: one element per line, LF endings.</summary>
internal sealed partial class SnapshotWriter(PdfSnapshotOptions options)
{
    // Every format version starts with this, so an older verified file is still known as a PDF snapshot.
    public const string FormatPrefix = "# pdfua-snapshot v";

    public const string FormatHeader = FormatPrefix + "1";

    internal const string None = "(none)";
    internal const string Ignored = "(ignored)";
    internal const string ChecksHeader = "# checks: ";
    private const string Missing = "(missing)";

    private readonly StringBuilder _snapshot = new();
    private readonly Scrubbing _scrubbing = new(options.Scrubbers);

    public static string Write(PdfUaDocument document, PdfSnapshotOptions options)
    {
        var scoped = options.Pages.Count == 0 ? document : PageScope.Keep(document, options.Pages);
        return new SnapshotWriter(options).Build(scoped, document);
    }

    // The checks look at the whole document, so their findings don't depend on which pages are printed.
    private string Build(PdfUaDocument document, PdfUaDocument whole)
    {
        Line(0, FormatHeader);
        WriteOptions();
        WriteSection(PdfSection.Document, () => WriteDocument(document.Document));
        WriteSection(PdfSection.Settings, () => WriteSettings(document.Settings));
        WriteSection(PdfSection.Metadata, () => WriteMetadata(document.Metadata));
        WriteSection(PdfSection.RoleMap, () => WriteRoleMap(document.RoleMap));
        WriteSection(PdfSection.Pages, () => WritePages(document.Pages, document.PageLabels));
        WriteSection(PdfSection.Bookmarks, () => WriteBookmarks(document.Bookmarks, document.BookmarksUnreadable));
        WriteSection(PdfSection.Structure, () => WriteStructure(document.Structure));
        WriteSection(PdfSection.Artifacts, () => WritePageTexts(document.Artifacts));
        WriteSection(PdfSection.Untagged, () => WriteUntagged(document));
        WriteSection(PdfSection.Fonts, () => WriteFonts(document.Fonts));
        WriteSection(PdfSection.Annotations, () => WriteAnnotations(document.Annotations));
        WriteSection(PdfSection.Forms, () => WriteForms(document.Form, document.FormFields));
        WriteSection(PdfSection.Layout, () => WriteLayout(document));
        if (options.CheckSet is { } set) WriteSection(PdfSection.Checks, () => WriteChecks(whole, set));
        return _snapshot.ToString();
    }

    private void WriteOptions()
    {
        if (options.IgnoredSections.Any()) Line(0, $"# ignored sections: {string.Join(", ", options.IgnoredSections.Select(Name))}");
        if (options.IgnoredFields.Any()) Line(0, $"# ignored fields: {string.Join(", ", options.IgnoredFields.Select(Name))}");
        if (options.ToleratedFields.Count > 0) Line(0, Tolerances.Header(options.ToleratedFields));
        if (options.Pages.Count > 0) Line(0, $"# only pages: {string.Join(", ", options.Pages)}");
        if (options.CheckSet is { } set) Line(0, $"{ChecksHeader}{set}");
    }

    // A finding prints as its rule, page and path, with no message text, so rewording a message never changes a
    // snapshot; findings on pages left out are left out too. A path can hold the document's own names, such as a form
    // field's, so it's scrubbed like quoted text.
    private void WriteChecks(PdfUaDocument document, PdfCheckSet set)
    {
        var findings = document.Check(set)
            .Where(finding => options.Pages.Count == 0 || finding.Page is not int page || options.Pages.Contains(page))
            .OrderBy(finding => finding.Check).ThenBy(finding => finding.Page ?? 0)
            .ThenBy(finding => finding.Path, StringComparer.Ordinal).ToList();
        if (findings.Count == 0) Line(1, None);
        foreach (var finding in findings)
        {
            var page = finding.Page is int number ? $" p{number}" : "";
            Line(1, $"{finding.Check}{page} {_scrubbing.Scrub(finding.Path)}");
        }
    }

    private void WriteSection(PdfSection section, Action write)
    {
        Line(0, Name(section));
        if (options.Ignores(section)) Line(1, Ignored);
        else write();
    }

    private void WriteDocument(PdfDocumentInfo info)
    {
        var line = new StringBuilder($"marked={YesNo(info.Marked)}");
        if (info.Suspects) line.Append(" suspects=yes");
        Key(line, PdfDetail.Language, "lang", info.Language ?? Missing);
        line.Append($" display-doc-title={YesNo(info.DisplayDocTitle)} pages={info.PageCount}");
        Line(1, line.ToString());
    }

    private void WriteMetadata(PdfMetadata metadata)
    {
        var names = new StringBuilder();
        QuotedKey(names, PdfDetail.Title, "title", metadata.Title, showMissing: true);
        QuotedKey(names, PdfDetail.Author, "author", metadata.Author, showMissing: true);
        QuotedKey(names, PdfDetail.Subject, "subject", metadata.Subject, showMissing: true);
        QuotedKey(names, PdfDetail.Keywords, "keywords", metadata.Keywords, showMissing: true);
        WriteKeys(names);
        var presence = new StringBuilder();
        Key(presence, PdfDetail.Dates, "creation-date", Present(metadata.HasCreatedDate));
        Key(presence, PdfDetail.Dates, "mod-date", Present(metadata.HasModifiedDate));
        Key(presence, PdfDetail.Producer, "producer", ProducerText(PdfDetail.Producer, metadata.Producer));
        Key(presence, PdfDetail.Creator, "creator", ProducerText(PdfDetail.Creator, metadata.Creator));
        WriteKeys(presence);
        var xmp = new StringBuilder($"pdfuaid-part={metadata.PdfUaPart ?? Missing}");
        QuotedKey(xmp, PdfDetail.Title, "xmp-title", metadata.XmpTitle, showMissing: true);
        QuotedKey(xmp, PdfDetail.Author, "xmp-creator", metadata.XmpCreator);
        QuotedKey(xmp, PdfDetail.Subject, "xmp-description", metadata.XmpDescription);
        Key(xmp, PdfDetail.Language, "xmp-language", metadata.XmpLanguage);
        Key(xmp, "pdfa", metadata.PdfAConformance);
        Line(1, metadata.XmpUnreadable ? "xmp=unreadable" : xmp.ToString());
        if (options.Ignores(PdfDetail.CustomInfo)) return;
        foreach (var entry in metadata.OtherInfo) Line(1, $"info {entry.Name}={Quote(entry.Value)}");
    }

    // The role map is a dictionary in the PDF, so its order means nothing; sort it so the snapshot is stable.
    private void WriteRoleMap(IReadOnlyList<PdfRoleMapping> roleMap)
    {
        if (roleMap.Count == 0) Line(1, None);
        foreach (var mapping in roleMap.OrderBy(r => r.Custom, StringComparer.Ordinal))
        {
            Line(1, $"{mapping.Custom} -> {mapping.Standard}");
        }
    }

    private void WritePages(IReadOnlyList<PdfPage> pages, IReadOnlyList<PdfPageLabelRange> labels)
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
            Key(line, PdfDetail.PageBoxes, "crop", BoxText(page.CropBox));
            Key(line, PdfDetail.PageBoxes, "bleed", BoxText(page.BleedBox));
            Key(line, PdfDetail.PageBoxes, "trim", BoxText(page.TrimBox));
            Key(line, PdfDetail.PageBoxes, "art", BoxText(page.ArtBox));
            Key(line, PdfDetail.PageBoxes, "user-unit", page.UserUnit?.ToString(System.Globalization.CultureInfo.InvariantCulture));
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

    private static string? BoxText(PdfBox? box) => box is null ? null : $"{box.X},{box.Y},{box.Width},{box.Height}";

    private void WriteBookmarks(IReadOnlyList<PdfBookmark> bookmarks, bool unreadable)
    {
        if (unreadable) Line(1, "unreadable");
        else if (bookmarks.Count == 0) Line(1, None);
        foreach (var bookmark in bookmarks) WriteBookmark(bookmark, 1);
    }

    private void WriteBookmark(PdfBookmark bookmark, int depth)
    {
        var line = new StringBuilder(bookmark.Page is int page ? $"{Quote(bookmark.Title)} -> p{page}" : Quote(bookmark.Title));
        QuotedKey(line, PdfDetail.LinkTargets, "uri", bookmark.Uri);
        QuotedKey(line, PdfDetail.LinkTargets, "file", bookmark.File);
        if (bookmark.Destination is { } destination)
        {
            // A page in another file is printed as dest=, so it isn't read as a page of this document.
            if (bookmark.Page is null) Key(line, PdfDetail.LinkTargets, "dest", PageText(destination));
            AppendView(line, destination);
        }

        Line(depth, line.ToString());
        foreach (var child in bookmark.Children) WriteBookmark(child, depth + 1);
    }

    // What marked content inside the element says of its own text, in reading order, with the text it covers:
    // span-lang=[fr:"bonjour"], span-expansion=["Mount":"Mt."].
    private void AppendMarkedSpans(StringBuilder line, PdfStructureElement element)
    {
        if (!options.Ignores(PdfDetail.Language)) Key(line, "span-lang", Spans(element, span => span.Language));
        if (!options.Ignores(PdfDetail.ActualText))
        {
            Key(line, "span-actual", Spans(element, span => QuoteIfPresent(span.ActualText)));
        }

        if (!options.Ignores(PdfDetail.AltText)) Key(line, "span-alt", Spans(element, span => QuoteIfPresent(span.AltText)));
        Key(line, "span-expansion", Spans(element, span => QuoteIfPresent(span.Expansion)));
    }

    // Each span's value, with the text the span covers when it covers some but not all of the element's, which the
    // line already prints: a language on an element's only MCID.
    private string? Spans(PdfStructureElement element, Func<PdfMarkedSpan, string?> valueOf) =>
        Bracketed(element.MarkedSpans.Select(span =>
            valueOf(span) is { } value && span.Text is { } text && text != element.Text
                ? $"{value}:{Quote(text)}"
                : valueOf(span)));

    private static string? Bracketed(IEnumerable<string?> values)
    {
        var present = values.OfType<string>().ToList();
        return present.Count > 0 ? $"[{string.Join(",", present)}]" : null;
    }

    private void WriteStructure(IReadOnlyList<PdfStructureElement> structure)
    {
        if (structure.Count == 0) Line(1, None);
        foreach (var element in structure) WriteElement(element, 1);
    }

    private void WriteElement(PdfStructureElement element, int depth)
    {
        var line = new StringBuilder(element.Name ?? element.Type);
        if (element.MappedType is not null) line.Append("->").Append(element.MappedType);
        if (element.FirstPage is int first)
        {
            line.Append(" p").Append(first);
            if (element.LastPage is int last && last != first) line.Append('-').Append(last);
        }

        Key(line, PdfDetail.Language, "lang", element.Language);
        QuotedKey(line, PdfDetail.AltText, "alt", element.AltText);
        QuotedKey(line, PdfDetail.ActualText, "actual", element.ActualText);
        Key(line, "expansion", QuoteIfPresent(element.Expansion));
        Key(line, "title", QuoteIfPresent(element.Title));
        Key(line, "scope", element.Scope);
        Key(line, "headers", element.Headers is null ? null : $"[{string.Join(",", element.Headers)}]");
        Key(line, "rowspan", element.RowSpan?.ToString());
        Key(line, "colspan", element.ColSpan?.ToString());
        Key(line, "numbering", element.ListNumbering);
        Key(line, "summary", QuoteIfPresent(element.Summary));
        Key(line, PdfDetail.ImageContents, "images", Bracketed(element.ImageHashes));
        foreach (var annotation in element.Annotations)
        {
            line.Append(" annot=").Append(annotation.Subtype);
            AppendAnnotationDetails(line, annotation);
        }

        AppendMarkedSpans(line, element);
        Key(line, "mcids-without-page", element.McidsWithoutPage > 0 ? element.McidsWithoutPage.ToString() : null);
        Key(line, "mcids-in-xobject", element.McidsInXObject > 0 ? element.McidsInXObject.ToString() : null);
        Key(line, "unread-children", element.UnreadChildren > 0 ? element.UnreadChildren.ToString() : null);
        if (element.ParentMissing) line.Append(" parent=missing");
        if (element.Text is not null) line.Append(' ').Append(Quote(element.Text));
        Line(depth, line.ToString());
        foreach (var child in element.Children) WriteElement(child, depth + 1);
    }

    private void WritePageTexts(IReadOnlyList<PdfPageText> texts)
    {
        if (texts.Count == 0) Line(1, None);
        foreach (var text in texts)
        {
            var line = new StringBuilder($"p{text.Page}");
            Key(line, "type", text.Type);
            Key(line, "subtype", text.Subtype);
            Line(1, line.Append(' ').Append(Quote(text.Text)).ToString());
        }
    }

    // Untagged text, then a count of untagged images and paths, page by page. The layout section lists each graphic.
    private void WriteUntagged(PdfUaDocument document)
    {
        var texts = document.Untagged.ToLookup(text => text.Page);
        var images = document.Images.Where(image => image.Role == PdfContentRole.Untagged).ToLookup(image => image.Page);
        var paths = document.Paths.Where(path => path.Role == PdfContentRole.Untagged).ToLookup(path => path.Page);
        var pages = texts.Select(page => page.Key).Concat(images.Select(page => page.Key))
            .Concat(paths.Select(page => page.Key)).Distinct().Order().ToList();
        if (pages.Count == 0) Line(1, None);
        foreach (var page in pages)
        {
            foreach (var text in texts[page]) Line(1, $"p{page} {Quote(text.Text)}");
            var imageCount = images[page].Count();
            var pathCount = paths[page].Count();
            if (imageCount > 0 || pathCount > 0) Line(1, $"p{page} images={imageCount} paths={pathCount}");
        }
    }

    // Two font objects can print the same line (two subsets of Arial), in no useful order; print each line once, sorted.
    private void WriteFonts(IReadOnlyList<PdfFont> fonts)
    {
        var lines = fonts.Select(FormatFont).Distinct().Order(StringComparer.Ordinal).ToList();
        if (lines.Count == 0) Line(1, None);
        foreach (var line in lines) Line(1, line);
    }

    private static string FormatFont(PdfFont font)
    {
        var subtype = font.Subtype is null ? "" : " " + font.Subtype;
        var line = $"{font.Name}{subtype} embedded={YesNo(font.Embedded)} to-unicode={YesNo(font.HasToUnicode)}";
        var width = font.Subtype == PdfFontTypes.Type0 ? 4 : 2;
        return font.UnmappedCodes.Count == 0 ? line : $"{line} unmapped=[{CodeRanges(font.UnmappedCodes, width)}]";
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

    private void Key(StringBuilder line, PdfDetail field, string key, string? value)
    {
        if (!options.Ignores(field)) Key(line, key, value);
    }

    // Quotes only what is printed, so an ignored value never uses up a Date_N or Guid_N token.
    private void QuotedKey(StringBuilder line, PdfDetail field, string key, string? value, bool showMissing = false)
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
    private string ProducerText(PdfDetail field, string? value)
    {
        var show = options.ShowsProducerAndCreator && value is not null && !options.Ignores(field);
        return show ? Quote(value) : Present(value is not null);
    }

    private static string YesNo(bool b) => b ? "yes" : "no";

    private static string Present(bool b) => b ? "<present>" : "<missing>";
}
