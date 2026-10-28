using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

public class LayoutTests
{
    [Fact]
    public void Load_ShouldSplitTextIntoRunsWithPositionFontSizeColorAndRole_WhenTextIsDrawn()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Take with food").Artifact("Page 1").Untagged("Stray");
        pdf.Structure(new TestElement("P", 0));

        var runs = pdf.Load().TextRuns;

        TextRun[] expected =
        [
            new(1, 72, 720, "Helvetica", 12, "gray:0", TextRenderMode.Fill, ContentRole.Tagged, "Take with food"),
            new(1, 72, 700, "Helvetica", 12, "gray:0", TextRenderMode.Fill, ContentRole.Artifact, "Page 1"),
            new(1, 72, 680, "Helvetica", 12, "gray:0", TextRenderMode.Fill, ContentRole.Untagged, "Stray"),
        ];
        Assert.Equal(expected, runs);
    }

    [Fact]
    public void Load_ShouldStartANewRun_WhenColorOrSizeChangesMidLine()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Raw("BT /F1 12 Tf 72 400 Td (Take ) Tj 1 0 0 rg (with) Tj /F1 14 Tf ( food) Tj ET");

        var runs = pdf.Load().TextRuns;

        Assert.Equal(["Take", "with", "food"], runs.Select(run => run.Text));
        Assert.Equal(["gray:0", "#ff0000", "#ff0000"], runs.Select(run => run.Color));
        Assert.Equal([12.0, 12.0, 14.0], runs.Select(run => run.Size));
    }

    [Fact]
    public void Load_ShouldRecordColorSpaceAndInvisibleText_WhenTextUsesThem()
    {
        var pdf = new TestPdf();
        pdf.AddPage()
            .Raw("BT 0 0 0 1 k /F1 12 Tf 72 400 Td (Printed) Tj ET")
            .Raw("BT 3 Tr /F1 12 Tf 72 380 Td (Hidden) Tj ET");

        var runs = pdf.Load().TextRuns;

        Assert.Equal(("cmyk:0,0,0,1", TextRenderMode.Fill), (runs[0].Color, runs[0].RenderMode));
        Assert.Equal(TextRenderMode.Invisible, runs[1].RenderMode);
    }

    [Fact]
    public void Load_ShouldNameThePatternKind_WhenTextOrPathIsPaintedWithAGradient()
    {
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> /Pattern << /P0 6 0 R >> >> "
                + "/Contents 4 0 R >>",
            TestPdf.Stream("/Pattern cs /P0 scn 72 500 100 20 re f BT /F1 12 Tf 72 400 Td (Shaded) Tj ET"),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /PatternType 2 /Shading << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 100 0] "
                + "/Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >> >> >>");

        Assert.Equal("pattern:shading", Assert.Single(document.Paths).Fill);
        Assert.Equal("pattern:shading", Assert.Single(document.TextRuns).Color);
    }

    [Fact]
    public void Load_ShouldListImagesWithPositionPixelSizeHashAndRole_WhenPageHasImages()
    {
        var pdf = new TestPdf();
        pdf.AddPage().TaggedImage(0).UntaggedImage();
        pdf.Structure(new TestElement("Figure", 0) { Entries = "/Alt (Logo)" });

        var images = pdf.Load().Images;

        Assert.Equal([ContentRole.Tagged, ContentRole.Untagged], images.Select(image => image.Role));
        Assert.All(images, image => Assert.Equal((72, 600, 10, 10, 1, 1),
            (image.X, image.Y, image.Width, image.Height, image.PixelWidth, image.PixelHeight)));
        Assert.Equal(images[0].Hash, images[1].Hash);
    }

    [Fact]
    public void Load_ShouldGiveTheRoleToTheRightImage_WhenDifferentImagesShareAPosition()
    {
        var pdf = new TestPdf();
        pdf.AddPage().UntaggedImage(gray: "20").TaggedImage(0, gray: "E0");
        pdf.Structure(new TestElement("Figure", 0) { Entries = "/Alt (Logo)" });

        var images = pdf.Load().Images;

        Assert.Equal([ContentRole.Untagged, ContentRole.Tagged], images.Select(image => image.Role));
    }

    [Fact]
    public void Load_ShouldGiveDifferentHashes_WhenImagePixelsDiffer()
    {
        var pdf = new TestPdf();
        pdf.AddPage().ArtifactImage(gray: "20").ArtifactImage(gray: "E0");

        var images = pdf.Load().Images;

        Assert.Equal([ContentRole.Artifact, ContentRole.Artifact], images.Select(image => image.Role));
        Assert.NotEqual(images[0].Hash, images[1].Hash);
    }

    [Fact]
    public void Load_ShouldStartANewRun_WhenTextOnALineIsSplitAcrossMarkedContent()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Raw("BT /F1 12 Tf 72 400 Td /P << /MCID 0 >> BDC (Account Number: ) Tj EMC "
            + "/P << /MCID 1 >> BDC (12345) Tj EMC ET");
        pdf.Structure(new TestElement("P", 0, 1));

        var document = pdf.Load();

        Assert.Equal("Account Number: 12345", Assert.Single(document.Structure).Text);
        Assert.Equal(["Account Number:", "12345"], document.TextRuns.Select(run => run.Text));
    }

    [Fact]
    public void Load_ShouldStartANewRun_WhenTextWrapsToTheNextLine()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Raw("/P << /MCID 0 >> BDC BT /F1 12 Tf 72 400 Td (First line) Tj 0 -14 Td (Second line) Tj ET EMC");
        pdf.Structure(new TestElement("P", 0));

        var runs = pdf.Load().TextRuns;

        Assert.Equal([("First line", 400), ("Second line", 386)], runs.Select(run => (run.Text, run.Y)));
        Assert.All(runs, run => Assert.Equal(ContentRole.Tagged, run.Role));
    }

    [Fact]
    public void Load_ShouldMarkTextUntagged_WhenNoElementPointsToItsMcid()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Reachable").Tagged(1, "Orphaned");
        pdf.Structure(new TestElement("P", 0));

        var runs = pdf.Load().TextRuns;

        Assert.Equal([ContentRole.Tagged, ContentRole.Untagged], runs.Select(run => run.Role));
    }

    [Fact]
    public void Load_ShouldListPaintedPathsWithFillStrokeAndRole_WhenPageHasVectorGraphics()
    {
        var pdf = new TestPdf();
        pdf.AddPage().UntaggedPath().ArtifactPath().ClippingPath().Raw("1 0 0 RG 2 w 72 300 m 172 300 l S");

        var paths = pdf.Load().Paths;

        PathInfo[] expected =
        [
            new(1, 72, 500, 100, 20, "gray:0", null, null, ContentRole.Untagged),
            new(1, 72, 480, 100, 1, "gray:0", null, null, ContentRole.Artifact),
            new(1, 72, 300, 100, 0, null, "#ff0000", 2, ContentRole.Untagged),
        ];
        Assert.Equal(expected, paths);
    }
}
