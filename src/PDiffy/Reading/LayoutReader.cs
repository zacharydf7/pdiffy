using System.Security.Cryptography;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics;

namespace PDiffy.Reading;

internal sealed record PageLayout(IReadOnlyList<PdfTextRun> TextRuns, IReadOnlyList<PdfImage> Images, IReadOnlyList<PdfVectorPath> Paths);

/// <summary>Every text run, image and painted path on a page, with its position, style and role.</summary>
internal sealed class LayoutReader(int pageNumber, PageContent content,
    IReadOnlySet<(int Page, MarkedContentId Mcid)> referencedMcids)
{
    private const string DefaultColor = "gray:0";

    public static PageLayout Read(int pageNumber, Page page, PageContent content,
        IReadOnlySet<(int Page, MarkedContentId Mcid)> referencedMcids)
    {
        var reader = new LayoutReader(pageNumber, content, referencedMcids);
        return new PageLayout(reader.Roles(content.TextRuns), reader.ReadImages(page), reader.ReadPaths(page));
    }

    // A run is read as tagged when marked content with an MCID draws it, and is untagged when no element points to that.
    private List<PdfTextRun> Roles(IReadOnlyList<OwnedRun> runs) =>
        runs.Select(owned => owned.Mcid is { } mcid && !referencedMcids.Contains((pageNumber, mcid))
            ? owned.Run with { Role = PdfContentRole.Untagged }
            : owned.Run).ToList();


    private List<PdfImage> ReadImages(Page page)
    {
        var unmatched = content.Owners.Images.ToList();
        var inLayers = content.Layers.Images.ToList();
        var images = new List<PdfImage>();
        foreach (var image in page.GetImages())
        {
            // Match by position, pixel size and image data, since the marked-content copy is a different object. PdfPig
            // doesn't say where in the content stream an owned image sits, so two identical images at the same spot can
            // swap roles; the untagged count stays right and they look the same.
            var match = unmatched.FindIndex(candidate => SameImage(candidate.Image, image));
            ContentOwner? owner = null;
            string? hash = null;
            if (match >= 0)
            {
                (_, owner, hash) = unmatched[match];
                unmatched.RemoveAt(match);
            }

            var inLayer = inLayers.FindIndex(candidate => SameImage(candidate.Image, image));
            IReadOnlyList<string> layers = [];
            if (inLayer >= 0)
            {
                layers = inLayers[inLayer].Layers;
                inLayers.RemoveAt(inLayer);
            }

            var bounds = image.BoundingBox;
            images.Add(new PdfImage
            {
                Page = pageNumber, X = Round(bounds.Left), Y = Round(bounds.Bottom), Width = Round(bounds.Width),
                Height = Round(bounds.Height), PixelWidth = image.WidthInSamples, PixelHeight = image.HeightInSamples,
                Hash = hash ?? Hash(image), Role = RoleOf(owner), Layers = layers,
            });
        }

        return images;
    }

    private List<PdfVectorPath> ReadPaths(Page page)
    {
        var paths = new List<PdfVectorPath>();
        foreach (var path in page.Paths.Where(IsPainted))
        {
            var bounds = path.GetBoundingRectangle() ?? new PdfRectangle();
            ContentOwner? owner = content.Owners.Paths.TryGetValue(path, out var found) ? found : null;
            // PdfPig leaves a color unset when the PDF never chose one; PDF's starting color is black in DeviceGray.
            var fill = path.IsFilled ? ColorText.Format(path.FillColor) ?? DefaultColor : null;
            var stroke = path.IsStroked ? ColorText.Format(path.StrokeColor) ?? DefaultColor : null;
            double? lineWidth = path.IsStroked ? Math.Round((double)path.LineWidth, 1, MidpointRounding.AwayFromZero) : null;
            paths.Add(new PdfVectorPath
            {
                Page = pageNumber, X = Round(bounds.Left), Y = Round(bounds.Bottom), Width = Round(bounds.Width),
                Height = Round(bounds.Height), Fill = fill, Stroke = stroke, LineWidth = lineWidth,
                Role = RoleOf(owner), Layers = content.Layers.Paths.GetValueOrDefault(path) ?? [],
            });
        }

        return paths;
    }

    private PdfContentRole RoleOf(ContentOwner? owner) => owner switch
    {
        null => PdfContentRole.Untagged,
        { IsArtifact: true } => PdfContentRole.Artifact,
        { Mcid: { } mcid } when referencedMcids.Contains((pageNumber, mcid)) => PdfContentRole.Tagged,
        _ => PdfContentRole.Untagged,
    };

    // A path that is neither filled nor stroked (such as a "W n" clip) draws nothing, so it isn't listed.
    private static bool IsPainted(PdfPath path) => path.IsFilled || path.IsStroked;

    private static bool SameImage(IPdfImage a, IPdfImage b) =>
        a.WidthInSamples == b.WidthInSamples && a.HeightInSamples == b.HeightInSamples
        && Round(a.BoundingBox.Left) == Round(b.BoundingBox.Left) && Round(a.BoundingBox.Bottom) == Round(b.BoundingBox.Bottom)
        && Round(a.BoundingBox.Width) == Round(b.BoundingBox.Width) && Round(a.BoundingBox.Height) == Round(b.BoundingBox.Height)
        && a.RawMemory.Span.SequenceEqual(b.RawMemory.Span);

    // The first 16 hex digits of a SHA-256 of the decoded pixels, or of the raw stream when PdfPig can't decode it (e.g. JPEG).
    // An image too large to decompress, or too damaged to, is hashed as stored, which still changes when the image does.
    public static string Hash(IPdfImage image)
    {
        Memory<byte> bytes;
        try
        {
            bytes = image.TryGetBytesAsMemory(out var decoded) ? decoded : image.RawMemory;
        }
        catch (Exception)
        {
            // PdfPig's filters throw many exception types on damaged data, as PdfObjects.Contents notes.
            bytes = image.RawMemory;
        }

        return Convert.ToHexStringLower(SHA256.HashData(bytes.Span))[..16];
    }

    internal static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
