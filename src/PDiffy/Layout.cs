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
/// <param name="Page">The page number, counted from 1.</param>
/// <param name="X">How far across the page the run's first letter starts, on its baseline.</param>
/// <param name="Y">How far up the page the run's baseline is, at its first letter.</param>
/// <param name="Font">The font's name without a subset prefix, such as "Helvetica-Bold" for "ABCDEF+Helvetica-Bold";
/// "(none)" when the PDF doesn't name it.</param>
/// <param name="Size">The size the text is drawn at, in points, rounded to one decimal place, such as 11.5.</param>
/// <param name="Color">The text's color, written like "gray:0", "#ff0000", "cmyk:0,0,0,1" or "pattern:shading"; null
/// when the PDF library can't tell.</param>
/// <param name="RenderMode">How the text is drawn: filled, outlined, invisible or used as a clip.</param>
/// <param name="Role">Whether the text is tagged, an artifact, or untagged.</param>
/// <param name="Text">The run's text, with a space wherever the gap between letters is wide enough for one, whitespace
/// runs collapsed to one space, and no space at either end.</param>
public sealed record PdfTextRun(
    int Page, int X, int Y, string Font, double Size, string? Color, PdfTextRenderMode RenderMode, PdfContentRole Role, string Text)
{
    /// <summary>
    /// The angle the text runs at, in degrees counterclockwise, such as 90 for text that reads upward; 0 for text across
    /// the page.
    /// </summary>
    public int Angle { get; init; }

    /// <summary>
    /// The optional-content layers the text is drawn in, outermost first, by name, such as "Draft"; empty when it's
    /// drawn outside optional content. A viewer can hide a layer, so its content may not show or print.
    /// </summary>
    public IReadOnlyList<string> Layers { get; init; } = [];
}

/// <summary>An image. <see cref="Hash"/> comes from the decoded pixels where possible, so recompressing alone doesn't change it.</summary>
/// <param name="Page">The page number, counted from 1.</param>
/// <param name="X">The left edge of the image as drawn, in whole points from the page's left edge.</param>
/// <param name="Y">The bottom edge of the image as drawn, in whole points from the page's bottom edge.</param>
/// <param name="Width">The width the image is drawn at on the page, in whole points.</param>
/// <param name="Height">The height the image is drawn at on the page, in whole points.</param>
/// <param name="PixelWidth">The image's own width in pixels, however large it's drawn.</param>
/// <param name="PixelHeight">The image's own height in pixels, however large it's drawn.</param>
/// <param name="Hash">The first 16 hex digits of a SHA-256 of the decoded pixels, or of the bytes as stored when the
/// pixels can't be decoded (such as a JPEG) or are too large to decompress.</param>
/// <param name="Role">Whether the image is tagged, an artifact, or untagged.</param>
public sealed record PdfImage(
    int Page, int X, int Y, int Width, int Height, int PixelWidth, int PixelHeight, string Hash, PdfContentRole Role)
{
    /// <summary>
    /// The optional-content layers the image is drawn in, outermost first, by name, such as "Draft"; empty when it's
    /// drawn outside optional content. A viewer can hide a layer, so its content may not show or print.
    /// </summary>
    public IReadOnlyList<string> Layers { get; init; } = [];
}

/// <summary>A painted vector path: its bounding box, and its fill and stroke. Unpainted parts are null.</summary>
/// <param name="Page">The page number, counted from 1.</param>
/// <param name="X">The left edge of the path's bounding box, in whole points from the page's left edge.</param>
/// <param name="Y">The bottom edge of the path's bounding box, in whole points from the page's bottom edge.</param>
/// <param name="Width">The bounding box's width, in whole points; 0 for a vertical line.</param>
/// <param name="Height">The bounding box's height, in whole points; 0 for a horizontal line.</param>
/// <param name="Fill">The fill color, written like "gray:0", "#ff0000", "cmyk:0,0,0,1" or "pattern:shading"; "gray:0"
/// (black) when the PDF fills without choosing a color, and null when the path isn't filled.</param>
/// <param name="Stroke">The outline's color, written the same way as <paramref name="Fill"/>; null when the path isn't
/// stroked.</param>
/// <param name="LineWidth">The outline's width as the PDF sets it, rounded to one decimal place; null when the path
/// isn't stroked.</param>
/// <param name="Role">Whether the path is tagged, an artifact, or untagged.</param>
public sealed record PdfVectorPath(
    int Page, int X, int Y, int Width, int Height, string? Fill, string? Stroke, double? LineWidth, PdfContentRole Role)
{
    /// <summary>
    /// The optional-content layers the path is drawn in, outermost first, by name, such as "Draft"; empty when it's
    /// drawn outside optional content. A viewer can hide a layer, so its content may not show or print.
    /// </summary>
    public IReadOnlyList<string> Layers { get; init; } = [];
}
