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
    private string FormatTextRun(PdfTextRun run)
    {
        var line = new StringBuilder($"p{run.Page} text {RoleName(run.Role)}");
        Key(line, PdfDetail.Positions, "at", $"{run.X},{run.Y}");
        Key(line, PdfDetail.FontNames, "font", run.Font);
        Key(line, PdfDetail.FontSizes, "size", Number(run.Size));
        Key(line, PdfDetail.Colors, "color", run.Color);
        if (run.RenderMode != PdfTextRenderMode.Fill) Key(line, "mode", RenderModeName(run.RenderMode));
        if (run.Angle != 0) Key(line, "angle", run.Angle.ToString(CultureInfo.InvariantCulture));
        Key(line, "layers", Bracketed(run.Layers.Select(Quote)));
        return line.Append(' ').Append(Quote(run.Text)).ToString();
    }

    private string FormatImage(PdfImage image)
    {
        var line = new StringBuilder($"p{image.Page} image {RoleName(image.Role)}");
        Key(line, PdfDetail.Positions, "at", $"{image.X},{image.Y}");
        Key(line, PdfDetail.Positions, "size", $"{image.Width}x{image.Height}");
        Key(line, PdfDetail.ImageContents, "pixels", $"{image.PixelWidth}x{image.PixelHeight}");
        Key(line, PdfDetail.ImageContents, "hash", image.Hash);
        Key(line, "layers", Bracketed(image.Layers.Select(Quote)));
        return line.ToString();
    }

    private string FormatPath(PdfVectorPath path)
    {
        var line = new StringBuilder($"p{path.Page} path {RoleName(path.Role)}");
        Key(line, PdfDetail.Positions, "at", $"{path.X},{path.Y}");
        Key(line, PdfDetail.Positions, "size", $"{path.Width}x{path.Height}");
        Key(line, PdfDetail.Colors, "fill", path.Fill);
        Key(line, PdfDetail.Colors, "stroke", path.Stroke);
        Key(line, PdfDetail.LineWidths, "width", path.LineWidth is double width ? Number(width) : null);
        Key(line, "layers", Bracketed(path.Layers.Select(Quote)));
        return line.ToString();
    }

    private static string RoleName(PdfContentRole role) => role.ToString().ToLowerInvariant();

    private static string RenderModeName(PdfTextRenderMode mode) => mode switch
    {
        PdfTextRenderMode.FillStroke => "fill-stroke",
        PdfTextRenderMode.FillClip => "fill-clip",
        PdfTextRenderMode.StrokeClip => "stroke-clip",
        PdfTextRenderMode.FillStrokeClip => "fill-stroke-clip",
        _ => mode.ToString().ToLowerInvariant(),
    };

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);
}
