using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <param name="ReferencedMcids">Every (page, MCID) an element points to, so content no element reaches can be found.</param>
/// <param name="ReferencedObjects">Every object, such as an annotation, that an element points to.</param>
internal sealed record StructureTree(
    IReadOnlyList<PdfRoleMapping> RoleMap, IReadOnlyList<PdfStructureElement> Elements,
    IReadOnlySet<(int Page, MarkedContentId Mcid)> ReferencedMcids,
    IReadOnlySet<IndirectReference> ReferencedObjects);

/// <summary>Turns the structure tree into <see cref="PdfStructureElement"/>s, joining each element to its marked-content text.</summary>
internal sealed class StructureTreeReader(PdfObjects objects, PageTree pages, DestinationReader destinations,
    Func<int, PageContent?> contentOf)
{
    private readonly Dictionary<string, string> _roleMap = [];
    private readonly Dictionary<string, string> _namesById = [];
    private readonly HashSet<(int Page, MarkedContentId Mcid)> _referencedMcids = [];
    private readonly HashSet<IndirectReference> _referencedObjects = [];
    private readonly HashSet<IndirectReference> _readElements = [];

    /// <param name="contentOf">What a page's content stream holds behind each MCID, given the page number.</param>
    public static StructureTree Read(PdfObjects objects, DictionaryToken catalog, PageTree pages,
        DestinationReader destinations, Func<int, PageContent?> contentOf)
    {
        var root = objects.Dictionary(catalog, "StructTreeRoot");
        if (root is null)
        {
            return new StructureTree([], [], new HashSet<(int, MarkedContentId)>(), new HashSet<IndirectReference>());
        }

        return new StructureTreeReader(objects, pages, destinations, contentOf).ReadRoot(root);
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
        var read = topLevel.Select(element => ReadElement(element, inheritedPage: null, depth: 1).Element).ToList();
        var elements = StructurePaths.Assign(read, StructurePaths.Root);
        var mappings = _roleMap.Select(pair => new PdfRoleMapping { Custom = pair.Key, Standard = pair.Value }).ToList();
        return new StructureTree(mappings, elements, _referencedMcids, _referencedObjects);
    }

    // Elements nested deeper than ReadLimits.MaxDepth are counted, not read, so a hostile tree can't overflow the stack.
    // Also gives the letters of the element and its descendants, in the order it lists them, for its parent's AllText.
    private (PdfStructureElement Element, List<(int Page, IReadOnlyList<Letter> Letters)> AllPieces) ReadElement(
        DictionaryToken element, int? inheritedPage, int depth)
    {
        var type = objects.Name(element, "S") ?? StructureTypes.None;
        var page = pages.NumberOf(element.Data.GetValueOrDefault("Pg")) ?? inheritedPage;
        var pieces = new List<(int Page, IReadOnlyList<Letter> Letters)>();
        var allPieces = new List<(int Page, IReadOnlyList<Letter> Letters)>();
        var spans = new List<PdfMarkedSpan>();
        var images = new List<string>();
        var textRuns = new List<PdfTextRun>();
        var pagesUsed = new SortedSet<int>();
        var annotations = new List<PdfAnnotation>();
        var children = new List<PdfStructureElement>();
        var mcidsWithoutPage = 0;
        var mcidsInXObject = 0;
        var unreadChildren = 0;

        foreach (var kid in objects.Items(element.Data.GetValueOrDefault("K")))
        {
            switch (objects.Resolve(kid))
            {
                case NumericToken mcid when page is int mcidPage:
                    AddText(mcidPage, new MarkedContentId(null, mcid.Int));
                    break;
                case NumericToken:
                    mcidsWithoutPage++;
                    break;
                case DictionaryToken reference when objects.Name(reference, "Type") == "MCR":
                    // A form XObject numbers its marked content on its own, so a reference into one names the form.
                    var referencePage = pages.NumberOf(reference.Data.GetValueOrDefault("Pg")) ?? page;
                    var form = reference.Data.GetValueOrDefault("Stm") is IndirectReferenceToken stream ? stream.Data
                        : (IndirectReference?)null;
                    if (referencePage is null) mcidsWithoutPage++;
                    else if (objects.Integer(reference, "MCID") is int mcrMcid)
                    {
                        AddText(referencePage.Value, new MarkedContentId(form, mcrMcid));
                    }

                    if (referencePage is not null && reference.Data.ContainsKey("Stm")) mcidsInXObject++;
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
                        var read = ReadElement(child, page, depth + 1);
                        children.Add(read.Element);
                        allPieces.AddRange(read.AllPieces);
                    }

                    break;
            }
        }

        void AddText(int textPage, MarkedContentId reference)
        {
            // On a page whose MCIDs couldn't be told apart by stream, a reference into a form reads the page's MCID.
            var mcid = contentOf(textPage)?.FormsKnown == false ? reference with { Form = null } : reference;
            _referencedMcids.Add((textPage, mcid));
            pagesUsed.Add(textPage);
            var content = contentOf(textPage);
            if (content?.LettersByMcid.GetValueOrDefault(mcid) is { Count: > 0 } letters)
            {
                pieces.Add((textPage, letters));
                allPieces.Add((textPage, letters));
            }

            if (content?.SpansByMcid.GetValueOrDefault(mcid) is { } found) spans.AddRange(found);
            if (content?.ImagesByMcid.GetValueOrDefault(mcid) is { } hashes) images.AddRange(hashes);
            if (content?.TextRunsByMcid.GetValueOrDefault(mcid) is { } runs) textRuns.AddRange(runs);
        }

        var attributes = Attributes(element);
        var result = new PdfStructureElement
        {
            Type = type,
            MappedType = MapRole(type),
            Name = objects.Text(element, "ID") is { } id ? _namesById.GetValueOrDefault(id) : null,
            FirstPage = pagesUsed.Count > 0 ? pagesUsed.Min : null,
            LastPage = pagesUsed.Count > 0 ? pagesUsed.Max : null,
            Language = objects.Text(element, "Lang"),
            AltText = objects.Text(element, "Alt"),
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
            McidsInXObject = mcidsInXObject,
            UnreadChildren = unreadChildren,
            ParentMissing = !element.Data.ContainsKey("P"),
            MarkedSpans = spans,
            ImageHashes = images,
            TextRuns = textRuns,
            Text = Join(pieces),
            AllText = Join(allPieces),
            Children = children,
        };
        return (result, allPieces);
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
            || TextJoiner.Gap(last, first) >= -last.PointSize * TextJoiner.SpaceGap);
    }

    private PdfAnnotation? ReadAnnotation(DictionaryToken objectReference, int? elementPage)
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
        return AnnotationReader.Read(objects, annotation, page, PdfContentRole.Tagged, destinations);
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
                var type = objects.Name(element, "S") ?? StructureTypes.None;
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
