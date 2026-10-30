namespace PDiffy.Tests.Unit.Docx;

public class DocxFormattingTests
{
    private const string Heading = """
        <w:style w:type="paragraph" w:styleId="Heading" w:default="1"><w:name w:val="Heading"/>
          <w:pPr><w:keepNext/><w:jc w:val="center"/><w:tabs><w:tab w:val="right" w:pos="9360" w:leader="dot"/></w:tabs></w:pPr>
          <w:rPr><w:b/><w:color w:val="FF0000"/></w:rPr></w:style>
        """;

    [Fact]
    public void ToSnapshot_ShouldListOnlyWhatAParagraphChanges_WhenItHasFormattingOfItsOwn()
    {
        var docx = new TestDocx
        {
            Styles = Heading,
            Body = """
                <w:p><w:pPr><w:jc w:val="center"/><w:ind w:left="720"/><w:spacing w:after="240"/></w:pPr><w:r><w:t>Same and new</w:t></w:r></w:p>
                <w:p><w:pPr><w:keepNext w:val="0"/><w:tabs><w:tab w:val="clear" w:pos="9360"/></w:tabs></w:pPr><w:r><w:t>Off</w:t></w:r></w:p>
                """,
        };

        var snapshot = docx.Load().ToSnapshot();

        Assert.Contains("  P style=Heading indent-left=36 spacing-after=12 \"Same and new\"\n", snapshot);
        Assert.Contains("  P style=Heading keep-next=no tabs=none \"Off\"\n", snapshot);
        Assert.Contains("  Heading paragraph \"Heading\" default bold color=FF0000 align=center keep-next tabs=right:468:dot\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldShowWhatARunTurnsOff_WhenItClearsItsStylesFormatting()
    {
        var docx = new TestDocx
        {
            Styles = Heading,
            Body = """
                <w:p><w:r><w:t>Loud </w:t></w:r><w:r><w:rPr><w:b w:val="0"/><w:color w:val="auto"/></w:rPr><w:t>quiet</w:t></w:r></w:p>
                """,
        };

        Assert.Contains("  P style=Heading \"Loud quiet\"\n    span \"quiet\" bold=no color=auto\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldShowWhatACharacterStyleTurnsOff_WhenItClearsTheParagraphStylesFormatting()
    {
        var docx = new TestDocx
        {
            Styles = Heading + """<w:style w:type="character" w:styleId="Plain"><w:rPr><w:color w:val="auto"/><w:u w:val="none"/></w:rPr></w:style>""",
            Body = """<w:p><w:r><w:t>Red </w:t></w:r><w:r><w:rPr><w:rStyle w:val="Plain"/></w:rPr><w:t>plain</w:t></w:r></w:p>""",
        };

        Assert.Contains("    span \"plain\" char-style=Plain color=auto\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void Load_ShouldMakeNoSpan_WhenARunSaysNoneOrAutoForSomethingNothingSet()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:p><w:r><w:t>Plain </w:t></w:r><w:r><w:rPr><w:u w:val="none"/><w:highlight w:val="none"/><w:color w:val="auto"/>
                  <w:vertAlign w:val="baseline"/></w:rPr><w:t>still plain</w:t></w:r></w:p>
                """,
        };

        var paragraph = Assert.Single(docx.Load().Paragraphs());

        Assert.Equal("Plain still plain", Assert.Single(paragraph.Spans).Text);
    }

    [Fact]
    public void ToSnapshot_ShouldNameTheCharacterStyleAndQuoteTheFont_WhenASpanUsesThem()
    {
        var docx = new TestDocx
        {
            Styles = """<w:style w:type="character" w:styleId="Code"><w:rPr><w:rFonts w:ascii="Courier New"/><w:u w:val="single"/></w:rPr></w:style>""",
            Body = """<w:p><w:r><w:t>Run </w:t></w:r><w:r><w:rPr><w:rStyle w:val="Code"/></w:rPr><w:t>dotnet test</w:t></w:r></w:p>""",
        };

        Assert.Contains("    span \"dotnet test\" char-style=Code font=\"Courier New\" underline=single\n", docx.Load().ToSnapshot());
    }

    [Theory]
    [InlineData("""<w:spacing w:line="276" w:lineRule="auto"/>""", "line=1.15")]
    [InlineData("""<w:spacing w:line="240" w:lineRule="exact"/>""", "line=12-exact")]
    [InlineData("""<w:ind w:hanging="360" w:firstLine="720"/>""", "hanging=18")]
    [InlineData("""<w:bidi/>""", "rtl-paragraph")]
    [InlineData("""<w:pBdr><w:top w:val="single" w:sz="4" w:color="ff0000"/></w:pBdr>""", "borders=top:single:0.5:FF0000")]
    [InlineData("""<w:shd w:val="clear" w:fill="ffff00"/>""", "shading=FFFF00")]
    public void ToSnapshot_ShouldPrintTheParagraphsLayout_WhenItSetsSpacingOrIndents(string properties, string expected)
    {
        var docx = new TestDocx { Body = $"<w:p><w:pPr>{properties}</w:pPr><w:r><w:t>Text</w:t></w:r></w:p>" };

        Assert.Contains($"  P {expected} \"Text\"\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldReplaceOnlyThatSide_WhenAParagraphSetsOneBorderItsStyleHas()
    {
        var docx = new TestDocx
        {
            Styles = """
                <w:style w:type="paragraph" w:styleId="Boxed"><w:pPr><w:pBdr>
                  <w:top w:val="single" w:sz="4"/><w:bottom w:val="single" w:sz="4"/>
                </w:pBdr></w:pPr></w:style>
                """,
            Body = """<w:p><w:pPr><w:pStyle w:val="Boxed"/><w:pBdr><w:bottom w:val="double" w:sz="12"/></w:pBdr></w:pPr><w:r><w:t>Text</w:t></w:r></w:p>""",
        };

        var snapshot = docx.Load().ToSnapshot();

        Assert.Contains("  P style=Boxed borders=top:single:0.5,bottom:double:1.5 \"Text\"\n", snapshot);
        Assert.Contains("  Boxed paragraph borders=top:single:0.5,bottom:single:0.5\n", snapshot);
    }

    [Theory]
    [InlineData("""<w:pBdr><w:bottom w:val="nil"/></w:pBdr>""", "borders=top:single:0.5")]
    [InlineData("""<w:pBdr><w:top w:val="none"/><w:bottom w:val="nil"/></w:pBdr>""", "borders=none")]
    [InlineData("""<w:shd w:fill="auto"/>""", "shading=none")]
    [InlineData("""<w:bidi w:val="0"/>""", "rtl-paragraph=no")]
    public void ToSnapshot_ShouldShowWhatAParagraphClears_WhenItTurnsOffItsStylesBordersShadingOrDirection(string properties,
        string expected)
    {
        var docx = new TestDocx
        {
            Styles = """
                <w:style w:type="paragraph" w:styleId="Boxed"><w:pPr><w:bidi/><w:shd w:fill="FFFF00"/><w:pBdr>
                  <w:top w:val="single" w:sz="4"/><w:bottom w:val="single" w:sz="4"/>
                </w:pBdr></w:pPr></w:style>
                """,
            Body = $"""<w:p><w:pPr><w:pStyle w:val="Boxed"/>{properties}</w:pPr><w:r><w:t>Text</w:t></w:r></w:p>""",
        };

        Assert.Contains($"  P style=Boxed {expected} \"Text\"\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldTellTheParagraphsDirectionFromItsTexts_WhenAStyleSetsBoth()
    {
        var docx = new TestDocx
        {
            Styles = """<w:style w:type="paragraph" w:styleId="Arabic"><w:pPr><w:bidi/></w:pPr><w:rPr><w:rtl/></w:rPr></w:style>""",
            Body = """<w:p><w:pPr><w:pStyle w:val="Arabic"/></w:pPr></w:p>""",
        };

        Assert.Contains("  Arabic paragraph rtl rtl-paragraph\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldNotPrintBorders_WhenAParagraphsDifferFromItsStylesOnlyInColorAndColorsAreIgnored()
    {
        var docx = new TestDocx
        {
            Styles = """<w:style w:type="paragraph" w:styleId="Boxed"><w:pPr><w:pBdr><w:top w:val="single" w:sz="4" w:color="FF0000"/></w:pBdr></w:pPr></w:style>""",
            Body = """<w:p><w:pPr><w:pStyle w:val="Boxed"/><w:pBdr><w:top w:val="single" w:sz="4" w:color="0000FF"/></w:pBdr></w:pPr><w:r><w:t>Text</w:t></w:r></w:p>""",
        };

        Assert.Contains("  P style=Boxed \"Text\"\n", docx.Load().ToSnapshot(o => o.Ignore(DocxDetail.Colors)));
    }

    [Fact]
    public void ToSnapshot_ShouldLeaveOutBorderColorsAndShading_WhenColorsAreIgnored()
    {
        var docx = new TestDocx
        {
            Body = """<w:p><w:pPr><w:pBdr><w:top w:val="single" w:sz="4" w:color="FF0000"/></w:pBdr><w:shd w:fill="FFFF00"/></w:pPr><w:r><w:t>Text</w:t></w:r></w:p>""",
        };

        var snapshot = docx.Load().ToSnapshot(o => o.Ignore(DocxDetail.Colors));

        Assert.Contains("  P borders=top:single:0.5 \"Text\"\n", snapshot);
    }

    [Fact]
    public void Load_ShouldDropAnInheritedFirstLineIndent_WhenAStyleHangsInstead()
    {
        var docx = new TestDocx
        {
            Styles = """
                <w:style w:type="paragraph" w:styleId="Body"><w:pPr><w:ind w:firstLine="720"/></w:pPr></w:style>
                <w:style w:type="paragraph" w:styleId="Item"><w:basedOn w:val="Body"/><w:pPr><w:ind w:hanging="360"/></w:pPr></w:style>
                """,
            Body = """<w:p><w:pPr><w:pStyle w:val="Item"/></w:pPr></w:p>""",
        };

        var layout = Assert.Single(docx.Load().Styles).Paragraph;

        Assert.Equal((null, 18.0), (layout.FirstLine, layout.Hanging));
    }

    [Fact]
    public void Load_ShouldTreatAStyleAsBodyText_WhenItsOutlineLevelIsNine()
    {
        var docx = new TestDocx
        {
            Styles = """
                <w:style w:type="paragraph" w:styleId="Heading1"><w:pPr><w:outlineLvl w:val="0"/></w:pPr></w:style>
                <w:style w:type="paragraph" w:styleId="TOCHeading"><w:basedOn w:val="Heading1"/><w:pPr><w:outlineLvl w:val="9"/></w:pPr></w:style>
                """,
            Body = """<w:p><w:pPr><w:pStyle w:val="TOCHeading"/></w:pPr></w:p>""",
        };

        Assert.Null(Assert.Single(docx.Load().Styles).Paragraph.HeadingLevel);
    }

    [Fact]
    public void Load_ShouldUseTheThemeFont_WhenARunNamesBothAFontAndAThemeFont()
    {
        var docx = new TestDocx
        {
            ThemeFonts = ("Cambria", "Calibri"),
            Body = """<w:p><w:r><w:rPr><w:rFonts w:ascii="Arial" w:asciiTheme="majorHAnsi"/></w:rPr><w:t>Heading</w:t></w:r></w:p>""",
        };

        Assert.Equal("Cambria", Assert.Single(Assert.Single(docx.Load().Paragraphs()).Spans).Format.Font);
    }

    [Fact]
    public void Load_ShouldGiveStylesTheDocumentsDefaultLayout_WhenTheyDontSetTheirOwn()
    {
        var docx = new TestDocx
        {
            Styles = """
                <w:docDefaults><w:pPrDefault><w:pPr><w:spacing w:after="160"/></w:pPr></w:pPrDefault></w:docDefaults>
                <w:style w:type="paragraph" w:styleId="Normal" w:default="1"/>
                """,
            Body = "<w:p/>",
        };

        Assert.Equal(8, Assert.Single(docx.Load().Styles).Paragraph.SpacingAfter);
    }

    [Theory]
    [InlineData("<w:vanish/>", "hidden")]
    [InlineData("<w:dstrike/>", "double-strike")]
    [InlineData("<w:rtl/>", "rtl")]
    [InlineData("<w:lang w:val=\"fr-FR\"/>", "lang=fr-FR")]
    public void ToSnapshot_ShouldPrintASpan_WhenARunIsHiddenDoubleStruckRightToLeftOrInAnotherLanguage(string properties,
        string expected)
    {
        var docx = new TestDocx
        {
            Body = $"<w:p><w:r><w:t xml:space=\"preserve\">Walk with </w:t></w:r><w:r><w:rPr>{properties}</w:rPr><w:t>care</w:t></w:r></w:p>",
        };

        Assert.Contains($"    span \"care\" {expected}\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void Load_ShouldTurnHiddenTextOff_WhenACharacterStyleHidesTextTheParagraphStyleAlreadyHides()
    {
        var docx = new TestDocx
        {
            Styles = """
                <w:style w:type="paragraph" w:styleId="Notes"><w:rPr><w:vanish/></w:rPr></w:style>
                <w:style w:type="character" w:styleId="Shown"><w:rPr><w:vanish/></w:rPr></w:style>
                """,
            Body = """<w:p><w:pPr><w:pStyle w:val="Notes"/></w:pPr><w:r><w:rPr><w:rStyle w:val="Shown"/></w:rPr><w:t>Hi</w:t></w:r></w:p>""",
        };

        Assert.False(Assert.Single(Assert.Single(docx.Load().Paragraphs()).Spans).Format.Hidden);
    }

    [Fact]
    public void ToSnapshot_ShouldPrintAStylesLanguage_WhenItIsntTheDocuments()
    {
        var docx = new TestDocx
        {
            Styles = """
                <w:docDefaults><w:rPrDefault><w:rPr><w:lang w:val="en-US"/></w:rPr></w:rPrDefault></w:docDefaults>
                <w:style w:type="paragraph" w:styleId="Normal" w:default="1"/>
                <w:style w:type="paragraph" w:styleId="French"><w:rPr><w:lang w:val="fr-FR"/></w:rPr></w:style>
                <w:style w:type="character" w:styleId="Strong"><w:rPr><w:b/></w:rPr></w:style>
                """,
            Body = """<w:p/><w:p><w:pPr><w:pStyle w:val="French"/></w:pPr><w:r><w:rPr><w:rStyle w:val="Strong"/></w:rPr><w:t>Oui</w:t></w:r></w:p>""",
        };

        var snapshot = docx.Load().ToSnapshot();

        Assert.Contains("  Normal paragraph default\n", snapshot);
        Assert.Contains("  French paragraph lang=fr-FR\n", snapshot);
        Assert.Contains("  Strong character bold\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldShowAColumnBreak_WhenAParagraphHasOne()
    {
        var docx = new TestDocx { Body = """<w:p><w:r><w:t>Left</w:t><w:br w:type="column"/><w:t>Right</w:t></w:r></w:p>""" };

        Assert.Contains("    break column at=4\n", docx.Load().ToSnapshot());
    }
}
