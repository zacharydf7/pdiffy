using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>Which marked content a piece of page content sits in: an artifact, an MCID, or neither (no owner at all).</summary>
internal readonly record struct ContentOwner(bool IsArtifact, MarkedContentId? Mcid);

internal sealed class ContentOwners
{
    public Dictionary<Letter, ContentOwner> Letters { get; } = new(ReferenceEqualityComparer.Instance);
    public Dictionary<PdfPath, ContentOwner> Paths { get; } = new(ReferenceEqualityComparer.Instance);

    // PdfPig builds new image objects on every call, so images can't be looked up by reference; LayoutReader matches them by
    // position. Each is hashed once here, for the element that owns it, and LayoutReader reuses the hash.
    public List<(IPdfImage Image, ContentOwner Owner, string Hash)> Images { get; } = [];
}

/// <summary>
/// What one page's content stream says: the letters behind each MCID, the marked content in each that sets its own
/// language or replacement text, the artifacts, untagged text, and who owns what.
/// </summary>
/// <param name="LettersByMcid">Each MCID's letters, the MCIDs in the order the page first draws them.</param>
/// <param name="FormsKnown">
/// Whether each MCID's content stream is known. When it isn't, every MCID is keyed as the page's own.
/// </param>
internal sealed record PageContent(OrderedDictionary<MarkedContentId, List<Letter>> LettersByMcid,
    IReadOnlyDictionary<MarkedContentId, List<PdfMarkedSpan>> SpansByMcid,
    IReadOnlyDictionary<MarkedContentId, List<string>> ImagesByMcid, IReadOnlyList<PdfPageText> Artifacts,
    IReadOnlyList<string> UntaggedLines, ContentOwners Owners, IReadOnlyList<OwnedRun> TextRuns,
    IReadOnlyDictionary<MarkedContentId, List<PdfTextRun>> TextRunsByMcid, ContentLayers Layers, bool FormsKnown);

internal sealed class PageContentReader
{
    private readonly ContentOwners _owners = new();
    private readonly List<PdfPageText> _artifacts = [];
    private readonly Dictionary<MarkedContentId, List<PdfMarkedSpan>> _spansByMcid = [];

    // The content stream each marked-content element is in: the page's own (null) or a form XObject's.
    private readonly Dictionary<MarkedContentElement, IndirectReference?> _streams =
        new(ReferenceEqualityComparer.Instance);

    // Each letter's place in page.Letters, which is in content order, so letters gathered by nesting can be put back.
    private readonly Dictionary<Letter, int> _drawingOrder = [];

    public static PageContent Read(Page page, DictionaryToken? resources, PdfObjects objects, FormXObjects forms) =>
        new PageContentReader(page.Number, objects).ReadPage(page, resources, forms);

    private readonly int _pageNumber;
    private readonly PdfObjects _objects;

    private PageContentReader(int pageNumber, PdfObjects objects) => (_pageNumber, _objects) = (pageNumber, objects);

    private PageContent ReadPage(Page page, DictionaryToken? resources, FormXObjects forms)
    {
        for (var i = 0; i < page.Letters.Count; i++) _drawingOrder[page.Letters[i]] = i;
        var markedContents = page.GetMarkedContents();
        var formsKnown = markedContents.Count == 0
            || MatchStreams(markedContents, MarkedContentStreams.Read(page.Operations, resources, forms));
        var pending = new Stack<(MarkedContentElement MarkedContent, MarkedContentId? EnclosingMcid)>();
        foreach (var markedContent in markedContents.Reverse())
        {
            pending.Push((markedContent, null));
        }

        while (pending.TryPop(out var next)) Visit(next.MarkedContent, next.EnclosingMcid, pending);

        // page.Letters is in content order, so each MCID's letters come out in reading order.
        var lettersByMcid = new OrderedDictionary<MarkedContentId, List<Letter>>();
        var untagged = new List<Letter>();
        foreach (var letter in page.Letters)
        {
            if (!_owners.Letters.TryGetValue(letter, out var owner))
            {
                untagged.Add(letter);
            }
            else if (owner.Mcid is { } mcid)
            {
                if (!lettersByMcid.TryGetValue(mcid, out var letters))
                {
                    letters = [];
                    lettersByMcid[mcid] = letters;
                }

                letters.Add(letter);
            }
        }

        var imagesByMcid = _owners.Images.Where(image => image.Owner.Mcid is not null).GroupBy(image => image.Owner.Mcid!.Value)
            .ToDictionary(group => group.Key, group => group.Select(image => image.Hash).ToList());
        var layers = ContentLayers.Read(markedContents, _objects);
        var textRuns = TextRunReader.Read(_pageNumber, page, _owners, layers);
        var textRunsByMcid = textRuns.Where(owned => owned.Mcid is not null).GroupBy(owned => owned.Mcid!.Value)
            .ToDictionary(group => group.Key, group => group.Select(owned => owned.Run).ToList());
        return new PageContent(lettersByMcid, _spansByMcid, imagesByMcid, _artifacts, TextJoiner.JoinLines(untagged), _owners,
            textRuns, textRunsByMcid, layers, formsKnown);
    }

    // Each element's content stream, matched to the sequences in the order they begin. When the counts disagree, as for
    // a form nested deeper than PDiffy follows, nothing is matched and every MCID is keyed as the page's own.
    private bool MatchStreams(IReadOnlyList<MarkedContentElement> markedContents, List<IndirectReference?> streams)
    {
        var elements = markedContents.SelectMany(SelfAndDescendants).ToList();
        if (elements.Count != streams.Count) return false;
        for (var i = 0; i < elements.Count; i++) _streams[elements[i]] = streams[i];
        return true;
    }

    // Marked content without its own MCID belongs to the nearest enclosing MCID, if there is one. Children go on the
    // stack instead of being visited in turn, so content nested thousands deep can't overflow it.
    private void Visit(MarkedContentElement markedContent, MarkedContentId? enclosingMcid,
        Stack<(MarkedContentElement MarkedContent, MarkedContentId? EnclosingMcid)> pending)
    {
        if (markedContent.IsArtifact)
        {
            var artifact = new ContentOwner(IsArtifact: true, Mcid: null);
            var letters = new List<Letter>();
            foreach (var element in SelfAndDescendants(markedContent))
            {
                letters.AddRange(element.Letters);
                Own(element, artifact);
            }

            var text = TextJoiner.Join(InDrawingOrder(letters));
            if (text.Length > 0)
            {
                var kind = markedContent as ArtifactMarkedContentElement;
                _artifacts.Add(new PdfPageText
                {
                    Page = _pageNumber, Text = text,
                    Type = kind?.Type is { } type and not ArtifactMarkedContentElement.ArtifactType.Unknown ? type.ToString() : null,
                    Subtype = kind?.SubType,
                });
            }

            return;
        }

        var mcid = markedContent.MarkedContentIdentifier >= 0
            ? new MarkedContentId(_streams.GetValueOrDefault(markedContent), markedContent.MarkedContentIdentifier)
            : enclosingMcid;
        if (mcid is { } owner)
        {
            Own(markedContent, new ContentOwner(IsArtifact: false, mcid));
            if (Span(markedContent) is { } span) SpansOf(owner).Add(span);
        }

        // Pushed last to first, so they're visited first to last.
        foreach (var child in markedContent.Children.Reverse())
        {
            pending.Push((child, mcid));
        }
    }

    // Marked content that says something of its own about its text, such as a word in another language.
    private PdfMarkedSpan? Span(MarkedContentElement markedContent)
    {
        var span = new PdfMarkedSpan
        {
            Language = NonEmpty(markedContent.Language),
            ActualText = NonEmpty(markedContent.ActualText),
            // PdfPig 0.1.16 leaves AlternateDescription empty for /Alt, so it's read from the property list.
            AltText = NonEmpty(markedContent.AlternateDescription)
                ?? NonEmpty(PdfObjects.TextOf(markedContent.Properties?.Data.GetValueOrDefault("Alt"))),
            Expansion = NonEmpty(markedContent.ExpandedForm),
        };
        if (span == new PdfMarkedSpan()) return null;

        var letters = InDrawingOrder(SelfAndDescendants(markedContent).SelectMany(element => element.Letters));
        return span with { Text = NonEmpty(TextJoiner.Join(letters)) };
    }

    // Letters gathered element by element are a parent's then its children's; the page draws them in content order.
    private List<Letter> InDrawingOrder(IEnumerable<Letter> letters) =>
        letters.OrderBy(letter => _drawingOrder.GetValueOrDefault(letter)).ToList();

    private static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private List<PdfMarkedSpan> SpansOf(MarkedContentId mcid)
    {
        if (!_spansByMcid.TryGetValue(mcid, out var spans))
        {
            spans = [];
            _spansByMcid[mcid] = spans;
        }

        return spans;
    }

    // PdfPig gives each marked-content element only its direct letters, images and paths.
    private void Own(MarkedContentElement element, ContentOwner owner)
    {
        foreach (var letter in element.Letters)
        {
            _owners.Letters[letter] = owner;
        }

        foreach (var path in element.Paths)
        {
            _owners.Paths[path] = owner;
        }

        foreach (var image in element.Images)
        {
            _owners.Images.Add((image, owner, LayoutReader.Hash(image)));
        }
    }

    // In order, walked with a stack for the same reason as Visit.
    private static IEnumerable<MarkedContentElement> SelfAndDescendants(MarkedContentElement markedContent)
    {
        var pending = new Stack<MarkedContentElement>([markedContent]);
        while (pending.TryPop(out var element))
        {
            yield return element;
            foreach (var child in element.Children.Reverse())
            {
                pending.Push(child);
            }
        }
    }
}
