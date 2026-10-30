using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Graphics;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>
/// The optional-content layers (/OC marked content) each letter, path and image on a page sits in, outermost first.
/// Optional content can wrap tagged content, artifacts or both, so it's read apart from who owns the content.
/// </summary>
internal sealed class ContentLayers
{
    public Dictionary<Letter, IReadOnlyList<string>> Letters { get; } = new(ReferenceEqualityComparer.Instance);
    public Dictionary<PdfPath, IReadOnlyList<string>> Paths { get; } = new(ReferenceEqualityComparer.Instance);

    // PdfPig builds new image objects on every call, so LayoutReader matches these by position, as it does owners.
    public List<(IPdfImage Image, IReadOnlyList<string> Layers)> Images { get; } = [];

    // Walked with a stack, like PageContentReader, so content nested thousands deep can't overflow it.
    public static ContentLayers Read(IReadOnlyList<MarkedContentElement> markedContents, PdfObjects objects)
    {
        var layers = new ContentLayers();
        var pending = new Stack<(MarkedContentElement MarkedContent, IReadOnlyList<string> Layers)>();
        foreach (var markedContent in markedContents.Reverse()) pending.Push((markedContent, []));
        while (pending.TryPop(out var next))
        {
            var (element, enclosing) = next;
            var inside = element.Tag == "OC" ? [.. enclosing, .. Names(element.Properties, objects)] : enclosing;
            if (inside.Count > 0) layers.Add(element, inside);
            foreach (var child in element.Children.Reverse()) pending.Push((child, inside));
        }

        return layers;
    }

    // PdfPig gives each marked-content element only its direct letters, images and paths.
    private void Add(MarkedContentElement element, IReadOnlyList<string> layers)
    {
        foreach (var letter in element.Letters) Letters[letter] = layers;
        foreach (var path in element.Paths) Paths[path] = layers;
        foreach (var image in element.Images) Images.Add((image, layers));
    }

    // An optional content group names itself; a membership dictionary (OCMD) lists the groups it depends on.
    private static IEnumerable<string> Names(DictionaryToken? properties, PdfObjects objects)
    {
        if (properties is null) return ["(unnamed)"];
        if (objects.Text(properties, "Name") is { } name) return [name];
        var groups = objects.Items(properties.Data.GetValueOrDefault("OCGs"))
            .Select(group => objects.Resolve(group) is DictionaryToken found ? objects.Text(found, "Name") : null)
            .OfType<string>().ToList();
        return groups.Count > 0 ? groups : ["(unnamed)"];
    }
}
