using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

/// <summary>Malformed files whose references loop back on themselves. Each must load instead of hanging or overflowing the stack.</summary>
public class MalformedPdfTests
{
    [Fact]
    public void Load_ShouldReadEachElementOnce_WhenStructureElementContainsItself()
    {
        var document = OnePage("/StructTreeRoot 6 0 R",
            extraObjects: ["<< /Type /StructTreeRoot /K [7 0 R] >>", "<< /Type /StructElem /S /P /Pg 3 0 R /K [0 7 0 R] >>"]);

        var paragraph = Assert.Single(document.Structure);
        Assert.Equal("Loop", paragraph.Text);
        Assert.Empty(paragraph.Children);
    }

    [Fact]
    public void Load_ShouldReadEachLabelNodeOnce_WhenPageLabelTreeContainsItself()
    {
        var document = OnePage("/PageLabels 6 0 R", extraObjects: ["<< /Kids [6 0 R] /Nums [0 << /S /D >>] >>"]);

        Assert.Equal([new PageLabelRange(1, "D", null, 1)], document.PageLabels);
    }

    [Fact]
    public void Load_ShouldReadEachFieldOnce_WhenFormFieldContainsItself()
    {
        var document = OnePage("/AcroForm << /Fields [6 0 R] >>", extraObjects: ["<< /T (loop) /FT /Tx /Kids [6 0 R] >>"]);

        var field = Assert.Single(document.FormFields);
        Assert.Equal(("loop", FormFieldType.Text), (field.Name, field.Type));
    }

    [Fact]
    public void Load_ShouldReadEachNameTreeNodeOnce_WhenEmbeddedFilesTreeContainsItself()
    {
        var document = OnePage("/Names << /EmbeddedFiles 6 0 R >>", extraObjects: ["<< /Kids [6 0 R] /Names [(a.txt) << >>] >>"]);

        Assert.Equal(["a.txt"], document.Settings.EmbeddedFiles.Select(file => file.Name));
    }

    [Fact]
    public void Load_ShouldStopFollowing_WhenActionsOrArraysReferToThemselves()
    {
        var document = OnePage("/OpenAction 6 0 R /ViewerPreferences << /PrintPageRange 7 0 R >>",
            extraObjects: ["<< /S /GoTo /D 6 0 R /Next 6 0 R >>", "[7 0 R]"]);

        Assert.Equal("GoTo", document.Settings.OpenAction);
        Assert.False(document.Settings.HasJavaScript);
        Assert.Equal("[[[[[...]]]]]", Assert.Single(document.Settings.ViewerPreferences).Value);
    }

    [Fact]
    public void Load_ShouldCountEachPageOnce_WhenPageTreeContainsItself()
    {
        var document = OnePage(pagesKids: "3 0 R 2 0 R");

        Assert.Single(document.Pages);
        Assert.Single(document.Fonts);
    }

    [Fact]
    public void Load_ShouldCountAMarkedContentReferenceWithoutAPage_WhenNeitherItNorItsElementNamesOne()
    {
        var document = OnePage(catalogEntries: "/StructTreeRoot 6 0 R",
            extraObjects: ["<< /Type /StructTreeRoot /K [7 0 R] >>", "<< /Type /StructElem /S /P /P 6 0 R /K [<< /Type /MCR /MCID 0 >>] >>"]);

        var paragraph = Assert.Single(document.Structure);

        Assert.Equal((1, null), (paragraph.McidsWithoutPage, paragraph.Text));
    }

    // Objects 1-5 are a one-page document whose page shows "Loop" as MCID 0; extra objects are numbered from 6.
    private static PdfUaDocument OnePage(string catalogEntries = "", string pagesKids = "3 0 R", string[]? extraObjects = null)
    {
        string[] objects =
        [
            $"<< /Type /Catalog /Pages 2 0 R /MarkInfo << /Marked true >> {catalogEntries} >>",
            $"<< /Type /Pages /Kids [{pagesKids}] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            TestPdf.Stream("/P << /MCID 0 >> BDC BT /F1 12 Tf 72 700 Td (Loop) Tj ET EMC"),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        ];
        return RawPdf.Load([.. objects, .. extraObjects ?? []]);
    }
}
