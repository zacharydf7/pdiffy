using System.Xml.Linq;
using static PDiffy.Docx.Reading.DocxXml;

namespace PDiffy.Docx.Reading;

/// <summary>
/// Works out how text and paragraphs look from the document defaults, a style and everything it's based on, and
/// the formatting set directly. Lengths in the file are twentieths of a point (twips) or half points for font sizes.
/// </summary>
internal sealed class StyleResolver
{
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    private readonly Dictionary<string, XElement> _styles = [];
    private readonly XElement? _defaultRun;
    private readonly XElement? _defaultParagraph;
    private readonly string? _majorFont;
    private readonly string? _minorFont;

    public StyleResolver(XDocument? styles, XDocument? theme)
    {
        // Some generators nest w:styles inside w:styles; styles are read wherever they sit, so their properties still show.
        var root = styles?.Root;
        foreach (var style in root?.Descendants(W + "style") ?? [])
        {
            if (Attr(style, "styleId") is { } id) _styles.TryAdd(id, style);
        }

        var defaults = root?.Descendants(W + "docDefaults").FirstOrDefault();
        _defaultRun = defaults?.Element(W + "rPrDefault")?.Element(W + "rPr");
        _defaultParagraph = defaults?.Element(W + "pPrDefault")?.Element(W + "pPr");
        Language = Attr(_defaultRun?.Element(W + "lang"), "val");
        DefaultParagraphStyle = _styles.Values
            .FirstOrDefault(style => Attr(style, "type") == "paragraph" && IsTrue(Attr(style, "default"))) is { } normal
            ? Attr(normal, "styleId")
            : null;

        var fonts = theme?.Root?.Descendants(A + "fontScheme").FirstOrDefault();
        _majorFont = (string?)fonts?.Element(A + "majorFont")?.Element(A + "latin")?.Attribute("typeface");
        _minorFont = (string?)fonts?.Element(A + "minorFont")?.Element(A + "latin")?.Attribute("typeface");
    }

    public string? Language { get; }

    public string? DefaultParagraphStyle { get; }

    public DocxStyle? Describe(string id)
    {
        if (!_styles.TryGetValue(id, out var style)) return null;
        var type = Attr(style, "type") ?? "paragraph";
        return new DocxStyle
        {
            Id = id,
            Type = type,
            Name = Attr(style.Element(W + "name"), "val"),
            BasedOn = Attr(style.Element(W + "basedOn"), "val"),
            IsDefault = IsTrue(Attr(style, "default")),
            Run = type == "character" ? CharacterStyleRun(id) : ParagraphRun(id),
            Paragraph = type == "paragraph" ? Layout(id, null) : new ParagraphFormat(),
        };
    }

    /// <summary>How a paragraph's text looks before any character style or direct formatting.</summary>
    public RunFormat ParagraphRun(string? styleId) =>
        Chain(styleId).Aggregate(ApplyRun(new RunFormat(), _defaultRun), (format, style) => ApplyRun(format, style.Element(W + "rPr")));

    /// <summary>
    /// How a run looks: the paragraph's look, then its character style, then direct formatting. Bold, italic, caps,
    /// small caps, strike and hidden in a character style switch the paragraph's setting rather than replacing it.
    /// </summary>
    public RunFormat Run(RunFormat paragraph, string? characterStyle, XElement? direct)
    {
        var format = paragraph;
        if (characterStyle is not null)
        {
            // Applied on top of the paragraph's look, so the style's "none" or "auto" clears what the paragraph style set.
            var style = CharacterStyleRun(characterStyle);
            format = Chain(characterStyle).Aggregate(paragraph, (look, level) => ApplyRun(look, level.Element(W + "rPr"))) with
            {
                Style = characterStyle,
                Bold = paragraph.Bold ^ style.Bold,
                Italic = paragraph.Italic ^ style.Italic,
                Caps = paragraph.Caps ^ style.Caps,
                SmallCaps = paragraph.SmallCaps ^ style.SmallCaps,
                Strike = paragraph.Strike ^ style.Strike,
                Hidden = paragraph.Hidden ^ style.Hidden,
            };
        }

        return ApplyRun(format, direct);
    }

    /// <summary>The list and level a paragraph style puts its paragraphs in, set by the nearest style in its chain.</summary>
    public (int List, int Level)? Numbering(string? styleId)
    {
        (int List, int Level)? numbering = null;
        foreach (var numPr in Chain(styleId).Select(style => style.Element(W + "pPr")?.Element(W + "numPr")).OfType<XElement>())
        {
            if (Integer(numPr.Element(W + "numId"), "val") is int list) numbering = list == 0 ? null : (list, numbering?.Level ?? 0);
            if (numbering is { } current && Integer(numPr.Element(W + "ilvl"), "val") is int level) numbering = (current.List, level);
        }

        return numbering;
    }

    /// <summary>A paragraph's layout: document defaults, its style from the root of the chain down, then its own pPr.</summary>
    public ParagraphFormat Layout(string? styleId, XElement? pPr) =>
        ApplyParagraph(Chain(styleId).Aggregate(ApplyParagraph(new ParagraphFormat(), _defaultParagraph),
            (format, style) => ApplyParagraph(format, style.Element(W + "pPr"))), pPr);

    private RunFormat CharacterStyleRun(string styleId) =>
        Chain(styleId).Aggregate(new RunFormat(), (format, style) => ApplyRun(format, style.Element(W + "rPr")));

    // The style and the styles it's based on, root first. A loop in the chain stops at the first repeat.
    private List<XElement> Chain(string? styleId)
    {
        var chain = new List<XElement>();
        var seen = new HashSet<string>();
        for (var id = styleId; id is not null && seen.Add(id) && _styles.TryGetValue(id, out var style);)
        {
            chain.Insert(0, style);
            id = Attr(style.Element(W + "basedOn"), "val");
        }

        return chain;
    }

    // "none", "auto" and "baseline" switch off what a style set, so they clear the value rather than being one.
    private RunFormat ApplyRun(RunFormat format, XElement? rPr)
    {
        if (rPr is null) return format;
        return format with
        {
            Font = Font(rPr.Element(W + "rFonts")) ?? format.Font,
            Size = Integer(rPr.Element(W + "sz"), "val") is int halfPoints ? halfPoints / 2.0 : format.Size,
            Bold = Toggle(rPr, "b") ?? format.Bold,
            Italic = Toggle(rPr, "i") ?? format.Italic,
            Underline = Cleared(Attr(rPr.Element(W + "u"), "val"), "none", format.Underline),
            Color = Cleared(NonEmpty(Attr(rPr.Element(W + "color"), "val"))?.ToUpperInvariant(), "AUTO", format.Color),
            Highlight = Cleared(Attr(rPr.Element(W + "highlight"), "val"), "none", format.Highlight),
            Caps = Toggle(rPr, "caps") ?? format.Caps,
            SmallCaps = Toggle(rPr, "smallCaps") ?? format.SmallCaps,
            Strike = Toggle(rPr, "strike") ?? format.Strike,
            VerticalAlign = Cleared(Attr(rPr.Element(W + "vertAlign"), "val"), "baseline", format.VerticalAlign),
            DoubleStrike = Toggle(rPr, "dstrike") ?? format.DoubleStrike,
            Hidden = Toggle(rPr, "vanish") ?? format.Hidden,
            RightToLeft = Toggle(rPr, "rtl") ?? format.RightToLeft,
            Language = NonEmpty(Attr(rPr.Element(W + "lang"), "val")) ?? format.Language,
        };
    }

    private static string? Cleared(string? value, string off, string? inherited) =>
        value is null ? inherited : value == off ? null : value;

    private static ParagraphFormat ApplyParagraph(ParagraphFormat format, XElement? pPr)
    {
        if (pPr is null) return format;
        var ind = pPr.Element(W + "ind");
        var spacing = pPr.Element(W + "spacing");
        var outline = pPr.Element(W + "outlineLvl");

        // The first line is indented or hanging, never both; when both are given, hanging wins.
        var hanging = Twips(ind, "hanging");
        var firstLine = hanging is null ? Twips(ind, "firstLine") : null;
        return format with
        {
            Align = Attr(pPr.Element(W + "jc"), "val") ?? format.Align,
            IndentLeft = Twips(ind, "left") ?? Twips(ind, "start") ?? format.IndentLeft,
            IndentRight = Twips(ind, "right") ?? Twips(ind, "end") ?? format.IndentRight,
            FirstLine = hanging is not null ? null : firstLine ?? format.FirstLine,
            Hanging = firstLine is not null ? null : hanging ?? format.Hanging,
            SpacingBefore = Twips(spacing, "before") ?? format.SpacingBefore,
            SpacingAfter = Twips(spacing, "after") ?? format.SpacingAfter,
            LineSpacing = LineSpacing(spacing) ?? format.LineSpacing,
            KeepNext = Toggle(pPr, "keepNext") ?? format.KeepNext,
            KeepLines = Toggle(pPr, "keepLines") ?? format.KeepLines,
            PageBreakBefore = Toggle(pPr, "pageBreakBefore") ?? format.PageBreakBefore,

            // Outline levels 0 to 8 are headings 1 to 9; 9 means body text, which is how a style stops being a heading.
            HeadingLevel = outline is null ? format.HeadingLevel : Integer(outline, "val") is >= 0 and <= 8 and var level ? level + 1 : null,
            Tabs = Tabs(format.Tabs, pPr.Element(W + "tabs")),
            RightToLeft = Toggle(pPr, "bidi") ?? format.RightToLeft,
            Borders = BorderSides(format.Borders, TableFormat.Borders(pPr.Element(W + "pBdr"))),
            Shading = pPr.Element(W + "shd") is { } shading ? TableFormat.Shading(shading) : format.Shading,
        };
    }

    private static readonly string[] SideOrder = ["top", "left", "bottom", "right", "between", "bar"];

    // Each side of a paragraph's borders is its own setting, so one set further down the chain replaces only that side,
    // and "nil" or "none" clears it.
    private static List<DocxBorder> BorderSides(IReadOnlyList<DocxBorder> inherited, IReadOnlyList<DocxBorder> set) =>
        inherited.Where(border => !set.Any(other => other.Side == border.Side)).Concat(set)
            .Where(border => border.Style is not ("nil" or "none"))
            .OrderBy(border => Array.IndexOf(SideOrder, border.Side)).ToList();

    // A tab stop with val="clear" removes one set further up the chain at the same position.
    private static List<DocxTabStop> Tabs(IReadOnlyList<DocxTabStop> inherited, XElement? tabs)
    {
        var stops = inherited.ToList();
        foreach (var tab in tabs?.Elements(W + "tab") ?? [])
        {
            var position = Twips(tab, "pos") ?? 0;
            stops.RemoveAll(stop => Points(stop.Position) == Points(position));
            if (Attr(tab, "val") is { } kind and not "clear")
            {
                var leader = Attr(tab, "leader") is { } fill and not "none" ? fill : null;
                stops.Add(new DocxTabStop { Alignment = kind, Position = position, Leader = leader });
            }
        }

        return stops;
    }

    // "auto" spacing is in 240ths of a line; "exact" and "atLeast" are lengths.
    private static DocxLineSpacing? LineSpacing(XElement? spacing)
    {
        if (Integer(spacing, "line") is not int line) return null;
        var rule = Attr(spacing, "lineRule") ?? DocxSizeRules.Auto;
        var value = rule == DocxSizeRules.Auto ? Math.Round(line / 240.0, 2) : line / 20.0;
        return new DocxLineSpacing { Value = value, Rule = rule };
    }

    // A theme font wins over a named one, which is often left over from before the theme changed.
    private string? Font(XElement? fonts) => Attr(fonts, "asciiTheme") switch
    {
        { } theme when theme.StartsWith("major", StringComparison.Ordinal) && _majorFont is not null => _majorFont,
        { } theme when theme.StartsWith("minor", StringComparison.Ordinal) && _minorFont is not null => _minorFont,
        _ => Attr(fonts, "ascii"),
    };

    private static bool? Toggle(XElement parent, string name) => parent.Element(W + name) is { } element ? IsOn(element) : null;
}
