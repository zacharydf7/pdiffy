using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>
/// Counts the reference XObjects (form XObjects with /Ref, which draw a page of another file) in the pages' resources,
/// in their annotations' appearance streams, and in the resources of the forms in them, each object once.
/// </summary>
internal static class ReferenceXObjects
{
    public static int Count(PdfObjects objects, PageTree pages)
    {
        var seen = new HashSet<IndirectReference>();
        var pending = new Stack<DictionaryToken>(pages.Pages.Select(page => page.Resources).OfType<DictionaryToken>());
        var count = 0;
        foreach (var appearance in pages.Pages.SelectMany(page => Appearances(objects, page.Dictionary)))
        {
            Visit(objects, appearance, seen, pending, ref count);
        }

        while (pending.TryPop(out var resources))
        {
            if (objects.Dictionary(resources, "XObject") is not { } xObjects) continue;

            foreach (var value in xObjects.Data.Values) Visit(objects, value, seen, pending, ref count);
        }

        return count;
    }

    private static void Visit(PdfObjects objects, IToken value, HashSet<IndirectReference> seen,
        Stack<DictionaryToken> pending, ref int count)
    {
        if (value is not IndirectReferenceToken reference || !seen.Add(reference.Data)) return;
        if (objects.Resolve(reference) is not StreamToken form) return;
        if (objects.Name(form.StreamDictionary, "Subtype") != "Form") return;

        if (form.StreamDictionary.Data.ContainsKey("Ref")) count++;
        if (objects.Dictionary(form.StreamDictionary, "Resources") is { } inner) pending.Push(inner);
    }

    // An annotation's normal, rollover and down appearances (/AP /N, /R, /D) are each a stream, or a dictionary of
    // streams by state, such as a check box's on and off.
    private static IEnumerable<IToken> Appearances(PdfObjects objects, DictionaryToken page)
    {
        foreach (var item in objects.Items(page.Data.GetValueOrDefault("Annots")))
        {
            if (objects.Resolve(item) is not DictionaryToken annotation) continue;
            if (objects.Dictionary(annotation, "AP") is not { } appearances) continue;

            foreach (var key in new[] { "N", "R", "D" })
            {
                var appearance = appearances.Data.GetValueOrDefault(key);
                if (objects.Resolve(appearance) is DictionaryToken states)
                {
                    foreach (var state in states.Data.Values) yield return state;
                }
                else if (appearance is not null)
                {
                    yield return appearance;
                }
            }
        }
    }
}
