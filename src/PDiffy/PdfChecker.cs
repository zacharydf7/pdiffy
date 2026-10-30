using System.Globalization;

namespace PDiffy;

/// <summary>Applies <see cref="PdfCheck"/> rules to a document's model, one method per rule.</summary>
internal sealed class PdfChecker(PdfUaDocument document)
{
    private readonly List<PdfCheckFinding> _findings = [];
    private List<(PdfStructureElement Element, string Path)>? _elements;

    public static IReadOnlyList<PdfCheckFinding> Run(PdfUaDocument document, IEnumerable<PdfCheck> checks)
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
            case PdfCheck.RoleMap: RoleMap(); break;
            case PdfCheck.XmpMetadata: XmpMetadata(); break;
            case PdfCheck.TaggedPdf: TaggedPdf(); break;
            case PdfCheck.PdfVersion: PdfVersion(); break;
            case PdfCheck.ListStructure: ListStructure(); break;
            case PdfCheck.TocStructure: TocStructure(); break;
            case PdfCheck.HeadingTypes: HeadingTypes(); break;
            default: throw new ArgumentOutOfRangeException(nameof(check), check, "Not a PdfCheck value.");
        }
    }

    private void DocumentLanguage()
    {
        if (IsBlank(document.Document.Language))
        {
            Add(PdfCheck.DocumentLanguage, null, Name(PdfSection.Document), "The document has no language");
        }
    }

    private void DocumentTitle()
    {
        if (IsBlank(document.Metadata.Title) && IsBlank(document.Metadata.XmpTitle))
        {
            Add(PdfCheck.DocumentTitle, null, Name(PdfSection.Metadata), "The document has no title");
        }

        if (!document.Document.DisplayDocTitle)
        {
            Add(PdfCheck.DocumentTitle, null, Name(PdfSection.Document),
                "The viewer shows the file name instead of the title");
        }
    }

    private void FigureAltText()
    {
        foreach (var (element, path) in Elements)
        {
            if (element.TypeOrMapped == StructureType.Figure && IsBlank(element.AltText) && IsBlank(element.ActualText))
            {
                Add(PdfCheck.FigureAltText, element.FirstPage, path, $"Figure{On(element.FirstPage)} has no alt text");
            }
        }
    }

    private void UntaggedContent()
    {
        // One finding per page, not per line or marked-content ID.
        foreach (var page in document.Untagged.GroupBy(text => text.Page))
        {
            var text = string.Join(" ", page.Select(line => line.Text));
            Add(PdfCheck.UntaggedContent, page.Key, Name(PdfSection.Untagged), $"Untagged text on p{page.Key}: \"{text}\"");
        }

        var images = document.Images.Where(image => image.Role == PdfContentRole.Untagged);
        var paths = document.Paths.Where(path => path.Role == PdfContentRole.Untagged);
        var graphics = images.Select(image => (image.Page, "image")).Concat(paths.Select(path => (path.Page, "path")));
        foreach (var group in graphics.GroupBy(graphic => graphic).OrderBy(group => group.Key.Page))
        {
            var (page, kind) = group.Key;
            Add(PdfCheck.UntaggedContent, page, $"{Name(PdfSection.Untagged)} > {kind}s",
                $"{group.Count()} untagged {kind}(s) on p{page}");
        }
    }

    private void LinkDescriptions()
    {
        foreach (var (element, path) in Elements)
        {
            foreach (var link in element.Annotations.Where(IsLink))
            {
                if (IsBlank(link.Contents) && IsBlank(element.AltText) && IsBlank(element.Text))
                {
                    Add(PdfCheck.LinkDescriptions, link.Page, path, $"Link{On(link.Page)} has no description");
                }
            }
        }

        var untagged = document.Annotations.Where(link => IsLink(link) && link.Role != PdfContentRole.Tagged);
        foreach (var link in untagged.Where(link => IsBlank(link.Contents)))
        {
            Add(PdfCheck.LinkDescriptions, link.Page, Name(PdfSection.Annotations),
                $"Untagged link{On(link.Page)} has no description");
        }
    }

    private void FormFieldNames()
    {
        foreach (var field in document.FormFields.Where(field => IsBlank(field.AccessibleName)))
        {
            Add(PdfCheck.FormFieldNames, field.Page, $"{Name(PdfSection.Forms)} > {field.Name}",
                $"Form field \"{field.Name}\" has no accessible name");
        }
    }

    // A header without a scope is still fine when the cells name it in their headers. A nested table's cells are
    // checked with that table, not again with the one around it.
    private void TableHeaders()
    {
        foreach (var (table, tablePath) in Elements.Where(pair => pair.Element.TypeOrMapped == StructureType.Table))
        {
            var cells = Descendants(table, tablePath, stopAt: StructureType.Table).ToList();
            var named = cells.SelectMany(pair => pair.Element.Headers ?? []).ToHashSet();
            foreach (var (cell, path) in cells)
            {
                var isNamed = cell.Name is { } name && named.Contains(name);
                if (cell.TypeOrMapped == StructureType.TableHeaderCell && cell.Scope is null && !isNamed)
                {
                    Add(PdfCheck.TableHeaders, cell.FirstPage, $"{path} > scope",
                        $"Header{On(cell.FirstPage)} has no scope, and no cell names it");
                }

                foreach (var missing in (cell.Headers ?? []).Where(header => header.StartsWith('?')))
                {
                    Add(PdfCheck.TableHeaders, cell.FirstPage, $"{path} > headers",
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
            if (StructureTypes.HeadingLevel(element.TypeOrMapped) is not int level) continue;

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
            Add(PdfCheck.FontsEmbedded, null, $"{Name(PdfSection.Fonts)} > {name}", $"Font {name} isn't embedded");
        }
    }

    private void FontsMapToUnicode()
    {
        foreach (var fonts in document.Fonts.GroupBy(font => font.Name))
        {
            var codes = fonts.SelectMany(font => font.UnmappedCodes).Distinct().Count();
            if (codes == 0) continue;

            Add(PdfCheck.FontsMapToUnicode, null, $"{Name(PdfSection.Fonts)} > {fonts.Key}",
                $"Font {fonts.Key} uses {codes} code(s) that don't map to Unicode");
        }
    }

    // Opening at a page, by its number or a named destination, is fine; JavaScript is reported once, on its own. Every
    // action chained after the open action counts too.
    private void NoActions()
    {
        var settings = document.Settings;
        if (settings.HasJavaScript) Add(PdfCheck.NoActions, null, Setting("javascript"), "The document has JavaScript");
        if (settings.HasAdditionalActions)
        {
            Add(PdfCheck.NoActions, null, Setting("additional-actions"),
                "The document runs actions on events such as closing or printing");
        }

        // Settings built by hand rather than read from a file have only the joined description.
        var actions = settings.OpenActions.Count > 0 ? settings.OpenActions
            : settings.OpenAction is { } only ? [only] : [];
        if (actions.Any(action => !IsPageDestination(action) && action != "JavaScript"))
        {
            Add(PdfCheck.NoActions, null, Setting("open-action"),
                $"The document runs an action when it opens: {settings.OpenAction}");
        }
    }

    private void NoEmbeddedFiles()
    {
        foreach (var file in document.Settings.EmbeddedFiles)
        {
            Add(PdfCheck.NoEmbeddedFiles, null, Setting(file.Name), $"The document embeds {file.Name}");
        }
    }

    // Each problem is reported at the role map entry it's fixed in, once per type. A chain of mappings stops at the
    // first standard type, so a custom type mapped to a remapped standard type still maps to a standard one.
    private void RoleMap()
    {
        var map = new Dictionary<string, string>();
        foreach (var mapping in document.RoleMap) map.TryAdd(mapping.Custom, mapping.Standard);

        var roleMap = Name(PdfSection.RoleMap);
        foreach (var (type, target) in map)
        {
            if (StructureTypes.IsStandardInPdf17(type) && target != type)
            {
                Add(PdfCheck.RoleMap, null, $"{roleMap} > {type} > standard",
                    $"Standard structure type {type} is remapped to {target}");
            }
            else if (!StructureTypes.IsStandardInPdf17(type)
                && Chain(type, map) is [.., var last] chain && last == type)
            {
                Add(PdfCheck.RoleMap, null, $"{roleMap} > {type} > loop",
                    $"Structure type {type} maps back to itself: {string.Join(" -> ", chain)}");
            }
        }

        // An element with no type can't be mapped, so it's not a role map problem.
        var used = Elements.Select(pair => pair.Element.Type)
            .Where(type => type != StructureTypes.None && !StructureTypes.IsStandardInPdf17(type));
        foreach (var (type, count) in used.CountBy(type => type))
        {
            if (Chain(type, map) is [.., var last] && StructureTypes.IsStandardInPdf17(last)) continue;

            Add(PdfCheck.RoleMap, null, $"{roleMap} > {type}",
                $"Structure type {type}, used by {count} element(s), doesn't map to a standard type");
        }
    }

    // Without readable XMP there's nothing else to check in it.
    private void XmpMetadata()
    {
        var metadata = document.Metadata;
        var path = Name(PdfSection.Metadata);
        if (!metadata.HasXmp || metadata.XmpUnreadable)
        {
            Add(PdfCheck.XmpMetadata, null, $"{path} > xmp",
                metadata.HasXmp ? "The XMP metadata can't be read" : "The document has no XMP metadata",
                "ISO 14289-1 7.1");
            return;
        }

        if (metadata.PdfUaPart != "1")
        {
            Add(PdfCheck.XmpMetadata, null, $"{path} > pdfuaid-part", metadata.PdfUaPart is null
                ? "The XMP doesn't say the document is PDF/UA (pdfuaid:part)"
                : $"The XMP says PDF/UA part {metadata.PdfUaPart}, not 1");
        }

        if (metadata.PdfUaIdPrefix is { } prefix)
        {
            var spelled = prefix.Length == 0 ? "without a prefix" : $"with the prefix \"{prefix}\"";
            Add(PdfCheck.XmpMetadata, null, $"{path} > pdfuaid-prefix",
                $"The XMP writes its PDF/UA identification {spelled}, not \"pdfuaid\"");
        }

        if (IsBlank(metadata.XmpTitle))
        {
            Add(PdfCheck.XmpMetadata, null, $"{path} > xmp-title", "The XMP has no title (dc:title)",
                "ISO 14289-1 7.1");
        }
    }

    private void TaggedPdf()
    {
        var info = document.Document;
        var section = Name(PdfSection.Document);
        if (!info.Marked)
        {
            Add(PdfCheck.TaggedPdf, null, $"{section} > marked",
                "The document isn't marked as tagged (MarkInfo Marked)", "ISO 14289-1 6.2");
        }

        if (info.Suspects)
        {
            Add(PdfCheck.TaggedPdf, null, $"{section} > suspects",
                "The document says its tags may not be reliable (MarkInfo Suspects)");
        }

        if (!info.HasStructureTree)
        {
            Add(PdfCheck.TaggedPdf, null, Name(PdfSection.Structure), "The document has no structure tree");
        }

        foreach (var (element, path) in Elements.Where(pair => pair.Element.ParentMissing))
        {
            Add(PdfCheck.TaggedPdf, element.FirstPage, path,
                $"{element.Type}{On(element.FirstPage)} has no parent entry (P)");
        }
    }

    // PDF 2.0 is for PDF/UA-2. A catalog /Version later than the header upgrades the file, so it counts too.
    private void PdfVersion()
    {
        var settings = document.Settings;
        var versions = new[] { ("version", "file header", settings.PdfVersion),
            ("catalog-version", "catalog", settings.CatalogVersion) };
        foreach (var (key, where, version) in versions)
        {
            if (!double.TryParse(version, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) continue;
            if (number <= 1.7) continue;

            Add(PdfCheck.PdfVersion, null, $"{Name(PdfSection.Settings)} > {key}",
                $"The {where} says PDF {version}; PDF/UA-1 files are PDF 1.0 to 1.7");
        }
    }

    private void ListStructure() => Nesting(PdfCheck.ListStructure,
        new() { [StructureType.ListItem] = StructureType.List, [StructureType.ListBody] = StructureType.ListItem },
        new()
        {
            [StructureType.List] = [StructureType.List, StructureType.ListItem, StructureType.Caption],
            [StructureType.ListItem] = [StructureType.Label, StructureType.ListBody],
        });

    private void TocStructure() => Nesting(PdfCheck.TocStructure,
        new() { [StructureType.TableOfContentsItem] = StructureType.TableOfContents },
        new()
        {
            [StructureType.TableOfContents] =
                [StructureType.TableOfContents, StructureType.TableOfContentsItem, StructureType.Caption],
        });

    // The first heading says which kind the document uses, so each heading of the other kind is reported. An H names
    // the section it's in, so a second H in one element, or in the root, is reported too, unless every H already is.
    private void HeadingTypes()
    {
        bool? numbered = null;
        foreach (var (element, path) in Elements)
        {
            var type = element.TypeOrMapped;
            var isNumbered = StructureTypes.HeadingLevel(type) is not null;
            if (!isNumbered && type != StructureType.Heading) continue;

            numbered ??= isNumbered;
            if (isNumbered == numbered) continue;

            var page = element.FirstPage;
            Add(PdfCheck.HeadingTypes, page, path, isNumbered
                ? $"{type}{On(page)} is numbered, but the document's first heading is H"
                : $"H{On(page)} isn't numbered, but the document's first heading is numbered");
        }

        if (numbered != false) return;

        foreach (var (parent, children) in Families())
        {
            var holder = parent?.TypeOrMapped ?? "structure tree root";
            var extra = children.Where(child => child.Element.TypeOrMapped == StructureType.Heading).Skip(1);
            foreach (var (heading, path) in extra)
            {
                Add(PdfCheck.HeadingTypes, heading.FirstPage, path,
                    $"H{On(heading.FirstPage)} is another H in the same {holder}, which can hold only one");
            }
        }
    }

    // Each element of a type that must sit in another, each child a container can't hold, and a caption that isn't
    // its container's first child. A child is reported once, for the first of these it breaks.
    private void Nesting(PdfCheck check, Dictionary<string, string> parents, Dictionary<string, string[]> holds)
    {
        foreach (var (parent, children) in Families())
        {
            var parentType = parent?.TypeOrMapped;
            var allowed = parentType is null ? null : holds.GetValueOrDefault(parentType);
            for (var i = 0; i < children.Count; i++)
            {
                var (child, path) = children[i];
                var (type, page) = (child.TypeOrMapped, child.FirstPage);
                if (parents.TryGetValue(type, out var required) && parentType != required)
                {
                    Add(check, page, path, $"{type}{On(page)} isn't inside {required}");
                }
                else if (allowed is not null && !allowed.Contains(type))
                {
                    Add(check, page, path, $"{type}{On(page)} is in {parentType}, which holds only "
                        + string.Join(", ", allowed));
                }
                else if (allowed is not null && type == StructureType.Caption && i > 0)
                {
                    Add(check, page, path, $"Caption{On(page)} isn't the first child of its {parentType}");
                }
            }
        }
    }

    // The structure tree root's children, then each element's, with their paths.
    private IEnumerable<(PdfStructureElement? Parent, List<(PdfStructureElement Element, string Path)> Children)>
        Families()
    {
        yield return (null, [.. StructurePaths.WithPaths(document.Structure, StructurePaths.Root)]);
        foreach (var (element, path) in Elements)
        {
            yield return (element, [.. StructurePaths.WithPaths(element.Children, path)]);
        }
    }

    // The type and the types it maps to in turn, up to the first standard type, a type with no mapping, or the first
    // type seen twice, which ends the chain when it loops.
    private static List<string> Chain(string type, Dictionary<string, string> map)
    {
        var chain = new List<string> { type };
        var seen = new HashSet<string> { type };
        var current = type;
        while (map.TryGetValue(current, out var next))
        {
            chain.Add(next);
            if (StructureTypes.IsStandardInPdf17(next) || !seen.Add(next)) break;

            current = next;
        }

        return chain;
    }

    // A destination prints as its page (p1 Fit, or p? Fit for a page that can't be found), as GoTo and the page, or
    // as named and its name. GoToR and GoToE open another file, so they don't count.
    private static bool IsPageDestination(string action) =>
        action == "GoTo" || action.StartsWith("GoTo ", StringComparison.Ordinal)
        || action.StartsWith("named ", StringComparison.Ordinal)
        || action is ['p', var page, ..] && (char.IsAsciiDigit(page) || page == '?');

    // Every structure element in reading order, with its path as a snapshot diff shows it. Read once, since several
    // checks walk them.
    private List<(PdfStructureElement Element, string Path)> Elements => _elements ??=
        [.. StructurePaths.WithPaths(document.Structure, StructurePaths.Root)
            .SelectMany(pair => Descendants(pair.Element, pair.Path, stopAt: null).Prepend(pair))];

    private static IEnumerable<(PdfStructureElement Element, string Path)> Descendants(PdfStructureElement element, string path,
        string? stopAt)
    {
        foreach (var child in StructurePaths.WithPaths(element.Children, path))
        {
            yield return child;
            if (child.Element.TypeOrMapped == stopAt) continue;
            foreach (var descendant in Descendants(child.Element, child.Path, stopAt)) yield return descendant;
        }
    }

    private static string Name(PdfSection section) => SnapshotWriter.Name(section);

    // A path into the settings section names the setting, so findings about different settings don't share a path.
    private static string Setting(string name) => $"{Name(PdfSection.Settings)} > {name}";

    private static bool IsLink(PdfAnnotation annotation) => annotation.Subtype == PdfAnnotationTypes.Link;

    private static bool IsBlank(string? text) => string.IsNullOrWhiteSpace(text);

    private static string On(int? page) => page is int number ? $" on p{number}" : "";

    // A rule that covers more than one clause names the finding's own, so it can be matched with a validator's report.
    private void Add(PdfCheck check, int? page, string path, string message, string? clause = null)
    {
        var (category, severity, reference) = RuleOf(check);
        _findings.Add(new PdfCheckFinding
        {
            Check = check, Category = category, Severity = severity, Reference = clause ?? reference, Page = page,
            Path = path, Message = message,
        });
    }

    // Each rule's family and the clause of PDF/UA-1 (ISO 14289-1) it comes from; house rules come from no standard.
    internal static PdfCheckRule RuleOf(PdfCheck check) => check switch
    {
        PdfCheck.DocumentLanguage => new(PdfCheckCategory.Language, PdfCheckSeverity.Error, "ISO 14289-1 7.2"),
        PdfCheck.DocumentTitle => new(PdfCheckCategory.Document, PdfCheckSeverity.Error, "ISO 14289-1 7.1"),
        PdfCheck.FigureAltText => new(PdfCheckCategory.AltText, PdfCheckSeverity.Error, "ISO 14289-1 7.3"),
        PdfCheck.UntaggedContent => new(PdfCheckCategory.Content, PdfCheckSeverity.Error, "ISO 14289-1 7.1"),
        PdfCheck.LinkDescriptions => new(PdfCheckCategory.Annotations, PdfCheckSeverity.Error, "ISO 14289-1 7.18.5"),
        PdfCheck.FormFieldNames => new(PdfCheckCategory.Forms, PdfCheckSeverity.Error, "ISO 14289-1 7.18.1"),
        PdfCheck.TableHeaders => new(PdfCheckCategory.Tables, PdfCheckSeverity.Error, "ISO 14289-1 7.5"),
        PdfCheck.HeadingLevels => new(PdfCheckCategory.Headings, PdfCheckSeverity.Error, "ISO 14289-1 7.4.2"),
        PdfCheck.HeadingTypes => new(PdfCheckCategory.Headings, PdfCheckSeverity.Error, "ISO 14289-1 7.4.4"),
        PdfCheck.FontsEmbedded => new(PdfCheckCategory.Fonts, PdfCheckSeverity.Error, "ISO 14289-1 7.21.4.1"),
        PdfCheck.FontsMapToUnicode => new(PdfCheckCategory.Fonts, PdfCheckSeverity.Error, "ISO 14289-1 7.21.7"),
        PdfCheck.RoleMap => new(PdfCheckCategory.Structure, PdfCheckSeverity.Error, "ISO 14289-1 7.1"),
        PdfCheck.XmpMetadata => new(PdfCheckCategory.Document, PdfCheckSeverity.Error, "ISO 14289-1 5"),
        PdfCheck.TaggedPdf => new(PdfCheckCategory.Structure, PdfCheckSeverity.Error, "ISO 14289-1 7.1"),
        PdfCheck.PdfVersion => new(PdfCheckCategory.Document, PdfCheckSeverity.Error, "ISO 14289-1 6.1"),
        PdfCheck.ListStructure or PdfCheck.TocStructure =>
            new(PdfCheckCategory.Lists, PdfCheckSeverity.Error, "ISO 14289-1 7.2"),
        PdfCheck.NoActions or PdfCheck.NoEmbeddedFiles =>
            new(PdfCheckCategory.HouseRules, PdfCheckSeverity.Warning, null),
        _ => throw new ArgumentOutOfRangeException(nameof(check), check, "Every check needs a rule."),
    };
}

/// <summary>A <see cref="PdfCheck"/>'s family, how much its findings matter and the clause it comes from.</summary>
internal readonly record struct PdfCheckRule(PdfCheckCategory Category, PdfCheckSeverity Severity, string? Reference);
