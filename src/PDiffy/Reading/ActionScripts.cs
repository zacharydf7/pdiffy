using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>Finds JavaScript in the actions of annotations, pages and bookmarks.</summary>
internal static class ActionScripts
{
    public static bool AnyIn(PdfObjects objects, PageTree pages, DictionaryToken catalog)
    {
        foreach (var page in pages.Pages)
        {
            if (AnyEventRuns(objects, page.Dictionary)) return true;
            foreach (var item in objects.Items(page.Dictionary.Data.GetValueOrDefault("Annots")))
            {
                if (objects.Resolve(item) is not DictionaryToken annotation) continue;
                var action = annotation.Data.GetValueOrDefault("A");
                if (Runs(objects, action) || AnyEventRuns(objects, annotation)) return true;
            }
        }

        return Bookmarks(objects, objects.Dictionary(catalog, "Outlines"))
            .Any(bookmark => Runs(objects, bookmark.Data.GetValueOrDefault("A")));
    }

    // Whether an action, or one it chains through /Next, is JavaScript.
    private static bool Runs(PdfObjects objects, IToken? action) =>
        objects.Chain(action).Any(chained => objects.Name(chained, "S") == "JavaScript");

    // /AA maps each event, such as a page opening (/O), to an action.
    private static bool AnyEventRuns(PdfObjects objects, DictionaryToken owner) =>
        objects.Dictionary(owner, "AA")?.Data.Values.Any(action => Runs(objects, action)) == true;

    // Each bookmark points to its first child and next sibling; one reached again (a malformed loop) is skipped.
    private static IEnumerable<DictionaryToken> Bookmarks(PdfObjects objects, DictionaryToken? outlines)
    {
        var visited = new HashSet<IndirectReference>();
        var pending = new Stack<IToken?>([outlines?.Data.GetValueOrDefault("First")]);
        while (pending.TryPop(out var next))
        {
            if (next is IndirectReferenceToken reference && !visited.Add(reference.Data)) continue;
            if (objects.Resolve(next) is not DictionaryToken bookmark) continue;

            yield return bookmark;
            pending.Push(bookmark.Data.GetValueOrDefault("Next"));
            pending.Push(bookmark.Data.GetValueOrDefault("First"));
        }
    }
}
