namespace PDiffy.Tests.Unit.Docx;

public class DocxDocumentTests
{
    private const string NormalStyle = """
        <w:style w:type="paragraph" w:styleId="Normal" w:default="1"><w:name w:val="Normal"/>
          <w:rPr><w:rFonts w:ascii="Arial"/><w:sz w:val="24"/></w:rPr></w:style>
        """;

    [Fact]
    public void ToSnapshot_ShouldWriteEverySection_WhenGivenASmallDocument()
    {
        var docx = new TestDocx
        {
            Styles = NormalStyle,
            Settings = """<w:compat><w:compatSetting w:name="compatibilityMode" w:val="15"/></w:compat>""",
            Core = "<dc:title>Cheese</dc:title><dcterms:created>2026-10-28T00:00:00Z</dcterms:created>",
            Body = """
                <w:p><w:r><w:t>Cheese </w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>has</w:t></w:r><w:r><w:t> history</w:t></w:r></w:p>
                <w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="720" w:footer="720"/></w:sectPr>
                """,
        };

        var snapshot = docx.Load().ToSnapshot();

        var expected = """
            # docx-snapshot v1
            document
              title="Cheese" lang=(missing) compat=15 track-changes=no protection=none even-odd-headers=no created=<present> modified=<missing>
            sections
              s1 letter margins=72,72,72,72 header=36 footer=36 columns=1 break=nextPage
            styles
              Normal paragraph "Normal" default font="Arial" size=12
            numbering
              (none)
            body
              P style=Normal "Cheese has history"
                span "has" bold
            headers
              (none)
            footers
              (none)
            notes
              (none)

            """;
        Assert.Equal(expected.ReplaceLineEndings("\n"), snapshot);
    }

    [Fact]
    public void Load_ShouldMergeRunsIntoOneText_WhenTheyLookTheSame()
    {
        var docx = new TestDocx { Styles = NormalStyle, Body = "<w:p><w:r><w:t>Chee</w:t></w:r><w:r><w:t xml:space=\"preserve\">se </w:t></w:r></w:p>" };

        var paragraph = Assert.Single(docx.Load().Paragraphs());

        Assert.Equal("Cheese ", paragraph.Text);
        Assert.Equal(paragraph.Run, Assert.Single(paragraph.Spans).Format);
    }

    [Fact]
    public void Load_ShouldResolveAStyle_WhenItIsBasedOnAnother()
    {
        var docx = new TestDocx
        {
            Styles = NormalStyle + """
                <w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/>
                  <w:pPr><w:keepNext/><w:outlineLvl w:val="0"/></w:pPr><w:rPr><w:sz w:val="32"/></w:rPr></w:style>
                """,
            Body = """<w:p><w:pPr><w:pStyle w:val="Heading1"/></w:pPr><w:r><w:t>Title</w:t></w:r></w:p>""",
        };

        var style = Assert.Single(docx.Load().Styles);

        Assert.Equal(("Arial", 16.0, true, 1), (style.Run.Font, style.Run.Size, style.Paragraph.KeepNext, style.Paragraph.HeadingLevel));
    }

    [Fact]
    public void Load_ShouldUseTheThemesFont_WhenAFontIsGivenByTheme()
    {
        var docx = new TestDocx
        {
            ThemeFonts = ("Cambria", "Calibri"),
            Styles = """<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:asciiTheme="minorHAnsi"/></w:rPr></w:rPrDefault></w:docDefaults>""",
            Body = "<w:p><w:r><w:t>Body</w:t></w:r></w:p>",
        };

        Assert.Equal("Calibri", Assert.Single(docx.Load().Paragraphs()).Run.Font);
    }

    [Fact]
    public void Load_ShouldTurnBoldOff_WhenABoldCharacterStyleIsUsedInABoldParagraphStyle()
    {
        var docx = new TestDocx
        {
            Styles = """
                <w:style w:type="paragraph" w:styleId="Strong" w:default="1"><w:rPr><w:b/></w:rPr></w:style>
                <w:style w:type="character" w:styleId="Emphasis"><w:rPr><w:b/></w:rPr></w:style>
                """,
            Body = """<w:p><w:r><w:rPr><w:rStyle w:val="Emphasis"/></w:rPr><w:t>Quiet</w:t></w:r></w:p>""",
        };

        var span = Assert.Single(Assert.Single(docx.Load().Paragraphs()).Spans);

        Assert.False(span.Format.Bold);
    }

    [Fact]
    public void ToSnapshot_ShouldShowTabsBreaksAndInvisibleCharacters_WhenTextHasThem()
    {
        var docx = new TestDocx { Body = "<w:p><w:r><w:t>a</w:t><w:tab/><w:t>b</w:t><w:br/><w:t>c\u00A0d\\e \"f\"</w:t></w:r></w:p>" };

        Assert.Contains("P \"a\\tb\\nc\\u00a0d\\\\e \\\"f\\\"\"", docx.Load().ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldShowAPageBreakAndWhereItIs_WhenAParagraphHasOne()
    {
        var docx = new TestDocx { Body = """<w:p><w:r><w:t>Before</w:t><w:br w:type="page"/><w:t>After</w:t></w:r></w:p>""" };

        Assert.Contains("  P \"BeforeAfter\"\n    break page at=6\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldNumberSectionsAndMarkWhereEachEnds_WhenTheDocumentHasSeveral()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:p><w:pPr><w:sectPr><w:pgSz w:w="15840" w:h="12240" w:orient="landscape"/><w:cols w:num="2"/></w:sectPr></w:pPr>
                  <w:r><w:t>Wide</w:t></w:r></w:p>
                <w:p><w:r><w:t>Narrow</w:t></w:r></w:p>
                <w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:type w:val="continuous"/><w:pgNumType w:start="3" w:fmt="lowerRoman"/>
                  <w:titlePg/><w:headerReference w:type="first" r:id="h1"/></w:sectPr>
                """,
        };

        var snapshot = docx.Load().ToSnapshot();

        Assert.Contains("  s1 letter-landscape margins=0,0,0,0 header=0 footer=0 columns=2 break=nextPage\n", snapshot);
        Assert.Contains("  s2 letter margins=0,0,0,0 header=0 footer=0 columns=1 break=continuous page-start=3 "
            + "page-format=lowerRoman title-page headers=first\n", snapshot);
        Assert.Contains("  P \"Wide\"\n    ends-section s1\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldRecordABookmarkBetweenBlocks_WhenOneStartsAtATable()
    {
        var body = """
            <w:bookmarkStart w:id="1" w:name="Doses"/><w:tbl><w:tr><w:tc><w:p><w:r><w:t>Warfarin</w:t></w:r></w:p></w:tc></w:tr></w:tbl>
            <w:bookmarkEnd w:id="1"/>
            """;

        Assert.Contains("body\n  bookmark \"Doses\"\n  Table\n", new TestDocx { Body = body }.Load().ToSnapshot());
    }

    [Fact]
    public void Load_ShouldReadTheFirstChoice_WhenParagraphsAreOfferedAsAlternateContent()
    {
        var body = """
            <mc:AlternateContent xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006">
              <mc:Choice Requires="w14"><w:p><w:r><w:t>New</w:t></w:r></w:p></mc:Choice>
              <mc:Fallback><w:p><w:r><w:t>Old</w:t></w:r></w:p></mc:Fallback></mc:AlternateContent>
            """;

        Assert.Equal("New", new TestDocx { Body = body }.Load().Paragraphs().Single().Text);
    }

    [Fact]
    public void ToSnapshot_ShouldRecordUnknownContentBetweenBlocksByHash_WhenPDiffyCannotReadIt()
    {
        var body = """<w:p/><x:chart xmlns:x="urn:example" x:kind="pie"/><w:newBlock/>""";

        var snapshot = new TestDocx { Body = body }.Load().ToSnapshot();

        Assert.Matches("\n  unsupported \\{urn:example\\}chart hash=[0-9a-f]{16}\n", snapshot);
        Assert.Matches("\n  unsupported w:newBlock hash=[0-9a-f]{16}\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldPrintNoUnsupportedBlocks_WhenTheBodyHasOnlyPropertiesAndMarkers()
    {
        var body = """
            <w:bookmarkStart w:id="0" w:name="_GoBack"/><w:permStart w:id="5"/><w:commentRangeStart w:id="1"/>
            <w:tbl><w:tr><w:tc><w:tcPr><w:tcW w:w="2880" w:type="dxa"/></w:tcPr><w:p/></w:tc></w:tr></w:tbl>
            <w:commentRangeEnd w:id="1"/><w:permEnd w:id="5"/><w:bookmarkEnd w:id="0"/>
            <w:sectPr><w:pgSz w:w="12240" w:h="15840"/></w:sectPr>
            """;

        Assert.DoesNotContain("unsupported", new TestDocx { Body = body }.Load().ToSnapshot());
    }

    [Fact]
    public void Load_ShouldRecordATrackedChangeToTheSection_WhenItsSettingsChangedWithTrackingOn()
    {
        var body = """
            <w:p/><w:sectPr><w:pgSz w:w="12240" w:h="15840"/>
              <w:sectPrChange w:id="9" w:author="Di"><w:sectPr><w:pgSz w:w="15840" w:h="12240"/></w:sectPr></w:sectPrChange></w:sectPr>
            """;

        var document = new TestDocx { Body = body }.Load();

        var revision = Assert.Single(document.Revisions());
        Assert.Equal(("section-format", "Di"), (revision.Kind, revision.Author));
        Assert.Single(document.PageSetups);
        Assert.Contains("\n    section-format author=\"Di\"\n", document.ToSnapshot());
    }

    [Fact]
    public void Load_ShouldReadTheDocumentsSettings_WhenTheSettingsPartHasThem()
    {
        var docx = new TestDocx
        {
            Styles = """<w:docDefaults><w:rPrDefault><w:rPr><w:lang w:val="es-MX"/></w:rPr></w:rPrDefault></w:docDefaults>""",
            Settings = """<w:trackRevisions/><w:documentProtection w:edit="forms" w:enforcement="1"/><w:evenAndOddHeaders/>""",
        };

        var info = docx.Load().Document;

        Assert.Equal(("es-MX", true, "forms", true), (info.Language, info.TrackChanges, info.Protection, info.EvenAndOddHeaders));
    }

    [Fact]
    public void ToSnapshot_ShouldNestCellParagraphsUnderTheirRows_WhenTheBodyHasATable()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:tbl><w:tr><w:tc><w:p><w:r><w:t>Drug</w:t></w:r></w:p></w:tc>
                  <w:sdt><w:sdtPr><w:tag w:val="food"/></w:sdtPr><w:sdtContent><w:tc><w:p><w:r><w:t>Food</w:t></w:r></w:p></w:tc>
                  </w:sdtContent></w:sdt></w:tr></w:tbl>
                """,
        };

        Assert.Contains("  Table\n    Row\n      Cell\n        P \"Drug\"\n      Cell\n        Control rich-text name=\"food\" "
            + "around\n        P \"Food\"\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void Load_ShouldReadTextInsideWrappersButNotDeletedText_WhenChangesAreTracked()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:p><w:ins><w:r><w:t>new </w:t></w:r></w:ins><w:del><w:r><w:delText>old </w:delText></w:r></w:del>
                  <w:hyperlink r:id="link"><w:r><w:t>link</w:t></w:r></w:hyperlink></w:p>
                """,
        };

        Assert.Equal("new link", Assert.Single(docx.Load().Paragraphs()).Text);
    }

    [Fact]
    public void ToSnapshot_ShouldRecordTextApartFromTheParagraph_WhenARunSitsInsideTheParagraphsProperties()
    {
        var docx = new TestDocx { Body = """<w:p><w:pPr><w:jc w:val="right"/><w:r><w:t>1</w:t></w:r></w:pPr><w:r><w:t>Body</w:t></w:r></w:p>""" };

        Assert.Contains("  P align=right \"Body\"\n    stray-text \"1\" at=0\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void Load_ShouldKeepTheMisplacedRunsTabsAndBreaks_WhenItHasNoText()
    {
        var docx = new TestDocx { Body = """<w:p><w:pPr><w:r><w:tab/><w:br/><w:br w:type="page"/><w:br w:type="column"/></w:r></w:pPr></w:p>""" };

        Assert.Equal("\t\n\f\u000E", docx.Load().Paragraphs().Single().Inlines.OfType<DocxStrayText>().Single().Text);
    }

    [Fact]
    public void Load_ShouldReadStyles_WhenTheyAreNestedInsideAnotherStylesElement()
    {
        var docx = new TestDocx { Styles = "<w:styles>" + NormalStyle + "</w:styles>", Body = "<w:p/>" };

        Assert.Equal("Arial", Assert.Single(docx.Load().Styles).Run.Font);
    }

    [Fact]
    public void Load_ShouldStopAtTheFirstRepeat_WhenStylesAreBasedOnEachOther()
    {
        var docx = new TestDocx
        {
            Styles = """
                <w:style w:type="paragraph" w:styleId="A"><w:basedOn w:val="B"/><w:rPr><w:sz w:val="20"/></w:rPr></w:style>
                <w:style w:type="paragraph" w:styleId="B"><w:basedOn w:val="A"/><w:rPr><w:sz w:val="30"/></w:rPr></w:style>
                """,
            Body = """<w:p><w:pPr><w:pStyle w:val="A"/></w:pPr></w:p>""",
        };

        Assert.Equal(10, Assert.Single(docx.Load().Styles).Run.Size);
    }

    [Fact]
    public void Load_ShouldListThePartAsUnreadable_WhenItsXmlIsBroken()
    {
        var docx = new TestDocx { Styles = "" };
        docx.RawParts["word/styles.xml"] = "<w:styles";

        Assert.Equal(["word/styles.xml"], docx.Load().Document.UnreadableParts);
    }

    [Fact]
    public void Load_ShouldThrow_WhenTheFileIsNotADocx()
    {
        Assert.Throws<InvalidDataException>(() => DocxDocument.Load(new MemoryStream("not a zip"u8.ToArray())));
    }

    [Fact]
    public void CompareTo_ShouldReportTheChangedWords_WhenAParagraphsTextChanges()
    {
        var before = new TestDocx { Body = "<w:p><w:r><w:t>Take with food</w:t></w:r></w:p>" }.Load();
        var after = new TestDocx { Body = "<w:p><w:r><w:t>Take after food</w:t></w:r></w:p>" }.Load();

        var change = Assert.Single(before.CompareTo(after).Changes);

        Assert.Equal((ChangeKind.TextChanged, "\"Take [-with-] {+after+} food\""), (change.Kind, change.Detail));
    }
}
