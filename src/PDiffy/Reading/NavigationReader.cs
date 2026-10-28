using UglyToad.PdfPig;
using UglyToad.PdfPig.Outline;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>Bookmarks and page labels.</summary>
internal static class NavigationReader
{
    public static IReadOnlyList<Bookmark> ReadBookmarks(PdfDocument document)
    {
        if (!document.TryGetBookmarks(out var bookmarks))
        {
            return [];
        }

        return bookmarks.Roots.Select(ToBookmark).ToList();
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
        int? page = node is DocumentBookmarkNode documentNode ? documentNode.PageNumber : null;
        return new Bookmark(TextJoiner.Normalize(node.Title), page, node.Children.Select(ToBookmark).ToList());
    }
}
