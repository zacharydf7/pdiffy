using System.Security.Cryptography;
using System.Text;

namespace PDiffy.Tests.Unit.Docx;

public class DocxInlineTests
{
    private static readonly byte[] Logo = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    private const string Drawing = """
        <w:drawing xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                   xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
          <wp:{0}><wp:extent cx="1524000" cy="508000"/><wp:docPr id="1" name="Picture 1" {1}/>
            <a:graphic><a:graphicData><a:blip r:embed="logo"/></a:graphicData></a:graphic></wp:{0}></w:drawing>
        """;

    [Fact]
    public void ToSnapshot_ShouldDescribeTheImage_WhenAParagraphHasOne()
    {
        var docx = new TestDocx { Body = Paragraph("Logo: ", string.Format(Drawing, "inline", "descr=\"Pharmacy logo\" title=\"Logo\"")) }
            .WithImage("logo", "image1.png", Logo);

        var hash = Convert.ToHexStringLower(SHA256.HashData(Logo))[..16];
        Assert.Contains($"    image inline size=120x40 alt=\"Pharmacy logo\" title=\"Logo\" type=png hash={hash} at=6\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldSayTheAltTextIsMissing_WhenAFloatingImageHasNone()
    {
        var docx = new TestDocx { Body = Paragraph("", string.Format(Drawing, "anchor", "")) }.WithImage("logo", "image1.png", Logo);

        Assert.Contains("    image floating size=120x40 alt=(missing) type=png", docx.Load().ToSnapshot());
    }

    [Fact]
    public void Load_ShouldMarkTheImageDecorative_WhenItsPropertiesSaySo()
    {
        var decorative = """name="Picture 1"><a:extLst xmlns:adec="http://schemas.microsoft.com/office/drawing/2017/decorative">"""
            + """<a:ext><adec:decorative val="1"/></a:ext></a:extLst></wp:docPr>""";
        var drawing = string.Format(Drawing, "inline", "").Replace("name=\"Picture 1\" />", decorative);
        Assert.Contains("adec:decorative", drawing);
        var docx = new TestDocx { Body = Paragraph("", drawing) }.WithImage("logo", "image1.png", Logo);

        Assert.True(Assert.Single(docx.Load().Images()).Decorative);
    }

    [Fact]
    public void Load_ShouldFindImagesInsideTables_WhenListingTheDocumentsImages()
    {
        var cell = Paragraph("", string.Format(Drawing, "inline", "descr=\"Chart\""));
        var docx = new TestDocx { Body = $"<w:tbl><w:tr><w:tc>{cell}</w:tc></w:tr></w:tbl>" }.WithImage("logo", "image1.png", Logo);

        Assert.Equal("Chart", Assert.Single(docx.Load().Images()).AltText);
    }

    [Fact]
    public void ToSnapshot_ShouldShowWhereEachLinkGoes_WhenTextLinksOutOrToABookmark()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:p><w:bookmarkStart w:id="0" w:name="_GoBack"/><w:bookmarkStart w:id="1" w:name="Top"/><w:r><w:t>Visit </w:t></w:r>
                  <w:hyperlink r:id="site"><w:r><w:t>example.com</w:t></w:r></w:hyperlink><w:r><w:t> or go </w:t></w:r>
                  <w:hyperlink w:anchor="Top"><w:r><w:t>back up</w:t></w:r></w:hyperlink></w:p>
                """,
        }.WithLink("site", "https://example.com");

        var snapshot = docx.Load().ToSnapshot();

        Assert.Contains("""
              P "Visit example.com or go back up"
                bookmark "Top" at=0
                link "example.com" -> https://example.com at=6
                link "back up" -> #Top at=24
            """.ReplaceLineEndings("\n"), snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldKeepTheSymbolInTheTextAndNameItsFont_WhenARunHasASymbol()
    {
        var docx = new TestDocx { Body = """<w:p><w:r><w:t>Done </w:t><w:sym w:font="Wingdings" w:char="f0fc"/></w:r></w:p>""" };

        Assert.Contains("  P \"Done \\uf0fc\"\n    symbol font=\"Wingdings\" code=F0FC at=5\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void Load_ShouldReadALegacyPicture_WhenTheImageIsVml()
    {
        var picture = """
            <w:pict xmlns:v="urn:schemas-microsoft-com:vml" xmlns:o="urn:schemas-microsoft-com:office:office">
              <v:shape style="width:72pt;height:36pt" alt="Signature"><v:imagedata r:id="logo" o:title="Sig"/></v:shape></w:pict>
            """;
        var docx = new TestDocx { Body = Paragraph("", picture) }.WithImage("logo", "image1.png", Logo);

        var image = Assert.Single(docx.Load().Images());

        Assert.Equal((72.0, 36.0, "Signature", "Sig", "png"), (image.Width, image.Height, image.AltText, image.Title, image.Type));
    }

    [Fact]
    public void ToSnapshot_ShouldSayTheTargetIsMissing_WhenALinksAddressCantBeFound()
    {
        var docx = new TestDocx { Body = """<w:p><w:hyperlink r:id="gone" w:anchor="Top"><w:r><w:t>broken</w:t></w:r></w:hyperlink></w:p>""" };

        Assert.Contains("    link \"broken\" -> (missing) at=0\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldNameTheGraphicsKind_WhenADrawingIsAChartRatherThanAPicture()
    {
        var chart = string.Format(Drawing, "inline", "descr=\"Sales\"")
            .Replace("<a:graphicData><a:blip r:embed=\"logo\"/></a:graphicData>", "<a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/chart\"/>");
        var docx = new TestDocx { Body = Paragraph("", chart) };

        Assert.Contains("    chart inline size=120x40 alt=\"Sales\" at=0\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void Load_ShouldSeeALegacyPictureAsFloating_WhenItIsPositionedAbsolutely()
    {
        var picture = """
            <w:pict xmlns:v="urn:schemas-microsoft-com:vml"><v:shape style="position:absolute;width:72pt;height:36pt"/></w:pict>
            """;

        Assert.True(Assert.Single(new TestDocx { Body = Paragraph("", picture) }.Load().Images()).Floating);
    }

    [Fact]
    public void ToSnapshot_ShouldNestTheTextBoxParagraphsUnderItsShape_WhenWordOffersTheShapeAsAlternateContent()
    {
        var textBox = """
            <mc:AlternateContent xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006">
              <mc:Choice Requires="wps"><w:drawing xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                  xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                  xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape">
                <wp:anchor><wp:extent cx="1270000" cy="635000"/><wp:docPr id="1" name="Text Box 1"/>
                  <a:graphic><a:graphicData uri="http://schemas.microsoft.com/office/word/2010/wordprocessingShape">
                    <wps:wsp><wps:txbx><w:txbxContent><w:p><w:r><w:t>Inside the box</w:t></w:r></w:p></w:txbxContent></wps:txbx></wps:wsp>
                  </a:graphicData></a:graphic></wp:anchor></w:drawing></mc:Choice>
              <mc:Fallback><w:pict xmlns:v="urn:schemas-microsoft-com:vml"><v:shape style="width:100pt;height:50pt"/></w:pict></mc:Fallback>
            </mc:AlternateContent>
            """;

        var document = new TestDocx { Body = Paragraph("Note: ", textBox) }.Load();

        Assert.Contains("    shape floating size=100x50 alt=(missing) at=6\n      P \"Inside the box\"\n", document.ToSnapshot());
        Assert.Single(document.Images());
        Assert.Contains(document.Paragraphs(), paragraph => paragraph.Text == "Inside the box");
    }

    [Fact]
    public void ToSnapshot_ShouldRecordTheEquationsTextAndAHashOfItsLayout_WhenAParagraphHasOne()
    {
        const string Math = "xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\"";
        var power = $"<w:p><m:oMath {Math}><m:sSup><m:e><m:r><m:t>r</m:t></m:r></m:e>"
            + "<m:sup><m:r><m:t>2</m:t></m:r></m:sup></m:sSup></m:oMath></w:p>";
        var plain = $"<w:p><m:oMath {Math}><m:r><m:t>r2</m:t></m:r></m:oMath></w:p>";

        var (math, plainMath) = (Single<DocxMath>(power), Single<DocxMath>(plain));

        Assert.Equal(("r2", "r2"), (math.Text, plainMath.Text));
        Assert.NotEqual(math.Hash, plainMath.Hash);
        Assert.Contains($"    math \"r2\" hash={math.Hash} at=0\n", new TestDocx { Body = power }.Load().ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldDescribeTheObject_WhenARunEmbedsOne()
    {
        byte[] sheet = [0x50, 0x4B, 3, 4, 9];
        var embedded = """
            <w:object xmlns:v="urn:schemas-microsoft-com:vml" xmlns:o="urn:schemas-microsoft-com:office:office">
              <v:shape style="width:144pt;height:72pt"/><o:OLEObject ProgID="Excel.Sheet.12" r:id="sheet"/></w:object>
            """;
        var docx = new TestDocx { Body = Paragraph("", embedded) }.WithPart("sheet", "oleObject", "embeddings/sheet1.xlsx", sheet);

        var hash = Convert.ToHexStringLower(SHA256.HashData(sheet))[..16];
        Assert.Contains($"    object inline size=144x72 alt=(missing) type=Excel.Sheet.12 hash={hash} at=0\n",
            docx.Load().ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldNameTheControlAndHashItsData_WhenARunHasAnActiveXControl()
    {
        var docx = ActiveX("checked=1");

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ControlPart + "checked=1")))[..16];
        Assert.Contains($"    control inline size=72x18 alt=(missing) type=CheckBox1 hash={hash} at=0\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldChange_WhenOnlyTheStateInAnActiveXControlsBinaryPartChanges()
    {
        var (unticked, ticked) = (ActiveX("checked=0").Load(), ActiveX("checked=1").Load());

        Assert.NotEqual(unticked.ToSnapshot(), ticked.ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldListTheControlPartAsUnreadable_WhenItIsNotXml()
    {
        var control = """<w:object><w:control r:id="box" w:name="CheckBox1"/></w:object>""";
        var docx = new TestDocx { Body = Paragraph("", control) }.WithPart("box", "control", "activeX/activeX1.xml", [1, 2, 3]);

        Assert.Contains("unreadable word/activeX/activeX1.xml", docx.Load().ToSnapshot());
    }

    // Word keeps a control's settings in a small XML part, and its state in the binary part that one points to.
    private const string ControlPart = """<ax:ocx xmlns:ax="http://schemas.microsoft.com/office/2006/activeX" """
        + """xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" """
        + """ax:classid="{8BD21D40-EC42-11CE-9E0D-00AA006002F3}" ax:persistence="persistStorage" r:id="rId1"/>""";

    private static TestDocx ActiveX(string state)
    {
        var control = """
            <w:object xmlns:v="urn:schemas-microsoft-com:vml"><v:shape style="width:72pt;height:18pt"/>
              <w:control r:id="box" w:name="CheckBox1"/></w:object>
            """;
        var docx = new TestDocx { Body = Paragraph("", control) }
            .WithPart("box", "control", "activeX/activeX1.xml", Encoding.UTF8.GetBytes(ControlPart));
        docx.RawParts["word/activeX/_rels/activeX1.xml.rels"] =
            """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">"""
            + """<Relationship Id="rId1" Type="http://schemas.microsoft.com/office/2006/relationships/activeXControlBinary" """
            + """Target="activeX1.bin"/></Relationships>""";
        docx.RawParts["word/activeX/activeX1.bin"] = state;
        return docx;
    }

    [Fact]
    public void ToSnapshot_ShouldRecordTheLinkedFile_WhenAnObjectIsLinkedRatherThanEmbedded()
    {
        var linked = """
            <w:object xmlns:v="urn:schemas-microsoft-com:vml" xmlns:o="urn:schemas-microsoft-com:office:office">
              <v:shape style="width:144pt;height:72pt"/><o:OLEObject Type="Link" ProgID="Excel.Sheet.12" r:id="sheet"/></w:object>
            """;
        var docx = new TestDocx { Body = Paragraph("", linked) }.WithLink("sheet", "file:///C:/data/sheet.xlsx");

        Assert.Contains("    object inline size=144x72 alt=(missing) type=Excel.Sheet.12 source=\"file:///C:/data/sheet.xlsx\" at=0\n",
            docx.Load().ToSnapshot());
    }

    [Fact]
    public void Load_ShouldKeepAPictureInsideATextBoxApartFromTheShape_WhenTheShapeHoldsOne()
    {
        var picture = string.Format(Drawing, "inline", "descr=\"Logo\"");
        var shape = $"""
            <w:drawing xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape">
              <wp:anchor><wp:extent cx="1270000" cy="635000"/><wp:docPr id="2" name="Text Box 2"/>
                <a:graphic><a:graphicData uri="http://schemas.microsoft.com/office/word/2010/wordprocessingShape">
                  <wps:wsp><wps:txbx><w:txbxContent>{Paragraph("", picture)}</w:txbxContent></wps:txbx></wps:wsp>
                </a:graphicData></a:graphic></wp:anchor></w:drawing>
            """;

        var images = new TestDocx { Body = Paragraph("", shape) }.WithImage("logo", "image1.png", Logo).Load().Images().ToList();

        Assert.Equal([("shape", null), ("image", "Logo")], images.Select(image => (image.Kind, image.AltText)));
    }

    [Fact]
    public void Load_ShouldReadTheFirstChoiceAndRightToLeftRuns_WhenAParagraphWrapsRunsInThem()
    {
        var body = """
            <w:p><mc:AlternateContent xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006">
              <mc:Choice Requires="w14"><w:r><w:t xml:space="preserve">New </w:t></w:r></mc:Choice>
              <mc:Fallback><w:r><w:t>Old </w:t></w:r></mc:Fallback></mc:AlternateContent>
              <w:dir w:val="rtl"><w:r><w:t>right</w:t></w:r></w:dir><w:bdo w:val="ltr"><w:r><w:t xml:space="preserve"> left</w:t></w:r></w:bdo></w:p>
            """;

        Assert.Equal("New right left", new TestDocx { Body = body }.Load().Paragraphs().Single().Text);
    }

    [Fact]
    public void ToSnapshot_ShouldNestTheParagraphsUnderTheShape_WhenALegacyVmlShapeHasATextBox()
    {
        var picture = """
            <w:pict xmlns:v="urn:schemas-microsoft-com:vml"><v:shape style="width:100pt;height:50pt">
              <v:textbox><w:txbxContent><w:p><w:r><w:t>Boxed</w:t></w:r></w:p></w:txbxContent></v:textbox></v:shape></w:pict>
            """;

        Assert.Contains("    shape inline size=100x50 alt=(missing) at=0\n      P \"Boxed\"\n",
            new TestDocx { Body = Paragraph("", picture) }.Load().ToSnapshot());
    }

    [Fact]
    public void Load_ShouldReadADisplayedEquationAsOne_WhenItIsAnEquationParagraph()
    {
        var body = """
            <w:p><m:oMathPara xmlns:m="http://schemas.openxmlformats.org/officeDocument/2006/math"><m:oMath><m:r><m:t>a=b</m:t></m:r></m:oMath>
              </m:oMathPara></w:p>
            """;

        Assert.Equal("a=b", Single<DocxMath>(body).Text);
    }

    [Fact]
    public void Load_ShouldHashAnEquationTheSameWay_WhenOnlyItsPrefixNamespaceDeclarationOrIndentingDiffers()
    {
        var compact = """<w:p><m:oMath xmlns:m="http://schemas.openxmlformats.org/officeDocument/2006/math"><m:r><m:t>x</m:t></m:r></m:oMath></w:p>""";
        var indented = """
            <w:p xmlns:mm="http://schemas.openxmlformats.org/officeDocument/2006/math">
              <mm:oMath>
                <mm:r><mm:t>x</mm:t></mm:r>
              </mm:oMath>
            </w:p>
            """;

        Assert.Equal(Single<DocxMath>(compact).Hash, Single<DocxMath>(indented).Hash);
    }

    [Fact]
    public void Load_ShouldNameUnsupportedParagraphContentByItsUsualPrefix_WhenTheFileUsesAnother()
    {
        var body = """
            <w:p xmlns:ww="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><ww:subDoc r:id="sub"/>
              <x:note xmlns:x="urn:example"/></w:p>
            """;

        var unsupported = new TestDocx { Body = body }.Load().Paragraphs().Single().Inlines.OfType<DocxUnsupported>();

        Assert.Equal(["w:subDoc", "{urn:example}note"], unsupported.Select(item => item.Element));
    }

    [Fact]
    public void Load_ShouldReadAnAbsoluteTabAsATabAndKeepASymbol_WhenItsCodeIsNotHex()
    {
        var inline = """<w:ptab w:relativeTo="margin" w:alignment="right"/><w:t>b</w:t><w:sym w:font="Wingdings" w:char="zz"/>""";

        var paragraph = new TestDocx { Body = Paragraph("a", inline) }.Load().Paragraphs().Single();

        Assert.Equal("a\tb", paragraph.Text);
        Assert.Equal(new DocxSymbol("Wingdings", "zz", 3), Assert.Single(paragraph.Inlines));
    }

    [Fact]
    public void ToSnapshot_ShouldRecordUnsupportedContentByHash_WhenPDiffyCannotReadIt()
    {
        static string Ruby(string guide, string rsid) => Paragraph("", $"<w:ruby><w:rubyPr/><w:rt><w:r w:rsidR=\"{rsid}\">"
            + $"<w:t>{guide}</w:t></w:r></w:rt><w:rubyBase><w:r><w:t>漢</w:t></w:r></w:rubyBase></w:ruby>");

        var unsupported = Single<DocxUnsupported>(Ruby("kan", "00A1"));
        var (otherSession, otherGuide) = (Single<DocxUnsupported>(Ruby("kan", "00B2")), Single<DocxUnsupported>(Ruby("han", "00A1")));

        Assert.Equal("w:ruby", unsupported.Element);
        Assert.Equal(unsupported.Hash, otherSession.Hash);
        Assert.NotEqual(unsupported.Hash, otherGuide.Hash);
        var snapshot = new TestDocx { Body = Ruby("kan", "00A1") }.Load().ToSnapshot();
        Assert.Contains($"    unsupported w:ruby hash={unsupported.Hash} at=0\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldPrintNoUnsupportedLines_WhenAParagraphHasOnlyMarkersWordAddsWhileEditing()
    {
        var body = """
            <w:p><w:bookmarkStart w:id="0" w:name="_GoBack"/><w:proofErr w:type="spellStart"/><w:commentRangeStart w:id="1"/>
              <w:r><w:lastRenderedPageBreak/><w:t>Plain</w:t></w:r><w:proofErr w:type="spellEnd"/><w:commentRangeEnd w:id="1"/>
              <w:bookmarkEnd w:id="0"/></w:p>
            """;

        Assert.DoesNotContain("unsupported", new TestDocx { Body = body }.Load().ToSnapshot());
    }

    private static T Single<T>(string body) where T : DocxInline =>
        new TestDocx { Body = body }.Load().Paragraphs().Single().Inlines.OfType<T>().Single();

    private static string Paragraph(string text, string inline) =>
        $"<w:p><w:r><w:t xml:space=\"preserve\">{text}</w:t>{inline}</w:r></w:p>";
}
