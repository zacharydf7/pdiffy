using System.Security.Cryptography;

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

    private static string Paragraph(string text, string inline) =>
        $"<w:p><w:r><w:t xml:space=\"preserve\">{text}</w:t>{inline}</w:r></w:p>";
}
