namespace PDiffy.Tests.Unit.Docx;

// Each case builds two documents that differ only in one detail, and checks that ignoring it makes them match.
public class DocxDetailTests
{
    [Theory]
    [InlineData(DocxDetail.FieldResults, Field + "3" + FieldEnd, Field + "4" + FieldEnd)]
    [InlineData(DocxDetail.FieldResults, BoldPage + "3" + BoldPageEnd, BoldPage + "4" + BoldPageEnd)]
    [InlineData(DocxDetail.FieldResults, TocEntry + "3" + TocEntryEnd, TocEntry + "4" + TocEntryEnd)]
    [InlineData(DocxDetail.FieldResults, NestedResult + "3" + NestedResultEnd, NestedResult + "4" + NestedResultEnd)]
    [InlineData(DocxDetail.FieldResults, InControl + "3" + InControlEnd, InControl + "4" + InControlEnd)]
    [InlineData(DocxDetail.Fonts, """<w:p><w:r><w:rPr><w:rFonts w:ascii="Arial"/></w:rPr><w:t>Walk</w:t></w:r></w:p>""",
        """<w:p><w:r><w:rPr><w:rFonts w:ascii="Calibri"/></w:rPr><w:t>Walk</w:t></w:r></w:p>""")]
    [InlineData(DocxDetail.FontSizes, """<w:p><w:r><w:rPr><w:sz w:val="24"/></w:rPr><w:t>Walk</w:t></w:r></w:p>""",
        """<w:p><w:r><w:rPr><w:sz w:val="28"/></w:rPr><w:t>Walk</w:t></w:r></w:p>""")]
    [InlineData(DocxDetail.Colors,
        """<w:p><w:r><w:rPr><w:color w:val="FF0000"/><w:highlight w:val="yellow"/></w:rPr><w:t>Walk</w:t></w:r></w:p>""",
        """<w:p><w:r><w:rPr><w:color w:val="0000FF"/><w:highlight w:val="green"/></w:rPr><w:t>Walk</w:t></w:r></w:p>""")]
    [InlineData(DocxDetail.Colors,
        """<w:tbl><w:tr><w:tc><w:tcPr><w:shd w:fill="D9D9D9"/></w:tcPr><w:p/></w:tc></w:tr></w:tbl>""",
        """<w:tbl><w:tr><w:tc><w:tcPr><w:shd w:fill="FFFF00"/></w:tcPr><w:p/></w:tc></w:tr></w:tbl>""")]
    [InlineData(DocxDetail.Colors,
        """<w:tbl><w:tblPr><w:tblBorders><w:top w:val="single" w:sz="4" w:color="D9D9D9"/></w:tblBorders></w:tblPr></w:tbl>""",
        """<w:tbl><w:tblPr><w:tblBorders><w:top w:val="single" w:sz="4" w:color="BFBFBF"/></w:tblBorders></w:tblPr></w:tbl>""")]
    [InlineData(DocxDetail.Colors,
        """<w:p><w:pPr><w:pBdr><w:top w:val="single" w:sz="4" w:color="D9D9D9"/></w:pBdr><w:shd w:fill="D9D9D9"/></w:pPr></w:p>""",
        """<w:p><w:pPr><w:pBdr><w:top w:val="single" w:sz="4" w:color="BFBFBF"/></w:pBdr><w:shd w:fill="FFFF00"/></w:pPr></w:p>""")]
    [InlineData(DocxDetail.Spacing,
        """<w:p><w:pPr><w:spacing w:after="120" w:line="276" w:lineRule="auto"/></w:pPr></w:p>""",
        """<w:p><w:pPr><w:spacing w:after="240" w:line="360" w:lineRule="auto"/></w:pPr></w:p>""")]
    [InlineData(DocxDetail.Indents, """<w:p><w:pPr><w:ind w:left="720" w:hanging="360"/></w:pPr></w:p>""",
        """<w:p><w:pPr><w:ind w:left="1440" w:hanging="180"/></w:pPr></w:p>""")]
    [InlineData(DocxDetail.RevisionAuthors,
        """<w:p><w:ins w:id="1" w:author="Ann"><w:r><w:t>Walk</w:t></w:r></w:ins></w:p>""",
        """<w:p><w:ins w:id="1" w:author="Bo"><w:r><w:t>Walk</w:t></w:r></w:ins></w:p>""")]
    public void ToSnapshot_ShouldNotChange_WhenOnlyAnIgnoredDetailDiffers(DocxDetail detail, string before,
        string after)
    {
        var (first, second) = (new TestDocx { Body = before }.Load(), new TestDocx { Body = after }.Load());

        var ignored = first.ToSnapshot(o => o.Ignore(detail));

        Assert.NotEqual(first.ToSnapshot(), second.ToSnapshot());
        Assert.Equal(ignored, second.ToSnapshot(o => o.Ignore(detail)));
        Assert.StartsWith($"# docx-snapshot v1\n# ignored fields: {SnapshotWriter.Name(detail)}\n", ignored);
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenOnlyIgnoredImageBytesDiffer()
    {
        static string Snapshot(byte[] bytes, bool ignore) => new TestDocx { Body = Image }
            .WithImage("logo", "image1.png", bytes).Load()
            .ToSnapshot(o => { if (ignore) o.Ignore(DocxDetail.ImageHashes); });

        var ignored = Snapshot([0x89, 0x50, 1], ignore: true);

        Assert.NotEqual(Snapshot([0x89, 0x50, 1], ignore: false), Snapshot([0x89, 0x50, 2], ignore: false));
        Assert.Equal(ignored, Snapshot([0x89, 0x50, 2], ignore: true));
        Assert.DoesNotContain("hash=", ignored);
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenOnlyAnIgnoredCommentAuthorDiffers()
    {
        static string Snapshot(string author, bool ignore) => new TestDocx
        {
            Body = """<w:p><w:r><w:t>Walk</w:t></w:r><w:r><w:commentReference w:id="0"/></w:r></w:p>""",
        }.WithXmlPart("remarks", "comments", "comments",
            $"""<w:comment w:id="0" w:author="{author}"><w:p><w:r><w:t>Check</w:t></w:r></w:p></w:comment>""")
            .Load().ToSnapshot(o => { if (ignore) o.Ignore(DocxDetail.CommentAuthors); });

        var ignored = Snapshot("Ann", ignore: true);

        Assert.NotEqual(Snapshot("Ann", ignore: false), Snapshot("Bo", ignore: false));
        Assert.Equal(ignored, Snapshot("Bo", ignore: true));
    }

    [Fact]
    public void ToSnapshot_ShouldTakeTheResultOutOfTheText_WhenFieldResultsAreIgnored()
    {
        var document = new TestDocx { Body = Field + "3" + FieldEnd }.Load();

        var snapshot = document.ToSnapshot(o => o.Ignore(DocxDetail.FieldResults));

        Assert.Contains("  P \"Page  of the guide\"\n    field \"PAGE\" at=5\n    note footnote 1 at=18\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldKeepTheText_WhenTheIgnoredResultIsInAnotherFieldsCode()
    {
        var document = new TestDocx
        {
            Body = """
                <w:p><w:r><w:t xml:space="preserve">Page </w:t><w:fldChar w:fldCharType="begin"/><w:instrText>IF </w:instrText>
                  <w:fldChar w:fldCharType="begin"/><w:instrText>PAGE</w:instrText><w:fldChar w:fldCharType="separate"/>
                  <w:t>1</w:t><w:fldChar w:fldCharType="end"/><w:instrText> = 2 "x"</w:instrText><w:fldChar w:fldCharType="end"/>
                  <w:t>of</w:t></w:r></w:p>
                """,
        }.Load();

        var snapshot = document.ToSnapshot(o => o.Ignore(DocxDetail.FieldResults));

        Assert.Contains("  P \"Page of\"\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenOnlyAnIgnoredListIndentDiffers()
    {
        static string Snapshot(string indent, bool ignore) => new TestDocx
        {
            Numbering = $"""
                <w:abstractNum w:abstractNumId="1"><w:lvl w:ilvl="0"><w:numFmt w:val="decimal"/><w:lvlText w:val="%1."/>
                  <w:pPr><w:ind w:left="{indent}" w:hanging="360"/></w:pPr></w:lvl></w:abstractNum>
                <w:num w:numId="1"><w:abstractNumId w:val="1"/></w:num>
                """,
            Body = """<w:p><w:pPr><w:numPr><w:ilvl w:val="0"/><w:numId w:val="1"/></w:numPr></w:pPr></w:p>""",
        }.Load().ToSnapshot(o => { if (ignore) o.Ignore(DocxDetail.Indents); });

        var ignored = Snapshot("720", ignore: true);

        Assert.NotEqual(Snapshot("720", ignore: false), Snapshot("1440", ignore: false));
        Assert.Equal(ignored, Snapshot("1440", ignore: true));
    }

    [Fact]
    public void ToSnapshot_ShouldKeepALegacyFieldsValue_WhenFieldResultsAreIgnored()
    {
        var document = new TestDocx
        {
            Body = """
                <w:p><w:r><w:fldChar w:fldCharType="begin"><w:ffData><w:name w:val="Field1"/><w:textInput/></w:ffData></w:fldChar>
                  </w:r><w:r><w:instrText>FORMTEXT</w:instrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r>
                  <w:r><w:t>Jane</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r></w:p>
                """,
        }.Load();

        var snapshot = document.ToSnapshot(o => o.Ignore(DocxDetail.FieldResults));

        Assert.Contains("  P\n    field \"FORMTEXT\" at=0\n    form legacy-text name=\"Field1\" value=\"Jane\" at=0\n",
            snapshot);
    }

    private const string Field =
        """<w:p><w:r><w:t xml:space="preserve">Page </w:t></w:r><w:fldSimple w:instr="PAGE"><w:r><w:t>""";

    private const string FieldEnd = """</w:t></w:r></w:fldSimple><w:r><w:t xml:space="preserve"> of the guide</w:t>"""
        + """</w:r><w:r><w:footnoteReference w:id="1"/></w:r></w:p>""";

    // Word's "Page X, Bold Numbers" footer: the result is a bold run, so it's also a span.
    private const string BoldPage = """<w:p><w:r><w:t xml:space="preserve">Page </w:t></w:r><w:fldSimple w:instr="PAGE">"""
        + """<w:r><w:rPr><w:b/></w:rPr><w:t>""";

    private const string BoldPageEnd = "</w:t></w:r></w:fldSimple></w:p>";

    // A table of contents entry: a link around the heading and the page it's on.
    private const string TocEntry = """<w:p><w:hyperlink w:anchor="_Toc1"><w:r><w:t xml:space="preserve">Intro </w:t></w:r>"""
        + """<w:fldSimple w:instr="PAGEREF _Toc1"><w:r><w:t>""";

    private const string TocEntryEnd = "</w:t></w:r></w:fldSimple></w:hyperlink></w:p>";

    private const string NestedResult = """<w:p><w:fldSimple w:instr="REF Chapter"><w:r><w:t xml:space="preserve">Part </w:t>"""
        + """</w:r><w:fldSimple w:instr="PAGE"><w:r><w:t>""";

    private const string NestedResultEnd = "</w:t></w:r></w:fldSimple></w:fldSimple><w:r><w:t>end</w:t></w:r></w:p>";

    // A content control's value is the text it covers, here a page number.
    private const string InControl = """<w:p><w:sdt><w:sdtPr><w:text/></w:sdtPr><w:sdtContent>"""
        + """<w:fldSimple w:instr="PAGE"><w:r><w:t>""";

    private const string InControlEnd = "</w:t></w:r></w:fldSimple></w:sdtContent></w:sdt></w:p>";

    private const string Image = """
        <w:p><w:r><w:drawing xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
            xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
          <wp:inline><wp:extent cx="1524000" cy="508000"/><wp:docPr id="1" name="Picture 1" descr="Logo"/>
            <a:graphic><a:graphicData><a:blip r:embed="logo"/></a:graphicData></a:graphic></wp:inline>
        </w:drawing></w:r></w:p>
        """;
}
