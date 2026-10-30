using System.Text;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics.Operations;
using UglyToad.PdfPig.Graphics.Operations.SpecialGraphicsState;
using UglyToad.PdfPig.Graphics.Operations.TextShowing;
using UglyToad.PdfPig.Graphics.Operations.TextState;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>A font and the bytes of every piece of text shown in it, drawn or invisible (render mode 3 or 7).</summary>
internal sealed record FontUse(DictionaryToken Font, List<byte[]> Bytes, List<byte[]> InvisibleBytes);

/// <summary>
/// Collects every font the pages use, once each, with the text shown in it: on the page itself and inside the form
/// XObjects the page draws, such as stamps or headers. Fonts are matched by object, because a resource name like F1
/// only means something within one resource dictionary.
/// </summary>
internal sealed class FontUsageReader(PdfObjects objects)
{
    private readonly FormXObjects _forms = new(objects);
    private readonly List<FontUse> _fonts = [];
    private readonly Dictionary<object, FontUse> _byKey = [];

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
        AddText(operations, resources, font: null, invisible: false, depth: 0);

    private void AddText(IReadOnlyList<IGraphicsStateOperation> operations, DictionaryToken? resources, FontUse? font,
        bool invisible, int depth)
    {
        AddResources(resources);
        // The font and the render mode are part of the graphics state, so q saves them and Q restores them.
        var saved = new Stack<(FontUse? Font, bool Invisible)>();
        foreach (var operation in operations)
        {
            switch (operation)
            {
                case Push:
                    saved.Push((font, invisible));
                    break;
                case Pop when saved.Count > 0:
                    (font, invisible) = saved.Pop();
                    break;
                case SetFontAndSize setFont:
                    var fonts = resources is null ? null : objects.Dictionary(resources, "Font");
                    font = Use(fonts?.Data.GetValueOrDefault(setFont.Font.Data));
                    break;
                case SetTextRenderingMode mode:
                    invisible = mode.Mode is TextRenderingMode.Neither or TextRenderingMode.NeitherClip;
                    break;
                case ShowText show:
                    Add(font, invisible, BytesOf(show.Bytes, show.Text));
                    break;
                case MoveToNextLineShowText show:
                    Add(font, invisible, BytesOf(show.Bytes, show.Text));
                    break;
                case MoveToNextLineShowTextWithSpacing show:
                    Add(font, invisible, BytesOf(show.Bytes, show.Text));
                    break;
                case ShowTextsWithPositioning show:
                    foreach (var item in show.Array)
                    {
                        if (item is StringToken text) Add(font, invisible, text.GetBytes());
                        else if (item is HexToken hex) Add(font, invisible, hex.Bytes.ToArray());
                    }

                    break;
                // A form starts with the graphics state of the content that draws it, so it inherits the current font
                // and render mode.
                case InvokeNamedXObject invoke when depth < FormXObjects.MaxDepth
                    && _forms.Open(invoke.Name.Data, resources) is { } form:
                    AddText(form.Operations, form.Resources, font, invisible, depth + 1);
                    _forms.Close(form);
                    break;
            }
        }
    }

    private static void Add(FontUse? font, bool invisible, byte[] bytes) =>
        (invisible ? font?.InvisibleBytes : font?.Bytes)?.Add(bytes);

    // The same font object is usually shared by many pages and forms; a font written inline belongs to one place.
    private FontUse? Use(IToken? value)
    {
        if (objects.Resolve(value) is not DictionaryToken font) return null;

        object key = value is IndirectReferenceToken reference ? reference.Data : font;
        if (!_byKey.TryGetValue(key, out var use))
        {
            use = new FontUse(font, [], []);
            _byKey[key] = use;
            _fonts.Add(use);
        }

        return use;
    }

    // PdfPig keeps only the text of a literal string, one character per byte; it keeps the bytes of a hex string.
    private static byte[] BytesOf(ReadOnlyMemory<byte> bytes, string? text) =>
        bytes.Length > 0 ? bytes.ToArray() : Encoding.Latin1.GetBytes(text ?? "");
}
