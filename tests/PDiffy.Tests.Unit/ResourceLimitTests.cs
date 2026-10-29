using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using PDiffy.Tests.Unit.Docx;
using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

/// <summary>Files built to exhaust the stack or memory. Each must load, with what PDiffy didn't read marked as such.</summary>
public class ResourceLimitTests
{
    [Theory]
    [InlineData(256, 0)]
    [InlineData(257, 1)]
    [InlineData(5000, 1)]
    public void Load_ShouldCountTheChildrenItLeavesOut_WhenStructureNestsMoreThan256Deep(int depth, int unreadChildren)
    {
        var elements = Enumerable.Range(0, depth).Select(i => i == depth - 1
            ? "<< /Type /StructElem /S /P /Pg 3 0 R /K 0 >>" : $"<< /Type /StructElem /S /Div /K [{8 + i} 0 R] >>");

        var document = OnePage("/StructTreeRoot 6 0 R", ["<< /Type /StructTreeRoot /K [7 0 R] >>", .. elements]);

        var deepest = document.Structure[0];
        for (var level = 1; level < Math.Min(depth, 256); level++) deepest = Assert.Single(deepest.Children);
        Assert.Equal((unreadChildren, unreadChildren == 0 ? "P" : "Div"), (deepest.UnreadChildren, deepest.Type));
        Assert.Equal(unreadChildren > 0, document.ToSnapshot().Contains(" unread-children=1"));
        Assert.Equal(unreadChildren > 0, document.Untagged.Any(text => text.Text == "Deep"));
    }

    [Theory]
    [InlineData(256, false)]
    [InlineData(257, true)]
    [InlineData(5000, true)]
    public void Load_ShouldMarkTheBookmarksUnreadable_WhenTheyNestMoreThan256Deep(int depth, bool unreadable)
    {
        var items = Enumerable.Range(0, depth).Select(i => i == depth - 1 ? $"<< /Title (b{i}) /Parent {6 + i} 0 R /Dest [3 0 R /Fit] >>"
            : $"<< /Title (b{i}) /Parent {6 + i} 0 R /Dest [3 0 R /Fit] /First {8 + i} 0 R /Last {8 + i} 0 R /Count 1 >>");

        var document = OnePage("/Outlines 6 0 R", ["<< /Type /Outlines /First 7 0 R /Last 7 0 R /Count 1 >>", .. items]);

        Assert.Equal((unreadable, !unreadable), (document.BookmarksUnreadable, document.Bookmarks.Count == 1));
        Assert.Equal(unreadable, document.ToSnapshot().Contains("bookmarks\n  unreadable\n"));
    }

    [Theory]
    [InlineData("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF")]
    [InlineData("<!DOCTYPE x [<!ENTITY a \"Guide\">]><x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF "
        + "xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description xmlns:dc=\"http://purl.org/dc/elements/1.1/\">"
        + "<dc:description>&a;</dc:description></rdf:Description></rdf:RDF></x:xmpmeta>")]
    public void Load_ShouldMarkTheXmpUnreadable_WhenItIsMalformedOrHasADtd(string xmp)
    {
        var document = OnePage("/Metadata 6 0 R", [$"<< /Type /Metadata /Subtype /XML /Length {xmp.Length} >>\nstream\n{xmp}\nendstream"]);

        Assert.Equal((true, null), (document.Metadata.XmpUnreadable, document.Metadata.XmpDescription));
        Assert.Contains("\n  xmp=unreadable\n", document.ToSnapshot());
        Assert.DoesNotContain("pdfuaid-part=", document.ToSnapshot());
    }

    [Fact]
    public void Load_ShouldReadEveryField_WhenFormFieldsNestThousandsDeep()
    {
        var fields = Enumerable.Range(0, 5000)
            .Select(i => i == 4999 ? $"<< /T (f{i}) /FT /Tx >>" : $"<< /T (f{i}) /Kids [{7 + i} 0 R] >>");

        var document = OnePage("/AcroForm << /Fields [6 0 R] >>", [.. fields]);

        Assert.EndsWith(".f4998.f4999", Assert.Single(document.FormFields).Name);
    }

    [Fact]
    public void Load_ShouldReadEveryEntry_WhenANumberTreeNestsThousandsDeep()
    {
        var nodes = Enumerable.Range(0, 5000).Select(i => i == 4999 ? "<< /Nums [0 << /S /D >>] >>" : $"<< /Kids [{7 + i} 0 R] >>");

        var document = OnePage("/PageLabels 6 0 R", [.. nodes]);

        Assert.Equal([new PageLabelRange(1, "D", null, 1)], document.PageLabels);
    }

    [Fact]
    public void Load_ShouldMarkThePageUnreadable_WhenItsContentDecompressesToMoreThan256Megabytes()
    {
        var bomb = Bomb();
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 5 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
            $"<< /Filter /FlateDecode /Length {bomb.Length} >>\nstream\n{bomb}\nendstream",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 6 0 R >>",
            TestPdf.Stream(""));

        Assert.Equal([true, false], document.Pages.Select(page => page.Unreadable));
    }

    [Fact]
    public void Load_ShouldHashTheImageAsStored_WhenItsPixelsDecompressToMoreThan256Megabytes()
    {
        var bomb = Bomb();
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Im1 5 0 R >> >> "
                + "/Contents 4 0 R >>",
            TestPdf.Stream("q 10 0 0 10 72 600 cm /Im1 Do Q"),
            "<< /Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 "
                + $"/Filter /FlateDecode /Length {bomb.Length} >>\nstream\n{bomb}\nendstream");

        var image = Assert.Single(document.Images);

        var stored = Convert.ToHexStringLower(SHA256.HashData(Encoding.Latin1.GetBytes(bomb)))[..16];
        Assert.Equal((false, stored), (document.Pages[0].Unreadable, image.Hash));
    }

    [Fact]
    public void Load_ShouldMarkTheXmpUnreadable_WhenItDecompressesToMoreThan256Megabytes()
    {
        // A root element, so the XMP would parse if the cap didn't stop it.
        var bomb = Bomb(start: "<x/>");

        var document = OnePage("/Metadata 6 0 R",
            [$"<< /Type /Metadata /Subtype /XML /Filter /FlateDecode /Length {bomb.Length} >>\nstream\n{bomb}\nendstream"]);

        Assert.True(document.Metadata.XmpUnreadable);
    }

    [Fact]
    public void Load_ShouldReadTheText_WhenMarkedContentNestsThousandsDeep()
    {
        var content = Nested("/Span << >> BDC ", " EMC", 5000, "BT /F1 12 Tf 72 700 Td (Deep) Tj ET");

        var document = OnePage("", [], content);

        Assert.Equal("Deep", Assert.Single(document.Untagged).Text);
    }

    [Fact]
    public void Load_ShouldReadTheXmpAsUtf8_WhenItHasNonAsciiText()
    {
        var pdf = new TestPdf { Xmp = Encoding.Latin1.GetString(Encoding.UTF8.GetBytes("<dc:description>Café</dc:description>")) };
        pdf.AddPage().Untagged("Text");

        var metadata = pdf.Load().Metadata;

        Assert.Equal((false, "Café"), (metadata.XmpUnreadable, metadata.XmpDescription));
    }

    [Fact]
    public void Load_ShouldListThePartAsUnreadable_WhenItHasADtd()
    {
        var docx = new TestDocx { Styles = "" };
        docx.RawParts["word/styles.xml"] = "<!DOCTYPE w:styles [<!ENTITY a \"aaaa\">]>"
            + "<w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">&a;</w:styles>";

        Assert.Equal(["word/styles.xml"], docx.Load().Document.UnreadableParts);
    }

    [Theory]
    [InlineData(256, false)]
    [InlineData(257, true)]
    public void Load_ShouldListThePartAsUnreadable_WhenItNestsMoreThan256Deep(int depth, bool unreadable)
    {
        var docx = new TestDocx { Styles = Nested("<w:x>", "</w:x>", depth - 1, "") };

        Assert.Equal(unreadable, docx.Load().Document.UnreadableParts.Contains("word/styles.xml"));
    }

    [Fact]
    public void Load_ShouldThrow_WhenTheBodyNestsThousandsDeep()
    {
        var docx = new TestDocx { Body = Nested("<w:sdt><w:sdtContent>", "</w:sdtContent></w:sdt>", 5000, "") };

        Assert.Throws<InvalidDataException>(() => docx.Load());
    }

    // Each table adds three levels: 41 tables in each document put the text 253 deep, 42 put it 259 deep.
    [Theory]
    [InlineData(41, false)]
    [InlineData(42, true)]
    public void Load_ShouldMarkAnInsertedDocumentUnreadable_WhenTogetherTheyNestMoreThan256Deep(int tables, bool unreadable)
    {
        const string Open = "<w:tbl><w:tr><w:tc>", Close = "<w:p/></w:tc></w:tr></w:tbl>";
        var inner = new TestDocx { Body = Nested(Open, Close, tables, "<w:p><w:r><w:t>Deepest</w:t></w:r></w:p>") }.Build().ToArray();
        var docx = new TestDocx { Body = Nested(Open, Close, tables, """<w:altChunk r:id="chunk"/>""") }
            .WithPart("chunk", "aFChunk", "inner.docx", inner);

        var snapshot = docx.Load().ToSnapshot();

        Assert.Equal((unreadable, !unreadable), (snapshot.Contains("Inserted docx unreadable"), snapshot.Contains("\"Deepest\"")));
    }

    [Fact]
    public void Load_ShouldMarkPartsUnreadable_WhenTheyAreLargerThan256Megabytes()
    {
        var docx = new TestDocx { Styles = "", Body = """<w:altChunk r:id="chunk"/>""" }.WithPart("chunk", "aFChunk", "inner.docx", []);

        var document = DocxDocument.Load(WithLargeParts(docx.Build(), "word/styles.xml", "word/inner.docx"));

        Assert.Equal(["word/styles.xml"], document.Document.UnreadableParts);
        Assert.Contains("Inserted docx unreadable", document.ToSnapshot());
    }

    // Objects 1-5 are a one-page document whose page shows "Deep" as MCID 0 unless given other content; extra objects are
    // numbered from 6.
    private static PdfUaDocument OnePage(string catalogEntries, string[] extraObjects,
        string content = "/P << /MCID 0 >> BDC BT /F1 12 Tf 72 700 Td (Deep) Tj ET EMC") => RawPdf.Load(
    [
        $"<< /Type /Catalog /Pages 2 0 R /MarkInfo << /Marked true >> {catalogEntries} >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        TestPdf.Stream(content),
        .. extraObjects,
    ]);

    // The start, then 256 MB and one byte of spaces, Flate-compressed to a few hundred KB: a decompression bomb.
    private static string Bomb(string start = "")
    {
        var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest))
        {
            zlib.Write(Encoding.Latin1.GetBytes(start));
            var spaces = Enumerable.Repeat((byte)' ', 1024 * 1024).ToArray();
            for (var i = 0; i < 256; i++) zlib.Write(spaces);
            zlib.WriteByte((byte)' ');
        }

        return Encoding.Latin1.GetString(compressed.ToArray());
    }

    private static string Nested(string open, string close, int count, string inside) =>
        string.Concat(Enumerable.Repeat(open, count)) + inside + string.Concat(Enumerable.Repeat(close, count));

    // Copies the package with the named parts replaced by 256 MB and one byte of zeros, which compress to a few hundred KB.
    private static MemoryStream WithLargeParts(MemoryStream package, params string[] parts)
    {
        var copy = new MemoryStream();
        using (var source = new ZipArchive(package, ZipArchiveMode.Read))
        using (var target = new ZipArchive(copy, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                using var output = target.CreateEntry(entry.FullName, CompressionLevel.Fastest).Open();
                if (!parts.Contains(entry.FullName))
                {
                    using var input = entry.Open();
                    input.CopyTo(output);
                    continue;
                }

                var zeros = new byte[1024 * 1024];
                for (var i = 0; i < 256; i++) output.Write(zeros);
                output.WriteByte(0);
            }
        }

        copy.Position = 0;
        return copy;
    }
}
