using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Outline;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>Bookmarks and page labels.</summary>
internal static class NavigationReader
{
    /// <summary>
    /// The bookmarks, or none when they nest deeper than <see cref="ReadLimits.MaxDepth"/>, loop back on themselves or
    /// have an item PdfPig can't read, such as one without a title.
    /// </summary>
    public static (IReadOnlyList<Bookmark> Bookmarks, bool Unreadable) ReadBookmarks(PdfDocument document, PdfObjects objects,
        DictionaryToken catalog)
    {
        // PdfPig reads the outline recursively and doesn't notice a loop, so either would overflow the stack.
        if (IsTooDeepOrLooped(objects, objects.Dictionary(catalog, "Outlines")))
        {
            return ([], true);
        }

        // PdfPig refuses the whole outline when one item is damaged, such as one without a text title.
        Bookmarks? bookmarks;
        try
        {
            if (!document.TryGetBookmarks(out bookmarks)) return ([], false);
        }
        catch (PdfDocumentFormatException)
        {
            return ([], true);
        }

        return (bookmarks.Roots.Select(ToBookmark).ToList(), false);
    }

    // Each item points to its first child and its next sibling, so an item reached a second time means a loop.
    private static bool IsTooDeepOrLooped(PdfObjects objects, DictionaryToken? outlines)
    {
        var visited = new HashSet<IndirectReference>();
        var open = new Stack<(IToken? Item, int Depth)>();
        open.Push((outlines?.Data.GetValueOrDefault("First"), 1));
        while (open.TryPop(out var next))
        {
            if (next.Item is not IndirectReferenceToken reference) continue;
            if (!visited.Add(reference.Data)) return true;
            if (objects.Resolve(reference) is not DictionaryToken item) continue;
            if (next.Depth > ReadLimits.MaxDepth) return true;
            open.Push((item.Data.GetValueOrDefault("Next"), next.Depth));
            open.Push((item.Data.GetValueOrDefault("First"), next.Depth + 1));
        }

        return false;
    }

    // Page labels are a number tree of (page index, label) pairs.
    public static IReadOnlyList<PageLabelRange> ReadPageLabels(PdfObjects objects, DictionaryToken catalog)
    {
        var ranges = new List<PageLabelRange>();
        foreach (var (key, value) in objects.TreeEntries(catalog.Data.GetValueOrDefault("PageLabels"), "Nums"))
        {
            if (objects.Resolve(key) is NumericToken pageIndex && objects.Resolve(value) is DictionaryToken label)
            {
                var start = objects.Integer(label, "St") ?? 1;
                ranges.Add(new PageLabelRange(pageIndex.Int + 1, objects.Name(label, "S"), objects.Text(label, "P"), start));
            }
        }

        return ranges.OrderBy(range => range.FirstPage).ToList();
    }

    private static Bookmark ToBookmark(BookmarkNode node)
    {
        // PdfPig's bookmarks to another file are document bookmarks too, but their page is in that file.
        var document = node as DocumentBookmarkNode;
        var local = node is not (ExternalBookmarkNode or EmbeddedBookmarkNode);
        var page = local ? document?.PageNumber : null;
        return new Bookmark(TextJoiner.Normalize(node.Title), page, node.Children.Select(ToBookmark).ToList())
        {
            Destination = document is null ? null : DestinationReader.FromBookmark(document.Destination),
            Uri = (node as UriBookmarkNode)?.Uri,
            File = node switch
            {
                ExternalBookmarkNode external => external.FileName,
                EmbeddedBookmarkNode embedded => embedded.FileSpecification,
                _ => null,
            },
        };
    }
}
