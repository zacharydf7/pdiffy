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
            MediaClip = MediaClip(objects, annotation),
        };
    }

    // The clip the first Rendition action plays, among the annotation's own action (/A) and those it runs on events
    // (/AA), each with the actions it chains through /Next. The clip has a content type and alternate text: a list of
    // language and text pairs.
    private static PdfMediaClip? MediaClip(PdfObjects objects, DictionaryToken annotation)
    {
        var events = objects.Dictionary(annotation, "AA")?.Data.Values ?? [];
        var actions = events.Prepend(annotation.Data.GetValueOrDefault("A")).SelectMany(objects.Chain);
        var clip = actions.Where(action => objects.Name(action, "S") == "Rendition")
            .Select(action => ClipData(objects, objects.Dictionary(action, "R")))
            .FirstOrDefault(data => data is not null);
        if (clip is null) return null;

        var texts = objects.Items(clip.Data.GetValueOrDefault("Alt")).Where((_, index) => index % 2 == 1)
            .Select(item => PdfObjects.TextOf(objects.Resolve(item))).Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
        return new PdfMediaClip
        {
            ContentType = objects.Text(clip, "CT"), AltText = texts.Count == 0 ? null : string.Join("; ", texts),
        };
    }

    // A selector rendition (SR) plays the first of its renditions a viewer can, so its first is read. A media rendition
    // (MR) names its clip (/C), and a clip section (MCS) the clip data (MCD) it's part of (/D). Each step is capped, so
    // a loop can't hang the read.
    private static DictionaryToken? ClipData(PdfObjects objects, DictionaryToken? rendition)
    {
        for (var depth = 0; rendition is not null && depth < ReadLimits.MaxDepth; depth++)
        {
            if (objects.Name(rendition, "S") != "SR") break;

            rendition = objects.Items(rendition.Data.GetValueOrDefault("R")).Select(objects.Resolve)
                .OfType<DictionaryToken>().FirstOrDefault();
        }

        var clip = rendition is null ? null : objects.Dictionary(rendition, "C");
        for (var depth = 0; depth < ReadLimits.MaxDepth && clip is not null; depth++)
        {
            if (objects.Name(clip, "S") != "MCS") break;

            clip = objects.Dictionary(clip, "D");
        }

        return clip;
    }

    // /F is a file name, or a file specification.
    private static string? FileName(PdfObjects objects, DictionaryToken action) =>
        objects.Text(action, "F")
        ?? (objects.Dictionary(action, "F") is { } specification ? objects.FileName(specification) : null);
}
