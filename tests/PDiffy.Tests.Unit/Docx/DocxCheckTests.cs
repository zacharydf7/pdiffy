namespace PDiffy.Tests.Unit.Docx;

public class DocxCheckTests
{
    private const string Decorative = """
        <a:extLst xmlns:adec="http://schemas.microsoft.com/office/drawing/2017/decorative"><a:ext><adec:decorative val="1"/></a:ext></a:extLst>
        """;

    [Theory]
    [InlineData("", "", true)]
    [InlineData("descr=\"\"", "", true)]
    [InlineData("descr=\"Trail map\"", "", false)]
    [InlineData("", Decorative, false)]
    public void Check_ShouldFindTheImageOnly_WhenItHasNoAltTextAndIsntDecorative(string attributes, string extensions,
        bool found)
    {
        var docx = new TestDocx { Body = ImageParagraph(attributes, extensions) };

        var findings = docx.Load().Check(DocxCheck.ImageAltText);

        Assert.Equal(found, findings.Count == 1);
    }

    [Fact]
    public void Check_ShouldDescribeTheFinding_WhenAnImageHasNoAltText()
    {
        var docx = new TestDocx { Body = ImageParagraph("", "") };

        var finding = Assert.Single(docx.Load().Check());

        Assert.Equal(DocxCheck.ImageAltText, finding.Check);
        Assert.Equal(DocxCheckCategory.AltText, finding.Category);
        Assert.Equal(CheckSeverity.Error, finding.Severity);
        Assert.Equal("WCAG 2.2 1.1.1", finding.Reference);
        Assert.Equal("body > P > image", finding.Path);
        Assert.Equal("ImageAltText: Image has no alt text (body > P > image)", finding.ToString());
        Assert.Equal("https://zacharydf7.github.io/pdiffy/reference/checks.html#word-imagealttext", finding.HelpUrl);
    }

    // A shape's text is what a screen reader reads, but an empty text box says nothing, and a picture grouped with a
    // caption still needs its own description.
    [Theory]
    [InlineData("", "<w:p><w:r><w:t>Ridge loop</w:t></w:r></w:p>", null)]
    [InlineData("", "<w:p/>", "Shape has no alt text")]
    [InlineData("<a:blip r:embed=\"map\"/>", "<w:p><w:r><w:t>Ridge loop</w:t></w:r></w:p>", "Image has no alt text")]
    public void Check_ShouldLeaveOutAShapeOnly_WhenItsTextBoxHasText(string picture, string text, string? message)
    {
        var docx = new TestDocx
        {
            Body = $"""
                <w:p><w:r><w:drawing xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                    xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                    xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape">
                  <wp:anchor><wp:extent cx="1270000" cy="635000"/><wp:docPr id="1" name="Text Box 1"/>
                    <a:graphic><a:graphicData uri="http://schemas.microsoft.com/office/word/2010/wordprocessingShape">
                      {picture}<wps:wsp><wps:txbx><w:txbxContent>{text}</w:txbxContent></wps:txbx>
                      </wps:wsp></a:graphicData></a:graphic></wp:anchor></w:drawing></w:r></w:p>
                """,
        };

        var findings = docx.Load().Check();

        Assert.Equal(message, findings.SingleOrDefault()?.Message);
    }

    // An ActiveX control is read by its own label, so only the embedded object needs alt text.
    [Theory]
    [InlineData("""<o:OLEObject ProgID="Excel.Sheet.12" r:id="sheet"/>""", "Object has no alt text")]
    [InlineData("""<w:control r:id="box" w:name="CheckBox1"/>""", null)]
    public void Check_ShouldFindAnObjectButNotAControl_WhenNeitherHasAltText(string item, string? message)
    {
        var docx = new TestDocx
        {
            Body = $"""
                <w:p><w:r><w:object xmlns:v="urn:schemas-microsoft-com:vml" xmlns:o="urn:schemas-microsoft-com:office:office">
                  <v:shape style="width:144pt;height:72pt"/>{item}</w:object></w:r></w:p>
                """,
        };

        var findings = docx.Load().Check();

        Assert.Equal(message, findings.SingleOrDefault()?.Message);
    }

    [Fact]
    public void Check_ShouldNameTheKind_WhenAChartHasNoAltText()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:p><w:r><w:drawing xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                    xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
                  <wp:inline><wp:extent cx="1524000" cy="508000"/><wp:docPr id="1" name="Chart 1"/>
                    <a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/chart"/></a:graphic>
                  </wp:inline></w:drawing></w:r></w:p>
                """,
        };

        var finding = Assert.Single(docx.Load().Check());

        Assert.Equal("Chart has no alt text", finding.Message);
    }

    [Fact]
    public void Check_ShouldGiveThePathADiffShows_WhenTheImageIsDeepInATable()
    {
        var cell = $"<w:tc><w:p><w:r><w:t>Trail</w:t></w:r></w:p>{ImageParagraph("", "")}</w:tc>";
        var table = $"<w:tbl><w:tblGrid><w:gridCol w:w=\"2000\"/></w:tblGrid><w:tr><w:tc><w:p/></w:tc></w:tr><w:tr>{cell}</w:tr></w:tbl>";
        var without = new TestDocx { Body = table.Replace(ImageParagraph("", ""), "<w:p/>") }.Load();
        var with = new TestDocx { Body = table }.Load();

        var finding = Assert.Single(with.Check());

        Assert.Equal("body > Table > Row[2] > Cell > P[2] > image", finding.Path);
        Assert.Contains(without.CompareTo(with).Changes, change => change.Path == finding.Path);
    }

    [Fact]
    public void Check_ShouldFindTheImage_WhenItIsInAHeader()
    {
        var docx = new TestDocx { Body = """<w:p/><w:sectPr><w:headerReference w:type="default" r:id="running"/></w:sectPr>""" }
            .WithHeader("running", ImageParagraph("", ""));

        var finding = Assert.Single(docx.Load().Check());

        Assert.Equal("headers > s1 > P > image", finding.Path);
    }

    [Fact]
    public void Check_ShouldApplyOnlyTheSetsRules_WhenGivenASet()
    {
        var docx = new TestDocx { Body = ImageParagraph("", "") }.Load();

        Assert.Single(docx.Check(DocxCheckSet.Latest));
        Assert.Empty(docx.Check(DocxCheckSet.Latest.Except(DocxCheck.ImageAltText)));
    }

    [Fact]
    public void ToSnapshot_ShouldRecordEachFindingByRuleAndPath_WhenAskedToCheck()
    {
        var docx = new TestDocx { Body = ImageParagraph("", "") }.Load();

        var snapshot = docx.ToSnapshot(options => options.Check(DocxCheckSet.Latest));

        Assert.Contains("# checks: latest, 1 rule\n", snapshot);
        Assert.EndsWith("checks\n  ImageAltText body > P > image\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldPrintNone_WhenTheChecksFindNothing()
    {
        var docx = new TestDocx { Body = ImageParagraph("descr=\"Trail map\"", "") }.Load();

        var snapshot = docx.ToSnapshot(options => options.Check(DocxCheckSet.Latest));

        Assert.EndsWith("checks\n  (none)\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldStillCheckTheWholeDocument_WhenTheBodyIsIgnored()
    {
        var docx = new TestDocx { Body = ImageParagraph("", "") }.Load();

        var snapshot = docx.ToSnapshot(options => options.Check(DocxCheckSet.Latest).Ignore(DocxSection.Body));

        Assert.EndsWith("checks\n  ImageAltText body > P > image\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldLeaveOutTheChecks_WhenNotAskedTo()
    {
        var snapshot = new TestDocx { Body = ImageParagraph("", "") }.Load().ToSnapshot();

        Assert.DoesNotContain("checks", snapshot);
    }

    // An upgrading user who opts in should see only the new header line and the new section.
    [Fact]
    public void Compare_ShouldReportOnlyTheHeaderLineAndTheNewSection_WhenATestStartsCheckingTheDocument()
    {
        var docx = new TestDocx { Body = ImageParagraph("", "") }.Load();

        var diff = SnapshotDiff.CompareSnapshots(docx.ToSnapshot(),
            docx.ToSnapshot(options => options.Check(DocxCheckSet.Latest)));

        Assert.Equal(["header", "checks"], diff.Changes.Select(change => change.SectionName));
        Assert.Equal("+ # checks: latest, 1 rule", diff.Changes[0].Detail);
        Assert.Equal(ChangeKind.Added, diff.Changes[1].Kind);
    }

    [Fact]
    public void Compare_ShouldReportANewProblem_WhenAnImageLosesItsAltText()
    {
        var before = new TestDocx { Body = ImageParagraph("descr=\"Trail map\"", "") }.Load();
        var after = new TestDocx { Body = ImageParagraph("", "") }.Load();

        var diff = before.CompareTo(after, options => options.Check(DocxCheckSet.Latest));

        var finding = Assert.Single(diff.Changes, change => change.DocxSection == DocxSection.Checks);
        Assert.Equal(ChangeKind.Added, finding.Kind);
        Assert.Equal("ImageAltText", finding.Path);
        Assert.Equal("body > P > image", finding.Detail);
        Assert.Null(finding.Page);
        Assert.Contains("1 new problem", diff.Describe());
    }

    [Fact]
    public void ToHtml_ShouldLinkAWordFindingToItsRulesEntry_WhenTheChecksChange()
    {
        var header = "# docx-snapshot v1\n# checks: latest, 1 rule\nchecks\n";

        var html = SnapshotDiff.CompareSnapshots(header + "  (none)\n", header + "  ImageAltText body > P > image\n")
            .ToHtml();

        Assert.Contains("<b>New problem</b> <a href=\"#check1\">ImageAltText</a> <code>body &gt; P &gt; image</code> "
            + "<a href=\"https://zacharydf7.github.io/pdiffy/reference/checks.html#word-imagealttext\">How to fix it</a>",
            html);
    }

    [Fact]
    public void Latest_ShouldHaveEveryRule_WhenCalled()
    {
        Assert.Equal(Enum.GetValues<DocxCheck>(), DocxCheckSet.Latest.Checks);
        Assert.Equal("latest, 1 rule", DocxCheckSet.Latest.ToString());
    }

    [Fact]
    public void Only_ShouldKeepTheRulesInTheCategories_WhenGivenSome()
    {
        var set = DocxCheckSet.Latest.Only(DocxCheckCategory.AltText);

        Assert.Equal([DocxCheck.ImageAltText], set.Checks);
        Assert.Equal("latest", set.Name);
    }

    private static string ImageParagraph(string attributes, string extensions) => $"""
        <w:p><w:r><w:drawing xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
            xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
          <wp:inline><wp:extent cx="1524000" cy="508000"/><wp:docPr id="1" name="Picture 1" {attributes}>{extensions}</wp:docPr>
            <a:graphic><a:graphicData><a:blip r:embed="map"/></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>
        """;
}
