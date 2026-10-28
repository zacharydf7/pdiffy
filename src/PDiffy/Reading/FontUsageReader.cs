using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics;
using UglyToad.PdfPig.Graphics.Operations;
using UglyToad.PdfPig.Graphics.Operations.SpecialGraphicsState;
using UglyToad.PdfPig.Graphics.Operations.TextShowing;
using UglyToad.PdfPig.Graphics.Operations.TextState;
using UglyToad.PdfPig.Parser;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>A font and the bytes of every piece of text shown in it.</summary>
internal sealed record FontUse(DictionaryToken Font, List<byte[]> Bytes);

/// <summary>
/// Collects every font the pages use, once each, with the text shown in it: on the page itself and inside the form
/// XObjects the page draws, such as stamps or headers. Fonts are matched by object, because a resource name like F1
/// only means something within one resource dictionary.
/// </summary>
internal sealed class FontUsageReader(PdfObjects objects)
{
    // Forms can draw other forms; this bounds a malformed chain that keeps going.
    private const int MaxFormDepth = 10;

    // The same settings PdfPig loads pages with. The depth guard keeps a running count, so each reader (one per Load)
    // gets its own.
    private readonly PageContentParser _parser = new(ReflectionGraphicsStateOperationFactory.Instance,
        new StackDepthGuard(new ParsingOptions().MaxStackDepth), useLenientParsing: true);

    private readonly List<FontUse> _fonts = [];
    private readonly Dictionary<object, FontUse> _byKey = [];

    // The forms being drawn right now. A malformed form that draws itself is skipped, as PdfPig skips it.
    private readonly HashSet<IndirectReference> _drawing = [];

    public IReadOnlyList<FontUse> Fonts => _fonts;

    /// <summary>Records the fonts in a resource dictionary, used or not.</summary>
    public void AddResources(DictionaryToken? resources)
    {
        var fonts = resources is null ? null : objects.Dictionary(resources, "Font");
        foreach (var value in fonts?.Data.Values ?? [])
        {
            Use(value);
        }
    }

    /// <summary>Records the text in a page's content, and in the forms it draws, against the fonts it's shown in.</summary>
    public void AddText(IReadOnlyList<IGraphicsStateOperation> operations, DictionaryToken? resources) =>
        AddText(operations, resources, font: null, depth: 0);

    private void AddText(IReadOnlyList<IGraphicsStateOperation> operations, DictionaryToken? resources, FontUse? font, int depth)
    {
        AddResources(resources);
        // The font is part of the graphics state, so q saves it and Q restores it.
        var saved = new Stack<FontUse?>();
        foreach (var operation in operations)
        {
            switch (operation)
            {
                case Push:
                    saved.Push(font);
                    break;
                case Pop when saved.Count > 0:
                    font = saved.Pop();
                    break;
                case SetFontAndSize setFont:
                    var fonts = resources is null ? null : objects.Dictionary(resources, "Font");
                    font = Use(fonts?.Data.GetValueOrDefault(setFont.Font.Data));
                    break;
                case ShowText show:
                    font?.Bytes.Add(BytesOf(show.Bytes, show.Text));
                    break;
                case MoveToNextLineShowText show:
                    font?.Bytes.Add(BytesOf(show.Bytes, show.Text));
                    break;
                case MoveToNextLineShowTextWithSpacing show:
                    font?.Bytes.Add(BytesOf(show.Bytes, show.Text));
                    break;
                case ShowTextsWithPositioning show:
                    foreach (var item in show.Array)
                    {
                        if (item is StringToken text) font?.Bytes.Add(text.GetBytes());
                        else if (item is HexToken hex) font?.Bytes.Add(hex.Bytes.ToArray());
                    }

                    break;
                case InvokeNamedXObject invoke when depth < MaxFormDepth:
                    AddFormText(invoke.Name.Data, resources, font, depth);
                    break;
            }
        }
    }

    // A form starts with the graphics state of the content that draws it, so it inherits the current font. A form with
    // no resources of its own uses the resources of the content that draws it. PdfPig already decoded and parsed every
    // form on a loaded page, with the same parser settings, so neither step fails here; a page where they would fail
    // is recorded as unreadable before this runs.
    private void AddFormText(string name, DictionaryToken? resources, FontUse? font, int depth)
    {
        var xObjects = resources is null ? null : objects.Dictionary(resources, "XObject");
        if (xObjects?.Data.GetValueOrDefault(name) is not IndirectReferenceToken reference
            || objects.Resolve(reference) is not StreamToken form || objects.Name(form.StreamDictionary, "Subtype") != "Form"
            || objects.Contents(form) is not { } content || !_drawing.Add(reference.Data))
        {
            return;
        }

        var formResources = objects.Dictionary(form.StreamDictionary, "Resources") ?? resources;
        var operations = _parser.Parse(0, new MemoryInputBytes(content), new ParsingOptions().Logger);
        AddText(operations, formResources, font, depth + 1);
        _drawing.Remove(reference.Data);
    }

    // The same font object is usually shared by many pages and forms; a font written inline belongs to one place.
    private FontUse? Use(IToken? value)
    {
        if (objects.Resolve(value) is not DictionaryToken font) return null;

        object key = value is IndirectReferenceToken reference ? reference.Data : font;
        if (!_byKey.TryGetValue(key, out var use))
        {
            use = new FontUse(font, []);
            _byKey[key] = use;
            _fonts.Add(use);
        }

        return use;
    }

    // PdfPig keeps only the text of a literal string, one character per byte; it keeps the bytes of a hex string.
    private static byte[] BytesOf(ReadOnlyMemory<byte> bytes, string? text) =>
        bytes.Length > 0 ? bytes.ToArray() : Encoding.Latin1.GetBytes(text ?? "");
}
