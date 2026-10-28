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
}
