using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

internal static class AnnotationReader
{
    /// <summary>Every annotation on every page, in each page's /Annots order.</summary>
    public static IReadOnlyList<PdfAnnotation> ReadAll(PdfObjects objects, PageTree pages, IReadOnlySet<IndirectReference> tagged,
        DestinationReader destinations)
    {
        var annotations = new List<PdfAnnotation>();
        for (var i = 0; i < pages.Pages.Count; i++)
        {
            foreach (var item in objects.Items(pages.Pages[i].Dictionary.Data.GetValueOrDefault("Annots")))
            {
                if (objects.Resolve(item) is DictionaryToken annotation)
                {
                    var role = item is IndirectReferenceToken reference && tagged.Contains(reference.Data)
                        ? PdfContentRole.Tagged
                        : PdfContentRole.Untagged;
                    annotations.Add(Read(objects, annotation, i + 1, role, destinations));
                }
            }
        }

        return annotations;
    }

    public static PdfAnnotation Read(PdfObjects objects, DictionaryToken annotation, int? page, PdfContentRole role,
        DestinationReader destinations)
    {
        var rect = objects.Box(annotation, "Rect") ?? new PdfBox { X = 0, Y = 0, Width = 0, Height = 0 };
        var action = objects.Dictionary(annotation, "A");
        var uri = action is null ? null : objects.Text(action, "URI");
        var type = action is null ? null : objects.Name(action, "S");
        return new PdfAnnotation
        {
            Subtype = objects.Name(annotation, "Subtype") ?? PdfAnnotationTypes.None,
            Page = page,
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height,
            Flags = (PdfAnnotationFlags)(objects.Integer(annotation, "F") ?? 0),
            Role = role,
            Uri = uri,
            Action = action is not null && uri is null ? type : null,
            Destination = type is "GoTo" or "GoToR"
                ? destinations.Read(action!.Data.GetValueOrDefault("D"), remote: type == "GoToR")
                : destinations.Read(annotation.Data.GetValueOrDefault("Dest")),
            File = type is "GoToR" or "Launch" ? FileName(objects, action!) : null,
            Contents = objects.Text(annotation, "Contents"),
            AccessibleName = objects.Text(annotation, "TU"),
            MediaClips = MediaClips(objects, annotation),
        };
    }

    // Every clip the Rendition actions play, among the annotation's own action (/A) and those it runs on events (/AA),
    // each with the actions it chains through /Next. A clip has a content type and alternate text: a list of language
    // and text pairs.
    private static List<PdfMediaClip> MediaClips(PdfObjects objects, DictionaryToken annotation)
    {
        var events = objects.Dictionary(annotation, "AA")?.Data.Values ?? [];
        var actions = events.Prepend(annotation.Data.GetValueOrDefault("A")).SelectMany(objects.Chain);
        var seen = new HashSet<IndirectReference>();
        var clips = actions.Where(action => objects.Name(action, "S") == "Rendition")
            .SelectMany(action => ClipData(objects, action.Data.GetValueOrDefault("R"), seen));
        return clips.Select(clip =>
        {
            var texts = objects.Items(clip.Data.GetValueOrDefault("Alt")).Where((_, index) => index % 2 == 1)
                .Select(item => PdfObjects.TextOf(objects.Resolve(item))).Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
            return new PdfMediaClip
            {
                ContentType = objects.Text(clip, "CT"), AltText = texts.Count == 0 ? null : string.Join("; ", texts),
            };
        }).ToList();
    }

    // A selector rendition (SR) lists renditions a viewer picks from, so each is read. A media rendition (MR) names its
    // clip (/C), and a clip section (MCS) the clip data (MCD) it's part of (/D). A rendition object is read once, so one
    // that two actions play prints once and a loop ends, and each step is capped.
    private static IEnumerable<DictionaryToken> ClipData(PdfObjects objects, IToken? token,
        HashSet<IndirectReference> seen, int depth = 0)
    {
        if (depth >= ReadLimits.MaxDepth) yield break;
        if (token is IndirectReferenceToken reference && !seen.Add(reference.Data)) yield break;
        if (objects.Resolve(token) is not DictionaryToken rendition) yield break;

        if (objects.Name(rendition, "S") == "SR")
        {
            var choices = objects.Items(rendition.Data.GetValueOrDefault("R"));
            foreach (var clip in choices.SelectMany(choice => ClipData(objects, choice, seen, depth + 1)))
            {
                yield return clip;
            }

            yield break;
        }

        var data = objects.Dictionary(rendition, "C");
        for (var step = 0; step < ReadLimits.MaxDepth && data is not null; step++)
        {
            if (objects.Name(data, "S") != "MCS") break;

            data = objects.Dictionary(data, "D");
        }

        if (data is not null) yield return data;
    }

    // /F is a file name, or a file specification.
    private static string? FileName(PdfObjects objects, DictionaryToken action) =>
        objects.Text(action, "F")
        ?? (objects.Dictionary(action, "F") is { } specification ? objects.FileName(specification) : null);
}
