using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>Reads a PDF into a <see cref="PdfUaDocument"/> using PdfPig.</summary>
internal static class PdfPigReader
{
    public static PdfUaDocument Read(Stream pdf)
    {
        using var document = PdfDocument.Open(pdf);
        var objects = new PdfObjects(document);
        var catalog = document.Structure.Catalog.CatalogDictionary;
        var pageTree = PageTree.Read(objects, catalog);

        var (pages, readable) = ReadPages(document, objects);
        var structure = StructureTreeReader.Read(objects, catalog, pageTree,
            (page, mcid) => readable.TryGetValue(page, out var read) ? read.Content.LettersByMcid.GetValueOrDefault(mcid) : null);
        var loose = CollectLooseContent(readable, structure.ReferencedMcids);
        var layouts = readable.OrderBy(pair => pair.Key)
            .Select(pair => LayoutReader.Read(pair.Key, pair.Value.Page, pair.Value.Content.Owners, structure.ReferencedMcids))
            .ToList();

        return new PdfUaDocument
        {
            Document = MetadataReader.ReadDocumentInfo(objects, catalog, document.NumberOfPages),
            Metadata = MetadataReader.ReadMetadata(document, objects),
            Settings = DocumentSettingsReader.Read(document, objects, catalog, pageTree),
            RoleMap = structure.RoleMap,
            Pages = pages,
            PageLabels = NavigationReader.ReadPageLabels(objects, catalog),
            Bookmarks = NavigationReader.ReadBookmarks(document),
            Structure = structure.Elements,
            Artifacts = loose.Artifacts,
            Untagged = loose.Untagged,
            Fonts = FontReader.Read(objects, pageTree, readable.ToDictionary(pair => pair.Key, pair => pair.Value.Page)),
            Annotations = AnnotationReader.ReadAll(objects, pageTree, structure.ReferencedObjects),
            FormFields = FormReader.Read(objects, catalog, pageTree),
            TextRuns = layouts.SelectMany(layout => layout.TextRuns).ToList(),
            Images = layouts.SelectMany(layout => layout.Images).ToList(),
            Paths = layouts.SelectMany(layout => layout.Paths).ToList(),
        };
    }

    private static (List<PageInfo> Pages, Dictionary<int, ReadPage> Readable) ReadPages(PdfDocument document, PdfObjects objects)
    {
        var pages = new List<PageInfo>();
        var readable = new Dictionary<int, ReadPage>();
        for (var number = 1; number <= document.NumberOfPages; number++)
        {
            if (TryGetPage(document, number) is not { } page)
            {
                pages.Add(new PageInfo(number, 0, 0, 0, null, false, Unreadable: true));
                continue;
            }

            pages.Add(ReadPageInfo(number, page, objects));
            readable[number] = new ReadPage(page, PageContentReader.Read(page));
        }

        return (pages, readable);
    }

    private static PageInfo ReadPageInfo(int number, Page page, PdfObjects objects)
    {
        var dictionary = page.Dictionary;
        // MediaBox size before rotation; the rotation is reported on its own.
        var size = page.MediaBox.Bounds;
        var hasAnnotations = objects.Items(dictionary.Data.GetValueOrDefault("Annots")).Count > 0;
        return new PageInfo(number, size.Width, size.Height, page.Rotation.Value, objects.Name(dictionary, "Tabs"), hasAnnotations)
        {
            CropBox = CropBoxOf(dictionary, objects),
            BleedBox = objects.Box(dictionary, "BleedBox"),
            TrimBox = objects.Box(dictionary, "TrimBox"),
            ArtBox = objects.Box(dictionary, "ArtBox"),
            UserUnit = objects.Get(dictionary, "UserUnit") is NumericToken unit ? unit.Double : null,
            Group = ReadGroup(dictionary, objects),
        };
    }

    private static PageGroup? ReadGroup(DictionaryToken page, PdfObjects objects)
    {
        if (objects.Dictionary(page, "Group") is not { } group)
        {
            return null;
        }

        var colorSpace = objects.Describe(group.Data.GetValueOrDefault("CS"));
        return new PageGroup(objects.Name(group, "S"), colorSpace, objects.Boolean(group, "I"), objects.Boolean(group, "K"));
    }

    // A CropBox can be set on a Pages node and inherited, so look up the /Parent chain when the page has none. The depth
    // limit stops a malformed loop of parents; real page trees are only a few levels deep.
    private static PageBox? CropBoxOf(DictionaryToken page, PdfObjects objects)
    {
        var node = page;
        for (var depth = 0; node is not null && depth < 32; depth++)
        {
            if (objects.Box(node, "CropBox") is { } box)
            {
                return box;
            }

            node = objects.Dictionary(node, "Parent");
        }

        return null;
    }

    // Artifact text, plus text assistive technology can't reach: text outside any tag, and tagged text that no
    // structure element points to.
    private static LooseContent CollectLooseContent(Dictionary<int, ReadPage> readable, IReadOnlySet<(int Page, int Mcid)> referenced)
    {
        var loose = new LooseContent([], []);
        foreach (var (number, (_, content)) in readable.OrderBy(pair => pair.Key))
        {
            loose.Artifacts.AddRange(content.Artifacts.Select(text => new PageText(number, text)));
            if (content.UntaggedText is not null)
            {
                loose.Untagged.Add(new PageText(number, content.UntaggedText));
            }

            foreach (var (mcid, letters) in content.LettersByMcid.OrderBy(pair => pair.Key))
            {
                if (!referenced.Contains((number, mcid)) && TextJoiner.Join(letters) is { Length: > 0 } text)
                {
                    loose.Untagged.Add(new PageText(number, text));
                }
            }
        }

        return loose;
    }

    // PdfPig loads a page's fonts when it loads the page, and some damaged fonts make it throw. That page is then
    // recorded as unreadable, so the snapshot shows it and the test fails, instead of the whole read failing.
    private static Page? TryGetPage(PdfDocument document, int number)
    {
        try
        {
            return document.GetPage(number);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private sealed record ReadPage(Page Page, PageContent Content);

    private sealed record LooseContent(List<PageText> Artifacts, List<PageText> Untagged);
}
