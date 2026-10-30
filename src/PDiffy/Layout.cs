namespace PDiffy;

/// <summary>How assistive technology treats a piece of page content.</summary>
public enum ContentRole
{
    /// <summary>Inside marked content that a structure element points to.</summary>
    Tagged,

    /// <summary>Marked as an artifact: decoration a screen reader skips on purpose.</summary>
    Artifact,

    /// <summary>Neither tagged nor an artifact, so assistive technology can't reach it.</summary>
    Untagged,
}

/// <summary>How text is drawn. Most text is <see cref="Fill"/>; <see cref="Invisible"/> text (often an OCR layer) has no ink.</summary>
public enum TextRenderMode
{
    Fill,
    Stroke,
    FillStroke,
    Invisible,
    FillClip,
    StrokeClip,
    FillStrokeClip,
    Clip,
}

/// <summary>
/// Text on one line that shares a font, size, color, render mode and role. Positions are whole points from the page's
/// bottom-left corner.
/// </summary>
public sealed record TextRun(
    int Page, int X, int Y, string Font, double Size, string? Color, TextRenderMode RenderMode, ContentRole Role, string Text)
{
    /// <summary>
    /// The angle the text runs at, in degrees counterclockwise, such as 90 for text that reads upward; 0 for text across
    /// the page.
    /// </summary>
    public int Angle { get; init; }
}

/// <summary>An image. <see cref="Hash"/> comes from the decoded pixels where possible, so recompressing alone doesn't change it.</summary>
public sealed record ImageInfo(
    int Page, int X, int Y, int Width, int Height, int PixelWidth, int PixelHeight, string Hash, ContentRole Role);

/// <summary>A painted vector path: its bounding box, and its fill and stroke. Unpainted parts are null.</summary>
public sealed record PathInfo(
    int Page, int X, int Y, int Width, int Height, string? Fill, string? Stroke, double? LineWidth, ContentRole Role);
