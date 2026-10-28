using System.Text;

namespace PDiffy.Tests.Unit.Docx;

public class DocxHeaderAndFieldTests
{
    private const string PageField = """
        <w:p><w:r><w:t xml:space="preserve">Page </w:t></w:r><w:r><w:fldChar w:fldCharType="begin"/></w:r>
          <w:r><w:instrText xml:space="preserve"> PAGE </w:instrText></w:r><w:r><w:instrText>  \* MERGEFORMAT </w:instrText></w:r>
          <w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:t>3</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r></w:p>
        """;

    [Fact]
    public void ToSnapshot_ShouldShowEachSectionsHeadersWithTheirFields_WhenSectionsNameHeaders()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:p><w:r><w:t>Body</w:t></w:r></w:p>
                <w:sectPr><w:headerReference w:type="first" r:id="cover"/><w:headerReference w:type="default" r:id="running"/></w:sectPr>
                """,
        }.WithHeader("running", PageField).WithHeader("cover", "<w:p><w:r><w:t>Cover</w:t></w:r></w:p>");

        var snapshot = docx.Load().ToSnapshot();

        Assert.EndsWith("""
            headers
              s1 default
                P "Page 3"
                  field "PAGE \\* MERGEFORMAT" result="3" at=5
              s1 first
                P "Cover"
            footers
              (none)

            """.ReplaceLineEndings("\n"), snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldSayTheHeaderIsMissing_WhenItsPartCantBeFound()
    {
        var docx = new TestDocx { Body = """<w:p/><w:sectPr><w:headerReference w:type="default" r:id="gone"/></w:sectPr>""" };

        Assert.Contains("headers\n  s1 default (missing)\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void Load_ShouldReadASimpleFieldsCodeAndResult_WhenTheFieldIsOneElement()
    {
        var docx = new TestDocx { Body = """<w:p><w:fldSimple w:instr=" NUMPAGES "><w:r><w:t>9</w:t></w:r></w:fldSimple></w:p>""" };

        var paragraph = Assert.Single(docx.Load().Paragraphs());

        Assert.Equal(("9", new DocxField("NUMPAGES", "9", 0)), (paragraph.Text, Assert.Single(paragraph.Inlines)));
    }

    [Fact]
    public void Load_ShouldListTheOuterFieldFirst_WhenFieldsAreNested()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:p><w:r><w:fldChar w:fldCharType="begin"/><w:instrText>IF </w:instrText><w:fldChar w:fldCharType="begin"/>
                  <w:instrText>PAGE</w:instrText><w:fldChar w:fldCharType="separate"/><w:t>1</w:t><w:fldChar w:fldCharType="end"/>
                  <w:instrText> = 1 "First"</w:instrText><w:fldChar w:fldCharType="separate"/><w:t>First</w:t><w:fldChar w:fldCharType="end"/></w:r></w:p>
                """,
        };

        var paragraph = Assert.Single(docx.Load().Paragraphs());

        Assert.Equal("First", paragraph.Text);
        Assert.Equal([new DocxField("IF 1 = 1 \"First\"", "First", 0), new DocxField("PAGE", "1", 0)], paragraph.Inlines);
    }

    [Fact]
    public void Load_ShouldRecordAFieldWithWhatItHasSoFar_WhenItRunsOnIntoTheNextParagraph()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:p><w:r><w:fldChar w:fldCharType="begin"/><w:instrText>TOC \o "1-3"</w:instrText><w:fldChar w:fldCharType="separate"/>
                  <w:t>Introduction</w:t></w:r></w:p>
                <w:p><w:r><w:t>Summary</w:t><w:fldChar w:fldCharType="end"/></w:r></w:p>
                """,
        };

        var paragraphs = docx.Load().Paragraphs().ToList();

        Assert.Equal(new DocxField("TOC \\o \"1-3\"", "Introduction", 0), Assert.Single(paragraphs[0].Inlines));
        Assert.Empty(paragraphs[1].Inlines);
    }

    [Fact]
    public void ToSnapshot_ShouldReadAnInsertedDocumentInPlace_WhenItIsADocx()
    {
        var inserted = new TestDocx { Body = "<w:p><w:r><w:t>From the inserted file</w:t></w:r></w:p>" }.Build().ToArray();
        var docx = new TestDocx { Body = """<w:p><w:r><w:t>Before</w:t></w:r></w:p><w:altChunk r:id="chunk"/>""" }
            .WithPart("chunk", "aFChunk", "inserted.docx", inserted);

        var document = docx.Load();

        Assert.Contains("  P \"Before\"\n  Inserted docx\n    styles\n      (none)\n    numbering\n      (none)\n    body\n"
            + "      P \"From the inserted file\"\n", document.ToSnapshot());
        Assert.Equal(["Before", "From the inserted file"], document.Paragraphs().Select(paragraph => paragraph.Text));
    }

    [Theory]
    [InlineData("inserted.html", "<p>Hello</p>", "Inserted html hash=")]
    [InlineData("inserted.docx", "not a zip", "Inserted docx unreadable hash=")]
    public void ToSnapshot_ShouldRecordTheInsertedFileByHash_WhenItIsNotADocxThatCanBeRead(string path, string content, string expected)
    {
        var docx = new TestDocx { Body = """<w:altChunk r:id="chunk"/>""" }.WithPart("chunk", "aFChunk", path, Encoding.UTF8.GetBytes(content));

        Assert.Contains($"  {expected}", docx.Load().ToSnapshot());
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void Load_ShouldReadInsertedDocumentsFiveLevelsDeep_WhenTheyNest(int levels, bool readsTheDeepest)
    {
        var innermost = new TestDocx { Body = "<w:p><w:r><w:t>Deepest</w:t></w:r></w:p>" }.Build().ToArray();
        for (var level = 0; level < levels; level++)
        {
            innermost = new TestDocx { Body = """<w:altChunk r:id="chunk"/>""" }.WithPart("chunk", "aFChunk", "inner.docx", innermost).Build().ToArray();
        }

        var snapshot = DocxDocument.Load(new MemoryStream(innermost)).ToSnapshot();

        Assert.Equal(readsTheDeepest, snapshot.Contains("Deepest"));
    }

    [Fact]
    public void ToSnapshot_ShouldSayTheInsertedFileIsMissing_WhenItsPartIsNotInTheFile()
    {
        var docx = new TestDocx { Body = """<w:altChunk r:id="chunk"/>""" }.WithPart("chunk", "aFChunk", "inserted.docx", bytes: null);

        Assert.Contains("  Inserted docx hash=(missing)\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void Load_ShouldListAnInsertedDocumentsUnreadableParts_WithTheDocumentsOwn()
    {
        var broken = new TestDocx { Styles = "" };
        broken.RawParts["word/styles.xml"] = "<w:styles";
        var docx = new TestDocx { Body = """<w:altChunk r:id="chunk"/>""" }.WithPart("chunk", "aFChunk", "inserted.docx", broken.Build().ToArray());

        Assert.Equal(["word/inserted.docx!word/styles.xml"], docx.Load().Document.UnreadableParts);
    }

    [Fact]
    public void ToSnapshot_ShouldNumberEachSectionsFooters_WhenTheDocumentHasTwoSections()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:p><w:pPr><w:sectPr><w:footerReference w:type="default" r:id="first"/></w:sectPr></w:pPr></w:p>
                <w:sectPr><w:footerReference w:type="default" r:id="second"/></w:sectPr>
                """,
        }.WithPart("first", "footer", "first.xml", Footer("One")).WithPart("second", "footer", "second.xml", Footer("Two"));

        Assert.EndsWith("footers\n  s1 default\n    P \"One\"\n  s2 default\n    P \"Two\"\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void Load_ShouldListHeaderImages_WhenListingTheDocumentsImages()
    {
        var picture = """<w:p><w:r><w:pict xmlns:v="urn:schemas-microsoft-com:vml"><v:shape alt="Logo"/></w:pict></w:r></w:p>""";
        var docx = new TestDocx { Body = """<w:p/><w:sectPr><w:headerReference w:type="default" r:id="top"/></w:sectPr>""" }
            .WithHeader("top", picture);

        Assert.Equal("Logo", Assert.Single(docx.Load().Images()).AltText);
    }

    [Fact]
    public void Load_ShouldCallALinkedPictureAnImage_WhenItsBlipLinksRatherThanEmbeds()
    {
        var drawing = """
            <w:p><w:r><w:drawing xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><wp:inline><wp:extent cx="12700" cy="12700"/>
              <a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><a:blip r:link="far"/></a:graphicData>
              </a:graphic></wp:inline></w:drawing></w:r></w:p>
            """;

        Assert.Equal("image", Assert.Single(new TestDocx { Body = drawing }.Load().Images()).Kind);
    }

    private static byte[] Footer(string text) => Encoding.UTF8.GetBytes(
        "<w:ftr xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">"
        + $"<w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:ftr>");

    [Fact]
    public void ToSnapshot_ShouldNameAVmlShapeWithoutAPictureAShape_WhenItHasNoImageData()
    {
        var shape = """<w:p><w:r><w:pict xmlns:v="urn:schemas-microsoft-com:vml"><v:shape style="width:72pt;height:36pt"/></w:pict></w:r></w:p>""";

        Assert.Contains("    shape inline size=72x36 alt=(missing) at=0\n", new TestDocx { Body = shape }.Load().ToSnapshot());
    }
}
