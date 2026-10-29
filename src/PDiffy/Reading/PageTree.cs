using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>Each page's raw dictionary and resources, read straight from the page tree so they're available even when PdfPig can't load the page.</summary>
internal sealed class PageTree
{
    private readonly Dictionary<IndirectReference, int> _numbers = [];
    private readonly List<PageEntry> _pages = [];
    private readonly HashSet<IndirectReference> _visited = [];

    public IReadOnlyList<PageEntry> Pages => _pages;

    public int? NumberOf(IToken? pageReference) =>
        pageReference is IndirectReferenceToken reference && _numbers.TryGetValue(reference.Data, out var number) ? number : null;

    public static PageTree Read(PdfObjects objects, DictionaryToken catalog)
    {
        var tree = new PageTree();
        tree.Add(objects, catalog.Data["Pages"], inheritedResources: null);
        return tree;
    }

    // Resources can be set on a Pages node and inherited by every page below it.
    // A node reached a second time (a malformed loop) is skipped.
    private void Add(PdfObjects objects, IToken node, DictionaryToken? inheritedResources)
    {
        if (node is IndirectReferenceToken visit && !_visited.Add(visit.Data))
        {
            return;
        }

        if (objects.Resolve(node) is not DictionaryToken dictionary)
        {
            return;
        }

        var resources = objects.Dictionary(dictionary, "Resources") ?? inheritedResources;
        // Some writers leave out /Type, so a node with /Kids is treated as an intermediate node too.
        if (objects.Name(dictionary, "Type") == "Pages" || dictionary.Data.ContainsKey("Kids"))
        {
            foreach (var kid in objects.Items(dictionary.Data.GetValueOrDefault("Kids")))
            {
                Add(objects, kid, resources);
            }

            return;
        }

        _pages.Add(new PageEntry(dictionary, resources));
        if (node is IndirectReferenceToken reference)
        {
            _numbers[reference.Data] = _pages.Count;
        }
    }
}

internal sealed record PageEntry(DictionaryToken Dictionary, DictionaryToken? Resources);
