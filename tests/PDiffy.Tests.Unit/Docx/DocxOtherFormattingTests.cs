using System.Text.RegularExpressions;
using PDiffy.Diffing;

namespace PDiffy.Tests.Unit.Docx;

public class DocxOtherFormattingTests
{
    private const string W14 = "xmlns:w14=\"http://schemas.microsoft.com/office/word/2010/wordml\"";

    // Character spacing isn't read; proofing marks, the complex-script size and Word 2010's ligatures are noise.
    [Theory]
    [InlineData("<w:spacing w:val=\"20\"/>", true)]
    [InlineData("<w:noProof/><w:szCs w:val=\"24\"/>", false)]
    [InlineData($"<w14:ligatures {W14} w14:val=\"standard\"/>", false)]
    public void ToSnapshot_ShouldPrintASpansOtherFormatting_WhenARunHasFormattingPDiffyDoesntRead(string format,
        bool printed)
    {
        var docx = new TestDocx { Body = Paragraph(format) };

        var snapshot = docx.Load().ToSnapshot();

        Assert.Equal(printed, snapshot.Contains("    span \"loop\" other="));
    }

    [Fact]
    public void ToSnapshot_ShouldChangeTheHash_WhenTheUnreadFormattingChanges()
    {
        var narrow = new TestDocx { Body = Paragraph("<w:spacing w:val=\"20\"/>") }.Load();
        var wide = new TestDocx { Body = Paragraph("<w:spacing w:val=\"40\"/>") }.Load();

        var change = Assert.Single(narrow.CompareTo(wide).Changes);

        Assert.Equal(ChangeKind.AttributeChanged, change.Kind);
        Assert.StartsWith("other: ", change.Detail);
    }

    // A paragraph mark's own formatting (w:rPr) and Word's conditional-format marker (w:cnfStyle) are noise.
    [Theory]
    [InlineData("<w:widowControl/>", true)]
    [InlineData("<w:rPr><w:b/></w:rPr><w:cnfStyle w:val=\"100000000000\"/>", false)]
    public void ToSnapshot_ShouldPrintAParagraphsOtherFormatting_WhenItsPropertiesHaveSomePDiffyDoesntRead(
        string properties, bool printed)
    {
        var docx = new TestDocx { Body = $"<w:p><w:pPr>{properties}</w:pPr><w:r><w:t>Trail notes</w:t></w:r></w:p>" };

        var snapshot = docx.Load().ToSnapshot();

        Assert.Equal(printed, Regex.IsMatch(snapshot, "  P other=[0-9a-f]{16} \"Trail notes\""));
    }

    [Fact]
    public void ToSnapshot_ShouldPrintTheOtherFormatting_WhenAStyleTableRowCellOrSectionHasSomePDiffyDoesntRead()
    {
        var docx = new TestDocx
        {
            Styles = """
                <w:style w:type="paragraph" w:styleId="Trail"><w:name w:val="Trail"/>
                  <w:rPr><w:kern w:val="28"/></w:rPr></w:style>
                """,
            Body = """
                <w:p><w:pPr><w:pStyle w:val="Trail"/></w:pPr><w:r><w:t>Ridge</w:t></w:r></w:p>
                <w:tbl><w:tblPr><w:tblInd w:w="200" w:type="dxa"/></w:tblPr>
                  <w:tblGrid><w:gridCol w:w="2000"/></w:tblGrid>
                  <w:tr><w:trPr><w:jc w:val="center"/></w:trPr>
                    <w:tc><w:tcPr><w:noWrap/></w:tcPr><w:p><w:r><w:t>Loop</w:t></w:r></w:p></w:tc></w:tr></w:tbl>
                <w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgBorders><w:top w:val="single" w:sz="4"/></w:pgBorders>
                  <w:docGrid w:linePitch="360"/></w:sectPr>
                """,
        };

        var lines = docx.Load().ToSnapshot().Split('\n');

        foreach (var start in new[] { "  Trail paragraph", "  Table", "    Row", "      Cell", "  s1" })
        {
            Assert.Contains(lines, line => line.StartsWith(start, StringComparison.Ordinal) && line.Contains(" other="));
        }
    }

    // The document grid (w:docGrid), which Word writes into every section, is noise.
    [Fact]
    public void ToSnapshot_ShouldPrintNoOtherFormatting_WhenASectionHasOnlyADocumentGrid()
    {
        var docx = new TestDocx
        {
            Body = """<w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:docGrid w:linePitch="360"/></w:sectPr>""",
        };

        Assert.DoesNotContain("other=", docx.Load().ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldLeaveOutTheOtherFormatting_WhenTheContentProfileIsUsed()
    {
        var docx = new TestDocx { Body = Paragraph("<w:spacing w:val=\"20\"/>") };

        var snapshot = docx.Load().ToSnapshot(options => options.Use(DocxProfile.Content));

        Assert.DoesNotContain("other=", snapshot);
    }

    // PDiffy reads none of a table style's own table, row, cell or paragraph properties, or its conditional formatting.
    [Theory]
    [InlineData("<w:tblPr><w:tblBorders><w:top w:val=\"single\"/></w:tblBorders></w:tblPr>",
        "<w:tblPr><w:tblBorders><w:top w:val=\"double\"/></w:tblBorders></w:tblPr>")]
    [InlineData("<w:pPr><w:spacing w:after=\"0\"/></w:pPr>", "<w:pPr><w:spacing w:after=\"120\"/></w:pPr>")]
    [InlineData("<w:tcPr><w:vAlign w:val=\"top\"/></w:tcPr>", "<w:tcPr><w:vAlign w:val=\"center\"/></w:tcPr>")]
    [InlineData("<w:tblStylePr w:type=\"firstRow\"><w:rPr><w:b/></w:rPr></w:tblStylePr>",
        "<w:tblStylePr w:type=\"lastRow\"><w:rPr><w:b/></w:rPr></w:tblStylePr>")]
    public void Compare_ShouldReportTheStylesOtherFormatting_WhenATableStyleChanges(string before, string after)
    {
        var earlier = TableStyled(before).Load();
        var later = TableStyled(after).Load();

        var change = Assert.Single(earlier.CompareTo(later).Changes);

        Assert.Equal("styles > TableGrid", change.Path);
        Assert.StartsWith("other: ", change.Detail);
    }

    [Fact]
    public void ToSnapshot_ShouldPrintARowsOtherFormatting_WhenItHasTableExceptions()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:tbl><w:tblGrid><w:gridCol w:w="2000"/></w:tblGrid>
                  <w:tr><w:tblPrEx><w:tblBorders><w:top w:val="double"/></w:tblBorders></w:tblPrEx>
                    <w:tc><w:p><w:r><w:t>Loop</w:t></w:r></w:p></w:tc></w:tr></w:tbl>
                """,
        };

        var snapshot = docx.Load().ToSnapshot();

        Assert.Matches("    Row other=[0-9a-f]{16}\n", snapshot);
    }

    private static TestDocx TableStyled(string properties) => new()
    {
        Styles = $"""<w:style w:type="table" w:styleId="TableGrid"><w:name w:val="Table Grid"/>{properties}</w:style>""",
        Body = """
            <w:tbl><w:tblPr><w:tblStyle w:val="TableGrid"/></w:tblPr><w:tblGrid><w:gridCol w:w="2000"/></w:tblGrid>
              <w:tr><w:tc><w:p><w:r><w:t>Loop</w:t></w:r></w:p></w:tc></w:tr></w:tbl>
            """,
    };

    private static string Paragraph(string format) =>
        $"""<w:p><w:r><w:t xml:space="preserve">Ridge </w:t></w:r><w:r><w:rPr>{format}</w:rPr><w:t>loop</w:t></w:r>"""
        + "</w:p>";
}
