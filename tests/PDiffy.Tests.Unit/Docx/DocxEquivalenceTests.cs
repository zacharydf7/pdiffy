using System.IO.Compression;

namespace PDiffy.Tests.Unit.Docx;

// Each test builds two DOCX files that differ only in how they're packaged or written, and checks they give the same
// snapshot. The files themselves are checked to differ, so a builder option that stopped working can't pass a test.
public class DocxEquivalenceTests
{
    private static readonly byte[] Logo = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenTheZipsEntriesAreInAnotherOrder()
    {
        AssertSameSnapshot(Sample("logo", "site"), Sample("logo", "site", reverseZipOrder: true));
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenTheZipIsCompressedDifferently()
    {
        AssertSameSnapshot(Sample("logo", "site"), Sample("logo", "site", compression: CompressionLevel.NoCompression));
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenRelationshipIdsDiffer()
    {
        AssertSameSnapshot(Sample("logo", "site"), Sample("rId7", "rId12"));
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenEditingSessionIdsDiffer()
    {
        static TestDocx Paragraph(string rsid) => new()
        {
            Body = $"""<w:p w:rsidR="{rsid}" w:rsidRDefault="{rsid}"><w:r w:rsidRPr="{rsid}"><w:t>Go with a guide</w:t></w:r></w:p>""",
        };

        AssertSameSnapshot(Paragraph("00A1B2C3"), Paragraph("00D4E5F6"));
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenTheSameTextIsSplitAcrossRunsThatLookTheSame()
    {
        var oneRun = new TestDocx { Body = "<w:p><w:r><w:rPr><w:b/></w:rPr><w:t>Go with a guide</w:t></w:r></w:p>" };
        var threeRuns = new TestDocx
        {
            Body = """
                <w:p><w:r><w:rPr><w:b/></w:rPr><w:t xml:space="preserve">Go </w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>with</w:t></w:r>
                  <w:r><w:rPr><w:b/></w:rPr><w:t xml:space="preserve"> a guide</w:t></w:r></w:p>
                """,
        };

        AssertSameSnapshot(oneRun, threeRuns);
    }

    // A paragraph with an image and a link, so relationship ids and parts beyond the document are involved.
    private static TestDocx Sample(string imageId, string linkId, bool reverseZipOrder = false,
        CompressionLevel compression = CompressionLevel.Optimal) => new TestDocx
    {
        ReverseZipOrder = reverseZipOrder,
        ZipCompression = compression,
        Body = $"""
            <w:p><w:hyperlink r:id="{linkId}"><w:r><w:t>example.com</w:t></w:r></w:hyperlink>
              <w:r><w:drawing xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                  xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
                <wp:inline><wp:extent cx="1524000" cy="508000"/><wp:docPr id="1" name="Picture 1" descr="Logo"/>
                  <a:graphic><a:graphicData><a:blip r:embed="{imageId}"/></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>
            """,
    }.WithImage(imageId, "image1.png", Logo).WithLink(linkId, "https://example.com");

    private static void AssertSameSnapshot(TestDocx first, TestDocx second)
    {
        Assert.NotEqual(first.Build().ToArray(), second.Build().ToArray());
        Assert.Equal(first.Load().ToSnapshot(), second.Load().ToSnapshot());
    }
}
