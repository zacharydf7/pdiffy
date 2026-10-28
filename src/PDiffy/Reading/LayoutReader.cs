using System.Security.Cryptography;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics;

namespace PDiffy.Reading;

internal sealed record PageLayout(IReadOnlyList<TextRun> TextRuns, IReadOnlyList<ImageInfo> Images, IReadOnlyList<PathInfo> Paths);

/// <summary>Every text run, image and painted path on a page, with its position, style and role.</summary>
internal sealed class LayoutReader(int pageNumber, ContentOwners owners, IReadOnlySet<(int Page, int Mcid)> referencedMcids)
{
    private const string DefaultColor = "gray:0";

    public static PageLayout Read(int pageNumber, Page page, ContentOwners owners, IReadOnlySet<(int Page, int Mcid)> referencedMcids)
    {
        var reader = new LayoutReader(pageNumber, owners, referencedMcids);
        return new PageLayout(reader.ReadTextRuns(page), reader.ReadImages(page), reader.ReadPaths(page));
    }

    // A run ends where the style or owner changes, or where the text moves to another line.
    private List<TextRun> ReadTextRuns(Page page)
    {
        var runs = new List<TextRun>();
        var letters = new List<Letter>();
        RunStyle? style = null;
        foreach (var letter in page.Letters)
        {
            var letterStyle = StyleOf(letter);
            if (letters.Count > 0 && (letterStyle != style || !OnSameLine(letters[^1], letter)))
            {
                AddRun(runs, letters, style!.Value);
                letters.Clear();
            }

            letters.Add(letter);
            style = letterStyle;
        }

        if (letters.Count > 0)
        {
            AddRun(runs, letters, style!.Value);
        }

        return runs;
    }

    private void AddRun(List<TextRun> runs, List<Letter> letters, RunStyle style)
    {
        var text = TextJoiner.Join(letters);
        if (text.Length == 0)
        {
            return;
        }

        var start = letters[0].StartBaseLine;
        runs.Add(new TextRun(pageNumber, Round(start.X), Round(start.Y), style.Font, style.Size, style.Color, style.RenderMode,
            RoleOf(style.Owner), text));
    }

    private RunStyle StyleOf(Letter letter)
    {
        var font = FontReader.WithoutSubsetPrefix(letter.FontName ?? "(none)");
        var size = Math.Round(letter.PointSize, 1, MidpointRounding.AwayFromZero);
        ContentOwner? owner = owners.Letters.TryGetValue(letter, out var found) ? found : null;
        return new RunStyle(font, size, ColorText.Format(letter.Color), ToRenderMode(letter.RenderingMode), owner);
    }

    // Text that jumps back to the left starts a new run even on the same baseline.
    private static bool OnSameLine(Letter previous, Letter next) =>
        TextJoiner.OnSameLine(previous, next) && next.StartBaseLine.X >= previous.StartBaseLine.X;

    private List<ImageInfo> ReadImages(Page page)
    {
        var unmatched = owners.Images.ToList();
        var images = new List<ImageInfo>();
        foreach (var image in page.GetImages())
        {
            // Match by position, pixel size and image data, since the marked-content copy is a different object. PdfPig
            // doesn't say where in the content stream an owned image sits, so two identical images at the same spot can
            // swap roles; the untagged count stays right and they look the same.
            var match = unmatched.FindIndex(candidate => SameImage(candidate.Image, image));
            ContentOwner? owner = null;
            if (match >= 0)
            {
                owner = unmatched[match].Owner;
                unmatched.RemoveAt(match);
            }

            var bounds = image.BoundingBox;
            images.Add(new ImageInfo(pageNumber, Round(bounds.Left), Round(bounds.Bottom), Round(bounds.Width), Round(bounds.Height),
                image.WidthInSamples, image.HeightInSamples, Hash(image), RoleOf(owner)));
        }

        return images;
    }

    private List<PathInfo> ReadPaths(Page page)
    {
        var paths = new List<PathInfo>();
        foreach (var path in page.Paths.Where(IsPainted))
        {
            var bounds = path.GetBoundingRectangle() ?? new PdfRectangle();
            ContentOwner? owner = owners.Paths.TryGetValue(path, out var found) ? found : null;
            // PdfPig leaves a color unset when the PDF never chose one; PDF's starting color is black in DeviceGray.
            var fill = path.IsFilled ? ColorText.Format(path.FillColor) ?? DefaultColor : null;
            var stroke = path.IsStroked ? ColorText.Format(path.StrokeColor) ?? DefaultColor : null;
            double? lineWidth = path.IsStroked ? Math.Round((double)path.LineWidth, 1, MidpointRounding.AwayFromZero) : null;
            paths.Add(new PathInfo(pageNumber, Round(bounds.Left), Round(bounds.Bottom), Round(bounds.Width), Round(bounds.Height),
                fill, stroke, lineWidth, RoleOf(owner)));
        }

        return paths;
    }

    private ContentRole RoleOf(ContentOwner? owner) => owner switch
    {
        null => ContentRole.Untagged,
        { IsArtifact: true } => ContentRole.Artifact,
        { Mcid: int mcid } when referencedMcids.Contains((pageNumber, mcid)) => ContentRole.Tagged,
        _ => ContentRole.Untagged,
    };

    // A path that is neither filled nor stroked (such as a "W n" clip) draws nothing, so it isn't listed.
    private static bool IsPainted(PdfPath path) => path.IsFilled || path.IsStroked;

    private static bool SameImage(IPdfImage a, IPdfImage b) =>
        a.WidthInSamples == b.WidthInSamples && a.HeightInSamples == b.HeightInSamples
        && Round(a.BoundingBox.Left) == Round(b.BoundingBox.Left) && Round(a.BoundingBox.Bottom) == Round(b.BoundingBox.Bottom)
        && Round(a.BoundingBox.Width) == Round(b.BoundingBox.Width) && Round(a.BoundingBox.Height) == Round(b.BoundingBox.Height)
        && a.RawMemory.Span.SequenceEqual(b.RawMemory.Span);

    // The first 16 hex digits of a SHA-256 of the decoded pixels, or of the raw stream when PdfPig can't decode it (e.g. JPEG).
    private static string Hash(IPdfImage image)
    {
        var bytes = image.TryGetBytesAsMemory(out var decoded) ? decoded : image.RawMemory;
        return Convert.ToHexStringLower(SHA256.HashData(bytes.Span))[..16];
    }

    // PDF defines exactly these eight modes.
    private static TextRenderMode ToRenderMode(TextRenderingMode mode) => mode switch
    {
        TextRenderingMode.Stroke => TextRenderMode.Stroke,
        TextRenderingMode.FillThenStroke => TextRenderMode.FillStroke,
        TextRenderingMode.Neither => TextRenderMode.Invisible,
        TextRenderingMode.FillClip => TextRenderMode.FillClip,
        TextRenderingMode.StrokeClip => TextRenderMode.StrokeClip,
        TextRenderingMode.FillThenStrokeClip => TextRenderMode.FillStrokeClip,
        TextRenderingMode.NeitherClip => TextRenderMode.Clip,
        _ => TextRenderMode.Fill,
    };

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private readonly record struct RunStyle(string Font, double Size, string? Color, TextRenderMode RenderMode, ContentOwner? Owner);
}
