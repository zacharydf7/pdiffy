using System.Text;
using PDiffy.Tests.Unit.Pdfs;
using UglyToad.PdfPig;
using static PDiffy.Tests.Unit.Pdfs.TestFonts;

namespace PDiffy.Tests.Unit;

// Each test builds two PDFs that differ only in how they're encoded, and checks they give the same snapshot. The files
// are checked to differ and to open with strict parsing, so a builder option that broke or stopped working fails a test.
public class EquivalenceTests
{
    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenContentImagesAndMetadataAreCompressed()
    {
        var compressed = Sample(compress: true);

        AssertSameSnapshot(Sample(), compressed);
        var pdf = Encoding.Latin1.GetString(compressed.Build());
        Assert.Equal(4, pdf.Split("/FlateDecode").Length - 1);
        Assert.DoesNotContain("/ASCIIHexDecode", pdf);
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenObjectsAreWrittenInAnotherOrder()
    {
        AssertSameSnapshot(Sample(), Sample(layout: new PdfLayout(ReverseOrder: true)));
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenObjectsAreNumberedDifferently()
    {
        AssertSameSnapshot(Sample(), Sample(layout: new PdfLayout(FirstNumber: 101)));
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenACrossReferenceStreamReplacesTheTable()
    {
        AssertSameSnapshot(Sample(), Sample(layout: new PdfLayout(CrossReferenceStream: true)));
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenAnUpdateRewritesAnObjectUnchanged()
    {
        AssertSameSnapshot(Sample(), Sample(layout: new PdfLayout(EmptyUpdate: true)));
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenTheFontSubsetPrefixDiffers()
    {
        AssertSameSnapshot(Sample(baseFont: "ABCDEF+Helvetica"), Sample(baseFont: "GHIJKL+Helvetica"));
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenDatesDocumentIdsAndProducerDiffer()
    {
        var first = Sample(infoEntries: "/CreationDate (D:20261027120000Z) /ModDate (D:20261027120000Z) /Producer (Writer 1.0)",
            trailerEntries: "/ID [<0102> <0102>]");
        var second = Sample(infoEntries: "/CreationDate (D:20261028093000Z) /ModDate (D:20261029100000Z) /Producer (Writer 2.5)",
            trailerEntries: "/ID [<0A0B> <0C0D>]");

        AssertSameSnapshot(first, second);
    }

    // The Windows and Linux output this project started from differed only in line endings inside XMP and ToUnicode streams.
    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenXmpUsesCrlfLineEndings()
    {
        const string Xmp = "<dc:title><rdf:Alt><rdf:li>Guide</rdf:li></rdf:Alt></dc:title>{0}<dc:language><rdf:Bag><rdf:li>en-US</rdf:li></rdf:Bag></dc:language>";

        AssertSameSnapshot(Sample(xmp: string.Format(Xmp, "\n")), Sample(xmp: string.Format(Xmp, "\r\n")));
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenToUnicodeUsesCrlfLineEndings()
    {
        var unix = OneFont(Type0, "<00410042> Tj", CMap("2 beginbfchar\n<0041> <0044>\n<0042> <006F>\nendbfchar"), CidFont);
        var windows = OneFont(Type0, "<00410042> Tj", CMap("2 beginbfchar\r\n<0041> <0044>\r\n<0042> <006F>\r\nendbfchar"), CidFont);

        Assert.Equal("Do", unix.TextRuns.Single().Text);
        Assert.Equal(unix.ToSnapshot(), windows.ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenTextIsDrawnInOnePieceOrInKernedPieces()
    {
        AssertSameSnapshot(OneLine("(Go with a guide) Tj"), OneLine("[(G) -15 (o with a g) 10 (uide)] TJ"));
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenMarkedContentIsNumberedDifferently()
    {
        static TestPdf Numbered(int first, int second)
        {
            var pdf = new TestPdf();
            pdf.AddPage().Tagged(first, "Elevation", "H1").Tagged(second, "Go with a guide");
            pdf.Structure(new TestElement("H1", first), new TestElement("P", second));
            return pdf;
        }

        AssertSameSnapshot(Numbered(0, 1), Numbered(7, 3));
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenStructureElementIdsDiffer()
    {
        static TestPdf Table(string id)
        {
            var pdf = new TestPdf();
            pdf.AddPage().Tagged(0, "Trail").Tagged(1, "Ridgeline");
            pdf.Structure(new TestElement("Table",
                new TestElement("TR", new TestElement("TH", 0) { Entries = $"/ID ({id}) /A << /O /Table /Scope /Column >>" }),
                new TestElement("TR", new TestElement("TD", 1) { Entries = $"/A << /O /Table /Headers [({id})] >>" })));
            return pdf;
        }

        AssertSameSnapshot(Table("h-trail"), Table("x9"));
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenAnAccentedLetterIsComposedOrDecomposed()
    {
        var composed = OneFont(Type0, "<0041> Tj", CMap("1 beginbfchar <0041> <00E9> endbfchar"), CidFont);
        var decomposed = OneFont(Type0, "<0041> Tj", CMap("1 beginbfchar <0041> <00650301> endbfchar"), CidFont);

        Assert.Equal("\u00E9", composed.TextRuns.Single().Text);
        Assert.Equal(composed.ToSnapshot(), decomposed.ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenSpacesBetweenWordsRepeat()
    {
        AssertSameSnapshot(OneLine("(Go with a guide) Tj"), OneLine("(Go  with   a guide) Tj"));
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenAPageIsWithinAPointOfAStandardSize()
    {
        var letter = new TestPdf();
        letter.AddPage().Tagged(0, "Elevation");
        var nearlyLetter = new TestPdf();
        nearlyLetter.AddPage(width: 612.4, height: 791.6).Tagged(0, "Elevation");

        AssertSameSnapshot(letter, nearlyLetter);
    }

    [Fact]
    public void ToSnapshot_ShouldNotChange_WhenAPositionMovesByLessThanItsRounding()
    {
        AssertSameSnapshot(OneLine("(Elevation) Tj", at: "72 700"), OneLine("(Elevation) Tj", at: "72.3 700.2"));
    }

    // A small document with something for each encoding difference to touch: tagged text, an artifact, an image, a
    // bookmark, XMP metadata and Info entries.
    private static TestPdf Sample(bool compress = false, PdfLayout? layout = null, string baseFont = "Helvetica",
        string infoEntries = "", string trailerEntries = "", string? xmp = null)
    {
        var pdf = new TestPdf
        {
            Title = "Guide", PdfUaPart = "1", CompressStreams = compress, Layout = layout ?? new PdfLayout(), BaseFont = baseFont,
            InfoEntries = infoEntries, TrailerEntries = trailerEntries, Xmp = xmp,
        };
        pdf.AddPage().Tagged(0, "Elevation", "H1").Tagged(1, "Go with a guide").Artifact("Page 1").TaggedImage(2);
        pdf.AddPage().Tagged(0, "Safety");
        pdf.Structure(new TestElement("Document",
            new TestElement("H1", 0), new TestElement("P", 1), new TestElement("Figure", 2) { Entries = "/Alt (Logo)" },
            new TestElement("P", 0) { Page = 2 }));
        pdf.Bookmark("Safety", 2);
        return pdf;
    }

    private static TestPdf OneLine(string text, string at = "72 700")
    {
        var pdf = new TestPdf();
        pdf.AddPage().Raw($"/P << /MCID 0 >> BDC BT /F1 12 Tf {at} Td {text} ET EMC");
        pdf.Structure(new TestElement("P", 0));
        return pdf;
    }

    // PdfPig recovers from a wrong cross-reference section by scanning the file, which would hide a layout option that
    // wrote one wrong, so every offset it read is checked to point at its object.
    private static void AssertSameSnapshot(TestPdf first, TestPdf second)
    {
        Assert.NotEqual(first.Build(), second.Build());
        foreach (var bytes in new[] { first.Build(), second.Build() })
        {
            using var strict = PdfDocument.Open(bytes, new ParsingOptions { UseLenientParsing = false });
            var text = Encoding.Latin1.GetString(bytes);
            foreach (var (reference, location) in strict.Structure.CrossReferenceTable.ObjectOffsets)
            {
                Assert.StartsWith($"{reference.ObjectNumber} 0 obj", text[(int)location.Value1..]);
            }
        }

        Assert.Equal(first.Load().ToSnapshot(), second.Load().ToSnapshot());
    }
}
