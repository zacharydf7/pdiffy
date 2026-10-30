using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

public class LayoutTests
{
    [Fact]
    public void Load_ShouldSplitTextIntoRunsWithPositionFontSizeColorAndRole_WhenTextIsDrawn()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Go with a guide").Artifact("Page 1").Untagged("Stray");
        pdf.Structure(new TestElement("P", 0));

        var runs = pdf.Load().TextRuns;

        TextRun[] expected =
        [
            new(1, 72, 720, "Helvetica", 12, "gray:0", TextRenderMode.Fill, ContentRole.Tagged, "Go with a guide"),
            new(1, 72, 700, "Helvetica", 12, "gray:0", TextRenderMode.Fill, ContentRole.Artifact, "Page 1"),
            new(1, 72, 680, "Helvetica", 12, "gray:0", TextRenderMode.Fill, ContentRole.Untagged, "Stray"),
        ];
        Assert.Equal(expected, runs);
    }

    [Fact]
    public void Load_ShouldStartANewRun_WhenColorOrSizeChangesMidLine()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Raw("BT /F1 12 Tf 72 400 Td (Go ) Tj 1 0 0 rg (with) Tj /F1 14 Tf ( a guide) Tj ET");

        var runs = pdf.Load().TextRuns;

        Assert.Equal(["Go", "with", "a guide"], runs.Select(run => run.Text));
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
    public void Load_ShouldListEachElementsOwnTextRuns_WhenElementsDrawTextInDifferentStyles()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Trail Guide", "H1")
            .Raw("/P << /MCID 1 >> BDC BT /F1 10 Tf 72 600 Td (Start at the lodge) Tj ET EMC").Tagged(2, "Map", "Span");
        pdf.Structure(new TestElement("H1", 0), new TestElement("P", 1, new TestElement("Span", 2)));

        var document = pdf.Load();

        var (heading, paragraph) = (document.Structure[0], document.Structure[1]);
        Assert.Equal([("Trail Guide", 12.0)], heading.TextRuns.Select(run => (run.Text, run.Size)));
        Assert.Equal([("Start at the lodge", 10.0)], paragraph.TextRuns.Select(run => (run.Text, run.Size)));
        Assert.Equal(["Map"], paragraph.Children[0].TextRuns.Select(run => run.Text));
        Assert.Equal(document.TextRuns, [heading.TextRuns[0], paragraph.TextRuns[0], paragraph.Children[0].TextRuns[0]]);
    }

    [Fact]
    public void Load_ShouldListTextRunsInTheElementsOrder_WhenItListsItsMarkedContentOutOfDrawingOrder()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Bring water").Tagged(1, "Trail closed");
        pdf.Structure(new TestElement("P", 1, 0));

        var runs = pdf.Load().Structure[0].TextRuns;

        Assert.Equal(["Trail closed", "Bring water"], runs.Select(run => run.Text));
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
    public void Load_ShouldTieEachFigureToTheImageItDraws_WhenAPageHasSeveralFigures()
    {
        var pdf = new TestPdf();
        pdf.AddPage().TaggedImage(0, gray: "20", y: 600).TaggedImage(1, gray: "E0", y: 400);
        pdf.Structure(new TestElement("Figure", 1) { Entries = "/Alt (Logo)" }, new TestElement("Figure", 0) { Entries = "/Alt (Map)" });

        var document = pdf.Load();

        var (map, logo) = (document.Images[0].Hash, document.Images[1].Hash);
        Assert.NotEqual(map, logo);
        Assert.Equal([[logo], [map]], document.Structure.Select(figure => figure.ImageHashes));
    }

    [Fact]
    public void CompareTo_ShouldReportBothFigures_WhenTheirImagesAreSwapped()
    {
        static PdfUaDocument Build(string mapGray, string logoGray)
        {
            var pdf = new TestPdf();
            pdf.AddPage().TaggedImage(0, mapGray, y: 600).TaggedImage(1, logoGray, y: 400);
            pdf.Structure(new TestElement("Figure", 0) { Entries = "/Alt (Map)" }, new TestElement("Figure", 1) { Entries = "/Alt (Logo)" });
            return pdf.Load();
        }

        var diff = Build("20", "E0").CompareTo(Build("E0", "20"));

        var figures = diff.Changes.Where(change => change.Section == Section.Structure).ToList();
        Assert.Equal(2, figures.Count);
        Assert.All(figures, change => Assert.Equal(ChangeKind.AttributeChanged, change.Kind));
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

    [Theory]
    [InlineData("0 1 -1 0", 90)]
    [InlineData("-1 0 0 -1", 180)]
    [InlineData("0 -1 1 0", 270)]
    [InlineData("0.866 0.5 -0.5 0.866", 30)]
    public void Load_ShouldReadRotatedTextAsOneRunOfWholeWords_WhenTextIsDrawnAtAnAngle(string matrix, int angle)
    {
        var pdf = new TestPdf();
        pdf.AddPage().Raw($"/P << /MCID 0 >> BDC BT /F1 12 Tf {matrix} 300 400 Tm (Hello world) Tj ET EMC");
        pdf.Structure(new TestElement("P", 0));

        var document = pdf.Load();

        var run = Assert.Single(document.TextRuns);
        Assert.Equal(("Hello world", angle), (run.Text, run.Angle));
        Assert.Equal("Hello world", Assert.Single(document.Structure).Text);
        Assert.Contains($" angle={angle} \"Hello world\"\n", document.ToSnapshot());
    }

    [Theory]
    [InlineData("0 1 -1 0", 90)]
    [InlineData("0.866 0.5 -0.5 0.866", 30)]
    public void Load_ShouldTakeTheAngleFromTheGlyphs_WhenRotatedLettersHaveNoWidth(string matrix, int angle)
    {
        var pdf = new TestPdf();
        pdf.AddPage().Raw($"BT /F1 12 Tf 0 Tz {matrix} 300 400 Tm (Hi there) Tj ET");

        var run = Assert.Single(pdf.Load().TextRuns);

        Assert.Equal(("Hi there", angle), (run.Text, run.Angle));
    }

    [Theory]
    [InlineData("3 Tc (Hello world) Tj", "Hello world")]
    [InlineData("3 Tc [(Hello) -600 (world)] TJ", "Hello world")]
    [InlineData("(1) Tj 100 0 Td (2) Tj 100 0 Td (3) Tj 100 0 Td (4) Tj", "1 2 3 4")]
    [InlineData("[(I) -333 (am) -333 (a) -333 (cat)] TJ", "I am a cat")]
    [InlineData("[(Go) -300 (to) -300 (B)] TJ", "Go to B")]
    [InlineData("[(1) -300 (2) -300 (3) -300 (4)] TJ", "1 2 3 4")]
    [InlineData("3 Tc (FAQ) Tj", "F A Q")]
    [InlineData("2.7 Tc (Hello world) Tj", "Hello world")]
    [InlineData("4 Tc (Hello world) Tj", "H e l l o w o r l d")]
    public void Load_ShouldPutSpacesOnlyBetweenWords_WhenTextIsLetterSpacedOrSetApart(string operators, string expected)
    {
        var pdf = new TestPdf();
        pdf.AddPage().Raw($"/P << /MCID 0 >> BDC BT /F1 12 Tf 72 400 Td {operators} ET EMC");
        pdf.Structure(new TestElement("P", 0));

        var document = pdf.Load();

        Assert.Equal(expected, Assert.Single(document.Structure).Text);
        Assert.Equal(expected, string.Join(" ", document.TextRuns.Select(run => run.Text)));
    }

    [Fact]
    public void Load_ShouldTakeLetterSpacingAsSuch_WhenItsRoundedJustPastTheCap()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Raw("BT /F1 10.5 Tf 72 400 Td 2.63 Tc (Hello world) Tj ET");

        Assert.Equal("Hello world", Assert.Single(pdf.Load().TextRuns).Text);
    }

    [Fact]
    public void Load_ShouldWorkOutLetterSpacingForEachLine_WhenOneElementHasALetterSpacedLineAndAnOrdinaryOne()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Raw("/P << /MCID 0 >> BDC BT /F1 12 Tf 72 400 Td 3 Tc (Trail guide) Tj 0 Tc 0 -20 Td (Go with a guide) Tj ET EMC");
        pdf.Structure(new TestElement("P", 0));

        var document = pdf.Load();

        Assert.Equal("Trail guide Go with a guide", Assert.Single(document.Structure).Text);
        Assert.Equal(["Trail guide", "Go with a guide"], document.TextRuns.Select(run => run.Text));
    }

    [Fact]
    public void Load_ShouldKeepRotatedLinesApart_WhenTheyRunSideBySideOrNextToTextAcrossThePage()
    {
        var pdf = new TestPdf();

        // Across sits on Length's axis, just past its end, so only the direction check keeps the two apart.
        pdf.AddPage().Raw("BT /F1 12 Tf 0 1 -1 0 300 400 Tm (Trail) Tj ET BT /F1 12 Tf 0 1 -1 0 320 400 Tm (Length) Tj ET")
            .Raw("BT /F1 12 Tf 320 440 Td (Across) Tj ET");

        var runs = pdf.Load().TextRuns;

        Assert.Equal([("Trail", 90), ("Length", 90), ("Across", 0)], runs.Select(run => (run.Text, run.Angle)));
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
