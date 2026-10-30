using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Exceptions;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>Reads a PDF into a <see cref="PdfUaDocument"/> using PdfPig.</summary>
internal static class PdfPigReader
{
    public static PdfUaDocument Read(Stream pdf)
    {
        var fileLength = pdf.Length;
        using var document = Open(pdf);
        var objects = new PdfObjects(document);
        var catalog = document.Structure.Catalog.CatalogDictionary;
        var pageTree = PageTree.Read(objects, catalog);
        var destinations = new DestinationReader(objects, pageTree, catalog);

        var (pages, readable) = ReadPages(document, objects);
        var structure = StructureTreeReader.Read(objects, catalog, pageTree, destinations,
            page => readable.TryGetValue(page, out var read) ? read.Content : null);
        var loose = CollectLooseContent(readable, structure.ReferencedMcids);
        var (bookmarks, bookmarksUnreadable) = NavigationReader.ReadBookmarks(document, objects, catalog);
        var (formFields, form) = FormReader.Read(objects, catalog, pageTree, fileLength);
        var settings = DocumentSettingsReader.Read(document, objects, catalog, destinations);
        var layouts = readable.OrderBy(pair => pair.Key)
            .Select(pair => LayoutReader.Read(pair.Key, pair.Value.Page, pair.Value.Content, structure.ReferencedMcids))
            .ToList();

        return new PdfUaDocument
        {
            Document = MetadataReader.ReadDocumentInfo(objects, catalog, document.NumberOfPages),
            Metadata = MetadataReader.ReadMetadata(document, objects),
            Settings = settings with
            {
                HasJavaScript = settings.HasJavaScript || formFields.Any(field => field.Scripts.Count > 0)
                    || ActionScripts.AnyIn(objects, pageTree, catalog),
            },
            RoleMap = structure.RoleMap,
            Pages = pages,
            PageLabels = NavigationReader.ReadPageLabels(objects, catalog),
            Bookmarks = bookmarks,
            BookmarksUnreadable = bookmarksUnreadable,
            Structure = structure.Elements,
            Artifacts = loose.Artifacts,
            Untagged = loose.Untagged,
            Fonts = FontReader.Read(objects, pageTree, readable.ToDictionary(pair => pair.Key, pair => pair.Value.Page)),
            Annotations = AnnotationReader.ReadAll(objects, pageTree, structure.ReferencedObjects, destinations),
            FormFields = formFields,
            Form = form,
            TextRuns = layouts.SelectMany(layout => layout.TextRuns).ToList(),
            Images = layouts.SelectMany(layout => layout.Images).ToList(),
            Paths = layouts.SelectMany(layout => layout.Paths).ToList(),
        };
    }

    // A file PdfPig can't open fails the way input that isn't a PDF does, so callers see one exception type.
    private static PdfDocument Open(Stream pdf)
    {
        try
        {
            return PdfDocument.Open(pdf, new ParsingOptions { FilterProvider = CappedFilterProvider.Instance });
        }
        catch (PdfDocumentEncryptedException problem)
        {
            throw new InvalidDataException($"The PDF is encrypted, and PDiffy can't open it: {problem.Message}", problem);
        }
        catch (ArgumentOutOfRangeException problem)
        {
            // PdfPig seeks to where the file says each object is, which can be past its end.
            throw new InvalidDataException("The PDF is too damaged to open: a position in the file points outside it.",
                problem);
        }
        catch (Exception problem)
        {
            // PdfPig throws many exception types on damaged data, as PdfObjects.Contents notes.
            throw new InvalidDataException($"The PDF is too damaged to open: {problem.Message}", problem);
        }
    }

    private static (List<PdfPage> Pages, Dictionary<int, ReadPage> Readable) ReadPages(PdfDocument document, PdfObjects objects)
    {
        var pages = new List<PdfPage>();
        var readable = new Dictionary<int, ReadPage>();
        for (var number = 1; number <= document.NumberOfPages; number++)
        {
            if (TryGetPage(document, number) is not { } page)
            {
                pages.Add(new PdfPage
                {
                    Number = number, Width = 0, Height = 0, Rotation = 0, Tabs = null, HasAnnotations = false,
                    Unreadable = true,
                });
                continue;
            }

            pages.Add(ReadPageInfo(number, page, objects));
            readable[number] = new ReadPage(page, PageContentReader.Read(page, objects));
        }

        return (pages, readable);
    }

    private static PdfPage ReadPageInfo(int number, Page page, PdfObjects objects)
    {
        var dictionary = page.Dictionary;
        // MediaBox size before rotation; the rotation is reported on its own.
        var size = page.MediaBox.Bounds;
        var hasAnnotations = objects.Items(dictionary.Data.GetValueOrDefault("Annots")).Count > 0;
        return new PdfPage
        {
            Number = number, Width = size.Width, Height = size.Height, Rotation = page.Rotation.Value,
            Tabs = objects.Name(dictionary, "Tabs"), HasAnnotations = hasAnnotations,
            CropBox = CropBoxOf(dictionary, objects), BleedBox = objects.Box(dictionary, "BleedBox"),
            TrimBox = objects.Box(dictionary, "TrimBox"), ArtBox = objects.Box(dictionary, "ArtBox"),
            UserUnit = objects.Get(dictionary, "UserUnit") is NumericToken unit ? unit.Double : null,
            Group = ReadGroup(dictionary, objects),
        };
    }

    private static PdfPageGroup? ReadGroup(DictionaryToken page, PdfObjects objects)
    {
        if (objects.Dictionary(page, "Group") is not { } group)
        {
            return null;
        }

        var colorSpace = objects.Describe(group.Data.GetValueOrDefault("CS"));
        return new PdfPageGroup
        {
            Type = objects.Name(group, "S"), ColorSpace = colorSpace, Isolated = objects.Boolean(group, "I"),
            Knockout = objects.Boolean(group, "K"),
        };
    }

    // A CropBox can be set on a Pages node and inherited, so look up the /Parent chain when the page has none. The depth
    // limit stops a malformed loop of parents; real page trees are only a few levels deep.
    private static PdfBox? CropBoxOf(DictionaryToken page, PdfObjects objects)
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
            loose.Artifacts.AddRange(content.Artifacts);
            loose.Untagged.AddRange(content.UntaggedLines.Select(line => new PdfPageText
            {
                Page = number, Text = line,
            }));

            foreach (var (mcid, letters) in content.LettersByMcid.OrderBy(pair => pair.Key))
            {
                if (!referenced.Contains((number, mcid)) && TextJoiner.Join(letters) is { Length: > 0 } text)
                {
                    loose.Untagged.Add(new PdfPageText { Page = number, Text = text });
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

    private sealed record LooseContent(List<PdfPageText> Artifacts, List<PdfPageText> Untagged);
}
