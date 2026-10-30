namespace PDiffy;

/// <summary>The values of <see cref="DocxInfo.Protection"/>.</summary>
public static class DocxProtections
{
    /// <summary>The document can't be edited.</summary>
    public const string ReadOnly = "readOnly";

    /// <summary>Only comments can be added.</summary>
    public const string Comments = "comments";

    /// <summary>Every edit is recorded as a tracked change.</summary>
    public const string TrackedChanges = "trackedChanges";

    /// <summary>Only form fields can be filled in.</summary>
    public const string Forms = "forms";
}

/// <summary>The values of <see cref="PageSetup.Break"/>.</summary>
public static class DocxSectionBreaks
{
    /// <summary>The section starts on a new page.</summary>
    public const string NextPage = "nextPage";

    /// <summary>The section starts on the same page.</summary>
    public const string Continuous = "continuous";

    /// <summary>The section starts on the next even page.</summary>
    public const string EvenPage = "evenPage";

    /// <summary>The section starts on the next odd page.</summary>
    public const string OddPage = "oddPage";

    /// <summary>The section starts in the next column.</summary>
    public const string NextColumn = "nextColumn";
}

/// <summary>
/// The values of <see cref="DocxHeaderFooter.Kind"/>, <see cref="PageSetup.Headers"/> and <see cref="PageSetup.Footers"/>.
/// </summary>
public static class DocxHeaderFooterKinds
{
    /// <summary>For the pages the other kinds don't cover.</summary>
    public const string Default = "default";

    /// <summary>For the section's first page.</summary>
    public const string First = "first";

    /// <summary>For even pages.</summary>
    public const string Even = "even";
}

/// <summary>The values of <see cref="DocxStyle.Type"/>.</summary>
public static class DocxStyleTypes
{
    /// <summary>A paragraph style.</summary>
    public const string Paragraph = "paragraph";

    /// <summary>A character style, for runs of text.</summary>
    public const string Character = "character";

    /// <summary>A table style.</summary>
    public const string Table = "table";

    /// <summary>A list style.</summary>
    public const string Numbering = "numbering";
}

/// <summary>The values of <see cref="RunFormat.Underline"/>.</summary>
public static class DocxUnderlines
{
    /// <summary>A single line.</summary>
    public const string Single = "single";

    /// <summary>A line under words but not the spaces between them.</summary>
    public const string Words = "words";

    /// <summary>A double line.</summary>
    public const string Double = "double";

    /// <summary>A thick line.</summary>
    public const string Thick = "thick";

    /// <summary>A dotted line.</summary>
    public const string Dotted = "dotted";

    /// <summary>A thick dotted line.</summary>
    public const string DottedHeavy = "dottedHeavy";

    /// <summary>A dashed line.</summary>
    public const string Dash = "dash";

    /// <summary>A thick dashed line.</summary>
    public const string DashedHeavy = "dashedHeavy";

    /// <summary>A line of long dashes.</summary>
    public const string DashLong = "dashLong";

    /// <summary>A thick line of long dashes.</summary>
    public const string DashLongHeavy = "dashLongHeavy";

    /// <summary>A line of dashes and dots.</summary>
    public const string DotDash = "dotDash";

    /// <summary>A thick line of dashes and dots.</summary>
    public const string DashDotHeavy = "dashDotHeavy";

    /// <summary>A line of dashes and pairs of dots.</summary>
    public const string DotDotDash = "dotDotDash";

    /// <summary>A thick line of dashes and pairs of dots.</summary>
    public const string DashDotDotHeavy = "dashDotDotHeavy";

    /// <summary>A wavy line.</summary>
    public const string Wave = "wave";

    /// <summary>A thick wavy line.</summary>
    public const string WavyHeavy = "wavyHeavy";

    /// <summary>A double wavy line.</summary>
    public const string WavyDouble = "wavyDouble";
}

/// <summary>The values of <see cref="RunFormat.Highlight"/>.</summary>
public static class DocxHighlights
{
    /// <summary>Black.</summary>
    public const string Black = "black";

    /// <summary>Blue.</summary>
    public const string Blue = "blue";

    /// <summary>Cyan.</summary>
    public const string Cyan = "cyan";

    /// <summary>Green.</summary>
    public const string Green = "green";

    /// <summary>Magenta.</summary>
    public const string Magenta = "magenta";

    /// <summary>Red.</summary>
    public const string Red = "red";

    /// <summary>Yellow.</summary>
    public const string Yellow = "yellow";

    /// <summary>White.</summary>
    public const string White = "white";

    /// <summary>Dark blue.</summary>
    public const string DarkBlue = "darkBlue";

    /// <summary>Dark cyan.</summary>
    public const string DarkCyan = "darkCyan";

    /// <summary>Dark green.</summary>
    public const string DarkGreen = "darkGreen";

    /// <summary>Dark magenta.</summary>
    public const string DarkMagenta = "darkMagenta";

    /// <summary>Dark red.</summary>
    public const string DarkRed = "darkRed";

    /// <summary>Dark yellow.</summary>
    public const string DarkYellow = "darkYellow";

    /// <summary>Dark gray.</summary>
    public const string DarkGray = "darkGray";

    /// <summary>Light gray.</summary>
    public const string LightGray = "lightGray";
}

/// <summary>The values of <see cref="RunFormat.VerticalAlign"/>.</summary>
public static class DocxRunVerticalAligns
{
    /// <summary>Raised and smaller, as in x².</summary>
    public const string Superscript = "superscript";

    /// <summary>Lowered and smaller, as in H₂O.</summary>
    public const string Subscript = "subscript";
}

/// <summary>The values of <see cref="ParagraphFormat.Align"/>.</summary>
public static class DocxAlignments
{
    /// <summary>At the start of the line: the left in left-to-right text.</summary>
    public const string Start = "start";

    /// <summary>Centered.</summary>
    public const string Center = "center";

    /// <summary>At the end of the line: the right in left-to-right text.</summary>
    public const string End = "end";

    /// <summary>On the left, as older files write it.</summary>
    public const string Left = "left";

    /// <summary>On the right, as older files write it.</summary>
    public const string Right = "right";

    /// <summary>Justified: spaces widen so each line but the last fills the width (both).</summary>
    public const string Justified = "both";

    /// <summary>Justified, with letters spaced out too, including the last line.</summary>
    public const string Distribute = "distribute";

    /// <summary>Justified Arabic text, stretched with a short kashida.</summary>
    public const string LowKashida = "lowKashida";

    /// <summary>Justified Arabic text, stretched with a medium kashida.</summary>
    public const string MediumKashida = "mediumKashida";

    /// <summary>Justified Arabic text, stretched with a long kashida.</summary>
    public const string HighKashida = "highKashida";

    /// <summary>Justified Thai text.</summary>
    public const string ThaiDistribute = "thaiDistribute";

    /// <summary>Aligned to the tab stop after the list number.</summary>
    public const string NumberTab = "numTab";
}

/// <summary>The values of <see cref="DocxBreak.Kind"/>.</summary>
public static class DocxBreakKinds
{
    /// <summary>A page break.</summary>
    public const string Page = "page";

    /// <summary>A column break.</summary>
    public const string Column = "column";
}

/// <summary>
/// The values of <see cref="DocxImage.Kind"/>. A drawing of a kind not listed here is named after the last part of the
/// graphic's type, such as "chartex".
/// </summary>
public static class DocxGraphicKinds
{
    /// <summary>A picture.</summary>
    public const string Image = "image";

    /// <summary>An embedded or linked object, such as a spreadsheet.</summary>
    public const string Object = "object";

    /// <summary>An ActiveX control.</summary>
    public const string Control = "control";

    /// <summary>A shape.</summary>
    public const string Shape = "shape";

    /// <summary>A group of shapes.</summary>
    public const string Group = "group";

    /// <summary>A drawing canvas.</summary>
    public const string Canvas = "canvas";

    /// <summary>Ink, drawn with a pen.</summary>
    public const string Ink = "ink";

    /// <summary>A chart.</summary>
    public const string Chart = "chart";

    /// <summary>A SmartArt diagram.</summary>
    public const string Diagram = "diagram";

    /// <summary>A drawing that doesn't say what it is.</summary>
    public const string Graphic = "graphic";
}

/// <summary>The values of <see cref="DocxCell.Merge"/>.</summary>
public static class DocxCellMerges
{
    /// <summary>The cell starts a merge with the cells below it.</summary>
    public const string Restart = "restart";

    /// <summary>The cell is merged into the cell above it.</summary>
    public const string Continue = "continue";
}

/// <summary>The values of <see cref="DocxCell.VerticalAlign"/>.</summary>
public static class DocxCellVerticalAligns
{
    /// <summary>At the top of the cell.</summary>
    public const string Top = "top";

    /// <summary>In the middle of the cell.</summary>
    public const string Center = "center";

    /// <summary>At the bottom of the cell.</summary>
    public const string Bottom = "bottom";

    /// <summary>Spread over the cell's height.</summary>
    public const string Both = "both";
}

/// <summary>The values of <see cref="DocxCell.TextDirection"/>.</summary>
public static class DocxTextDirections
{
    /// <summary>Left to right, lines from top to bottom: the usual direction (lrTb).</summary>
    public const string LeftToRightTopToBottom = "lrTb";

    /// <summary>Top to bottom, lines from right to left: text turned a quarter turn clockwise (tbRl).</summary>
    public const string TopToBottomRightToLeft = "tbRl";

    /// <summary>Bottom to top, lines from left to right: text turned a quarter turn counterclockwise (btLr).</summary>
    public const string BottomToTopLeftToRight = "btLr";

    /// <summary>Left to right, lines from top to bottom, with East Asian characters upright (lrTbV).</summary>
    public const string LeftToRightTopToBottomUpright = "lrTbV";

    /// <summary>Top to bottom, lines from right to left, with East Asian characters upright (tbRlV).</summary>
    public const string TopToBottomRightToLeftUpright = "tbRlV";

    /// <summary>Top to bottom, lines from left to right, with East Asian characters upright (tbLrV).</summary>
    public const string TopToBottomLeftToRightUpright = "tbLrV";
}
