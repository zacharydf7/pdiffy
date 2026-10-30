namespace PDiffy;

/// <summary>How assistive technology treats a piece of page content.</summary>
public enum PdfContentRole
{
    /// <summary>Inside marked content that a structure element points to.</summary>
    Tagged,

    /// <summary>Marked as an artifact: decoration a screen reader skips on purpose.</summary>
    Artifact,

    /// <summary>Neither tagged nor an artifact, so assistive technology can't reach it.</summary>
    Untagged,
}

/// <summary>How text is drawn. Most text is <see cref="Fill"/>; <see cref="Invisible"/> text (often an OCR layer) has no ink.</summary>
public enum PdfTextRenderMode
{
    /// <summary>Filled in, the usual way text is drawn.</summary>
    Fill,

    /// <summary>Outlined only.</summary>
    Stroke,

    /// <summary>Filled in and outlined.</summary>
    FillStroke,

    /// <summary>Neither filled in nor outlined, so it can be selected and read aloud but not seen.</summary>
    Invisible,

    /// <summary>Filled in, then added to the clipping path, which limits what later drawing shows.</summary>
    FillClip,

    /// <summary>Outlined, then added to the clipping path.</summary>
    StrokeClip,

    /// <summary>Filled in and outlined, then added to the clipping path.</summary>
    FillStrokeClip,

    /// <summary>Only added to the clipping path, so it has no ink of its own.</summary>
    Clip,
}

/// <summary>
/// Text on one line that shares a font, size, color, render mode and role. Positions are whole points from the page's
/// bottom-left corner.
/// </summary>
public sealed record PdfTextRun
{
    internal PdfTextRun()
    {
    }

    /// <summary>The page number, counted from 1.</summary>
    public required int Page { get; init; }

    /// <summary>How far across the page the run's first letter starts, on its baseline.</summary>
    public required int X { get; init; }

    /// <summary>How far up the page the run's baseline is, at its first letter.</summary>
    public required int Y { get; init; }

    /// <summary>
    /// The font's name without a subset prefix, such as "Helvetica-Bold" for "ABCDEF+Helvetica-Bold"; "(none)" when the
    /// PDF doesn't name it.
    /// </summary>
    public required string Font { get; init; }

    /// <summary>The size the text is drawn at, in points, rounded to one decimal place, such as 11.5.</summary>
    public required double Size { get; init; }

    /// <summary>
    /// The text's color, written like "gray:0", "#ff0000", "cmyk:0,0,0,1" or "pattern:shading"; null when the PDF
    /// library can't tell.
    /// </summary>
    public required string? Color { get; init; }

    /// <summary>How the text is drawn: filled, outlined, invisible or used as a clip.</summary>
    public required PdfTextRenderMode RenderMode { get; init; }

    /// <summary>Whether the text is tagged, an artifact, or untagged.</summary>
    public required PdfContentRole Role { get; init; }

    /// <summary>
    /// The run's text, with a space wherever the gap between letters is wide enough for one, whitespace runs collapsed
    /// to one space, and no space at either end.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// The angle the text runs at, in degrees counterclockwise, such as 90 for text that reads upward; 0 for text across
    /// the page.
    /// </summary>
    public int Angle { get; init; }

    /// <summary>
    /// The optional-content layers the text is drawn in, outermost first, by name, such as "Draft"; empty when it's
    /// drawn outside optional content. A viewer can hide a layer, so its content may not show or print.
    /// </summary>
    public IReadOnlyList<string> Layers { get; init => field = ValueList<string>.Of(value); } = ValueList<string>.Empty;
}

/// <summary>An image. <see cref="Hash"/> comes from the decoded pixels where possible, so recompressing alone doesn't change it.</summary>
public sealed record PdfImage
{
    internal PdfImage()
    {
    }

    /// <summary>The page number, counted from 1.</summary>
    public required int Page { get; init; }

    /// <summary>The left edge of the image as drawn, in whole points from the page's left edge.</summary>
    public required int X { get; init; }

    /// <summary>The bottom edge of the image as drawn, in whole points from the page's bottom edge.</summary>
    public required int Y { get; init; }

    /// <summary>The width the image is drawn at on the page, in whole points.</summary>
    public required int Width { get; init; }

    /// <summary>The height the image is drawn at on the page, in whole points.</summary>
    public required int Height { get; init; }

    /// <summary>The image's own width in pixels, however large it's drawn.</summary>
    public required int PixelWidth { get; init; }

    /// <summary>The image's own height in pixels, however large it's drawn.</summary>
    public required int PixelHeight { get; init; }

    /// <summary>
    /// The first 16 hex digits of a SHA-256 of the decoded pixels, or of the bytes as stored when the pixels can't be
    /// decoded (such as a JPEG) or are too large to decompress.
    /// </summary>
    public required string Hash { get; init; }

    /// <summary>Whether the image is tagged, an artifact, or untagged.</summary>
    public required PdfContentRole Role { get; init; }

    /// <summary>
    /// The optional-content layers the image is drawn in, outermost first, by name, such as "Draft"; empty when it's
    /// drawn outside optional content. A viewer can hide a layer, so its content may not show or print.
    /// </summary>
    public IReadOnlyList<string> Layers { get; init => field = ValueList<string>.Of(value); } = ValueList<string>.Empty;
}

/// <summary>A painted vector path: its bounding box, and its fill and stroke. Unpainted parts are null.</summary>
public sealed record PdfVectorPath
{
    internal PdfVectorPath()
    {
    }

    /// <summary>The page number, counted from 1.</summary>
    public required int Page { get; init; }

    /// <summary>The left edge of the path's bounding box, in whole points from the page's left edge.</summary>
    public required int X { get; init; }

    /// <summary>The bottom edge of the path's bounding box, in whole points from the page's bottom edge.</summary>
    public required int Y { get; init; }

    /// <summary>The bounding box's width, in whole points; 0 for a vertical line.</summary>
    public required int Width { get; init; }

    /// <summary>The bounding box's height, in whole points; 0 for a horizontal line.</summary>
    public required int Height { get; init; }

    /// <summary>
    /// The fill color, written like "gray:0", "#ff0000", "cmyk:0,0,0,1" or "pattern:shading"; "gray:0" (black) when the
    /// PDF fills without choosing a color, and null when the path isn't filled.
    /// </summary>
    public required string? Fill { get; init; }

    /// <summary>
    /// The outline's color, written the same way as <see cref="Fill"/>; null when the path isn't stroked.
    /// </summary>
    public required string? Stroke { get; init; }

    /// <summary>
    /// The outline's width as the PDF sets it, rounded to one decimal place; null when the path isn't stroked.
    /// </summary>
    public required double? LineWidth { get; init; }

    /// <summary>Whether the path is tagged, an artifact, or untagged.</summary>
    public required PdfContentRole Role { get; init; }

    /// <summary>
    /// The optional-content layers the path is drawn in, outermost first, by name, such as "Draft"; empty when it's
    /// drawn outside optional content. A viewer can hide a layer, so its content may not show or print.
    /// </summary>
    public IReadOnlyList<string> Layers { get; init => field = ValueList<string>.Of(value); } = ValueList<string>.Empty;
}
