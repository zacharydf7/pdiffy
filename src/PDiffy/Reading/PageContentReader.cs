using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Graphics;

namespace PDiffy.Reading;

/// <summary>Which marked content a piece of page content sits in: an artifact, an MCID, or neither (no owner at all).</summary>
internal readonly record struct ContentOwner(bool IsArtifact, int? Mcid);

internal sealed class ContentOwners
{
    public Dictionary<Letter, ContentOwner> Letters { get; } = new(ReferenceEqualityComparer.Instance);
    public Dictionary<PdfPath, ContentOwner> Paths { get; } = new(ReferenceEqualityComparer.Instance);

    // PdfPig builds new image objects on every call, so images can't be looked up by reference; LayoutReader matches them by position.
    public List<(IPdfImage Image, ContentOwner Owner)> Images { get; } = [];
}

/// <summary>What one page's content stream says: the letters behind each MCID, the artifacts, untagged text, and who owns what.</summary>
internal sealed record PageContent(IReadOnlyDictionary<int, List<Letter>> LettersByMcid, IReadOnlyList<string> Artifacts,
    string? UntaggedText, ContentOwners Owners);

internal sealed class PageContentReader
{
    private readonly ContentOwners _owners = new();
    private readonly List<string> _artifacts = [];

    public static PageContent Read(Page page) => new PageContentReader().ReadPage(page);

    private PageContent ReadPage(Page page)
    {
        foreach (var markedContent in page.GetMarkedContents())
        {
            Visit(markedContent, enclosingMcid: null);
        }

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

        var untaggedText = TextJoiner.Join(untagged);
        return new PageContent(lettersByMcid, _artifacts, untaggedText.Length > 0 ? untaggedText : null, _owners);
    }

    // Marked content without its own MCID belongs to the nearest enclosing MCID, if there is one.
    private void Visit(MarkedContentElement markedContent, int? enclosingMcid)
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

            var text = TextJoiner.Join(letters);
            if (text.Length > 0)
            {
                _artifacts.Add(text);
            }

            return;
        }

        var mcid = markedContent.MarkedContentIdentifier >= 0 ? markedContent.MarkedContentIdentifier : enclosingMcid;
        if (mcid is not null)
        {
            Own(markedContent, new ContentOwner(IsArtifact: false, mcid));
        }

        foreach (var child in markedContent.Children)
        {
            Visit(child, mcid);
        }
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
            _owners.Images.Add((image, owner));
        }
    }

    private static IEnumerable<MarkedContentElement> SelfAndDescendants(MarkedContentElement markedContent)
    {
        yield return markedContent;
        foreach (var child in markedContent.Children)
        {
            foreach (var descendant in SelfAndDescendants(child))
            {
                yield return descendant;
            }
        }
    }
}
