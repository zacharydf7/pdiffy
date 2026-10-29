namespace PDiffy;

/// <summary>Applies <see cref="PdfCheck"/> rules to a document's model, one method per rule.</summary>
internal sealed class PdfChecker(PdfUaDocument document)
{
    private readonly List<CheckFinding> _findings = [];
    private List<(StructureElement Element, string Path)>? _elements;

    public static IReadOnlyList<CheckFinding> Run(PdfUaDocument document, IEnumerable<PdfCheck> checks)
    {
        var checker = new PdfChecker(document);
        foreach (var check in checks.Distinct().Order()) checker.Apply(check);
        return checker._findings;
    }

    private void Apply(PdfCheck check)
    {
        switch (check)
        {
            case PdfCheck.DocumentLanguage: DocumentLanguage(); break;
            case PdfCheck.DocumentTitle: DocumentTitle(); break;
            case PdfCheck.FigureAltText: FigureAltText(); break;
            case PdfCheck.UntaggedContent: UntaggedContent(); break;
            case PdfCheck.LinkDescriptions: LinkDescriptions(); break;
            case PdfCheck.FormFieldNames: FormFieldNames(); break;
            case PdfCheck.TableHeaders: TableHeaders(); break;
            case PdfCheck.HeadingLevels: HeadingLevels(); break;
            case PdfCheck.FontsEmbedded: FontsEmbedded(); break;
            case PdfCheck.FontsMapToUnicode: FontsMapToUnicode(); break;
            case PdfCheck.NoActions: NoActions(); break;
            case PdfCheck.NoEmbeddedFiles: NoEmbeddedFiles(); break;
            default: throw new ArgumentOutOfRangeException(nameof(check), check, "Not a PdfCheck value.");
        }
    }

    private void DocumentLanguage()
    {
        if (IsBlank(document.Document.Lang))
        {
            Add(PdfCheck.DocumentLanguage, null, Name(Section.Document), "The document has no language");
        }
    }

    private void DocumentTitle()
    {
        if (IsBlank(document.Metadata.Title) && IsBlank(document.Metadata.XmpTitle))
        {
            Add(PdfCheck.DocumentTitle, null, Name(Section.Metadata), "The document has no title");
        }

        if (!document.Document.DisplayDocTitle)
        {
            Add(PdfCheck.DocumentTitle, null, Name(Section.Document),
                "The viewer shows the file name instead of the title");
        }
    }

    private void FigureAltText()
    {
        foreach (var (element, path) in Elements)
        {
            if (TypeOf(element) == StructureType.Figure && IsBlank(element.Alt) && IsBlank(element.ActualText))
            {
                Add(PdfCheck.FigureAltText, element.FirstPage, path, $"Figure{On(element.FirstPage)} has no alt text");
            }
        }
    }

    private void UntaggedContent()
    {
        foreach (var text in document.Untagged)
        {
            Add(PdfCheck.UntaggedContent, text.Page, Name(Section.Untagged),
                $"Untagged text on p{text.Page}: \"{text.Text}\"");
        }

        var images = document.Images.Where(image => image.Role == ContentRole.Untagged);
        var paths = document.Paths.Where(path => path.Role == ContentRole.Untagged);
        var graphics = images.Select(image => (image.Page, "image")).Concat(paths.Select(path => (path.Page, "path")));
        foreach (var group in graphics.GroupBy(graphic => graphic).OrderBy(group => group.Key.Page))
        {
            var (page, kind) = group.Key;
            Add(PdfCheck.UntaggedContent, page, Name(Section.Untagged), $"{group.Count()} untagged {kind}(s) on p{page}");
        }
    }

    private void LinkDescriptions()
    {
        foreach (var (element, path) in Elements)
        {
            foreach (var link in element.Annotations.Where(IsLink))
            {
                if (IsBlank(link.Contents) && IsBlank(element.Alt) && IsBlank(element.Text))
                {
                    Add(PdfCheck.LinkDescriptions, link.Page, path, $"Link{On(link.Page)} has no description");
                }
            }
        }

        var untagged = document.Annotations.Where(link => IsLink(link) && link.Role != ContentRole.Tagged);
        foreach (var link in untagged.Where(link => IsBlank(link.Contents)))
        {
            Add(PdfCheck.LinkDescriptions, link.Page, Name(Section.Annotations),
                $"Untagged link{On(link.Page)} has no description");
        }
    }

    private void FormFieldNames()
    {
        foreach (var field in document.FormFields.Where(field => IsBlank(field.AccessibleName)))
        {
            Add(PdfCheck.FormFieldNames, field.Page, $"{Name(Section.Forms)} > {field.Name}",
                $"Form field \"{field.Name}\" has no accessible name");
        }
    }

    // A header without a scope is still fine when the cells name it in their headers. A nested table's cells are
    // checked with that table, not again with the one around it.
    private void TableHeaders()
    {
        foreach (var (table, tablePath) in Elements.Where(pair => TypeOf(pair.Element) == StructureType.Table))
        {
            var cells = Descendants(table, tablePath, stopAt: StructureType.Table).ToList();
            var named = cells.SelectMany(pair => pair.Element.Headers ?? []).ToHashSet();
            foreach (var (cell, path) in cells)
            {
                var isNamed = cell.Name is { } name && named.Contains(name);
                if (TypeOf(cell) == StructureType.TableHeaderCell && cell.Scope is null && !isNamed)
                {
                    Add(PdfCheck.TableHeaders, cell.FirstPage, path,
                        $"Header{On(cell.FirstPage)} has no scope, and no cell names it");
                }

                foreach (var missing in (cell.Headers ?? []).Where(header => header.StartsWith('?')))
                {
                    Add(PdfCheck.TableHeaders, cell.FirstPage, path,
                        $"Cell{On(cell.FirstPage)} names a header that doesn't exist ({missing[1..]})");
                }
            }
        }
    }

    private void HeadingLevels()
    {
        var previous = 0;
        foreach (var (element, path) in Elements)
        {
            if (TypeOf(element) is not ['H', var digit] || !char.IsAsciiDigit(digit)) continue;

            var level = digit - '0';
            if (level > previous + 1)
            {
                var detail = previous == 0 ? "comes before any H1" : $"follows H{previous}, skipping a level";
                Add(PdfCheck.HeadingLevels, element.FirstPage, path, $"H{level}{On(element.FirstPage)} {detail}");
            }

            previous = level;
        }
    }

    // Two font objects can share a name (two subsets of Arial, or one object per page), so each name is reported once.
    private void FontsEmbedded()
    {
        foreach (var name in document.Fonts.Where(font => !font.Embedded).Select(font => font.Name).Distinct())
        {
            Add(PdfCheck.FontsEmbedded, null, $"{Name(Section.Fonts)} > {name}", $"Font {name} isn't embedded");
        }
    }

    private void FontsMapToUnicode()
    {
        foreach (var fonts in document.Fonts.GroupBy(font => font.Name))
        {
            var codes = fonts.SelectMany(font => font.UnmappedCodes).Distinct().Count();
            if (codes == 0) continue;

            Add(PdfCheck.FontsMapToUnicode, null, $"{Name(Section.Fonts)} > {fonts.Key}",
                $"Font {fonts.Key} uses {codes} code(s) that don't map to Unicode");
        }
    }

    // Opening at a page, by its number or a named destination, is fine; JavaScript is reported once, on its own. Every
    // action chained after the open action counts too.
    private void NoActions()
    {
        var settings = document.Settings;
        if (settings.HasJavaScript) Add(PdfCheck.NoActions, null, Name(Section.Settings), "The document has JavaScript");
        if (settings.HasAdditionalActions)
        {
            Add(PdfCheck.NoActions, null, Name(Section.Settings),
                "The document runs actions on events such as closing or printing");
        }

        // Settings built by hand rather than read from a file have only the joined description.
        var actions = settings.OpenActions.Count > 0 ? settings.OpenActions
            : settings.OpenAction is { } only ? [only] : [];
        if (actions.Any(action => !IsPageDestination(action) && action != "JavaScript"))
        {
            Add(PdfCheck.NoActions, null, Name(Section.Settings),
                $"The document runs an action when it opens: {settings.OpenAction}");
        }
    }

    private void NoEmbeddedFiles()
    {
        foreach (var file in document.Settings.EmbeddedFiles)
        {
            Add(PdfCheck.NoEmbeddedFiles, null, Name(Section.Settings), $"The document embeds {file.Name}");
        }
    }

    // A destination prints as its page (p1 Fit, or p? Fit for a page that can't be found), as GoTo and the page, or
    // as named and its name. GoToR and GoToE open another file, so they don't count.
    private static bool IsPageDestination(string action) =>
        action == "GoTo" || action.StartsWith("GoTo ", StringComparison.Ordinal)
        || action.StartsWith("named ", StringComparison.Ordinal)
        || action is ['p', var page, ..] && (char.IsAsciiDigit(page) || page == '?');

    // Every structure element in reading order, with its path as a snapshot diff shows it. Read once, since several
    // checks walk them.
    private List<(StructureElement Element, string Path)> Elements => _elements ??=
        [.. WithPaths(document.Structure, Name(Section.Structure))
            .SelectMany(pair => Descendants(pair.Element, pair.Path, stopAt: null).Prepend(pair))];

    private static IEnumerable<(StructureElement Element, string Path)> Descendants(StructureElement element, string path,
        string? stopAt)
    {
        foreach (var child in WithPaths(element.Children, path))
        {
            yield return child;
            if (TypeOf(child.Element) == stopAt) continue;
            foreach (var descendant in Descendants(child.Element, child.Path, stopAt)) yield return descendant;
        }
    }

    // A sibling that shares its key with others gets its place among them: P[2].
    private static IEnumerable<(StructureElement Element, string Path)> WithPaths(
        IReadOnlyList<StructureElement> siblings, string parentPath)
    {
        var keys = siblings.Select(KeyOf).ToList();
        var counts = keys.CountBy(key => key).ToDictionary();
        var seen = new Dictionary<string, int>();
        for (var i = 0; i < siblings.Count; i++)
        {
            var place = seen[keys[i]] = seen.GetValueOrDefault(keys[i]) + 1;
            var label = counts[keys[i]] > 1 ? $"{keys[i]}[{place}]" : keys[i];
            yield return (siblings[i], $"{parentPath} > {label}");
        }
    }

    // As the snapshot prints an element: its name or type, then the type it's mapped to.
    private static string KeyOf(StructureElement element) =>
        (element.Name ?? element.Type) + (element.MappedType is null ? "" : "->" + element.MappedType);

    private static string TypeOf(StructureElement element) => element.MappedType ?? element.Type;

    private static string Name(Section section) => SnapshotWriter.Name(section);

    private static bool IsLink(Annotation annotation) => annotation.Subtype == "Link";

    private static bool IsBlank(string? text) => string.IsNullOrWhiteSpace(text);

    private static string On(int? page) => page is int number ? $" on p{number}" : "";

    private void Add(PdfCheck check, int? page, string path, string message) =>
        _findings.Add(new CheckFinding(check, page, path, message));
}
