namespace PDiffy;

/// <summary>Narrows a document to some of its pages, for <see cref="PdfSnapshotOptions.OnlyPages"/>.</summary>
internal static class PageScope
{
    public static PdfUaDocument Keep(PdfUaDocument document, IReadOnlySet<int> pages)
    {
        var count = document.Document.PageCount;
        var last = pages.Max();
        if (last > count)
        {
            throw new ArgumentOutOfRangeException(nameof(pages), $"Page {last} is past the end of a {count}-page document.");
        }

        return Filter(document, pages);
    }

    private static PdfUaDocument Filter(PdfUaDocument document, IReadOnlySet<int> pages) => document with
    {
        Pages = document.Pages.Where(page => pages.Contains(page.Number)).ToList(),
        Bookmarks = KeepBookmarks(document.Bookmarks, pages),
        Structure = KeepElements(document.Structure, pages),
        Artifacts = document.Artifacts.Where(text => pages.Contains(text.Page)).ToList(),
        Untagged = document.Untagged.Where(text => pages.Contains(text.Page)).ToList(),
        Annotations = document.Annotations.Where(annotation => OnPage(annotation.Page, pages)).ToList(),
        FormFields = document.FormFields.Where(field => OnPage(field.Page, pages)).ToList(),
        TextRuns = document.TextRuns.Where(run => pages.Contains(run.Page)).ToList(),
        Images = document.Images.Where(image => pages.Contains(image.Page)).ToList(),
        Paths = document.Paths.Where(path => pages.Contains(path.Page)).ToList(),
    };

    private static List<PdfStructureElement> KeepElements(IReadOnlyList<PdfStructureElement> elements, IReadOnlySet<int> pages)
    {
        var kept = new List<PdfStructureElement>();
        foreach (var element in elements)
        {
            var children = KeepElements(element.Children, pages);
            var last = element.LastPage ?? element.FirstPage;
            var onPage = element.FirstPage is int first && pages.Any(page => page >= first && page <= last);
            if (onPage || children.Count > 0) kept.Add(element with { Children = children });
        }

        return kept;
    }

    private static List<PdfBookmark> KeepBookmarks(IReadOnlyList<PdfBookmark> bookmarks, IReadOnlySet<int> pages)
    {
        var kept = new List<PdfBookmark>();
        foreach (var bookmark in bookmarks)
        {
            var children = KeepBookmarks(bookmark.Children, pages);
            if (OnPage(bookmark.Page, pages) || children.Count > 0) kept.Add(bookmark with { Children = children });
        }

        return kept;
    }

    private static bool OnPage(int? page, IReadOnlySet<int> pages) => page is int number && pages.Contains(number);
}
