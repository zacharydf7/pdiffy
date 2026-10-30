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
        };
    }

    // /F is a file name, or a file specification.
    private static string? FileName(PdfObjects objects, DictionaryToken action) =>
        objects.Text(action, "F")
        ?? (objects.Dictionary(action, "F") is { } specification ? objects.FileName(specification) : null);
}
