using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <param name="ReferencedMcids">Every (page, MCID) an element points to, so content no element reaches can be found.</param>
/// <param name="ReferencedObjects">Every object, such as an annotation, that an element points to.</param>
internal sealed record StructureTree(
    IReadOnlyList<RoleMapping> RoleMap, IReadOnlyList<StructureElement> Elements, IReadOnlySet<(int Page, int Mcid)> ReferencedMcids,
    IReadOnlySet<IndirectReference> ReferencedObjects);

/// <summary>Turns the structure tree into <see cref="StructureElement"/>s, joining each element to its marked-content text.</summary>
internal sealed class StructureTreeReader(PdfObjects objects, PageTree pages,
    Func<int, int, IReadOnlyList<Letter>?> lettersOf)
{
    private readonly Dictionary<string, string> _roleMap = [];
    private readonly Dictionary<string, string> _namesById = [];
    private readonly HashSet<(int Page, int Mcid)> _referencedMcids = [];
    private readonly HashSet<IndirectReference> _referencedObjects = [];
    private readonly HashSet<IndirectReference> _readElements = [];

    /// <param name="lettersOf">The letters behind an MCID, given (page number, MCID).</param>
    public static StructureTree Read(PdfObjects objects, DictionaryToken catalog, PageTree pages,
        Func<int, int, IReadOnlyList<Letter>?> lettersOf)
    {
        var root = objects.Dictionary(catalog, "StructTreeRoot");
        if (root is null)
        {
            return new StructureTree([], [], new HashSet<(int, int)>(), new HashSet<IndirectReference>());
        }

        return new StructureTreeReader(objects, pages, lettersOf).ReadRoot(root);
    }

    private StructureTree ReadRoot(DictionaryToken root)
    {
        var roleMap = objects.Dictionary(root, "RoleMap");
        foreach (var (custom, standard) in roleMap?.Data ?? new Dictionary<string, IToken>())
        {
            if (objects.Resolve(standard) is NameToken name)
            {
                _roleMap[custom] = name.Data;
            }
        }

        var topLevel = Elements(root, _readElements).ToList();
        NameReferencedHeaders(topLevel);
        var elements = topLevel.Select(element => ReadElement(element, inheritedPage: null, depth: 1)).ToList();
        var mappings = _roleMap.Select(pair => new RoleMapping(pair.Key, pair.Value)).ToList();
        return new StructureTree(mappings, elements, _referencedMcids, _referencedObjects);
    }

    // Elements nested deeper than ReadLimits.MaxDepth are counted, not read, so a hostile tree can't overflow the stack.
    private StructureElement ReadElement(DictionaryToken element, int? inheritedPage, int depth)
    {
        var type = objects.Name(element, "S") ?? "(none)";
        var page = pages.NumberOf(element.Data.GetValueOrDefault("Pg")) ?? inheritedPage;
        var pieces = new List<(int Page, IReadOnlyList<Letter> Letters)>();
        var pagesUsed = new SortedSet<int>();
        var annotations = new List<Annotation>();
        var children = new List<StructureElement>();
        var mcidsWithoutPage = 0;
        var unreadChildren = 0;

        foreach (var kid in objects.Items(element.Data.GetValueOrDefault("K")))
        {
            switch (objects.Resolve(kid))
            {
                case NumericToken mcid when page is int mcidPage:
                    AddText(mcidPage, mcid.Int);
                    break;
                case NumericToken:
                    mcidsWithoutPage++;
                    break;
                case DictionaryToken reference when objects.Name(reference, "Type") == "MCR":
                    var referencePage = pages.NumberOf(reference.Data.GetValueOrDefault("Pg")) ?? page;
                    if (referencePage is null) mcidsWithoutPage++;
                    else if (objects.Integer(reference, "MCID") is int mcrMcid) AddText(referencePage.Value, mcrMcid);
                    break;
                case DictionaryToken reference when objects.Name(reference, "Type") == "OBJR":
                    if (ReadAnnotation(reference, page) is { } annotation)
                    {
                        annotations.Add(annotation);
                    }

                    if (pages.NumberOf(reference.Data.GetValueOrDefault("Pg")) is int objectPage)
                    {
                        pagesUsed.Add(objectPage);
                    }

                    break;
                case DictionaryToken child when IsElement(child):
                    if (depth == ReadLimits.MaxDepth)
                    {
                        unreadChildren++;
                    }
                    else if (IsFirstVisit(kid, _readElements))
                    {
                        children.Add(ReadElement(child, page, depth + 1));
                    }

                    break;
            }
        }

        void AddText(int textPage, int mcid)
        {
            _referencedMcids.Add((textPage, mcid));
            pagesUsed.Add(textPage);
            if (lettersOf(textPage, mcid) is { Count: > 0 } letters)
            {
                pieces.Add((textPage, letters));
            }
        }

        var attributes = Attributes(element);
        return new StructureElement
        {
            Type = type,
            MappedType = MapRole(type),
            Name = objects.Text(element, "ID") is { } id ? _namesById.GetValueOrDefault(id) : null,
            FirstPage = pagesUsed.Count > 0 ? pagesUsed.Min : null,
            LastPage = pagesUsed.Count > 0 ? pagesUsed.Max : null,
            Lang = objects.Text(element, "Lang"),
            Alt = objects.Text(element, "Alt"),
            ActualText = objects.Text(element, "ActualText"),
            Expansion = objects.Text(element, "E"),
            Title = objects.Text(element, "T"),
            Scope = FirstAttribute(attributes, a => objects.Name(a, "Scope")),
            Headers = HeaderIds(attributes)?.Select(id => _namesById.GetValueOrDefault(id) ?? "?" + id).ToList(),
            RowSpan = FirstAttribute(attributes, a => objects.Integer(a, "RowSpan")),
            ColSpan = FirstAttribute(attributes, a => objects.Integer(a, "ColSpan")),
            ListNumbering = FirstAttribute(attributes, a => objects.Name(a, "ListNumbering")),
            Summary = FirstAttribute(attributes, a => objects.Text(a, "Summary")),
            Annotations = annotations,
            McidsWithoutPage = mcidsWithoutPage,
            UnreadChildren = unreadChildren,
            Text = Join(pieces),
            Children = children,
        };
    }

    // Join pieces that follow on from each other by position, so a word split across MCIDs stays one word.
    private static string? Join(List<(int Page, IReadOnlyList<Letter> Letters)> pieces)
    {
        var texts = new List<string>();
        for (var i = 0; i < pieces.Count;)
        {
            var letters = new List<Letter>(pieces[i++].Letters);
            while (i < pieces.Count && FollowsOn(pieces[i - 1], pieces[i])) letters.AddRange(pieces[i++].Letters);
            texts.Add(TextJoiner.Join(letters));
        }

        var text = TextJoiner.Normalize(string.Join(" ", texts));
        return text.Length > 0 ? text : null;
    }

    // Different pages can't be compared, and a piece starting well before the last one ended was drawn elsewhere.
    private static bool FollowsOn((int Page, IReadOnlyList<Letter> Letters) previous,
        (int Page, IReadOnlyList<Letter> Letters) next)
    {
        var (last, first) = (previous.Letters[^1], next.Letters[0]);
        return next.Page == previous.Page && (!TextJoiner.OnSameLine(last, first)
            || first.StartBaseLine.X >= last.EndBaseLine.X - last.PointSize * TextJoiner.SpaceGap);
    }

    private Annotation? ReadAnnotation(DictionaryToken objectReference, int? elementPage)
    {
        if (objectReference.Data.GetValueOrDefault("Obj") is IndirectReferenceToken target)
        {
            _referencedObjects.Add(target.Data);
        }

        if (objects.Dictionary(objectReference, "Obj") is not { } annotation)
        {
            return null;
        }

        var page = pages.NumberOf(objectReference.Data.GetValueOrDefault("Pg")) ?? elementPage;
        return AnnotationReader.Read(objects, annotation, page, ContentRole.Tagged);
    }

    // /Headers lists other elements' /ID values. In the snapshot, unresolved ones print as ?id so a broken reference shows up.
    private IReadOnlyList<string>? HeaderIds(IReadOnlyList<DictionaryToken> attributes)
    {
        var headers = FirstAttribute(attributes, a => objects.Get(a, "Headers"));
        if (headers is null)
        {
            return null;
        }

        return objects.Items(headers).Select(item => PdfObjects.TextOf(objects.Resolve(item)) ?? "").ToList();
    }

    // Only elements that a /Headers entry refers to get a name (TH1, TH2), numbered per type in reading order.
    private void NameReferencedHeaders(IReadOnlyList<DictionaryToken> topLevel)
    {
        var allElements = new List<DictionaryToken>();
        var visited = new HashSet<IndirectReference>();
        foreach (var element in topLevel)
        {
            AddWithDescendants(element, allElements, visited, depth: 1);
        }

        var referenced = new HashSet<string>();
        foreach (var element in allElements)
        {
            referenced.UnionWith(HeaderIds(Attributes(element)) ?? []);
        }

        var countByType = new Dictionary<string, int>();
        foreach (var element in allElements)
        {
            if (objects.Text(element, "ID") is { } id && referenced.Contains(id) && !_namesById.ContainsKey(id))
            {
                var type = objects.Name(element, "S") ?? "(none)";
                countByType[type] = countByType.GetValueOrDefault(type) + 1;
                _namesById[id] = type + countByType[type];
            }
        }
    }

    // Role maps can chain (custom -> custom -> standard); follow the chain to its end, stopping on a loop.
    private string? MapRole(string type)
    {
        var current = type;
        var seen = new HashSet<string> { type };
        while (_roleMap.TryGetValue(current, out var next) && seen.Add(next))
        {
            current = next;
        }

        return current == type ? null : current;
    }

    private void AddWithDescendants(DictionaryToken element, List<DictionaryToken> allElements, HashSet<IndirectReference> visited,
        int depth)
    {
        allElements.Add(element);
        if (depth == ReadLimits.MaxDepth) return;
        foreach (var child in Elements(element, visited))
        {
            AddWithDescendants(child, allElements, visited, depth + 1);
        }
    }

    private IEnumerable<DictionaryToken> Elements(DictionaryToken parent, HashSet<IndirectReference> visited)
    {
        foreach (var kid in objects.Items(parent.Data.GetValueOrDefault("K")))
        {
            if (IsFirstVisit(kid, visited) && objects.Resolve(kid) is DictionaryToken child && IsElement(child))
            {
                yield return child;
            }
        }
    }

    // A malformed structure tree can loop back on itself; an element reached a second time is skipped.
    private static bool IsFirstVisit(IToken kid, HashSet<IndirectReference> visited) =>
        kid is not IndirectReferenceToken reference || visited.Add(reference.Data);

    // An element can have several attribute dictionaries; the first one that sets a key wins.
    private static T? FirstAttribute<T>(IReadOnlyList<DictionaryToken> attributes, Func<DictionaryToken, T?> read) =>
        attributes.Select(read).FirstOrDefault(value => value is not null);

    // /A holds one attribute dictionary or an array of them.
    private IReadOnlyList<DictionaryToken> Attributes(DictionaryToken element) =>
        objects.Items(element.Data.GetValueOrDefault("A")).Select(objects.Resolve).OfType<DictionaryToken>().ToList();

    private bool IsElement(DictionaryToken dictionary) =>
        dictionary.Data.ContainsKey("S") && objects.Name(dictionary, "Type") is null or "StructElem";
}
