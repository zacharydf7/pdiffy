using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.UniversalAccessibility;

namespace PDiffy.Tests.Unit.Producers;

/// <summary>
/// A tagged PDF written by PDFsharp, so the tests read a writer other than <c>TestPdf</c>. Each <c>Heading</c>,
/// <c>Paragraph</c>, <c>Artifact</c> or <c>Figure</c> call draws one line on the current page, from the top down.
/// </summary>
internal sealed class PdfSharpPdf
{
    private readonly PdfDocument _document = new();
    private readonly StructureBuilder _tags;
    private XGraphics _page = null!;
    private double _y;

    static PdfSharpPdf() => GlobalFontSettings.FontResolver = new BundledFont();

    public PdfSharpPdf(string language = "en-US")
    {
        _document.Info.Title = "Producer test";
        var accessibility = UAManager.ForDocument(_document);
        accessibility.SetDocumentLanguage(language);
        _tags = accessibility.StructureBuilder;
        NewPage();
    }

    public PdfSharpPdf NewPage()
    {
        _page = XGraphics.FromPdfPage(_document.AddPage());
        _y = 72;
        return this;
    }

    public PdfSharpPdf Heading(string text) => Tagged(PdfBlockLevelElementTag.Heading1, 20, text);

    /// <summary>A paragraph drawn one piece after another on the same line.</summary>
    public PdfSharpPdf Paragraph(params string[] pieces) => Tagged(PdfBlockLevelElementTag.Paragraph, 12, pieces);

    public PdfSharpPdf Artifact(string text)
    {
        _tags.BeginArtifact();
        Draw(12, text);
        _tags.End();
        return this;
    }

    /// <summary>A blue square tagged as a figure.</summary>
    public PdfSharpPdf Figure(string alt)
    {
        var square = new XRect(72, _y, 50, 50);
        _tags.BeginElement(PdfIllustrationElementTag.Figure, alt, square);
        _page.DrawRectangle(XBrushes.Blue, square);
        _tags.End();
        _y += 70;
        return this;
    }

    public PdfUaDocument Load()
    {
        var pdf = new MemoryStream();
        _document.Save(pdf);
        return PdfUaDocument.Load(new MemoryStream(pdf.ToArray()));
    }

    private PdfSharpPdf Tagged(PdfBlockLevelElementTag tag, double size, params string[] pieces)
    {
        _tags.BeginElement(tag);
        Draw(size, pieces);
        _tags.End();
        return this;
    }

    private void Draw(double size, params string[] pieces)
    {
        var font = new XFont("Liberation Sans", size);
        var x = 72.0;
        foreach (var piece in pieces)
        {
            _page.DrawString(piece, font, XBrushes.Black, x, _y);
            x += _page.MeasureString(piece, font).Width;
        }

        _y += size * 2;
    }

    // PDFsharp finds no fonts on Linux by itself, so every font it asks for is the bundled Liberation Sans.
    private sealed class BundledFont : IFontResolver
    {
        private static readonly byte[] Font = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Producers", "Fonts", "LiberationSans-Regular.ttf"));

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) => new("LiberationSans");

        public byte[] GetFont(string faceName) => Font;
    }
}
