namespace PDiffy.Tests.Unit.Docx;

public class DocxTableTests
{
    [Fact]
    public void ToSnapshot_ShouldDescribeTheTablesGridHeaderRowsAndCells_WhenATableHasThem()
    {
        var docx = new TestDocx
        {
            Styles = """<w:style w:type="table" w:styleId="TableGrid"><w:name w:val="Table Grid"/></w:style>""",
            Body = """
                <w:tbl><w:tblPr><w:tblStyle w:val="TableGrid"/></w:tblPr><w:tblGrid><w:gridCol w:w="2880"/><w:gridCol w:w="4320"/></w:tblGrid>
                  <w:tr><w:trPr><w:tblHeader/></w:trPr>
                    <w:tc><w:tcPr><w:gridSpan w:val="2"/><w:shd w:val="clear" w:fill="d9d9d9"/></w:tcPr><w:p><w:r><w:t>Drugs</w:t></w:r></w:p></w:tc></w:tr>
                  <w:tr><w:tc><w:tcPr><w:vMerge w:val="restart"/></w:tcPr><w:p><w:r><w:t>Warfarin</w:t></w:r></w:p></w:tc>
                    <w:tc><w:p><w:r><w:t>Food</w:t></w:r></w:p></w:tc></w:tr>
                  <w:tr><w:tc><w:tcPr><w:vMerge/></w:tcPr><w:p/></w:tc><w:tc><w:p><w:r><w:t>Alcohol</w:t></w:r></w:p></w:tc></w:tr></w:tbl>
                """,
        };

        var snapshot = docx.Load().ToSnapshot();

        Assert.Contains("""
              Table style=TableGrid widths=144,216
                Row header
                  Cell span=2 shading=D9D9D9
                    P "Drugs"
                Row
                  Cell merge=restart
                    P "Warfarin"
                  Cell
                    P "Food"
                Row
                  Cell merge=continue
                    P
            """.ReplaceLineEndings("\n"), snapshot);
        Assert.Contains("  TableGrid table \"Table Grid\"\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldRecordWidthsMarginsAndBorders_WhenATableAndItsCellsSetThem()
    {
        var body = """
            <w:tbl><w:tblPr><w:tblW w:w="2500" w:type="pct"/>
                <w:tblBorders><w:top w:val="single" w:sz="4" w:color="000000"/><w:insideH w:val="dashed" w:sz="12" w:color="auto"/></w:tblBorders>
                <w:tblCellMar><w:start w:w="108"/><w:end w:w="108"/></w:tblCellMar></w:tblPr>
              <w:tr><w:tc><w:tcPr><w:tcW w:w="2880" w:type="dxa"/><w:tcMar><w:top w:w="0"/></w:tcMar>
                <w:tcBorders><w:bottom w:val="double" w:sz="6"/></w:tcBorders></w:tcPr><w:p/></w:tc>
                <w:tc><w:tcPr><w:tcW w:w="0" w:type="auto"/></w:tcPr><w:p/></w:tc></w:tr></w:tbl>
            """;

        var snapshot = new TestDocx { Body = body }.Load().ToSnapshot();

        Assert.Contains("""
              Table width=50% cell-margins=left:5.4,right:5.4 borders=top:single:0.5:000000,insideH:dashed:1.5
                Row
                  Cell width=144 margins=top:0 borders=bottom:double:0.8
                    P
                  Cell width=auto
            """.ReplaceLineEndings("\n"), snapshot);
    }

    [Theory]
    [InlineData("w:w=\"2500\" w:type=\"pct\"", "50%")]
    [InlineData("w:w=\"50%\" w:type=\"pct\"", "50%")]
    [InlineData("w:w=\"0\" w:type=\"nil\"", "0")]
    [InlineData("w:w=\"0\" w:type=\"auto\"", "auto")]
    [InlineData("w:w=\"2880\" w:type=\"dxa\"", "144")]
    [InlineData("w:w=\"2880\"", "144")]
    public void Load_ShouldReadTheTablesWidth_WhenGivenEachKindOfWidth(string attributes, string expected)
    {
        var body = $"<w:tbl><w:tblPr><w:tblW {attributes}/></w:tblPr><w:tr><w:tc><w:p/></w:tc></w:tr></w:tbl>";

        var table = Assert.IsType<DocxTable>(Assert.Single(new TestDocx { Body = body }.Load().Body));

        Assert.Equal(expected, table.Width);
    }

    [Fact]
    public void Load_ShouldRecordTrackedChangesToRows_WhenRowsAreInsertedDeletedOrReformatted()
    {
        var body = """
            <w:tbl><w:tr><w:trPr><w:ins w:id="1" w:author="Ana"/></w:trPr><w:tc><w:p/></w:tc></w:tr>
              <w:tr><w:trPr><w:del w:id="2" w:author="Bo"/><w:trPrChange w:id="3" w:author="Cy"><w:trPr/></w:trPrChange></w:trPr>
                <w:tc><w:p/></w:tc></w:tr></w:tbl>
            """;

        var document = new TestDocx { Body = body }.Load();

        Assert.Equal([("row-insert", "Ana"), ("row-delete", "Bo"), ("row-format", "Cy")],
            document.Revisions().Select(revision => (revision.Kind, revision.Author)));
        Assert.Contains("    Row\n      row-insert author=\"Ana\"\n      Cell\n", document.ToSnapshot());
    }

    [Fact]
    public void Load_ShouldRecordTrackedChangesToTheTableAndItsCells_WhenTheirPropertiesChangeWithTrackingOn()
    {
        var body = """
            <w:tbl><w:tblPr><w:tblPrChange w:id="1" w:author="Ana"><w:tblPr/></w:tblPrChange></w:tblPr>
              <w:tblGrid><w:gridCol w:w="2880"/><w:tblGridChange w:id="2" w:author="Bo"><w:tblGrid/></w:tblGridChange></w:tblGrid>
              <w:tr><w:tblPrEx><w:tblPrExChange w:id="3" w:author="Cy"><w:tblPrEx/></w:tblPrExChange></w:tblPrEx>
                <w:tc><w:tcPr><w:cellIns w:id="4" w:author="Di"/><w:tcPrChange w:id="5" w:author="Ed"><w:tcPr/></w:tcPrChange></w:tcPr>
                  <w:p/></w:tc>
                <w:tc><w:tcPr><w:cellDel w:id="6" w:author="Fay"/></w:tcPr><w:p/></w:tc>
                <w:tc><w:tcPr><w:cellMerge w:id="7" w:author="Gus" w:vMerge="cont"/></w:tcPr><w:p/></w:tc></w:tr></w:tbl>
            """;

        var document = new TestDocx { Body = body }.Load();

        (string, string?)[] expected =
        [
            ("table-format", "Ana"), ("table-grid", "Bo"), ("row-table-format", "Cy"), ("cell-insert", "Di"),
            ("cell-format", "Ed"), ("cell-delete", "Fay"), ("cell-merge", "Gus"),
        ];
        Assert.Equal(expected, document.Revisions().Select(revision => (revision.Kind, revision.Author)));
        Assert.Contains("  Table widths=144\n    table-format author=\"Ana\"\n    table-grid author=\"Bo\"\n    Row\n"
            + "      row-table-format author=\"Cy\"\n      Cell\n        cell-insert author=\"Di\"\n"
            + "        cell-format author=\"Ed\"\n        P\n", document.ToSnapshot());
    }

    [Fact]
    public void Revisions_ShouldListChangesInReadingOrder_WhenAnEarlierCellsTextChangedAndALaterCellWasDeleted()
    {
        var body = """
            <w:tbl><w:tr><w:tc><w:p><w:ins w:id="1" w:author="Ana"><w:r><w:t>New</w:t></w:r></w:ins></w:p></w:tc>
              <w:tc><w:tcPr><w:cellDel w:id="2" w:author="Bo"/></w:tcPr><w:p/></w:tc></w:tr></w:tbl>
            """;

        var revisions = new TestDocx { Body = body }.Load().Revisions();

        Assert.Equal(["insert", "cell-delete"], revisions.Select(revision => revision.Kind));
    }

    [Fact]
    public void ToSnapshot_ShouldNameTheControlsAroundARow_WhenRowsSitInARepeatingSection()
    {
        var body = """
            <w:tbl><w:sdt><w:sdtPr><w:tag w:val="drugs"/><w15:repeatingSection xmlns:w15="http://schemas.microsoft.com/office/word/2012/wordml"/></w:sdtPr>
              <w:sdtContent><w:sdt><w:sdtPr><w15:repeatingSectionItem xmlns:w15="http://schemas.microsoft.com/office/word/2012/wordml"/></w:sdtPr>
                <w:sdtContent><w:tr><w:tc><w:p><w:r><w:t>Warfarin</w:t></w:r></w:p></w:tc></w:tr></w:sdtContent></w:sdt></w:sdtContent></w:sdt></w:tbl>
            """;

        Assert.Contains("""
                Row
                  Control repeating-section name="drugs"
                  Control repeating-section-item
                  Cell
                    P "Warfarin"
            """.ReplaceLineEndings("\n"), new TestDocx { Body = body }.Load().ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldRecordBookmarksAndUnknownContentInPlace_WhenTheyComeBetweenRowsAndCells()
    {
        var body = """
            <w:tbl><w:tblPr/><w:tblGrid/><w:bookmarkStart w:id="1" w:name="Doses"/>
              <w:tr><w:trPr/><w:tblPrEx/><w:tc><w:p/></w:tc><w:bookmarkStart w:id="2" w:colFirst="1" w:colLast="1" w:name="Food"/>
                <w:tc><w:p/></w:tc><w:newCellThing/></w:tr>
              <w:bookmarkEnd w:id="1"/><w:bookmarkEnd w:id="2"/><w:newRowThing/></w:tbl>
            """;

        var snapshot = new TestDocx { Body = body }.Load().ToSnapshot();

        Assert.Matches("""
              Table
                bookmark "Doses"
                Row
                  Cell
                    P
                  bookmark "Food"
                  Cell
                    P
                  unsupported w:newCellThing hash=[0-9a-f]{16}
                unsupported w:newRowThing hash=[0-9a-f]{16}

            """.ReplaceLineEndings("\n"), snapshot);
    }

    [Fact]
    public void Load_ShouldReadTheFirstChoice_WhenRowsOrCellsAreOfferedAsAlternateContent()
    {
        var body = """
            <w:tbl xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006">
              <mc:AlternateContent><mc:Choice Requires="w14"><w:tr><w:tc><w:p><w:r><w:t>New</w:t></w:r></w:p></w:tc>
                <mc:AlternateContent><mc:Choice Requires="w14"><w:tc><w:p><w:r><w:t>Cell</w:t></w:r></w:p></w:tc></mc:Choice>
                  <mc:Fallback/></mc:AlternateContent></w:tr></mc:Choice>
                <mc:Fallback><w:tr><w:tc><w:p><w:r><w:t>Old</w:t></w:r></w:p></w:tc></w:tr></mc:Fallback></mc:AlternateContent></w:tbl>
            """;

        var row = Assert.Single(Assert.IsType<DocxTable>(Assert.Single(new TestDocx { Body = body }.Load().Body)).Rows);

        var texts = row.Cells.Select(cell => Assert.IsType<DocxParagraph>(Assert.Single(cell.Blocks)).Text);
        Assert.Equal(["New", "Cell"], texts);
    }
}
