using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

internal static class AnnotationReader
{
    /// <summary>Every annotation on every page, in each page's /Annots order.</summary>
    public static IReadOnlyList<Annotation> ReadAll(PdfObjects objects, PageTree pages, IReadOnlySet<IndirectReference> tagged,
        DestinationReader destinations)
    {
        var annotations = new List<Annotation>();
        for (var i = 0; i < pages.Pages.Count; i++)
        {
            foreach (var item in objects.Items(pages.Pages[i].Dictionary.Data.GetValueOrDefault("Annots")))
            {
                if (objects.Resolve(item) is DictionaryToken annotation)
                {
                    var role = item is IndirectReferenceToken reference && tagged.Contains(reference.Data)
                        ? ContentRole.Tagged
                        : ContentRole.Untagged;
                    annotations.Add(Read(objects, annotation, i + 1, role, destinations));
                }
            }
        }

        return annotations;
    }

    public static Annotation Read(PdfObjects objects, DictionaryToken annotation, int? page, ContentRole role,
        DestinationReader destinations)
    {
        var rect = objects.Box(annotation, "Rect") ?? new PageBox(0, 0, 0, 0);
        var action = objects.Dictionary(annotation, "A");
        var uri = action is null ? null : objects.Text(action, "URI");
        var type = action is null ? null : objects.Name(action, "S");
        return new Annotation
        {
            Subtype = objects.Name(annotation, "Subtype") ?? "(none)",
            Page = page,
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height,
            Flags = (AnnotationFlags)(objects.Integer(annotation, "F") ?? 0),
            Role = role,
            Uri = uri,
            Action = action is not null && uri is null ? type : null,
            HasDestination = annotation.Data.ContainsKey("Dest"),
            Destination = type is "GoTo" or "GoToR"
                ? destinations.Read(action!.Data.GetValueOrDefault("D"), remote: type == "GoToR")
                : destinations.Read(annotation.Data.GetValueOrDefault("Dest")),
            File = type is "GoToR" or "Launch" ? FileName(objects, action!) : null,
            Contents = objects.Text(annotation, "Contents"),
            AccessibleName = objects.Text(annotation, "TU"),
        };
    }

    // /F is a file name, or a file specification whose /UF (Unicode) or /F holds it.
    private static string? FileName(PdfObjects objects, DictionaryToken action) =>
        objects.Text(action, "F") ?? (objects.Dictionary(action, "F") is { } specification
            ? objects.Text(specification, "UF") ?? objects.Text(specification, "F")
            : null);
}
