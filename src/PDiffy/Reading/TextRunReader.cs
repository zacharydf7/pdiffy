using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace PDiffy.Reading;

/// <summary>A text run, and the MCID of the marked content that draws it, if any.</summary>
internal readonly record struct OwnedRun(PdfTextRun Run, int? Mcid);

/// <summary>
/// A page's text runs, with each one's style and owner, read before the structure tree so elements can list their own.
/// A run drawn by marked content with an MCID is tagged until <see cref="LayoutReader"/> finds no element points to it.
/// </summary>
internal sealed class TextRunReader(int pageNumber, ContentOwners owners, ContentLayers layers)
{
    public static List<OwnedRun> Read(int pageNumber, Page page, ContentOwners owners, ContentLayers layers) =>
        new TextRunReader(pageNumber, owners, layers).ReadTextRuns(page);

    // A run ends where the style, owner or layers change, or where the text moves to another line.
    private List<OwnedRun> ReadTextRuns(Page page)
    {
        var runs = new List<OwnedRun>();
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

    private void AddRun(List<OwnedRun> runs, List<Letter> letters, RunStyle style)
    {
        var text = TextJoiner.Join(letters);
        if (text.Length == 0)
        {
            return;
        }

        var start = letters[0].StartBaseLine;
        var run = new PdfTextRun
        {
            Page = pageNumber, X = LayoutReader.Round(start.X), Y = LayoutReader.Round(start.Y), Font = style.Font,
            Size = style.Size, Color = style.Color, RenderMode = style.RenderMode, Role = RoleOf(style.Owner),
            Text = text, Angle = TextJoiner.Angle(letters[0]),
            Layers = layers.Letters.GetValueOrDefault(letters[0]) ?? [],
        };
        runs.Add(new OwnedRun(run, style.Owner?.Mcid));
    }

    private static PdfContentRole RoleOf(ContentOwner? owner) => owner switch
    {
        { IsArtifact: true } => PdfContentRole.Artifact,
        { Mcid: not null } => PdfContentRole.Tagged,
        _ => PdfContentRole.Untagged,
    };

    private RunStyle StyleOf(Letter letter)
    {
        var font = FontReader.WithoutSubsetPrefix(letter.FontName ?? "(none)");
        var size = Math.Round(letter.PointSize, 1, MidpointRounding.AwayFromZero);
        ContentOwner? owner = owners.Letters.TryGetValue(letter, out var found) ? found : null;
        var color = ColorText.Format(letter.Color);
        var inLayers = string.Join('\n', layers.Letters.GetValueOrDefault(letter) ?? []);
        return new RunStyle(font, size, color, ToRenderMode(letter.RenderingMode), owner, inLayers);
    }

    // Text that jumps back to the start of its line starts a new run even on the same baseline.
    private static bool OnSameLine(Letter previous, Letter next) =>
        TextJoiner.OnSameLine(previous, next) && TextJoiner.Advance(previous, next) >= 0;

    // PDF defines exactly these eight modes.
    private static PdfTextRenderMode ToRenderMode(TextRenderingMode mode) => mode switch
    {
        TextRenderingMode.Stroke => PdfTextRenderMode.Stroke,
        TextRenderingMode.FillThenStroke => PdfTextRenderMode.FillStroke,
        TextRenderingMode.Neither => PdfTextRenderMode.Invisible,
        TextRenderingMode.FillClip => PdfTextRenderMode.FillClip,
        TextRenderingMode.StrokeClip => PdfTextRenderMode.StrokeClip,
        TextRenderingMode.FillThenStrokeClip => PdfTextRenderMode.FillStrokeClip,
        TextRenderingMode.NeitherClip => PdfTextRenderMode.Clip,
        _ => PdfTextRenderMode.Fill,
    };

    // Layers are compared by their names, so two pieces of optional content in one layer don't split a run.
    private readonly record struct RunStyle(string Font, double Size, string? Color, PdfTextRenderMode RenderMode,
        ContentOwner? Owner, string Layers);
}
