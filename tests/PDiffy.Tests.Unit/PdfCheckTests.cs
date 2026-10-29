using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

public class PdfCheckTests
{
    [Fact]
    public void Check_ShouldFindNoLanguage_WhenTheCatalogHasNone()
    {
        var document = OnePage(new TestPdf { Lang = null });

        var finding = Assert.Single(document.Check(PdfCheck.DocumentLanguage));

        Assert.Equal("DocumentLanguage: The document has no language (document)", finding.ToString());
    }

    [Theory]
    [InlineData(null, "", 1)]
    [InlineData("Guide", "", 0)]
    [InlineData("Guide", "/ViewerPreferences << /DisplayDocTitle false >>", 1)]
    [InlineData(null, "/ViewerPreferences << /DisplayDocTitle false >>", 2)]
    public void Check_ShouldFindAMissingTitleAndAHiddenOne_WhenTheDocumentHasThem(string? title, string catalogEntries,
        int findings)
    {
        var document = OnePage(new TestPdf { Title = title }.CatalogEntries(catalogEntries));

        Assert.Equal(findings, document.Check(PdfCheck.DocumentTitle).Count);
    }

    [Fact]
    public void Check_ShouldFindTheFigureWithoutAltText_WhenOnlyOneOfTwoHasIt()
    {
        var pdf = new TestPdf();
        pdf.AddPage().TaggedImage(0).TaggedImage(1);
        pdf.Structure(new TestElement("Figure", 0), new TestElement("Figure", 1) { Entries = "/Alt (Logo)" });

        var finding = Assert.Single(pdf.Load().Check(PdfCheck.FigureAltText));

        Assert.Equal((1, "structure > Figure[1]", "Figure on p1 has no alt text"),
            (finding.Page, finding.Path, finding.Message));
    }

    [Fact]
    public void Check_ShouldFindUntaggedTextAndCountUntaggedGraphics_WhenAPageHasThem()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Untagged("Draft").UntaggedImage().UntaggedPath().UntaggedPath();

        var findings = pdf.Load().Check(PdfCheck.UntaggedContent).Select(finding => finding.Message);

        Assert.Equal(["Untagged text on p1: \"Draft\"", "1 untagged image(s) on p1", "2 untagged path(s) on p1"], findings);
    }

    [Theory]
    [InlineData("", "", "", 1)]
    [InlineData("Our website", "", "", 0)]
    [InlineData("", "/Alt (Our website)", "", 0)]
    [InlineData("", "", "Our website", 0)]
    public void Check_ShouldFindATaggedLinkWithoutADescription_WhenNeitherItNorItsElementHasOne(string contents,
        string entries, string text, int findings)
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, text, "Link");
        var link = new TestLink("https://example.com", contents);
        object[] kids = text.Length > 0 ? [0, link] : [link];
        pdf.Structure(new TestElement("Link", kids) { Entries = entries });

        Assert.Equal(findings, pdf.Load().Check(PdfCheck.LinkDescriptions).Count);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("/Contents (Our website)", 0)]
    public void Check_ShouldFindAnUntaggedLinkWithoutContents_WhenTheLinkHasNone(string contents, int findings)
    {
        var pdf = new TestPdf();
        pdf.AddPage().Annotation($"/Subtype /Link /Rect [72 700 172 720] {contents}");

        Assert.Equal(findings, pdf.Load().Check(PdfCheck.LinkDescriptions).Count);
    }

    [Fact]
    public void Check_ShouldFindTheFormFieldWithoutAnAccessibleName_WhenOnlyOneHasNone()
    {
        var pdf = new TestPdf().FormField("street", "/FT /Tx /TU (Street address)").FormField("city", "/FT /Tx");
        pdf.AddPage();

        var finding = Assert.Single(pdf.Load().Check(PdfCheck.FormFieldNames));

        Assert.Equal(("forms > city", "Form field \"city\" has no accessible name"), (finding.Path, finding.Message));
    }

    [Theory]
    [InlineData(" /A << /O /Table /Scope /Column >>", "(TH1)", 0)]
    [InlineData("", "(TH1)", 0)]
    [InlineData("", "(other)", 2)]
    public void Check_ShouldFindHeadersWithoutAScopeAndMissingHeaders_WhenATableHasThem(string scope, string named,
        int findings)
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Drug").Tagged(1, "Warfarin");
        pdf.Structure(new TestElement("Table",
            new TestElement("TR", new TestElement("TH", 0) { Entries = "/ID (TH1)" + scope }),
            new TestElement("TR", new TestElement("TD", 1) { Entries = $"/A << /O /Table /Headers [{named}] >>" })));

        Assert.Equal(findings, pdf.Load().Check(PdfCheck.TableHeaders).Count);
    }

    [Fact]
    public void Check_ShouldReportANestedTablesHeaderOnce_WhenATableHoldsAnother()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Dose");
        var inner = new TestElement("Table", new TestElement("TR", new TestElement("TH", 0)));
        pdf.Structure(new TestElement("Table", new TestElement("TR", new TestElement("TD", inner))));

        var finding = Assert.Single(pdf.Load().Check(PdfCheck.TableHeaders));

        Assert.Equal("structure > Table > TR > TD > Table > TR > TH", finding.Path);
    }

    [Theory]
    [InlineData(new[] { "H1", "H2", "H2", "H3", "H1" }, new string[0])]
    [InlineData(new[] { "H2" }, new[] { "H2 on p1 comes before any H1" })]
    [InlineData(new[] { "H1", "H3" }, new[] { "H3 on p1 follows H1, skipping a level" })]
    public void Check_ShouldFindSkippedHeadingLevels_WhenHeadingsSkipOne(string[] headings, string[] expected)
    {
        var pdf = new TestPdf();
        var page = pdf.AddPage();
        for (var i = 0; i < headings.Length; i++) page.Tagged(i, $"Heading {i}", headings[i]);
        pdf.Structure([.. headings.Select((heading, i) => new TestElement(heading, i))]);

        Assert.Equal(expected, pdf.Load().Check(PdfCheck.HeadingLevels).Select(finding => finding.Message));
    }

    [Fact]
    public void Check_ShouldFindEachFontNameOnceThatIsNotEmbeddedOrDoesntMapToUnicode_WhenTwoObjectsShareIt()
    {
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> "
                + "/Contents 4 0 R >>",
            TestPdf.Stream("BT /F1 12 Tf 72 700 Td (ABA) Tj /F2 12 Tf (ABA) Tj ET"),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding << /Differences [65 /g65 /B] >> >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding << /Differences [65 /g65 /B] >> >>");

        var findings = document.Check(PdfCheck.FontsEmbedded, PdfCheck.FontsMapToUnicode).Select(finding => finding.ToString());

        Assert.Equal(["FontsEmbedded: Font Helvetica isn't embedded (fonts > Helvetica)",
            "FontsMapToUnicode: Font Helvetica uses 1 code(s) that don't map to Unicode (fonts > Helvetica)"], findings);
    }

    [Fact]
    public void Check_ShouldFindJavaScriptActionsAndEmbeddedFiles_WhenTheCatalogHasThem()
    {
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R /AA << /WC << /S /JavaScript /JS (cleanup) >> >> "
                + "/OpenAction << /S /URI /URI (https://example.com) >> "
                + "/Names << /EmbeddedFiles << /Names [(data.xml) 5 0 R] >> >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
            TestPdf.Stream(""),
            "<< /Type /Filespec /F (data.xml) /UF (data.xml) /EF << /F 6 0 R >> >>",
            TestPdf.Stream("hello"));

        var findings = document.Check(PdfCheck.NoActions, PdfCheck.NoEmbeddedFiles).Select(finding => finding.Message);

        Assert.Equal(["The document has JavaScript", "The document runs actions on events such as closing or printing",
            "The document runs an action when it opens: URI https://example.com", "The document embeds data.xml"], findings);
    }

    [Theory]
    [InlineData("/OpenAction [3 0 R /Fit]")]
    [InlineData("/OpenAction << /S /GoTo /D [3 0 R /Fit] >>")]
    [InlineData("/OpenAction (Chapter1)")]
    public void Check_ShouldFindNoAction_WhenTheDocumentOnlyOpensAtAPage(string catalogEntries)
    {
        var document = OnePage(new TestPdf().CatalogEntries(catalogEntries));

        Assert.Empty(document.Check(PdfCheck.NoActions));
    }

    [Fact]
    public void Check_ShouldApplyEveryCheckInTheirOrder_WhenGivenNoneOrGivenThemTwiceOutOfOrder()
    {
        var document = OnePage(new TestPdf { Lang = null });

        var all = document.Check();

        Assert.Equal(document.Check([.. Enum.GetValues<PdfCheck>().Reverse(), PdfCheck.DocumentLanguage]), all);
        Assert.Equal([PdfCheck.DocumentLanguage, PdfCheck.DocumentTitle, PdfCheck.FontsEmbedded],
            all.Select(finding => finding.Check));
    }

    private static PdfUaDocument OnePage(TestPdf pdf)
    {
        pdf.AddPage().Tagged(0, "Hello");
        pdf.Structure(new TestElement("P", 0));
        return pdf.Load();
    }
}
