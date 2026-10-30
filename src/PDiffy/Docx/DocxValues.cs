namespace PDiffy;

/// <summary>A border on one side of a paragraph, table or cell.</summary>
public sealed record DocxBorder
{
    internal DocxBorder()
    {
    }

    /// <summary>
    /// The side: "top", "left", "bottom" or "right", "insideH" or "insideV" for the lines between a table's cells, or
    /// "between" or "bar" for a paragraph.
    /// </summary>
    public required string Side { get; init; }

    /// <summary>The line style as Word writes it, such as "single", "double" or "dotted"; "none" when it's turned off.</summary>
    public required string Style { get; init; }

    /// <summary>The line's width in points.</summary>
    public required double Width { get; init; }

    /// <summary>The color in upper-case hex, such as "000000", or null when Word picks it.</summary>
    public required string? Color { get; init; }
}

/// <summary>The space on one side between a cell's edge and its text.</summary>
public sealed record DocxMargin
{
    internal DocxMargin()
    {
    }

    /// <summary>The side: "top", "left", "bottom" or "right".</summary>
    public required string Side { get; init; }

    /// <summary>The space in points.</summary>
    public required double Width { get; init; }
}

/// <summary>A custom tab stop.</summary>
public sealed record DocxTabStop
{
    internal DocxTabStop()
    {
    }

    /// <summary>How text lines up at the stop, as Word writes it, such as "left", "center", "right" or "decimal".</summary>
    public required string Alignment { get; init; }

    /// <summary>Where the stop is, in points from the paragraph's left edge.</summary>
    public required double Position { get; init; }

    /// <summary>The characters that fill the space before the stop, such as "dot" or "hyphen", or null when none.</summary>
    public required string? Leader { get; init; }
}

/// <summary>A paragraph's line spacing.</summary>
public sealed record DocxLineSpacing
{
    internal DocxLineSpacing()
    {
    }

    /// <summary>A multiple of single spacing, such as 1.15, when <see cref="Rule"/> is "auto"; otherwise points.</summary>
    public required double Value { get; init; }

    /// <summary>"auto" for a multiple, "exact" or "atLeast" for a length, as in <see cref="DocxSizeRules"/>.</summary>
    public required string Rule { get; init; }
}

/// <summary>The width of a table or cell.</summary>
public sealed record DocxWidth
{
    internal DocxWidth()
    {
    }

    /// <summary>The width, in the <see cref="Unit"/>; 0 when the unit is <see cref="DocxWidthUnit.Auto"/>.</summary>
    public required double Value { get; init; }

    /// <summary>Whether the width is in points, a percentage of the page or table, or left to Word.</summary>
    public required DocxWidthUnit Unit { get; init; }
}

/// <summary>The unit of a <see cref="DocxWidth"/>.</summary>
public enum DocxWidthUnit
{
    /// <summary>Points.</summary>
    Points,

    /// <summary>A percentage of the table's width, or of the page's for a table.</summary>
    Percent,

    /// <summary>No width set: Word sizes it to fit.</summary>
    Auto,
}

/// <summary>A table row's height.</summary>
public sealed record DocxRowHeight
{
    internal DocxRowHeight()
    {
    }

    /// <summary>The height in points; ignored when <see cref="Rule"/> is "auto".</summary>
    public required double Value { get; init; }

    /// <summary>
    /// "atLeast" when the row grows to fit its text, "exact" when it doesn't, or "auto" when Word sizes it, as in
    /// <see cref="DocxSizeRules"/>.
    /// </summary>
    public required string Rule { get; init; }
}

/// <summary>The values of <see cref="DocxLineSpacing.Rule"/> and <see cref="DocxRowHeight.Rule"/>.</summary>
public static class DocxSizeRules
{
    /// <summary>A line spacing that's a multiple of single spacing, or a row height Word decides.</summary>
    public const string Auto = "auto";

    /// <summary>Exactly the given length.</summary>
    public const string Exact = "exact";

    /// <summary>At least the given length, more when the content needs it.</summary>
    public const string AtLeast = "atLeast";
}
