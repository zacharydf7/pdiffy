using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>
/// Counts the reference XObjects (form XObjects with /Ref, which draw a page of another file) in the pages' resources
/// and the resources of the forms in them, each object once.
/// </summary>
internal static class ReferenceXObjects
{
    public static int Count(PdfObjects objects, PageTree pages)
    {
        var seen = new HashSet<IndirectReference>();
        var pending = new Stack<DictionaryToken>(pages.Pages.Select(page => page.Resources).OfType<DictionaryToken>());
        var count = 0;
        while (pending.TryPop(out var resources))
        {
            if (objects.Dictionary(resources, "XObject") is not { } xObjects) continue;

            foreach (var value in xObjects.Data.Values)
            {
                if (value is not IndirectReferenceToken reference || !seen.Add(reference.Data)) continue;
                if (objects.Resolve(reference) is not StreamToken form) continue;
                if (objects.Name(form.StreamDictionary, "Subtype") != "Form") continue;

                if (form.StreamDictionary.Data.ContainsKey("Ref")) count++;
                if (objects.Dictionary(form.StreamDictionary, "Resources") is { } inner) pending.Push(inner);
            }
        }

        return count;
    }
}
