using PDiffy.Tests.Unit.Producers;

namespace PDiffy.Tests.Unit;

// PDFs from another writer, checked for what must hold whatever wrote them rather than against exact snapshots.
public class PdfSharpTests
{
    [Fact]
    public void Load_ShouldReadEachElementWithItsTextAndPage_WhenPdfSharpWroteThem()
    {
        var pdf = new PdfSharpPdf().Heading("Quarterly report").Paragraph("Revenue grew.")
            .NewPage().Paragraph("Costs fell.");

        var document = pdf.Load();

        (string, string?, int?)[] expected =
            [("H1", "Quarterly report", 1), ("P", "Revenue grew.", 1), ("P", "Costs fell.", 2)];
        var root = Assert.Single(document.Structure);
        Assert.Equal(expected, root.Children.Select(element => (element.Type, element.Text, element.FirstPage)));
    }

    [Theory]
    [InlineData("Revenue grew ", "by twelve percent.", "Revenue grew by twelve percent.")]
    [InlineData("Num", "ber", "Number")]
    public void Load_ShouldJoinTheText_WhenPdfSharpDrewAParagraphInPieces(string first, string second, string expected)
    {
        var pdf = new PdfSharpPdf().Paragraph(first, second);

        var document = pdf.Load();

        Assert.Equal(expected, Assert.Single(document.Elements("P")).Text);
    }

    [Fact]
    public void Load_ShouldReadTheAltTextAndLanguage_WhenPdfSharpSetThem()
    {
        var pdf = new PdfSharpPdf(language: "de-DE").Figure("A blue square");

        var document = pdf.Load();

        var figure = Assert.Single(document.Elements("Figure"));
        Assert.Equal(("A blue square", "de-DE"), (figure.Alt, document.Document.Lang));
    }

    [Fact]
    public void Load_ShouldKeepArtifactsOutOfTheStructure_WhenPdfSharpMarkedThem()
    {
        var pdf = new PdfSharpPdf().Artifact("Page 1").Paragraph("Body text.");

        var document = pdf.Load();

        Assert.Equal([new PageText(1, "Page 1")], document.Artifacts);
        Assert.Equal(["Body text."], document.Elements("P").Select(element => element.Text));
        Assert.Empty(document.Untagged);
    }

    [Fact]
    public void ToSnapshot_ShouldBeIdentical_WhenPdfSharpWritesTheSameDocumentTwice()
    {
        static PdfSharpPdf Build() => new PdfSharpPdf().Heading("Report").Figure("Chart").NewPage().Paragraph("End.");

        var first = Build().Load().ToSnapshot();
        var second = Build().Load().ToSnapshot();

        Assert.Equal(first, second);
    }
}
