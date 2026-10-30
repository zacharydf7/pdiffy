using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics;
using UglyToad.PdfPig.Graphics.Operations;
using UglyToad.PdfPig.Parser;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>
/// A form XObject a content stream draws: the form's object, its operations and the resources they use.
/// </summary>
internal sealed record FormContent(IndirectReference Reference, IReadOnlyList<IGraphicsStateOperation> Operations,
    DictionaryToken? Resources);

/// <summary>
/// Opens the form XObjects a content stream draws with Do, as PdfPig does when it loads the page, for readers that walk
/// a page's operations and the forms inside it.
/// </summary>
internal sealed class FormXObjects(PdfObjects objects)
{
    // Forms can draw other forms; this bounds a malformed chain that keeps going.
    public const int MaxDepth = 10;

    // The same settings PdfPig loads pages with. The depth guard keeps a running count, so each instance gets its own.
    private readonly PageContentParser _parser = new(ReflectionGraphicsStateOperationFactory.Instance,
        new StackDepthGuard(new ParsingOptions().MaxStackDepth), useLenientParsing: true);

    // The forms being drawn right now. A malformed form that draws itself is skipped, as PdfPig skips it.
    private readonly HashSet<IndirectReference> _drawing = [];

    // A form drawn many times, such as a map symbol, is parsed once per instance.
    private readonly Dictionary<IndirectReference, IReadOnlyList<IGraphicsStateOperation>> _parsed = [];

    // A form with no resources of its own uses the resources of the content that draws it. PdfPig already decoded and
    // parsed every form on a loaded page, with the same parser settings, so neither step fails here; a page where they
    // would fail is recorded as unreadable before this runs. Close the form once its operations are walked.
    public FormContent? Open(string name, DictionaryToken? resources)
    {
        var xObjects = resources is null ? null : objects.Dictionary(resources, "XObject");
        if (xObjects?.Data.GetValueOrDefault(name) is not IndirectReferenceToken reference
            || objects.Resolve(reference) is not StreamToken form || objects.Name(form.StreamDictionary, "Subtype") != "Form"
            || objects.Contents(form) is not { } content || !_drawing.Add(reference.Data))
        {
            return null;
        }

        if (!_parsed.TryGetValue(reference.Data, out var operations))
        {
            operations = _parser.Parse(0, new MemoryInputBytes(content), new ParsingOptions().Logger);
            _parsed[reference.Data] = operations;
        }

        var formResources = objects.Dictionary(form.StreamDictionary, "Resources") ?? resources;
        return new FormContent(reference.Data, operations, formResources);
    }

    public void Close(FormContent form) => _drawing.Remove(form.Reference);
}
