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
        Assert.Equal([PdfCheck.DocumentLanguage, PdfCheck.DocumentTitle, PdfCheck.FontsEmbedded],
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
