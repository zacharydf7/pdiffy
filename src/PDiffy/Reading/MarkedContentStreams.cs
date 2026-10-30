using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics.Operations;
using UglyToad.PdfPig.Graphics.Operations.MarkedContent;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>
/// An MCID and the content stream it's numbered in: the page's own (<see cref="Form"/> null) or a form XObject's. Each
/// stream numbers its marked content from 0, so a form drawn on the page can reuse the page's MCIDs.
/// </summary>
internal readonly record struct MarkedContentId(IndirectReference? Form, int Mcid);

/// <summary>
/// The content stream each marked-content sequence on a page is in, in the order the sequences begin, following the
/// page into the form XObjects it draws as PdfPig does: null for the page's own content, or the form's object.
/// </summary>
internal static class MarkedContentStreams
{
    public static List<IndirectReference?> Read(IReadOnlyList<IGraphicsStateOperation> operations,
        DictionaryToken? resources, FormXObjects forms)
    {
        var streams = new List<IndirectReference?>();
        Walk(operations, resources, form: null, depth: 0, forms, streams);
        return streams;
    }

    private static void Walk(IReadOnlyList<IGraphicsStateOperation> operations, DictionaryToken? resources,
        IndirectReference? form, int depth, FormXObjects forms, List<IndirectReference?> streams)
    {
        foreach (var operation in operations)
        {
            switch (operation)
            {
                case BeginMarkedContent or BeginMarkedContentWithProperties:
                    streams.Add(form);
                    break;
                case InvokeNamedXObject invoke when depth < FormXObjects.MaxDepth
                    && forms.Open(invoke.Name.Data, resources) is { } drawn:
                    Walk(drawn.Operations, drawn.Resources, drawn.Reference, depth + 1, forms, streams);
                    forms.Close(drawn);
                    break;
            }
        }
    }
}
