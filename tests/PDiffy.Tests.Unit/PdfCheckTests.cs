using System.Text;
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

        var findings = pdf.Load().Check(PdfCheck.UntaggedContent);

        Assert.Equal(["Untagged text on p1: \"Draft\"", "1 untagged image(s) on p1", "2 untagged path(s) on p1"],
            findings.Select(finding => finding.Message));
        Assert.Equal(["untagged", "untagged > images", "untagged > paths"], findings.Select(finding => finding.Path));
    }

    [Fact]
    public void Check_ShouldFindUntaggedTextOncePerPage_WhenAPageHasSeveralUntaggedLines()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Untagged("Draft").Untagged("Not for release");

        var finding = Assert.Single(pdf.Load().Check(PdfCheck.UntaggedContent));

        Assert.Equal("Untagged text on p1: \"Draft Not for release\"", finding.Message);
    }

    [Fact]
    public void Check_ShouldFindUntaggedTextOncePerPage_WhenThePageAlsoHasTaggedTextNoElementPointsTo()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Untagged("Draft").Tagged(0, "Trail notes");

        var finding = Assert.Single(pdf.Load().Check(PdfCheck.UntaggedContent));

        Assert.Equal("Untagged text on p1: \"Draft Trail notes\"", finding.Message);
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

    [Theory]
    [InlineData("", "", "click here", "click here")]
    [InlineData("", "", "Read more >", "Read more >")]
    [InlineData("", "", " here. ", "here.")]
    [InlineData("", "", "Learn more \u00BB", "Learn more \u00BB")]
    [InlineData("more", "", "", "more")]
    [InlineData("this link", "", "", "this link")]
    [InlineData("", "", "Click", "Click")]
    [InlineData("", "", "More info:", "More info:")]
    [InlineData("", "/ActualText (Download the ridge loop trail map)", "click here", null)]
    [InlineData("", "/Alt (Link)", "", "Link")]
    [InlineData("", "", "Trail map of the ridge loop", null)]
    [InlineData("", "", "Here is the trail map", null)]
    [InlineData("Download the ridge loop trail map", "", "click here", null)]
    [InlineData("", "", "", null)]
    public void Check_ShouldWarnAboutATaggedLink_WhenNoneOfItsTextSaysWhereItGoes(string contents, string entries,
        string text, string? vague)
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, text, "Link");
        var link = new TestLink("https://example.com", contents);
        object[] kids = text.Length > 0 ? [0, link] : [link];
        pdf.Structure(new TestElement("Link", kids) { Entries = entries });

        var findings = pdf.Load().Check(PdfCheck.LinkText);

        string[] expected = vague is null ? []
            : [$"structure > Link: Link on p1 says \"{vague}\", which doesn't say where it goes"];
        Assert.Equal(expected, findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    [Fact]
    public void Check_ShouldNotWarnAboutALink_WhenAChildElementSaysWhereItGoes()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Download the ridge loop trail map", "Span");
        var link = new TestLink("https://example.com", "click here");
        pdf.Structure(new TestElement("Link", new TestElement("Span", 0), link));

        Assert.Empty(pdf.Load().Check(PdfCheck.LinkText));
    }

    [Theory]
    [InlineData("/Contents (Click here)", 1)]
    [InlineData("/Contents (Our website)", 0)]
    [InlineData("", 0)]
    public void Check_ShouldWarnAboutAnUntaggedLink_WhenItsContentsAreVague(string contents, int findings)
    {
        var pdf = new TestPdf();
        pdf.AddPage().Annotation($"/Subtype /Link /Rect [72 700 172 720] {contents}");

        Assert.Equal(findings, pdf.Load().Check(PdfCheck.LinkText).Count);
    }

    [Theory]
    [InlineData("<41010102>", 1)]
    [InlineData("<4142>", 0)]
    public void Check_ShouldFindAFontShowingTheMissingGlyph_WhenItsEncodingSendsCodesThere(string text, int findings)
    {
        var font = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>";

        var found = TestFonts.OneFont(font, $"{text} Tj").Check(PdfCheck.NoMissingGlyphs);

        Assert.Equal(findings, found.Count);
        Assert.All(found, finding => Assert.Equal(
            "fonts > Helvetica: Font Helvetica shows the missing-glyph box for 2 code(s)",
            $"{finding.Path}: {finding.Message}"));
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
        pdf.AddPage().Tagged(0, "Trail").Tagged(1, "Ridgeline");
        pdf.Structure(new TestElement("Table",
            new TestElement("TR", new TestElement("TH", 0) { Entries = "/ID (TH1)" + scope }),
            new TestElement("TR", new TestElement("TD", 1) { Entries = $"/A << /O /Table /Headers [{named}] >>" })));

        Assert.Equal(findings, pdf.Load().Check(PdfCheck.TableHeaders).Count);
    }

    [Fact]
    public void Check_ShouldReportANestedTablesHeaderOnce_WhenATableHoldsAnother()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Stage");
        var inner = new TestElement("Table", new TestElement("TR", new TestElement("TH", 0)));
        pdf.Structure(new TestElement("Table", new TestElement("TR", new TestElement("TD", inner))));

        var finding = Assert.Single(pdf.Load().Check(PdfCheck.TableHeaders));

        Assert.Equal("structure > Table > TR > TD > Table > TR > TH > scope", finding.Path);
    }

    [Fact]
    public void Check_ShouldNameTheKeyOfEachHeaderProblem_WhenOneCellHasTwo()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Trail");
        pdf.Structure(new TestElement("Table",
            new TestElement("TR", new TestElement("TH", 0) { Entries = "/A << /O /Table /Headers [(gone)] >>" })));

        var findings = pdf.Load().Check(PdfCheck.TableHeaders);

        Assert.Equal(["structure > Table > TR > TH > scope", "structure > Table > TR > TH > headers"],
            findings.Select(finding => finding.Path));
    }

    [Theory]
    [InlineData(new[] { "H1", "H2", "H2", "H3", "H1" }, new string[0])]
    [InlineData(new[] { "H2" }, new[] { "H2 on p1 comes before any H1" })]
    [InlineData(new[] { "H1", "H3" }, new[] { "H3 on p1 follows H1, skipping a level" })]
    [InlineData(new[] { "H2", "H7" }, new[] { "H2 on p1 comes before any H1" })]
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

        var findings = document.Check(PdfCheck.NoActions, PdfCheck.NoEmbeddedFiles);

        Assert.Equal(["The document has JavaScript", "The document runs actions on events such as closing or printing",
            "The document runs an action when it opens: URI https://example.com", "The document embeds data.xml"],
            findings.Select(finding => finding.Message));
        Assert.Equal(
            ["settings > javascript", "settings > additional-actions", "settings > open-action", "settings > data.xml"],
            findings.Select(finding => finding.Path));
    }

    [Theory]
    [InlineData("/OpenAction [3 0 R /Fit]")]
    [InlineData("/OpenAction << /S /GoTo /D [3 0 R /Fit] >>")]
    [InlineData("/OpenAction (Chapter1)")]
    [InlineData("/OpenAction (Chapter 1, Intro)")]
    [InlineData("/OpenAction << /S /GoTo /D [3 0 R /Fit] /Next << /S /GoTo /D [3 0 R /FitH 700] >> >>")]
    public void Check_ShouldFindNoAction_WhenTheDocumentOnlyOpensAtAPage(string catalogEntries)
    {
        var document = OnePage(new TestPdf().CatalogEntries(catalogEntries));

        Assert.Empty(document.Check(PdfCheck.NoActions));
    }

    [Fact]
    public void Check_ShouldReportChainedJavaScriptOnce_WhenItRunsAfterGoingToAPage()
    {
        var document = OnePage(new TestPdf().CatalogEntries(
            "/OpenAction << /S /GoTo /D [3 0 R /Fit] /Next << /S /JavaScript /JS (x) >> >>"));

        var finding = Assert.Single(document.Check(PdfCheck.NoActions));

        Assert.Equal("The document has JavaScript", finding.Message);
    }

    [Fact]
    public void Check_ShouldReportJavaScript_WhenOnlyAFormFieldRunsAScript()
    {
        var document = OnePage(new TestPdf().FormField("total", "/FT /Tx /AA << /C << /S /JavaScript /JS (sum()) >> >>"));

        var finding = Assert.Single(document.Check(PdfCheck.NoActions));

        Assert.Equal("The document has JavaScript", finding.Message);
    }

    [Theory]
    [InlineData("/OpenAction << /S /GoToR /F (other.pdf) /D [0 /Fit] >>", "GoToR")]
    [InlineData("/OpenAction << /S /GoToE /D [0 /Fit] >>", "GoToE")]
    [InlineData("/OpenAction << /S /GoTo /D [3 0 R /Fit] /Next << /S /Launch /F (setup.exe) >> >>", "GoTo p? Fit, Launch")]
    public void Check_ShouldFindTheOpenAction_WhenItOpensAnotherFile(string catalogEntries, string action)
    {
        var document = OnePage(new TestPdf().CatalogEntries(catalogEntries));

        var finding = Assert.Single(document.Check(PdfCheck.NoActions));

        Assert.Equal($"The document runs an action when it opens: {action}", finding.Message);
    }

    // Each mapping is "Custom=Standard"; each expected finding is "path: message".
    [Theory]
    [InlineData("Para", new[] { "Para=P" }, new string[0])]
    [InlineData("Para", new[] { "Para=Body", "Body=P" }, new string[0])]
    [InlineData("P", new[] { "P=P" }, new string[0])]
    [InlineData("Aside", new[] { "Aside=Note" }, new string[0])]
    [InlineData("Para", new[] { "Para=Aside" },
        new[] { "role-map > Para: Structure type Para, used by 1 element(s), doesn't map to a standard type" })]
    [InlineData("Para", new string[0],
        new[] { "role-map > Para: Structure type Para, used by 1 element(s), doesn't map to a standard type" })]
    [InlineData("Para", new[] { "Para=Body" },
        new[] { "role-map > Para: Structure type Para, used by 1 element(s), doesn't map to a standard type" })]
    [InlineData("A", new[] { "A=B", "B=A" },
        new[]
        {
            "role-map > A > loop: Structure type A maps back to itself: A -> B -> A",
            "role-map > B > loop: Structure type B maps back to itself: B -> A -> B",
            "role-map > A: Structure type A, used by 1 element(s), doesn't map to a standard type",
        })]
    [InlineData("A", new[] { "A=B", "B=C", "C=B" },
        new[]
        {
            "role-map > B > loop: Structure type B maps back to itself: B -> C -> B",
            "role-map > C > loop: Structure type C maps back to itself: C -> B -> C",
            "role-map > A: Structure type A, used by 1 element(s), doesn't map to a standard type",
        })]
    [InlineData("P", new[] { "P=Span" },
        new[] { "role-map > P > standard: Standard structure type P is remapped to Span" })]
    [InlineData("Para", new[] { "Para=P", "P=Span" },
        new[] { "role-map > P > standard: Standard structure type P is remapped to Span" })]
    public void Check_ShouldFindUnmappedTypesLoopsAndRemappedStandardTypes_WhenTheRoleMapHasThem(string type,
        string[] mappings, string[] expected)
    {
        var pdf = new TestPdf();
        foreach (var mapping in mappings) pdf.RoleMap(mapping.Split('=')[0], mapping.Split('=')[1]);
        pdf.AddPage().Tagged(0, "Hello", type);
        pdf.Structure(new TestElement(type, 0));

        var findings = pdf.Load().Check(PdfCheck.RoleMap);

        Assert.Equal(expected, findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    private const string XmpTitle = "<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">Guide</rdf:li></rdf:Alt></dc:title>";

    // Each expected finding is "path: message".
    [Theory]
    [InlineData("1", XmpTitle, new string[0])]
    [InlineData(null, null, new[] { "metadata > xmp: The document has no XMP metadata" })]
    [InlineData(null, "<dc:title>", new[] { "metadata > xmp: The XMP metadata can't be read" })]
    [InlineData(null, XmpTitle,
        new[] { "metadata > pdfuaid-part: The XMP doesn't say the document is PDF/UA (pdfuaid:part)" })]
    [InlineData("2", XmpTitle, new[] { "metadata > pdfuaid-part: The XMP says PDF/UA part 2, not 1" })]
    [InlineData("1", null, new[] { "metadata > xmp-title: The XMP has no title (dc:title)" })]
    [InlineData(null, "<rdf:Description pdfuaid:part=\"1\"/>" + XmpTitle, new string[0])]
    [InlineData(null, "<rdf:Description xmlns:ua=\"http://www.aiim.org/pdfua/ns/id/\" ua:part=\"1\"/>" + XmpTitle,
        new[]
        {
            "metadata > pdfuaid-prefix: The XMP writes its PDF/UA identification with the prefix \"ua\", not \"pdfuaid\"",
        })]
    [InlineData(null, "<ua:part xmlns:ua=\"http://www.aiim.org/pdfua/ns/id/\">1</ua:part>" + XmpTitle,
        new[]
        {
            "metadata > pdfuaid-prefix: The XMP writes its PDF/UA identification with the prefix \"ua\", not \"pdfuaid\"",
        })]
    public void Check_ShouldFindMissingOrWrongXmp_WhenTheMetadataLacksWhatPdfUaAsks(string? part, string? xmp,
        string[] expected)
    {
        var document = OnePage(new TestPdf { PdfUaPart = part, Xmp = xmp });

        var findings = document.Check(PdfCheck.XmpMetadata);

        Assert.Equal(expected, findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    // Built by hand, since TestPdf's XMP declares the pdfuaid prefix, which would be in scope.
    [Fact]
    public void Check_ShouldFindTheIdentifierWithoutAPrefix_WhenTheXmpWritesItInADefaultNamespace()
    {
        var xmp = "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
            + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description>"
            + "<part xmlns=\"http://www.aiim.org/pdfua/ns/id/\">1</part></rdf:Description></rdf:RDF></x:xmpmeta>";
        var document = RawPdf.Load("<< /Type /Catalog /Pages 2 0 R /Metadata 3 0 R >>",
            "<< /Type /Pages /Kids [] /Count 0 >>",
            $"<< /Type /Metadata /Subtype /XML /Length {xmp.Length} >>\nstream\n{xmp}\nendstream");

        var findings = document.Check(PdfCheck.XmpMetadata)
            .Where(finding => finding.Path == "metadata > pdfuaid-prefix");

        Assert.Equal(["The XMP writes its PDF/UA identification without a prefix, not \"pdfuaid\""],
            findings.Select(finding => finding.Message));
    }

    [Fact]
    public void Check_ShouldFindUnmarkedSuspectAndParentlessTags_WhenTheDocumentHasThem()
    {
        var pdf = new TestPdf { MarkInfo = "/Marked false /Suspects true" };
        pdf.AddPage().Tagged(0, "Hello");
        pdf.Structure(new TestElement("P", 0) { WithoutParent = true });

        var findings = pdf.Load().Check(PdfCheck.TaggedPdf);

        Assert.Equal(
        [
            "document > marked: The document isn't marked as tagged (MarkInfo Marked)",
            "document > suspects: The document says its tags may not be reliable (MarkInfo Suspects)",
            "structure > P: P on p1 has no parent entry (P)",
        ], findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    [Fact]
    public void Check_ShouldGiveEachFindingItsOwnClause_WhenARuleCoversMoreThanOne()
    {
        var document = OnePage(new TestPdf { MarkInfo = "/Marked false", PdfUaPart = "2" });

        var findings = document.Check(PdfCheck.XmpMetadata, PdfCheck.TaggedPdf);

        Assert.Equal(
        [
            ("metadata > pdfuaid-part", "ISO 14289-1 5"), ("metadata > xmp-title", "ISO 14289-1 7.1"),
            ("document > marked", "ISO 14289-1 6.2"),
        ], findings.Select(finding => (finding.Path, finding.Reference)));
    }

    [Fact]
    public void Check_ShouldFindNoStructureTree_WhenTheCatalogHasNone()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Untagged("Hello");

        var findings = pdf.Load().Check(PdfCheck.TaggedPdf);

        Assert.Equal(["structure: The document has no structure tree"], findings.Select(f => $"{f.Path}: {f.Message}"));
    }

    [Theory]
    [InlineData("1.7", "", new string[0])]
    [InlineData("2.0", "",
        new[] { "settings > version: The file header says PDF 2.0; PDF/UA-1 files are PDF 1.0 to 1.7" })]
    [InlineData("1.7", "/Version /2.0",
        new[] { "settings > catalog-version: The catalog says PDF 2.0; PDF/UA-1 files are PDF 1.0 to 1.7" })]
    public void Check_ShouldFindAVersionPastPdf17_WhenTheHeaderOrCatalogSaysPdf20(string header, string catalogEntries,
        string[] expected)
    {
        var pdf = new TestPdf().CatalogEntries(catalogEntries);
        pdf.AddPage().Tagged(0, "Hello");
        pdf.Structure(new TestElement("P", 0));
        var bytes = pdf.Build();
        Encoding.ASCII.GetBytes($"%PDF-{header}").CopyTo(bytes, 0);

        var findings = PdfUaDocument.Load(new MemoryStream(bytes)).Check(PdfCheck.PdfVersion);

        Assert.Equal(expected, findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    [Fact]
    public void Check_ShouldFindNothing_WhenListsAndTablesOfContentsHoldWhatTheyShould()
    {
        var pdf = new TestPdf();
        var page = pdf.AddPage();
        for (var i = 0; i < 8; i++) page.Tagged(i, $"Text {i}");
        pdf.Structure(
            new TestElement("L", new TestElement("Caption", 0),
                new TestElement("LI", new TestElement("Lbl", 1), new TestElement("LBody", 2)),
                new TestElement("L", new TestElement("LI", new TestElement("LBody", 3)))),
            new TestElement("TOC", new TestElement("Caption", 4), new TestElement("TOCI", 5),
                new TestElement("TOC", new TestElement("TOCI", 6))),
            new TestElement("P", 7));

        Assert.Empty(pdf.Load().Check(PdfCheck.ListStructure, PdfCheck.TocStructure));
    }

    [Fact]
    public void Check_ShouldFindMisplacedItemsWrongChildrenAndLateCaptions_WhenListsHaveThem()
    {
        var pdf = new TestPdf();
        var page = pdf.AddPage();
        for (var i = 0; i < 11; i++) page.Tagged(i, $"Text {i}");
        pdf.Structure(
            new TestElement("LI", new TestElement("LBody", 0)),
            new TestElement("L",
                new TestElement("LI", new TestElement("Lbl", 1), new TestElement("P", 2), new TestElement("LBody", 3)),
                new TestElement("Caption", 4), new TestElement("LBody", 5), new TestElement("P", 9)),
            new TestElement("TOC", new TestElement("TOCI", 6), new TestElement("Caption", 7),
                new TestElement("P", 10)),
            new TestElement("TOCI", 8));

        var findings = pdf.Load().Check(PdfCheck.ListStructure, PdfCheck.TocStructure);

        Assert.Equal(
        [
            "structure > LI: LI isn't inside L",
            "structure > L > Caption: Caption on p1 isn't the first child of its L",
            "structure > L > LBody: LBody on p1 isn't inside LI",
            "structure > L > P: P on p1 is in L, which holds only L, LI, Caption",
            "structure > L > LI > P: P on p1 is in LI, which holds only Lbl, LBody",
            "structure > TOCI: TOCI on p1 isn't inside TOC",
            "structure > TOC > Caption: Caption on p1 isn't the first child of its TOC",
            "structure > TOC > P: P on p1 is in TOC, which holds only TOC, TOCI, Caption",
        ], findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    // Each heading is "Sect/Type", the section it's in, or none for the root, and its type; each expected finding is
    // "path: message".
    [Theory]
    [InlineData(new[] { "1/H", "2/H" }, new string[0])]
    [InlineData(new[] { "1/H1", "2/H2" }, new string[0])]
    [InlineData(new[] { "1/H", "2/H2" },
        new[] { "structure > Sect[2] > H2: H2 on p1 is numbered, but the document's first heading is H" })]
    [InlineData(new[] { "1/H1", "2/H" },
        new[] { "structure > Sect[2] > H: H on p1 isn't numbered, but the document's first heading is numbered" })]
    [InlineData(new[] { "1/H", "1/H" },
        new[] { "structure > Sect > H[2]: H on p1 is another H in the same Sect, which can hold only one" })]
    [InlineData(new[] { "1/H1", "2/H", "2/H" },
        new[]
        {
            "structure > Sect[2] > H[1]: H on p1 isn't numbered, but the document's first heading is numbered",
            "structure > Sect[2] > H[2]: H on p1 isn't numbered, but the document's first heading is numbered",
        })]
    [InlineData(new[] { "/H", "/H" },
        new[] { "structure > H[2]: H on p1 is another H in the same structure tree root, which can hold only one" })]
    public void Check_ShouldFindMixedHeadingKindsAndASecondH_WhenTheDocumentHasThem(string[] headings,
        string[] expected)
    {
        var pdf = new TestPdf();
        var page = pdf.AddPage();
        for (var i = 0; i < headings.Length; i++) page.Tagged(i, $"Heading {i}");
        var groups = headings.Select((heading, mcid) => (Parts: heading.Split('/'), Mcid: mcid))
            .GroupBy(heading => heading.Parts[0]);
        foreach (var group in groups)
        {
            var elements = group.Select(heading => new TestElement(heading.Parts[1], heading.Mcid)).ToArray();
            pdf.Structure(group.Key == "" ? elements : [new TestElement("Sect", elements)]);
        }

        var findings = pdf.Load().Check(PdfCheck.HeadingTypes);

        Assert.Equal(expected, findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    [Fact]
    public void Check_ShouldFindNotesWithoutAnIdAndWithAnEarlierNotesId_WhenTheDocumentHasThem()
    {
        var pdf = new TestPdf();
        var page = pdf.AddPage();
        for (var i = 0; i < 5; i++) page.Tagged(i, $"Note {i}");
        pdf.Structure(new TestElement("Note", 0) { Entries = "/ID (n1)" }, new TestElement("Note", 1),
            new TestElement("Note", 2) { Entries = "/ID (n1)" }, new TestElement("Note", 3) { Entries = "/ID (n2)" },
            new TestElement("Note", 4) { Entries = "/ID ()" });

        var findings = pdf.Load().Check(PdfCheck.NoteIds);

        Assert.Equal(
        [
            "structure > Note[2]: Note on p1 has no ID",
            "structure > Note[3]: Note on p1 has the ID n1, which an earlier note has",
            "structure > Note[5]: Note on p1 has no ID",
        ], findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    [Fact]
    public void Check_ShouldFindNothing_WhenTablesHoldWhatTheyShould()
    {
        var pdf = new TestPdf();
        var page = pdf.AddPage();
        for (var i = 0; i < 6; i++) page.Tagged(i, $"Cell {i}");
        pdf.Structure(
            new TestElement("Table", new TestElement("Caption", 0),
                new TestElement("THead", new TestElement("TR", new TestElement("TH", 1))),
                new TestElement("TBody", new TestElement("TR", new TestElement("TD", 2))),
                new TestElement("TFoot", new TestElement("TR", new TestElement("TD", 3)))),
            new TestElement("Table", new TestElement("TR", new TestElement("TD", 4)), new TestElement("Caption", 5)));

        Assert.Empty(pdf.Load().Check(PdfCheck.TableStructure));
    }

    [Fact]
    public void Check_ShouldFindMisplacedRowsCellsAndGroupsAndExtraParts_WhenTablesHaveThem()
    {
        var pdf = new TestPdf();
        var page = pdf.AddPage();
        for (var i = 0; i < 19; i++) page.Tagged(i, $"Cell {i}");
        pdf.Structure(
            new TestElement("TR", new TestElement("TD", 0)),
            new TestElement("Table",
                new TestElement("TR", new TestElement("TD", 1), new TestElement("P", 2)),
                new TestElement("THead", new TestElement("TR", new TestElement("TH", 3)), new TestElement("P", 14)),
                new TestElement("THead", new TestElement("TR", new TestElement("TH", 4))),
                new TestElement("Caption", 5), new TestElement("TFoot", new TestElement("P", 6)),
                new TestElement("TFoot", new TestElement("TR", new TestElement("TD", 13))),
                new TestElement("TD", 7), new TestElement("P", 12)),
            new TestElement("Table",
                new TestElement("TBody", new TestElement("TR", new TestElement("TD", 8)), new TestElement("P", 15)),
                new TestElement("Caption", 9), new TestElement("Caption", 10)),
            new TestElement("THead", new TestElement("TR", new TestElement("TH", 11))),
            new TestElement("TBody", new TestElement("TR", new TestElement("TD", 16))),
            new TestElement("TFoot", new TestElement("TR", new TestElement("TD", 17))),
            new TestElement("TH", 18));

        var findings = pdf.Load().Check(PdfCheck.TableStructure);

        Assert.Equal(
        [
            "structure > TR: TR isn't inside Table, THead, TBody or TFoot",
            "structure > THead: THead isn't inside Table",
            "structure > TBody: TBody isn't inside Table",
            "structure > TFoot: TFoot isn't inside Table",
            "structure > TH: TH on p1 isn't inside TR",
            "structure > Table[1] > Caption: Caption on p1 isn't the first or last child of its Table",
            "structure > Table[1] > TD: TD on p1 isn't inside TR",
            "structure > Table[1] > P: P on p1 is in Table, which holds only TR, THead, TBody, TFoot, Caption",
            "structure > Table[1] > TR > P: P on p1 is in TR, which holds only TH, TD",
            "structure > Table[1] > THead[1] > P: P on p1 is in THead, which holds only TR",
            "structure > Table[1] > TFoot[1] > P: P on p1 is in TFoot, which holds only TR",
            "structure > Table[2] > Caption[1]: Caption on p1 isn't the first or last child of its Table",
            "structure > Table[2] > TBody > P: P on p1 is in TBody, which holds only TR",
            "structure > Table[1] > THead[1]: THead is in a Table with no TBody",
            "structure > Table[1] > THead[2]: THead is another THead in the same Table, which can hold only one",
            "structure > Table[1] > TFoot[1]: TFoot is in a Table with no TBody",
            "structure > Table[1] > TFoot[2]: TFoot is another TFoot in the same Table, which can hold only one",
            "structure > Table[2] > Caption[2]: Caption on p1 is another Caption in the same Table, "
                + "which can hold only one",
        ], findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    private static TestElement Cell(int mcid, int rowSpan = 1, int colSpan = 1) =>
        new("TD", mcid) { Entries = $"/A << /O /Table /RowSpan {rowSpan} /ColSpan {colSpan} >>" };

    [Fact]
    public void Check_ShouldFindNothing_WhenEveryRowCoversTheSameColumnsCountingSpans()
    {
        var pdf = new TestPdf();
        var page = pdf.AddPage();
        for (var i = 0; i < 7; i++) page.Tagged(i, $"Cell {i}");
        pdf.Structure(
            new TestElement("Table", new TestElement("THead", new TestElement("TR", Cell(0, colSpan: 2))),
                new TestElement("TBody", new TestElement("TR", Cell(1), Cell(2)))),
            new TestElement("Table", new TestElement("TR", Cell(3, rowSpan: 2), Cell(4)),
                new TestElement("TR", Cell(5))),
            new TestElement("Table", new TestElement("TR", Cell(6))));

        Assert.Empty(pdf.Load().Check(PdfCheck.TableGrid));
    }

    [Fact]
    public void Check_ShouldFindShortRowsOverlapsAndSpansPastTheLastRow_WhenTablesHaveThem()
    {
        var pdf = new TestPdf();
        var page = pdf.AddPage();
        for (var i = 0; i < 11; i++) page.Tagged(i, $"Cell {i}");
        pdf.Structure(
            new TestElement("Table", new TestElement("TR", Cell(0), Cell(1), Cell(2)),
                new TestElement("TR", Cell(3), Cell(4))),
            new TestElement("Table", new TestElement("TR", Cell(5), Cell(6, rowSpan: 2)),
                new TestElement("TR", Cell(7, colSpan: 2))),
            new TestElement("Table", new TestElement("TR", Cell(8, rowSpan: 3), Cell(9)),
                new TestElement("TR", Cell(10))));

        var findings = pdf.Load().Check(PdfCheck.TableGrid);

        Assert.Equal(
        [
            "structure > Table[1] > TR[2]: TR has 2 column(s), but the first row has 3",
            "structure > Table[2] > TR[2] > TD: TD on p1 overlaps a cell spanning from an earlier row",
            "structure > Table[3] > TR[1] > TD[1]: TD on p1 spans 3 rows, past the table's last row",
        ], findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    // A span of 0 counts as 1, and a huge one is laid out only as far as the table goes, so it can't exhaust memory.
    [Fact]
    public void Check_ShouldReportASpanPastTheLastRowWithoutLayingItOut_WhenASpanIsZeroOrHuge()
    {
        var pdf = new TestPdf();
        var page = pdf.AddPage();
        for (var i = 0; i < 4; i++) page.Tagged(i, $"Cell {i}");
        pdf.Structure(new TestElement("Table",
            new TestElement("TR", Cell(0, rowSpan: int.MaxValue), Cell(1, rowSpan: 0)),
            new TestElement("TR", Cell(2, colSpan: int.MaxValue))));

        var findings = pdf.Load().Check(PdfCheck.TableGrid);

        Assert.Equal(
        [
            $"structure > Table > TR[1] > TD[1]: TD on p1 spans {int.MaxValue} rows, past the table's last row",
            "structure > Table > TR[2]: TR has 1001 column(s), but the first row has 2",
        ], findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    [Fact]
    public void Check_ShouldFindNothing_WhenEachAnnotationIsTaggedWithItsElement()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Trail map");
        pdf.Structure(new TestElement("Link", 0, new TestLink("https://example.com", "Trail map")),
            new TestElement("Form", new TestAnnotation("/Subtype /Widget")),
            new TestElement("Form", new TestAnnotation("/Subtype /Widget"), new TestAnnotation("/Subtype /Widget"))
            {
                Entries = "/A << /O /PrintField /Role /rb >>",
            },
            new TestElement("Annot", new TestAnnotation("/Subtype /Text /Contents (Note)")));

        Assert.Empty(pdf.Load().Check(PdfCheck.AnnotationTags));
    }

    [Fact]
    public void Check_ShouldFindAnnotationsInTheWrongElementAndTrapNets_WhenTheDocumentHasThem()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Trail map").Tagged(1, "Name").Annotation("/Subtype /TrapNet /Rect [0 0 10 10]")
            .Annotation("/Subtype /Link /Rect [0 0 10 10] /Contents (Map)")
            .Annotation("/Subtype /Widget /Rect [0 0 10 10]").Annotation("/Subtype /Text /Rect [0 0 10 10] /F 2");
        pdf.Structure(new TestElement("P", 0, new TestLink("https://example.com", "Trail map")),
            new TestElement("P", new TestAnnotation("/Subtype /Widget")),
            new TestElement("P", new TestAnnotation("/Subtype /Text /Contents (Note)")),
            new TestElement("Form", new TestAnnotation("/Subtype /Widget"), new TestAnnotation("/Subtype /Widget")),
            new TestElement("Form", 1, new TestAnnotation("/Subtype /Widget")),
            new TestElement("Form", new TestAnnotation("/Subtype /Widget"), new TestElement("Lbl")),
            new TestElement("Artifact", new TestAnnotation("/Subtype /PrinterMark")),
            new TestElement("P", new TestAnnotation("/Subtype /TrapNet")));

        var findings = pdf.Load().Check(PdfCheck.AnnotationTags);

        Assert.Equal(
        [
            "structure > P[1]: Link annotation on p1 is tagged with P, not Link (ISO 14289-1 7.18.5)",
            "structure > P[2]: Widget annotation on p1 is tagged with P, not Form (ISO 14289-1 7.18.4)",
            "structure > P[3]: Text annotation on p1 is tagged with P, not Annot (ISO 14289-1 7.18.1)",
            "structure > Form[1]: Form on p1 sets no field role, so it should hold one widget and nothing else "
                + "(ISO 14289-1 7.18.4)",
            "structure > Form[2]: Form on p1 sets no field role, so it should hold one widget and nothing else "
                + "(ISO 14289-1 7.18.4)",
            "structure > Form[3]: Form on p1 sets no field role, so it should hold one widget and nothing else "
                + "(ISO 14289-1 7.18.4)",
            "structure > Artifact: PrinterMark annotation on p1 is tagged, but printer marks are artifacts "
                + "(ISO 14289-1 7.18.8)",
            "annotations: Untagged Link annotation on p1 isn't in Link (ISO 14289-1 7.18.5)",
            "annotations: Untagged Widget annotation on p1 isn't in Form (ISO 14289-1 7.18.4)",
            "annotations: TrapNet annotation on p1, which PDF/UA doesn't allow (ISO 14289-1 7.18.2)",
            "annotations: TrapNet annotation on p1, which PDF/UA doesn't allow (ISO 14289-1 7.18.2)",
        ], findings.Select(finding => $"{finding.Path}: {finding.Message} ({finding.Reference})"));
    }

    [Fact]
    public void Check_ShouldFindAnnotationsWithoutADescription_WhenNeitherTheyNorTheirElementHaveOne()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Annotation("/Subtype /Text /Rect [0 0 10 10]").Annotation("/Subtype /Text /Rect [0 0 10 10] /F 2")
            .Annotation("/Subtype /Stamp /Rect [0 0 10 10] /Contents (Approved)")
            .Annotation("/Subtype /Link /Rect [0 0 10 10]").Annotation("/Subtype /Widget /Rect [0 0 10 10]")
            .Annotation("/Subtype /Popup /Rect [0 0 10 10]").Annotation("/Subtype /PrinterMark /Rect [0 0 10 10]")
            .Annotation("/Subtype /TrapNet /Rect [0 0 10 10]");
        pdf.Structure(new TestElement("Annot", new TestAnnotation("/Subtype /Text")),
            new TestElement("Annot", new TestAnnotation("/Subtype /Text")) { Entries = "/Alt (Comment)" },
            new TestElement("Annot", new TestAnnotation("/Subtype /Text /Contents (Check the map)")));

        var findings = pdf.Load().Check(PdfCheck.AnnotationDescriptions);

        Assert.Equal(
        [
            "structure > Annot[1]: Text annotation on p1 has no description",
            "annotations: Untagged Text annotation on p1 has no description",
        ], findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    [Theory]
    [InlineData("S", false, 0)]
    [InlineData("R", false, 1)]
    [InlineData(null, false, 1)]
    [InlineData("R", true, 0)]
    public void Check_ShouldFindOnlyPagesWithAnnotationsOutOfStructureOrder_WhenGivenEachTabOrder(string? tabs,
        bool withoutAnnotations, int expected)
    {
        var pdf = new TestPdf();
        var page = pdf.AddPage(tabs: tabs);
        if (!withoutAnnotations) page.Annotation("/Subtype /Text /Rect [0 0 10 10] /Contents (Note)");

        var findings = pdf.Load().Check(PdfCheck.TabOrder);

        Assert.Equal(expected, findings.Count);
        Assert.All(findings, finding => Assert.Equal(
            $"pages > tabs: Page 1 has annotations, but its tab order is {tabs ?? "not set"}, not S",
            $"{finding.Path}: {finding.Message}"));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("en-US")]
    [InlineData("zh-Hant-TW")]
    [InlineData("es-419")]
    [InlineData("de-CH-1996")]
    [InlineData("sr-Latn-RS")]
    [InlineData("en-US-x-trail")]
    [InlineData("x-klingon")]
    [InlineData("i-navajo")]
    [InlineData("en-GB-oed")]
    [InlineData("sgn-BE-FR")]
    [InlineData("zh-min-nan")]
    [InlineData("")]
    public void Check_ShouldFindNothing_WhenTheDocumentLanguageIsAWellFormedTagOrEmpty(string language)
    {
        var document = OnePage(new TestPdf { Lang = language });

        Assert.Empty(document.Check(PdfCheck.LanguageTags));
    }

    [Theory]
    [InlineData("english")]
    [InlineData("en_US")]
    [InlineData("en-")]
    [InlineData("en--US")]
    [InlineData("e")]
    [InlineData("abcdefghi")]
    [InlineData("en-a")]
    [InlineData("en-x")]
    [InlineData("i-english")]
    [InlineData("en\n")]
    [InlineData(" ")]
    public void Check_ShouldFindTheDocumentLanguage_WhenGivenAMalformedTag(string language)
    {
        var document = OnePage(new TestPdf { Lang = language });

        var finding = Assert.Single(document.Check(PdfCheck.LanguageTags));

        Assert.Equal("document > lang", finding.Path);
    }

    [Fact]
    public void Check_ShouldFindLanguagesThatArentWellFormedTags_WhenTheDocumentElementsOrMarkedContentSetThem()
    {
        var pdf = new TestPdf { Lang = "english" };
        pdf.AddPage().Tagged(0, "Bonjour")
            .Raw("/P << /MCID 1 >> BDC BT /F1 12 Tf 72 300 Td /Span << /Lang (fr_FR) >> BDC (Salut) Tj EMC ET EMC");
        pdf.Structure(new TestElement("P", 0) { Entries = "/Lang (en_US)" }, new TestElement("P", 1));

        var findings = pdf.Load().Check(PdfCheck.LanguageTags);

        Assert.Equal(
        [
            "document > lang: The document's language \"english\" isn't a valid language tag",
            "structure > P[1]: P on p1 has the language \"en_US\", which isn't a valid language tag",
            "structure > P[2] > span-lang: Marked content in P on p1 has the language \"fr_FR\", which isn't a valid "
                + "language tag",
        ], findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    // Text that has a language: a paragraph in German, a French section's paragraph and link, a paragraph whose text is
    // all in French marked content, and a form field's widget in a Form in English. Text that has none: a paragraph, a
    // figure's alt text, marked content's alt text, a link's contents, an untagged comment, an untagged widget's name,
    // a bookmark and an XMP title for the default language.
    private static TestPdf WithTextInManyPlaces(string? lang, string xmp = XmpTitle)
    {
        var pdf = new TestPdf { Lang = lang, Xmp = xmp };
        pdf.AddPage().Tagged(0, "Hallo").Tagged(1, "Hello").Tagged(2, "Bonjour")
            .Raw("/P << /MCID 3 >> BDC BT /F1 12 Tf 72 300 Td /Span << /Lang (fr-FR) >> BDC (Salut) Tj EMC ET EMC")
            .Raw("/Figure << /MCID 4 /Alt (Ridge view) >> BDC EMC /Figure << /MCID 4 /Alt (Summit) >> BDC EMC")
            .Annotation("/Subtype /Text /Rect [0 0 10 10] /Contents (Note)")
            .Annotation("/Subtype /Widget /Rect [0 0 10 10] /TU (Hiker name)");
        pdf.Structure(new TestElement("P", 0) { Entries = "/Lang (de-DE)" }, new TestElement("P", 1),
            new TestElement("Div", new TestElement("P", 2),
                new TestElement("Link", new TestLink("https://a.example", "Map")))
            {
                Entries = "/Lang (fr-FR)",
            },
            new TestElement("P", 3), new TestElement("Figure") { Entries = "/Alt (Trail map)" },
            new TestElement("Figure", 4), new TestElement("Link", new TestLink("https://example.com", "Trail map")),
            new TestElement("Form", new TestAnnotation("/Subtype /Widget /TU (Emergency phone)"))
            {
                Entries = "/Lang (en-US)",
            });
        pdf.Bookmark("Safety", 1);
        return pdf;
    }

    [Fact]
    public void Check_ShouldFindTextWithNoLanguage_WhenTheDocumentHasNoneAndOnlySomeElementsSetOne()
    {
        var findings = WithTextInManyPlaces(lang: null).Load().Check(PdfCheck.TextLanguage);

        Assert.Equal(
        [
            "structure > P[2]: P on p1 has text with no language",
            "structure > Figure[1]: Figure has text with no language",
            "structure > Figure[2] > span: Marked content in Figure on p1 has alt text, replacement text or an "
                + "expansion with no language",
            "structure > Link > contents: Link annotation on p1 in Link has text with no language",
            "annotations: Untagged Text annotation on p1 has text with no language",
            "annotations: Untagged Widget annotation on p1 has text with no language",
            "bookmarks: Bookmark titles have no language",
            "metadata > xmp: The XMP title or description is for the default language, and the document has none",
        ], findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    // A radio group keeps its name on the field, and its widgets are kids; a text field is its own widget, so its name
    // is checked with the widget. The paragraph's language opens the check.
    [Fact]
    public void Check_ShouldFindAFieldsNameWithNoLanguage_WhenItIsKeptOnTheFieldAndNotOnAWidget()
    {
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [6 0 R 9 0 R] >> /StructTreeRoot 10 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [7 0 R 8 0 R 9 0 R] /Contents 4 0 R >>",
            TestPdf.Stream(""), "<< >>",
            "<< /T (trail) /FT /Btn /Ff 32768 /TU (Trail choice) /Kids [7 0 R 8 0 R] >>",
            "<< /Type /Annot /Subtype /Widget /Parent 6 0 R /Rect [72 300 92 320] >>",
            "<< /Type /Annot /Subtype /Widget /Parent 6 0 R /Rect [72 260 92 280] >>",
            "<< /Type /Annot /Subtype /Widget /T (hiker) /FT /Tx /TU (Hiker name) /Rect [72 200 272 220] >>",
            "<< /Type /StructTreeRoot /K [11 0 R] >>",
            "<< /Type /StructElem /S /P /P 10 0 R /Lang (de-DE) /Alt (Hallo) >>");

        var findings = document.Check(PdfCheck.TextLanguage).Where(finding => finding.Path.StartsWith("forms"));

        Assert.Equal(["forms > trail: Form field \"trail\" has an accessible name with no language"],
            findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    [Fact]
    public void Check_ShouldFindNoXmpTextWithoutALanguage_WhenItsTitleNamesALanguage()
    {
        var xmp = "<dc:title><rdf:Alt><rdf:li xml:lang=\"en-US\">Guide</rdf:li></rdf:Alt></dc:title>";

        var findings = WithTextInManyPlaces(lang: null, xmp).Load().Check(PdfCheck.TextLanguage);

        Assert.DoesNotContain(findings, finding => finding.Path == "metadata > xmp");
    }

    [Fact]
    public void Check_ShouldFindNoTextWithoutALanguage_WhenTheDocumentHasOne()
    {
        Assert.Empty(WithTextInManyPlaces(lang: "en-US").Load().Check(PdfCheck.TextLanguage));
    }

    // DocumentLanguage already reports the one fix.
    [Fact]
    public void Check_ShouldFindNoTextWithoutALanguage_WhenNothingSetsALanguage()
    {
        var document = OnePage(new TestPdf { Lang = null });

        Assert.Empty(document.Check(PdfCheck.TextLanguage));
    }

    [Fact]
    public void Check_ShouldFindTheFormulaWithoutAltText_WhenOnlyOneOfThreeHasNone()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "x = 1").Tagged(1, "y = 2").Tagged(2, "z = 3");
        pdf.Structure(new TestElement("Formula", 0) { Entries = "/Alt (x equals 1)" },
            new TestElement("Formula", 1) { Entries = "/ActualText (y equals 2)" }, new TestElement("Formula", 2));

        var findings = pdf.Load().Check(PdfCheck.FormulaAltText);

        Assert.Equal(["structure > Formula[3]: Formula on p1 has no alt text"],
            findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    private static string Screen(string clip) =>
        $"/Subtype /Screen /Rect [0 0 10 10] /A << /S /Rendition /R << /S /MR /C << /S /MCD {clip} >> >> >>";

    [Fact]
    public void Check_ShouldFindMediaClipsWithoutAContentTypeOrAlternateText_WhenAScreenAnnotationPlaysThem()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Annotation(Screen("/CT (video/mp4) /Alt [(en-US) (Trail tour)]")).Annotation(Screen(""))
            .Annotation("/Subtype /Screen /Rect [0 0 10 10] /A << /S /Rendition /OP 0 >>")
            .Annotation("/Subtype /Screen /Rect [0 0 10 10] /AA << /PO << /S /Rendition /R << /S /SR /R [<< /S /MR "
                + "/C << /S /MCS /D << /S /MCD /CT (video/mp4) >> >> >>] >> >> >>");

        var findings = pdf.Load().Check(PdfCheck.MediaClips);

        Assert.Equal(
        [
            "annotations > media-type: Media clip on p1 has no content type",
            "annotations > media-alt: Media clip on p1 has no alternate text",
            "annotations > media-alt: Media clip on p1 has no alternate text",
        ], findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    [Fact]
    public void Check_ShouldFindArtifactsInsideTaggedContentAndTaggedContentInsideArtifacts_WhenAPageHasThem()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Fine").Artifact("Page 1");
        pdf.AddPage()
            .Raw("/P << /MCID 0 >> BDC BT /F1 12 Tf 72 700 Td (Ridge) Tj ET "
                + "/Artifact BMC BT /F1 12 Tf 72 680 Td (1) Tj ET EMC EMC")
            .Raw("/Artifact BMC /P << /MCID 1 >> BDC BT /F1 12 Tf 72 660 Td (Loop) Tj ET EMC EMC");
        pdf.Structure(new TestElement("P", 0), new TestElement("P", 0) { Page = 2 },
            new TestElement("P", 1) { Page = 2 });

        var findings = pdf.Load().Check(PdfCheck.ArtifactNesting);

        Assert.Equal(
        [
            "pages > artifacts-in-tagged: Page 2 has 1 artifact(s) inside tagged content",
            "pages > artifacts-holding-tagged: Page 2 has 1 artifact(s) holding tagged content",
        ], findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    // Marked content without an MCID, such as a layer or a language span, isn't tagged content, and an artifact that
    // has an MCID of its own holds nothing.
    [Fact]
    public void Check_ShouldFindNoNesting_WhenAnArtifactSitsInMarkedContentWithoutAnMcidOrHasItsOwn()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Ridge")
            .Raw("/OC /L1 BDC /Artifact BMC BT /F1 12 Tf 72 680 Td (Draft) Tj ET EMC EMC")
            .Raw("/Span << /Lang (fr-FR) >> BDC /Artifact BMC BT /F1 12 Tf 72 660 Td (1) Tj ET EMC EMC")
            .Raw("/Artifact << /MCID 3 >> BDC BT /F1 12 Tf 72 640 Td (Footer) Tj ET EMC");
        pdf.Structure(new TestElement("P", 0));

        Assert.Empty(pdf.Load().Check(PdfCheck.ArtifactNesting));
    }

    [Fact]
    public void Check_ShouldFindUnnamedLayerConfigurationsAndOnesThatChangeLayersByThemselves_WhenTheDocumentHasThem()
    {
        var document = OnePage(new TestPdf().CatalogEntries(
            " /OCProperties << /OCGs [] /D << /AS [] >> /Configs [<< /Name (Print) >> << >>] >>"));

        var findings = document.Check(PdfCheck.LayerConfigurations);

        Assert.Equal(
        [
            "settings > layer-config: An optional-content configuration has no name",
            "settings > layer-config > auto-state: Optional-content configuration (unnamed) turns layers on or off by "
                + "itself (AS)",
            "settings > layer-config: An optional-content configuration has no name",
        ], findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    [Fact]
    public void Check_ShouldFindTheEmbeddedFileNamedOneWay_WhenOnlyOneOfTwoHasBothNames()
    {
        var document = OnePage(new TestPdf().CatalogEntries(" /Names << /EmbeddedFiles << /Names [(a.xml) << /Type "
            + "/Filespec /F (a.xml) /UF (a.xml) >> (b.xml) << /Type /Filespec /F (b.xml) >>] >> >>"));

        var findings = document.Check(PdfCheck.EmbeddedFileNames);

        Assert.Equal(["settings > b.xml: Embedded file b.xml isn't named both as F and as UF"],
            findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    [Theory]
    [InlineData(" /NeedsRendering true", 1)]
    [InlineData("", 0)]
    public void Check_ShouldFindADynamicXfaForm_WhenTheCatalogAsksTheViewerToBuildThePages(string entries, int expected)
    {
        var document = OnePage(new TestPdf().CatalogEntries($"{entries} /AcroForm << /Fields [] /XFA [] >>"));

        var findings = document.Check(PdfCheck.NoDynamicXfa);

        Assert.Equal(expected, findings.Count);
        Assert.All(findings, finding => Assert.Equal("forms > xfa: The form is a dynamic XFA form",
            $"{finding.Path}: {finding.Message}"));
    }

    // /O and /U are the standard security handler's hashes (revision 3, 128-bit RC4) for empty passwords, this /ID and
    // each /P: -3388 sets bit 10 (extract for accessibility), and -3900 is the same with it cleared.
    [Theory]
    [InlineData(-3900, "5FD6921A1CFF9657277210A70C29CD1E", 1)]
    [InlineData(-3388, "E2B62D625B527B35FC4A69438DF367F8", 0)]
    public void Check_ShouldFindEncryptionThatStopsAccessibilityExtraction_WhenThePermissionIsntGiven(int permissions,
        string userHash, int expected)
    {
        var id = string.Concat(Enumerable.Repeat("01", 16));
        var document = RawPdf.LoadWithTrailer($"/Encrypt 4 0 R /ID [<{id}> <{id}>]",
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
            $"<< /Filter /Standard /V 2 /R 3 /Length 128 /P {permissions} "
                + "/O <36451BD39D753B7C1D10922C28E6665AA4F3353FB0348B536893E3B1DB5C579B> "
                + $"/U <{userHash}00000000000000000000000000000000> >>");

        var findings = document.Check(PdfCheck.AccessibilityPermission);

        Assert.Equal(expected, findings.Count);
        Assert.All(findings, finding => Assert.Equal(
            "settings > permissions: The encryption doesn't let assistive technology extract the text",
            $"{finding.Path}: {finding.Message}"));
    }

    [Fact]
    public void Check_ShouldFindReferenceXObjects_WhenAPageHasOne()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Map").ReferenceForm();
        pdf.Structure(new TestElement("P", 0));

        var findings = pdf.Load().Check(PdfCheck.NoReferenceXObjects);

        Assert.Equal(["settings > reference-xobjects: The pages' resources hold 1 reference XObject(s)"],
            findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    [Theory]
    [InlineData("IMG_2041.jpg", true)]
    [InlineData("trail-map final.PNG", true)]
    [InlineData("image", true)]
    [InlineData("Graphic 3", true)]
    [InlineData("picture_12", true)]
    [InlineData(" alt text ", true)]
    [InlineData("IMG_2041 (1).jpg", true)]
    [InlineData("Screenshot 2024-01-05 at 10.32.11.png", true)]
    [InlineData("Screenshot 2024-01-05 at 10.32.11\u202FAM.png", true)]
    [InlineData("ridge loop trail map draft two.png", true)]
    [InlineData("ridge loop trail map draft two final.png", false)]
    [InlineData("Scan of the signed contract saved as contract.pdf", false)]
    [InlineData("Trail map of the ridge loop", false)]
    [InlineData("Image of the summit at dawn", false)]
    [InlineData("Logo", false)]
    public void Check_ShouldWarnAboutAltText_WhenItIsAFileNameOrAPlaceholder(string altText, bool expected)
    {
        var pdf = new TestPdf();
        pdf.AddPage().TaggedImage(0);
        var hex = Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(altText));
        pdf.Structure(new TestElement("Figure", 0) { Entries = $"/Alt <FEFF{hex}>" });

        var findings = pdf.Load().Check(PdfCheck.MeaningfulAltText);

        Assert.Equal(expected ? 1 : 0, findings.Count);
        Assert.All(findings, finding => Assert.Equal(
            $"structure > Figure: Figure on p1 has the alt text \"{altText.Trim()}\", which says nothing about it",
            $"{finding.Path}: {finding.Message}"));
    }

    [Fact]
    public void Check_ShouldWarnAboutMarkedContentsAltText_WhenItIsAPlaceholder()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Raw("/Figure << /MCID 0 /Alt (image1.png) >> BDC EMC");
        pdf.Structure(new TestElement("Figure", 0) { Entries = "/Alt (Ridge view)" });

        var findings = pdf.Load().Check(PdfCheck.MeaningfulAltText);

        Assert.Equal(["structure > Figure > span-alt: Marked content in Figure on p1 has the alt text \"image1.png\", "
            + "which says nothing about it"], findings.Select(finding => $"{finding.Path}: {finding.Message}"));
    }

    [Fact]
    public void Check_ShouldFindNoRoleMapProblem_WhenAnElementHasNoType()
    {
        var document = Documents.Empty() with { Structure = [new PdfStructureElement { Type = StructureTypes.None }] };

        Assert.Empty(document.Check(PdfCheck.RoleMap));
    }

    [Fact]
    public void Check_ShouldApplyEveryCheckInTheirOrder_WhenGivenNoneOrGivenThemTwiceOutOfOrder()
    {
        var document = OnePage(new TestPdf { Lang = null });

        var all = document.Check();

        Assert.Equal(document.Check([.. Enum.GetValues<PdfCheck>().Reverse(), PdfCheck.DocumentLanguage]), all);
        Assert.Equal([PdfCheck.DocumentLanguage, PdfCheck.DocumentTitle, PdfCheck.FontsEmbedded, PdfCheck.XmpMetadata],
            all.Select(finding => finding.Check));
    }

    [Fact]
    public void Check_ShouldGiveEachFindingItsRulesCategorySeverityAndReference_WhenRulesFail()
    {
        var document = OnePage(new TestPdf { Lang = null }.CatalogEntries(
            "/Names << /EmbeddedFiles << /Names [(data.xml) << /Type /Filespec /F (data.xml) >>] >> >>"));

        var findings = document.Check(PdfCheck.DocumentLanguage, PdfCheck.NoEmbeddedFiles);

        Assert.Equal(
        [
            (PdfCheckCategory.Language, PdfCheckSeverity.Error, "ISO 14289-1 7.2"),
            (PdfCheckCategory.HouseRules, PdfCheckSeverity.Warning, null),
        ], findings.Select(finding => (finding.Category, finding.Severity, finding.Reference)));
    }

    [Theory]
    [InlineData(PdfCheck.DocumentLanguage, PdfCheckCategory.Language, PdfCheckSeverity.Error, "ISO 14289-1 7.2")]
    [InlineData(PdfCheck.DocumentTitle, PdfCheckCategory.Document, PdfCheckSeverity.Error, "ISO 14289-1 7.1")]
    [InlineData(PdfCheck.FigureAltText, PdfCheckCategory.AltText, PdfCheckSeverity.Error, "ISO 14289-1 7.3")]
    [InlineData(PdfCheck.UntaggedContent, PdfCheckCategory.Content, PdfCheckSeverity.Error, "ISO 14289-1 7.1")]
    [InlineData(PdfCheck.LinkDescriptions, PdfCheckCategory.Annotations, PdfCheckSeverity.Error, "ISO 14289-1 7.18.5")]
    [InlineData(PdfCheck.FormFieldNames, PdfCheckCategory.Forms, PdfCheckSeverity.Error, "ISO 14289-1 7.18.1")]
    [InlineData(PdfCheck.TableHeaders, PdfCheckCategory.Tables, PdfCheckSeverity.Error, "ISO 14289-1 7.5")]
    [InlineData(PdfCheck.HeadingLevels, PdfCheckCategory.Headings, PdfCheckSeverity.Error, "ISO 14289-1 7.4.2")]
    [InlineData(PdfCheck.FontsEmbedded, PdfCheckCategory.Fonts, PdfCheckSeverity.Error, "ISO 14289-1 7.21.4.1")]
    [InlineData(PdfCheck.FontsMapToUnicode, PdfCheckCategory.Fonts, PdfCheckSeverity.Error, "ISO 14289-1 7.21.7")]
    [InlineData(PdfCheck.NoActions, PdfCheckCategory.HouseRules, PdfCheckSeverity.Warning, null)]
    [InlineData(PdfCheck.NoEmbeddedFiles, PdfCheckCategory.HouseRules, PdfCheckSeverity.Warning, null)]
    [InlineData(PdfCheck.RoleMap, PdfCheckCategory.Structure, PdfCheckSeverity.Error, "ISO 14289-1 7.1")]
    [InlineData(PdfCheck.XmpMetadata, PdfCheckCategory.Document, PdfCheckSeverity.Error, "ISO 14289-1 5")]
    [InlineData(PdfCheck.TaggedPdf, PdfCheckCategory.Structure, PdfCheckSeverity.Error, "ISO 14289-1 7.1")]
    [InlineData(PdfCheck.PdfVersion, PdfCheckCategory.Document, PdfCheckSeverity.Error, "ISO 14289-1 6.1")]
    [InlineData(PdfCheck.ListStructure, PdfCheckCategory.Lists, PdfCheckSeverity.Error, "ISO 14289-1 7.2")]
    [InlineData(PdfCheck.TocStructure, PdfCheckCategory.Lists, PdfCheckSeverity.Error, "ISO 14289-1 7.2")]
    [InlineData(PdfCheck.HeadingTypes, PdfCheckCategory.Headings, PdfCheckSeverity.Error, "ISO 14289-1 7.4.4")]
    [InlineData(PdfCheck.NoteIds, PdfCheckCategory.Structure, PdfCheckSeverity.Error, "ISO 14289-1 7.9")]
    [InlineData(PdfCheck.TableStructure, PdfCheckCategory.Tables, PdfCheckSeverity.Error, "ISO 14289-1 7.2")]
    [InlineData(PdfCheck.TableGrid, PdfCheckCategory.Tables, PdfCheckSeverity.Error, "ISO 14289-1 7.2")]
    [InlineData(PdfCheck.AnnotationTags, PdfCheckCategory.Annotations, PdfCheckSeverity.Error, "ISO 14289-1 7.18")]
    [InlineData(PdfCheck.AnnotationDescriptions, PdfCheckCategory.Annotations, PdfCheckSeverity.Error,
        "ISO 14289-1 7.18.1")]
    [InlineData(PdfCheck.TabOrder, PdfCheckCategory.Annotations, PdfCheckSeverity.Error, "ISO 14289-1 7.18.3")]
    [InlineData(PdfCheck.LanguageTags, PdfCheckCategory.Language, PdfCheckSeverity.Error, "ISO 14289-1 7.2")]
    [InlineData(PdfCheck.TextLanguage, PdfCheckCategory.Language, PdfCheckSeverity.Error, "ISO 14289-1 7.2")]
    [InlineData(PdfCheck.FormulaAltText, PdfCheckCategory.AltText, PdfCheckSeverity.Error, "ISO 14289-1 7.7")]
    [InlineData(PdfCheck.MediaClips, PdfCheckCategory.AltText, PdfCheckSeverity.Error, "ISO 14289-1 7.18.6.2")]
    [InlineData(PdfCheck.ArtifactNesting, PdfCheckCategory.Content, PdfCheckSeverity.Error, "ISO 14289-1 7.1")]
    [InlineData(PdfCheck.LayerConfigurations, PdfCheckCategory.Document, PdfCheckSeverity.Error, "ISO 14289-1 7.10")]
    [InlineData(PdfCheck.EmbeddedFileNames, PdfCheckCategory.Document, PdfCheckSeverity.Error, "ISO 14289-1 7.11")]
    [InlineData(PdfCheck.NoDynamicXfa, PdfCheckCategory.Forms, PdfCheckSeverity.Error, "ISO 14289-1 7.15")]
    [InlineData(PdfCheck.AccessibilityPermission, PdfCheckCategory.Document, PdfCheckSeverity.Error,
        "ISO 14289-1 7.16")]
    [InlineData(PdfCheck.NoReferenceXObjects, PdfCheckCategory.Content, PdfCheckSeverity.Error, "ISO 14289-1 7.20")]
    [InlineData(PdfCheck.MeaningfulAltText, PdfCheckCategory.AltText, PdfCheckSeverity.Warning, "WCAG 2.2 1.1.1")]
    [InlineData(PdfCheck.LinkText, PdfCheckCategory.Annotations, PdfCheckSeverity.Warning, "WCAG 2.2 2.4.4")]
    [InlineData(PdfCheck.NoMissingGlyphs, PdfCheckCategory.Fonts, PdfCheckSeverity.Error, "ISO 14289-1 7.21.8")]
    public void RuleOf_ShouldGiveTheRulesCategorySeverityAndClause_WhenGivenEachRule(PdfCheck check,
        PdfCheckCategory category, PdfCheckSeverity severity, string? reference)
    {
        var rule = PdfChecker.RuleOf(check);

        Assert.Equal(new PdfCheckRule(category, severity, reference), rule);
    }

    [Fact]
    public void RuleOf_ShouldKnowEveryRule_WhenGivenAnyCheck()
    {
        Assert.All(Enum.GetValues<PdfCheck>(), check => PdfChecker.RuleOf(check));
    }

    [Fact]
    public void Check_ShouldApplyOnlyTheSetsRules_WhenGivenASet()
    {
        var document = OnePage(new TestPdf { Lang = null, Title = null });

        var findings = document.Check(PdfCheckSet.Latest.Only(PdfCheckCategory.Language));

        Assert.Equal([PdfCheck.DocumentLanguage], findings.Select(finding => finding.Check));
    }

    [Fact]
    public void HelpUrl_ShouldLinkToTheRulesEntryOnTheDocsSite_WhenGivenAFinding()
    {
        var finding = Assert.Single(OnePage(new TestPdf { Lang = null }).Check(PdfCheck.DocumentLanguage));

        Assert.Equal("https://zacharydf7.github.io/pdiffy/reference/checks.html#documentlanguage", finding.HelpUrl);
    }

    // Each rule's help link points at a heading of its own name on the check rules page, and the entry's category,
    // severity and clause must match the rule's.
    [Fact]
    public void ChecksPage_ShouldHaveAnEntryMatchingEveryRule_WhenCalled()
    {
        var page = File.ReadAllText(Path.Combine(DocsSampleTests.DocsFolder(), "reference", "checks.md"))
            .ReplaceLineEndings("\n");

        Assert.All(Enum.GetValues<PdfCheck>(), check =>
        {
            var (category, severity, reference) = PdfChecker.RuleOf(check);
            var start = page.IndexOf($"\n## {check}\n", StringComparison.Ordinal);
            Assert.True(start >= 0, $"No entry for {check}");
            var entry = page[start..page.IndexOf("**Why", start, StringComparison.Ordinal)].Replace('\n', ' ');
            Assert.Contains($"**Category:** `{category}`", entry);
            Assert.Contains($"**Severity:** `{severity}`", entry);
            Assert.Contains($"**From:** {reference ?? "a house rule"}", entry);
        });
    }

    private static PdfUaDocument OnePage(TestPdf pdf)
    {
        pdf.AddPage().Tagged(0, "Hello");
        pdf.Structure(new TestElement("P", 0));
        return pdf.Load();
    }
}
