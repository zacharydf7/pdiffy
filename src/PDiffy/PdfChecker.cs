using System.Globalization;
using System.Text.RegularExpressions;

namespace PDiffy;

/// <summary>Applies <see cref="PdfCheck"/> rules to a document's model, one method per rule.</summary>
internal sealed class PdfChecker(PdfUaDocument document)
{
    // A file name of at most six words, such as "IMG_2041 (1).jpg" or "Screenshot 2024-01-05 at 10.32.11.png", or a
    // placeholder word with an optional number, such as "image 3". A longer sentence ending in a file name describes.
    private static readonly Regex PlaceholderAltText = new(
        @"^(?:(?:[\w().-]+ ){0,5}[\w().-]+\.(?:png|jpe?g|gif|bmp|tiff?|svg|webp|emf|wmf|eps|pdf)"
        + @"|(?:image|img|picture|pic|photo|graphic|figure|icon|alt|alt text|untitled|placeholder|spacer|blank)"
        + @"(?:[\s_#-]*\d+)?)\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // A well-formed BCP 47 tag: a language with its extended subtags, then an optional script, region, variants,
    // extensions and private use; or a private-use tag, or one of the irregular tags BCP 47 lists by name. The grammar
    // also allows a language of 4 to 8 letters, but the registry has none, so a word such as "english" isn't one.
    private static readonly Regex LanguageTag = new(
        @"^(?:[a-z]{2,3}(?:-[a-z]{3}){0,3}(?:-[a-z]{4})?(?:-(?:[a-z]{2}|[0-9]{3}))?"
        + @"(?:-(?:[a-z0-9]{5,8}|[0-9][a-z0-9]{3}))*(?:-[0-9a-wy-z](?:-[a-z0-9]{2,8})+)*(?:-x(?:-[a-z0-9]{1,8})+)?"
        + @"|x(?:-[a-z0-9]{1,8})+|en-GB-oed|sgn-(?:BE-FR|BE-NL|CH-DE)"
        + @"|i-(?:ami|bnn|default|enochian|hak|klingon|lux|mingo|navajo|pwn|tao|tay|tsu))\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
            case PdfCheck.NoteIds: NoteIds(); break;
            case PdfCheck.TableStructure: TableStructure(); break;
            case PdfCheck.TableGrid: TableGrid(); break;
            case PdfCheck.AnnotationTags: AnnotationTags(); break;
            case PdfCheck.AnnotationDescriptions: AnnotationDescriptions(); break;
            case PdfCheck.TabOrder: TabOrder(); break;
            case PdfCheck.LanguageTags: LanguageTags(); break;
            case PdfCheck.TextLanguage: TextLanguage(); break;
            case PdfCheck.FormulaAltText: FormulaAltText(); break;
            case PdfCheck.MediaClips: MediaClips(); break;
            case PdfCheck.ArtifactNesting: ArtifactNesting(); break;
            case PdfCheck.LayerConfigurations: LayerConfigurations(); break;
            case PdfCheck.EmbeddedFileNames: EmbeddedFileNames(); break;
            case PdfCheck.NoDynamicXfa: NoDynamicXfa(); break;
            case PdfCheck.AccessibilityPermission: AccessibilityPermission(); break;
            case PdfCheck.NoReferenceXObjects: NoReferenceXObjects(); break;
            case PdfCheck.MeaningfulAltText: MeaningfulAltText(); break;
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

    private void FigureAltText() => MissingAltText(PdfCheck.FigureAltText, StructureType.Figure);

    private void FormulaAltText() => MissingAltText(PdfCheck.FormulaAltText, StructureType.Formula);

    private void MissingAltText(PdfCheck check, string type)
    {
        foreach (var (element, path) in Elements)
        {
            if (element.TypeOrMapped == type && IsBlank(element.AltText) && IsBlank(element.ActualText))
            {
                Add(check, element.FirstPage, path, $"{type}{On(element.FirstPage)} has no alt text");
            }
        }
    }

    private void MediaClips()
    {
        foreach (var annotation in document.Annotations)
        {
            if (annotation.MediaClip is not { } clip) continue;

            var page = annotation.Page;
            if (IsBlank(clip.ContentType))
            {
                Add(PdfCheck.MediaClips, page, $"{Name(PdfSection.Annotations)} > media-type",
                    $"Media clip{On(page)} has no content type");
            }

            if (IsBlank(clip.AltText))
            {
                Add(PdfCheck.MediaClips, page, $"{Name(PdfSection.Annotations)} > media-alt",
                    $"Media clip{On(page)} has no alternate text");
            }
        }
    }

    private void ArtifactNesting()
    {
        foreach (var page in document.Pages)
        {
            if (page.ArtifactsInTaggedContent > 0)
            {
                Add(PdfCheck.ArtifactNesting, page.Number, $"{Name(PdfSection.Pages)} > artifacts-in-tagged",
                    $"Page {page.Number} has {page.ArtifactsInTaggedContent} artifact(s) inside tagged content");
            }

            if (page.ArtifactsHoldingTaggedContent > 0)
            {
                Add(PdfCheck.ArtifactNesting, page.Number, $"{Name(PdfSection.Pages)} > artifacts-holding-tagged",
                    $"Page {page.Number} has {page.ArtifactsHoldingTaggedContent} artifact(s) holding tagged content");
            }
        }
    }

    private void LayerConfigurations()
    {
        foreach (var configuration in document.Settings.LayerConfigurations)
        {
            var name = configuration.Name is { } named && !IsBlank(named) ? $"\"{named}\"" : null;
            if (name is null)
            {
                Add(PdfCheck.LayerConfigurations, null, Setting("layer-config"),
                    "An optional-content configuration has no name");
            }

            if (configuration.ChangesLayersByItself)
            {
                Add(PdfCheck.LayerConfigurations, null, $"{Setting("layer-config")} > auto-state",
                    $"Optional-content configuration {name ?? "(unnamed)"} turns layers on or off by itself (AS)");
            }
        }
    }

    private void EmbeddedFileNames()
    {
        foreach (var file in document.Settings.EmbeddedFiles.Where(file => !file.NamedBothWays))
        {
            Add(PdfCheck.EmbeddedFileNames, null, Setting(file.Name),
                $"Embedded file {file.Name} isn't named both as F and as UF");
        }
    }

    private void NoDynamicXfa()
    {
        if (document.Form?.Xfa == PdfXfaForm.Dynamic)
        {
            Add(PdfCheck.NoDynamicXfa, null, $"{Name(PdfSection.Forms)} > xfa", "The form is a dynamic XFA form");
        }
    }

    private void AccessibilityPermission()
    {
        var settings = document.Settings;
        if (settings.IsEncrypted && settings.Permissions is { } permissions
            && !permissions.HasFlag(PdfPermissions.ExtractForAccessibility))
        {
            Add(PdfCheck.AccessibilityPermission, null, Setting("permissions"),
                "The encryption doesn't let assistive technology extract the text");
        }
    }

    private void NoReferenceXObjects()
    {
        if (document.Settings.ReferenceXObjects > 0)
        {
            Add(PdfCheck.NoReferenceXObjects, null, Setting("reference-xobjects"),
                $"The pages' resources hold {document.Settings.ReferenceXObjects} reference XObject(s)");
        }
    }

    private void MeaningfulAltText()
    {
        foreach (var (element, path) in Elements)
        {
            var (type, page) = (element.TypeOrMapped, element.FirstPage);
            if (element.AltText is { } alt && IsPlaceholder(alt))
            {
                Add(PdfCheck.MeaningfulAltText, page, path,
                    $"{type}{On(page)} has the alt text \"{alt.Trim()}\", which says nothing about it");
            }

            var spanAlts = element.MarkedSpans.Select(span => span.AltText).OfType<string>().Where(IsPlaceholder);
            foreach (var spanAlt in spanAlts)
            {
                Add(PdfCheck.MeaningfulAltText, page, $"{path} > span-alt", $"Marked content in {type}{On(page)} has "
                    + $"the alt text \"{spanAlt.Trim()}\", which says nothing about it");
            }
        }
    }

    private static bool IsPlaceholder(string altText) => PlaceholderAltText.IsMatch(altText.Trim());

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
        new() { [StructureType.ListItem] = [StructureType.List], [StructureType.ListBody] = [StructureType.ListItem] },
        new()
        {
            [StructureType.List] = [StructureType.List, StructureType.ListItem, StructureType.Caption],
            [StructureType.ListItem] = [StructureType.Label, StructureType.ListBody],
        });

    private void TocStructure() => Nesting(PdfCheck.TocStructure,
        new() { [StructureType.TableOfContentsItem] = [StructureType.TableOfContents] },
        new()
        {
            [StructureType.TableOfContents] =
                [StructureType.TableOfContents, StructureType.TableOfContentsItem, StructureType.Caption],
        });

    // A table's caption can be its first or last child, unlike a list's.
    private void TableStructure()
    {
        string[] table = [StructureType.Table];
        string[] row = [StructureType.TableRow];
        Nesting(PdfCheck.TableStructure,
            new()
            {
                [StructureType.TableRow] =
                    [StructureType.Table, StructureType.TableHead, StructureType.TableBody, StructureType.TableFoot],
                [StructureType.TableHead] = table, [StructureType.TableBody] = table, [StructureType.TableFoot] = table,
                [StructureType.TableHeaderCell] = row, [StructureType.TableDataCell] = row,
            },
            new()
            {
                [StructureType.Table] = [StructureType.TableRow, StructureType.TableHead, StructureType.TableBody,
                    StructureType.TableFoot, StructureType.Caption],
                [StructureType.TableRow] = [StructureType.TableHeaderCell, StructureType.TableDataCell],
                [StructureType.TableHead] = row, [StructureType.TableBody] = row, [StructureType.TableFoot] = row,
            },
            captionCanBeLast: true);
        TableParts();
    }

    // A table holds one THead, one TFoot and one Caption at most, and a TBody when it has a THead or TFoot. A caption
    // in the middle is already reported by Nesting, so it isn't reported again as another caption.
    private void TableParts()
    {
        foreach (var (table, tablePath) in Elements.Where(pair => pair.Element.TypeOrMapped == StructureType.Table))
        {
            var parts = StructurePaths.WithPaths(table.Children, tablePath).ToList();
            var hasBody = parts.Any(part => part.Element.TypeOrMapped == StructureType.TableBody);
            var seen = new HashSet<string>();
            for (var i = 0; i < parts.Count; i++)
            {
                var (part, path) = parts[i];
                var (type, page) = (part.TypeOrMapped, part.FirstPage);
                if (type is not (StructureType.TableHead or StructureType.TableFoot or StructureType.Caption)) continue;

                var inMiddle = type == StructureType.Caption && i > 0 && i < parts.Count - 1;
                if (!seen.Add(type) && !inMiddle)
                {
                    Add(PdfCheck.TableStructure, page, path,
                        $"{type}{On(page)} is another {type} in the same Table, which can hold only one");
                }
                else if (type != StructureType.Caption && !hasBody)
                {
                    Add(PdfCheck.TableStructure, page, path, $"{type}{On(page)} is in a Table with no TBody");
                }
            }
        }
    }

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

    // The first note with an ID keeps it; each later one with the same ID is reported.
    private void NoteIds()
    {
        var seen = new HashSet<string>();
        foreach (var (note, path) in Elements.Where(pair => pair.Element.TypeOrMapped == StructureType.Note))
        {
            var page = note.FirstPage;
            if (note.Id is not { } id || IsBlank(id)) Add(PdfCheck.NoteIds, page, path, $"Note{On(page)} has no ID");
            else if (!seen.Add(id))
            {
                Add(PdfCheck.NoteIds, page, path, $"Note{On(page)} has the ID {id}, which an earlier note has");
            }
        }
    }

    // Only an annotation for a reader needs a tag (see IsReadable). A printer mark mustn't be tagged, and a TrapNet
    // mustn't be there at all, which is reported once.
    private void AnnotationTags()
    {
        foreach (var (element, path) in Elements)
        {
            var type = element.TypeOrMapped;
            var tagged = element.Annotations
                .Where(a => a.Subtype is not (PdfAnnotationTypes.Popup or PdfAnnotationTypes.TrapNet));
            foreach (var (subtype, page) in tagged.Select(a => (a.Subtype, a.Page)))
            {
                if (subtype == PdfAnnotationTypes.PrinterMark)
                {
                    Add(PdfCheck.AnnotationTags, page, path,
                        $"PrinterMark annotation{On(page)} is tagged, but printer marks are artifacts",
                        "ISO 14289-1 7.18.8");
                    continue;
                }

                var (expected, clause) = TagFor(subtype);
                if (type != expected)
                {
                    Add(PdfCheck.AnnotationTags, page, path,
                        $"{subtype} annotation{On(page)} is tagged with {type}, not {expected}", clause);
                }
            }

            if (type == StructureType.Form && element.FieldRole is null && !HoldsOnlyAWidget(element))
            {
                Add(PdfCheck.AnnotationTags, element.FirstPage, path,
                    $"Form{On(element.FirstPage)} sets no field role, so it should hold one widget and nothing else",
                    "ISO 14289-1 7.18.4");
            }
        }

        foreach (var annotation in document.Annotations.Where(a => a.Role != PdfContentRole.Tagged && IsReadable(a)))
        {
            var (expected, clause) = TagFor(annotation.Subtype);
            Add(PdfCheck.AnnotationTags, annotation.Page, Name(PdfSection.Annotations),
                $"Untagged {annotation.Subtype} annotation{On(annotation.Page)} isn't in {expected}", clause);
        }

        foreach (var trapNet in document.Annotations.Where(a => a.Subtype == PdfAnnotationTypes.TrapNet))
        {
            Add(PdfCheck.AnnotationTags, trapNet.Page, Name(PdfSection.Annotations),
                $"TrapNet annotation{On(trapNet.Page)}, which PDF/UA doesn't allow", "ISO 14289-1 7.18.2");
        }
    }

    // Links have their own rule, and a form field is described by its accessible name.
    private void AnnotationDescriptions()
    {
        foreach (var (element, path) in Elements)
        {
            foreach (var annotation in element.Annotations.Where(NeedsDescription))
            {
                if (!IsBlank(annotation.Contents) || !IsBlank(element.AltText)) continue;

                Add(PdfCheck.AnnotationDescriptions, annotation.Page, path,
                    $"{annotation.Subtype} annotation{On(annotation.Page)} has no description");
            }
        }

        var untagged = document.Annotations.Where(a => a.Role != PdfContentRole.Tagged && NeedsDescription(a));
        foreach (var annotation in untagged.Where(a => IsBlank(a.Contents)))
        {
            Add(PdfCheck.AnnotationDescriptions, annotation.Page, Name(PdfSection.Annotations),
                $"Untagged {annotation.Subtype} annotation{On(annotation.Page)} has no description");
        }
    }

    private static bool NeedsDescription(PdfAnnotation annotation) =>
        IsReadable(annotation) && annotation.Subtype is not (PdfAnnotationTypes.Link or PdfAnnotationTypes.Widget);

    // A pop-up belongs to the annotation it opens from, a hidden annotation isn't shown, and printer marks and trap
    // networks are for print production, so none of them is for a reader.
    private static bool IsReadable(PdfAnnotation annotation) =>
        !annotation.Flags.HasFlag(PdfAnnotationFlags.Hidden) && annotation.Subtype is not (PdfAnnotationTypes.Popup
            or PdfAnnotationTypes.PrinterMark or PdfAnnotationTypes.TrapNet);

    private void TabOrder()
    {
        foreach (var page in document.Pages.Where(p => p.HasAnnotations && p.Tabs != PdfTabOrders.Structure))
        {
            Add(PdfCheck.TabOrder, page.Number, $"{Name(PdfSection.Pages)} > tabs",
                $"Page {page.Number} has annotations, but its tab order is {page.Tabs ?? "not set"}, not S");
        }
    }

    // An empty language means an unknown one, which DocumentLanguage reports for the document.
    private void LanguageTags()
    {
        if (document.Document.Language is { } language && IsMalformed(language))
        {
            Add(PdfCheck.LanguageTags, null, $"{Name(PdfSection.Document)} > lang",
                $"The document's language \"{language}\" isn't a valid language tag");
        }

        foreach (var (element, path) in Elements)
        {
            var (type, page) = (element.TypeOrMapped, element.FirstPage);
            if (element.Language is { } own && IsMalformed(own))
            {
                Add(PdfCheck.LanguageTags, page, path, $"{type}{On(page)} has the language \"{own}\", "
                    + "which isn't a valid language tag");
            }

            foreach (var span in element.MarkedSpans.Where(span => span.Language is { } lang && IsMalformed(lang)))
            {
                Add(PdfCheck.LanguageTags, page, $"{path} > span-lang", $"Marked content in {type}{On(page)} has the "
                    + $"language \"{span.Language}\", which isn't a valid language tag");
            }
        }
    }

    // A document language gives every piece of text one, so only a document without one is checked, and only when
    // something in it sets a language; otherwise DocumentLanguage reports the one fix. An element's language passes to
    // what it holds, and marked content's to the text it covers. Bookmarks can't set one, so they need the document's,
    // as does a field's name kept on the field.
    private void TextLanguage()
    {
        if (!IsBlank(document.Document.Language)) return;
        if (!Elements.Any(pair => !IsBlank(pair.Element.Language)
            || pair.Element.MarkedSpans.Any(span => !IsBlank(span.Language)))) return;

        foreach (var (element, path, language) in WithLanguages(document.Structure, StructurePaths.Root, null))
        {
            if (language is not null) continue;

            var (type, page) = (element.TypeOrMapped, element.FirstPage);
            if (HasTextWithoutLanguage(element) || !IsBlank(element.AltText) || !IsBlank(element.ActualText)
                || !IsBlank(element.Expansion))
            {
                Add(PdfCheck.TextLanguage, page, path, $"{type}{On(page)} has text with no language");
            }

            if (element.MarkedSpans.Any(span => IsBlank(span.Language)
                && (!IsBlank(span.AltText) || !IsBlank(span.ActualText) || !IsBlank(span.Expansion))))
            {
                Add(PdfCheck.TextLanguage, page, $"{path} > span",
                    $"Marked content in {type}{On(page)} has alt text, replacement text or an expansion "
                    + "with no language");
            }

            foreach (var annotation in element.Annotations.Where(HasText))
            {
                Add(PdfCheck.TextLanguage, annotation.Page, $"{path} > contents",
                    $"{annotation.Subtype} annotation{On(annotation.Page)} in {type} has text with no language");
            }
        }

        foreach (var annotation in document.Annotations.Where(a => a.Role != PdfContentRole.Tagged && HasText(a)))
        {
            Add(PdfCheck.TextLanguage, annotation.Page, Name(PdfSection.Annotations),
                $"Untagged {annotation.Subtype} annotation{On(annotation.Page)} has text with no language");
        }

        // A field whose widgets are its kids, such as a radio group, keeps its name on the field, where no element's
        // language reaches it. A field combined with its widget is checked with the widget, above.
        foreach (var field in document.FormFields)
        {
            if (field.CombinedWithWidget || IsBlank(field.AccessibleName)) continue;

            Add(PdfCheck.TextLanguage, field.Page, $"{Name(PdfSection.Forms)} > {field.Name}",
                $"Form field \"{field.Name}\" has an accessible name with no language");
        }

        if (document.Bookmarks.Count > 0)
        {
            Add(PdfCheck.TextLanguage, null, Name(PdfSection.Bookmarks), "Bookmark titles have no language");
        }

        if (document.Metadata.XmpHasDefaultLanguageText)
        {
            Add(PdfCheck.TextLanguage, null, $"{Name(PdfSection.Metadata)} > xmp",
                "The XMP title or description is for the default language, and the document has none");
        }
    }

    // Text that marked content gives a language of its own has one, so only the rest of the element's text counts.
    private static bool HasTextWithoutLanguage(PdfStructureElement element)
    {
        if (element.Text is not { } text || IsBlank(text)) return false;

        var covered = element.MarkedSpans.Where(span => !IsBlank(span.Language)).Select(span => span.Text);
        return WithoutSpaces(text) != WithoutSpaces(string.Concat(covered));
    }

    // An annotation's text: its contents, or a widget's accessible name.
    private static bool HasText(PdfAnnotation annotation) =>
        !IsBlank(annotation.Contents) || !IsBlank(annotation.AccessibleName);

    private static string WithoutSpaces(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));

    // Each element with the language it has, its own or the nearest one around it; null when none sets one.
    private static IEnumerable<(PdfStructureElement Element, string Path, string? Language)> WithLanguages(
        IReadOnlyList<PdfStructureElement> elements, string path, string? inherited)
    {
        foreach (var (element, elementPath) in StructurePaths.WithPaths(elements, path))
        {
            var language = IsBlank(element.Language) ? inherited : element.Language;
            yield return (element, elementPath, language);
            foreach (var child in WithLanguages(element.Children, elementPath, language)) yield return child;
        }
    }

    private static bool IsMalformed(string language) => language.Length > 0 && !LanguageTag.IsMatch(language);

    // The element an annotation is tagged with, and the clause that says so.
    private static (string Element, string Clause) TagFor(string subtype) => subtype switch
    {
        PdfAnnotationTypes.Link => (StructureType.Link, "ISO 14289-1 7.18.5"),
        PdfAnnotationTypes.Widget => (StructureType.Form, "ISO 14289-1 7.18.4"),
        _ => (StructureType.Annotation, "ISO 14289-1 7.18.1"),
    };

    private static bool HoldsOnlyAWidget(PdfStructureElement form) =>
        form.Annotations is [{ Subtype: PdfAnnotationTypes.Widget }] && form.Children.Count == 0 && IsBlank(form.Text)
        && form.ImageHashes.Count == 0;

    // HTML's cap on a cell's column span, so a hostile span can't make the grid huge.
    private const int MaxColumnSpan = 1000;

    // Lays each table out as a grid, row by row, each cell in the first column no cell above still covers, as a viewer
    // does, then compares what each row covers with the first row. A row span is laid out only as far as the table's
    // last row, so a huge one costs nothing.
    private void TableGrid()
    {
        foreach (var (table, tablePath) in Elements.Where(pair => pair.Element.TypeOrMapped == StructureType.Table))
        {
            var rows = Rows(table, tablePath).ToList();
            var covered = new HashSet<(int Row, int Column)>();
            for (var row = 0; row < rows.Count; row++)
            {
                var column = 0;
                foreach (var (cell, path) in Cells(rows[row].Element, rows[row].Path))
                {
                    while (covered.Contains((row, column))) column++;

                    var rowSpan = Math.Max(1, cell.RowSpan ?? 1);
                    var colSpan = Math.Clamp(cell.ColSpan ?? 1, 1, MaxColumnSpan);
                    var rowsLeft = rows.Count - row;
                    var slots = Enumerable.Range(row, Math.Min(rowSpan, rowsLeft))
                        .SelectMany(r => Enumerable.Range(column, colSpan).Select(c => (r, c))).ToList();
                    var (type, page) = (cell.TypeOrMapped, cell.FirstPage);
                    if (slots.Any(covered.Contains))
                    {
                        Add(PdfCheck.TableGrid, page, path,
                            $"{type}{On(page)} overlaps a cell spanning from an earlier row");
                    }

                    if (rowSpan > rowsLeft)
                    {
                        Add(PdfCheck.TableGrid, page, path,
                            $"{type}{On(page)} spans {rowSpan} rows, past the table's last row");
                    }

                    covered.UnionWith(slots);
                    column += colSpan;
                }
            }

            var widths = new int[rows.Count];
            foreach (var (row, _) in covered) widths[row]++;
            for (var row = 1; row < rows.Count; row++)
            {
                if (widths[row] == widths[0]) continue;

                Add(PdfCheck.TableGrid, rows[row].Element.FirstPage, rows[row].Path,
                    $"TR has {widths[row]} column(s), but the first row has {widths[0]}");
            }
        }
    }

    // A table's rows, whether straight in it or in a THead, TBody or TFoot, in the order the tree lists them.
    private static IEnumerable<(PdfStructureElement Element, string Path)> Rows(PdfStructureElement table, string path)
    {
        foreach (var (child, childPath) in StructurePaths.WithPaths(table.Children, path))
        {
            var type = child.TypeOrMapped;
            if (type == StructureType.TableRow) yield return (child, childPath);
            if (type is not (StructureType.TableHead or StructureType.TableBody or StructureType.TableFoot)) continue;

            foreach (var row in StructurePaths.WithPaths(child.Children, childPath))
            {
                if (row.Element.TypeOrMapped == StructureType.TableRow) yield return row;
            }
        }
    }

    private static IEnumerable<(PdfStructureElement Element, string Path)> Cells(PdfStructureElement row,
        string path) =>
        StructurePaths.WithPaths(row.Children, path).Where(pair =>
            pair.Element.TypeOrMapped is StructureType.TableHeaderCell or StructureType.TableDataCell);

    // Each element of a type that must sit in another, each child a container can't hold, and a caption that isn't
    // its container's first child (or last, when it can be). A child is reported once, for the first of these it
    // breaks.
    private void Nesting(PdfCheck check, Dictionary<string, string[]> parents, Dictionary<string, string[]> holds,
        bool captionCanBeLast = false)
    {
        foreach (var (parent, children) in Families())
        {
            var parentType = parent?.TypeOrMapped;
            var allowed = parentType is null ? null : holds.GetValueOrDefault(parentType);
            for (var i = 0; i < children.Count; i++)
            {
                var (child, path) = children[i];
                var (type, page) = (child.TypeOrMapped, child.FirstPage);
                if (parents.TryGetValue(type, out var required) && !required.Contains(parentType))
                {
                    Add(check, page, path, $"{type}{On(page)} isn't inside {Either(required)}");
                }
                else if (allowed is not null && !allowed.Contains(type))
                {
                    Add(check, page, path, $"{type}{On(page)} is in {parentType}, which holds only "
                        + string.Join(", ", allowed));
                }
                else if (allowed is not null && type == StructureType.Caption && i > 0
                    && !(captionCanBeLast && i == children.Count - 1))
                {
                    var where = captionCanBeLast ? "first or last" : "first";
                    Add(check, page, path, $"Caption{On(page)} isn't the {where} child of its {parentType}");
                }
            }
        }
    }

    // "L", or "Table, THead or TBody".
    private static string Either(string[] types) =>
        types.Length == 1 ? types[0] : $"{string.Join(", ", types[..^1])} or {types[^1]}";

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

    // Each rule's family and the clause it comes from: of PDF/UA-1 (ISO 14289-1), or for a warning PDF/UA doesn't
    // check, of WCAG 2.2. House rules come from no standard.
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
        PdfCheck.NoteIds => new(PdfCheckCategory.Structure, PdfCheckSeverity.Error, "ISO 14289-1 7.9"),
        PdfCheck.TableStructure or PdfCheck.TableGrid =>
            new(PdfCheckCategory.Tables, PdfCheckSeverity.Error, "ISO 14289-1 7.2"),
        PdfCheck.AnnotationTags => new(PdfCheckCategory.Annotations, PdfCheckSeverity.Error, "ISO 14289-1 7.18"),
        PdfCheck.AnnotationDescriptions =>
            new(PdfCheckCategory.Annotations, PdfCheckSeverity.Error, "ISO 14289-1 7.18.1"),
        PdfCheck.TabOrder => new(PdfCheckCategory.Annotations, PdfCheckSeverity.Error, "ISO 14289-1 7.18.3"),
        PdfCheck.LanguageTags or PdfCheck.TextLanguage =>
            new(PdfCheckCategory.Language, PdfCheckSeverity.Error, "ISO 14289-1 7.2"),
        PdfCheck.FormulaAltText => new(PdfCheckCategory.AltText, PdfCheckSeverity.Error, "ISO 14289-1 7.7"),
        PdfCheck.MediaClips => new(PdfCheckCategory.AltText, PdfCheckSeverity.Error, "ISO 14289-1 7.18.6.2"),
        PdfCheck.ArtifactNesting => new(PdfCheckCategory.Content, PdfCheckSeverity.Error, "ISO 14289-1 7.1"),
        PdfCheck.LayerConfigurations => new(PdfCheckCategory.Document, PdfCheckSeverity.Error, "ISO 14289-1 7.10"),
        PdfCheck.EmbeddedFileNames => new(PdfCheckCategory.Document, PdfCheckSeverity.Error, "ISO 14289-1 7.11"),
        PdfCheck.NoDynamicXfa => new(PdfCheckCategory.Forms, PdfCheckSeverity.Error, "ISO 14289-1 7.15"),
        PdfCheck.AccessibilityPermission => new(PdfCheckCategory.Document, PdfCheckSeverity.Error, "ISO 14289-1 7.16"),
        PdfCheck.NoReferenceXObjects => new(PdfCheckCategory.Content, PdfCheckSeverity.Error, "ISO 14289-1 7.20"),
        PdfCheck.MeaningfulAltText => new(PdfCheckCategory.AltText, PdfCheckSeverity.Warning, "WCAG 2.2 1.1.1"),
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
