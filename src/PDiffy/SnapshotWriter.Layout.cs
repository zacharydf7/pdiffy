using System.Globalization;
using System.Text;

namespace PDiffy;

internal sealed partial class SnapshotWriter
{
    // Page by page: text runs, then images, then paths, each in content order.
    private void WriteLayout(PdfUaDocument document)
    {
        var runs = document.TextRuns.ToLookup(run => run.Page);
        var images = document.Images.ToLookup(image => image.Page);
        var paths = document.Paths.ToLookup(path => path.Page);
        var pages = runs.Select(page => page.Key).Concat(images.Select(page => page.Key))
            .Concat(paths.Select(page => page.Key)).Distinct().Order().ToList();
        if (pages.Count == 0) Line(1, None);
        foreach (var page in pages)
        {
            foreach (var run in runs[page]) Line(1, FormatTextRun(run));
            foreach (var image in images[page]) Line(1, FormatImage(image));
            foreach (var path in paths[page]) Line(1, FormatPath(path));
        }
    }

    // Fill is ordinary text, so only other render modes are printed.
    private string FormatTextRun(TextRun run)
    {
        var line = new StringBuilder($"p{run.Page} text {RoleName(run.Role)}");
        Key(line, Field.Positions, "at", $"{run.X},{run.Y}");
        Key(line, Field.FontNames, "font", run.Font);
        Key(line, Field.FontSizes, "size", Number(run.Size));
        Key(line, Field.Colors, "color", run.Color);
        if (run.RenderMode != TextRenderMode.Fill) Key(line, "mode", RenderModeName(run.RenderMode));
        return line.Append(' ').Append(Quote(run.Text)).ToString();
    }

    private string FormatImage(ImageInfo image)
    {
        var line = new StringBuilder($"p{image.Page} image {RoleName(image.Role)}");
        Key(line, Field.Positions, "at", $"{image.X},{image.Y}");
        Key(line, Field.Positions, "size", $"{image.Width}x{image.Height}");
        Key(line, Field.ImageContents, "pixels", $"{image.PixelWidth}x{image.PixelHeight}");
        Key(line, Field.ImageContents, "hash", image.Hash);
        return line.ToString();
    }

    private string FormatPath(PathInfo path)
    {
        var line = new StringBuilder($"p{path.Page} path {RoleName(path.Role)}");
        Key(line, Field.Positions, "at", $"{path.X},{path.Y}");
        Key(line, Field.Positions, "size", $"{path.Width}x{path.Height}");
        Key(line, Field.Colors, "fill", path.Fill);
        Key(line, Field.Colors, "stroke", path.Stroke);
        Key(line, Field.LineWidths, "width", path.LineWidth is double width ? Number(width) : null);
        return line.ToString();
    }

    private static string RoleName(ContentRole role) => role.ToString().ToLowerInvariant();

    private static string RenderModeName(TextRenderMode mode) => mode switch
    {
        TextRenderMode.FillStroke => "fill-stroke",
        TextRenderMode.FillClip => "fill-clip",
        TextRenderMode.StrokeClip => "stroke-clip",
        TextRenderMode.FillStrokeClip => "fill-stroke-clip",
        _ => mode.ToString().ToLowerInvariant(),
    };

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);
}
