using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Graphics;

namespace PDiffy.Reading;

/// <summary>Which marked content a piece of page content sits in: an artifact, an MCID, or neither (no owner at all).</summary>
internal readonly record struct ContentOwner(bool IsArtifact, int? Mcid);

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
internal sealed record PageContent(IReadOnlyDictionary<int, List<Letter>> LettersByMcid,
    IReadOnlyDictionary<int, List<MarkedSpan>> SpansByMcid, IReadOnlyDictionary<int, List<string>> ImagesByMcid,
    IReadOnlyList<PageText> Artifacts, IReadOnlyList<string> UntaggedLines, ContentOwners Owners);

internal sealed class PageContentReader
{
    private readonly ContentOwners _owners = new();
    private readonly List<PageText> _artifacts = [];
    private readonly Dictionary<int, List<MarkedSpan>> _spansByMcid = [];

    // Each letter's place in page.Letters, which is in content order, so letters gathered by nesting can be put back.
    private readonly Dictionary<Letter, int> _drawingOrder = [];

    public static PageContent Read(Page page) => new PageContentReader(page.Number).ReadPage(page);

    private readonly int _pageNumber;

    private PageContentReader(int pageNumber) => _pageNumber = pageNumber;

    private PageContent ReadPage(Page page)
    {
        for (var i = 0; i < page.Letters.Count; i++) _drawingOrder[page.Letters[i]] = i;
        var pending = new Stack<(MarkedContentElement MarkedContent, int? EnclosingMcid)>();
        foreach (var markedContent in page.GetMarkedContents().Reverse())
        {
            pending.Push((markedContent, null));
        }

        while (pending.TryPop(out var next)) Visit(next.MarkedContent, next.EnclosingMcid, pending);

        // page.Letters is in content order, so each MCID's letters come out in reading order.
        var lettersByMcid = new Dictionary<int, List<Letter>>();
        var untagged = new List<Letter>();
        foreach (var letter in page.Letters)
        {
            if (!_owners.Letters.TryGetValue(letter, out var owner))
            {
                untagged.Add(letter);
            }
            else if (owner.Mcid is int mcid)
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
        return new PageContent(lettersByMcid, _spansByMcid, imagesByMcid, _artifacts, TextJoiner.JoinLines(untagged), _owners);
    }

    // Marked content without its own MCID belongs to the nearest enclosing MCID, if there is one. Children go on the
    // stack instead of being visited in turn, so content nested thousands deep can't overflow it.
    private void Visit(MarkedContentElement markedContent, int? enclosingMcid,
        Stack<(MarkedContentElement MarkedContent, int? EnclosingMcid)> pending)
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
                _artifacts.Add(new PageText(_pageNumber, text)
                {
                    Type = kind?.Type is { } type and not ArtifactMarkedContentElement.ArtifactType.Unknown ? type.ToString() : null,
                    Subtype = kind?.SubType,
                });
            }

            return;
        }

        var mcid = markedContent.MarkedContentIdentifier >= 0 ? markedContent.MarkedContentIdentifier : enclosingMcid;
        if (mcid is int owner)
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
    private MarkedSpan? Span(MarkedContentElement markedContent)
    {
        var span = new MarkedSpan
        {
            Language = NonEmpty(markedContent.Language),
            ActualText = NonEmpty(markedContent.ActualText),
            // PdfPig 0.1.16 leaves AlternateDescription empty for /Alt, so it's read from the property list.
            AltText = NonEmpty(markedContent.AlternateDescription)
                ?? NonEmpty(PdfObjects.TextOf(markedContent.Properties?.Data.GetValueOrDefault("Alt"))),
            Expansion = NonEmpty(markedContent.ExpandedForm),
        };
        if (span == new MarkedSpan()) return null;

        var letters = InDrawingOrder(SelfAndDescendants(markedContent).SelectMany(element => element.Letters));
        return span with { Text = NonEmpty(TextJoiner.Join(letters)) };
    }

    // Letters gathered element by element are a parent's then its children's; the page draws them in content order.
    private List<Letter> InDrawingOrder(IEnumerable<Letter> letters) =>
        letters.OrderBy(letter => _drawingOrder.GetValueOrDefault(letter)).ToList();

    private static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private List<MarkedSpan> SpansOf(int mcid)
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
