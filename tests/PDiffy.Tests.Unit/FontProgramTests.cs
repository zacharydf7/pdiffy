using PDiffy.Tests.Unit.Pdfs;
using static PDiffy.Tests.Unit.Pdfs.TestFonts;

namespace PDiffy.Tests.Unit;

// The test program has glyphs .notdef, A and B, 500, 600 and 700 units wide in a 1000-unit em, and a (3,1) cmap for A
// and B. A simple font is object 4, with its descriptor as object 6 and the program as object 7; a CID font is object 7,
// with its descriptor as object 8 and the program as object 9.
public class FontProgramTests
{
    [Theory]
    [InlineData("(AB)", "600 700", new int[0], null)]
    [InlineData("(AB)", "600 650", new int[0], PdfFontProblem.GlyphWidths)]
    [InlineData("(ABC)", "600 700 800", new[] { 0x43 }, null)]
    public void Load_ShouldCompareWithTheProgram_WhenATrueTypeFontEmbedsOne(string text, string widths, int[] missing,
        PdfFontProblem? problem)
    {
        var document = OneFont(SimpleFont(symbolic: false, widths), $"{text} Tj", Descriptor(32, 7),
            new TestTrueType().ToStream());

        var font = Assert.Single(document.Fonts);
        Assert.Equal(missing, font.MissingGlyphCodes);
        Assert.Equal(problem is { } expected ? [expected] : [], font.Problems);
    }

    // In a 2048-unit em, glyphs A and B are 600.1 and 700.2 thousandths of an em wide.
    [Theory]
    [InlineData("600 700", null)]
    [InlineData("600 710", PdfFontProblem.GlyphWidths)]
    public void Load_ShouldScaleTheProgramsWidths_WhenItsEmIsNotAThousandUnits(string widths, PdfFontProblem? problem)
    {
        var program = new TestTrueType { UnitsPerEm = 2048, Widths = [1024, 1229, 1434] };

        var document = OneFont(SimpleFont(symbolic: false, widths), "(AB) Tj", Descriptor(32, 7), program.ToStream());

        Assert.Equal(problem is { } expected ? [expected] : [], Assert.Single(document.Fonts).Problems);
    }

    // Adieresis is C4 in WinAnsi but 80 in Mac Roman, so a (1,0) cmap is looked up by the name's Mac Roman code.
    [Fact]
    public void Load_ShouldFindTheGlyphByItsMacRomanCode_WhenTheProgramHasOnlyAMacCmap()
    {
        var program = new TestTrueType { Cmaps = [(1, 0, new() { [0x80] = 1 })] };
        var font = SimpleFont(symbolic: false, "600").Replace("/FirstChar 65", "/FirstChar 196");

        var document = OneFont(font, "<C4> Tj", Descriptor(32, 7), program.ToStream());

        var found = Assert.Single(document.Fonts);
        Assert.Empty(found.MissingGlyphCodes);
        Assert.Empty(found.Problems);
    }

    // trailA isn't in the Adobe Glyph List, so only the post table names its glyph.
    [Fact]
    public void Load_ShouldFindTheGlyphByItsPostName_WhenTheCmapsCantNameIt()
    {
        var program = new TestTrueType { GlyphNames = [".notdef", "trailA", "B"] };
        var font = SimpleFont(symbolic: false, "600 700").Replace("/Encoding /WinAnsiEncoding",
            "/Encoding << /BaseEncoding /WinAnsiEncoding /Differences [65 /trailA] >>");

        var document = OneFont(font, "(AB) Tj", Descriptor(32, 7), program.ToStream());

        Assert.Empty(Assert.Single(document.Fonts).MissingGlyphCodes);
    }

    [Fact]
    public void Load_ShouldReportAnUnreadableProgram_WhenTheFontFileIsntOne()
    {
        var document = OneFont(SimpleFont(symbolic: false, "600 700"), "(AB) Tj", Descriptor(32, 7),
            TestPdf.Stream("not a font program"));

        var font = Assert.Single(document.Fonts);
        Assert.Empty(font.MissingGlyphCodes);
        Assert.Equal([PdfFontProblem.UnreadableProgram], font.Problems);
    }

    // A non-symbolic font can't reach its glyphs through a (3,0) cmap, so A and B are missing.
    [Theory]
    [InlineData(false, 1, 0, new int[0], null)]
    [InlineData(false, 3, 0, new[] { 0x41, 0x42 }, PdfFontProblem.TrueTypeCmap)]
    [InlineData(true, 3, 0, new int[0], null)]
    [InlineData(true, 1, 0, new int[0], null)]
    public void Load_ShouldFindACmapThatDoesntSuitTheFont_WhenTheProgramHasOne(bool symbolic, int platform, int encoding,
        int[] missing, PdfFontProblem? problem)
    {
        var codes = (platform, encoding) == (3, 0) ? new Dictionary<int, int> { [0xF041] = 1, [0xF042] = 2 }
            : new Dictionary<int, int> { [0x41] = 1, [0x42] = 2 };
        var program = new TestTrueType { Cmaps = [(platform, encoding, codes)] };

        var document = OneFont(SimpleFont(symbolic, "600 700"), "(AB) Tj", Descriptor(symbolic ? 4 : 32, 7),
            program.ToStream());

        var font = Assert.Single(document.Fonts);
        Assert.Equal(missing, font.MissingGlyphCodes);
        Assert.Equal(problem is { } expected ? [expected] : [], font.Problems);
    }

    [Fact]
    public void Load_ShouldFindASymbolicCmapProblem_WhenTheProgramHasSeveralCmapsAndNoSymbolOne()
    {
        var codes = new Dictionary<int, int> { [0x41] = 1, [0x42] = 2 };
        var program = new TestTrueType { Cmaps = [(3, 1, codes), (1, 0, codes)] };

        var document = OneFont(SimpleFont(symbolic: true, "600 700"), "(AB) Tj", Descriptor(4, 7), program.ToStream());

        Assert.Equal([PdfFontProblem.SymbolicCmap], Assert.Single(document.Fonts).Problems);
    }

    [Theory]
    [InlineData("/W [1 [600 700]]", "<00010002>", new int[0], null)]
    [InlineData("/W [1 1 600 2 2 700]", "<00010002>", new int[0], null)]
    [InlineData("/W [1 2 600]", "<00010002>", new int[0], PdfFontProblem.GlyphWidths)]
    [InlineData("/DW 600", "<0001>", new int[0], null)]
    [InlineData("/W [1 [600 700]]", "<00010005>", new[] { 0x0005 }, null)]
    public void Load_ShouldCompareWithTheProgram_WhenACidFontEmbedsOne(string widths, string text, int[] missing,
        PdfFontProblem? problem)
    {
        var document = OneFont(Type0, $"{text} Tj", CMap(""), CidFont($"/CIDToGIDMap /Identity {widths}"),
            Descriptor(4, 9), new TestTrueType().ToStream());

        var font = Assert.Single(document.Fonts);
        Assert.Equal(missing, font.MissingGlyphCodes);
        Assert.Equal(problem is { } expected ? [expected] : [], font.Problems);
    }

    // Invisible text (render mode 3), such as an OCR layer, is held only to the .notdef rule: glyph 5 past the
    // program's last and the wrong width count only when the text is drawn.
    [Theory]
    [InlineData("0 Tr", new[] { 0x0005 }, PdfFontProblem.GlyphWidths)]
    [InlineData("3 Tr", new int[0], null)]
    public void Load_ShouldCheckOnlyDrawnText_WhenSomeTextIsInvisible(string mode, int[] missing,
        PdfFontProblem? problem)
    {
        var document = OneFont(Type0, $"{mode} <00010005> Tj", CMap(""), CidFont("/CIDToGIDMap /Identity /W [1 [650]]"),
            Descriptor(4, 9), new TestTrueType().ToStream());

        var font = Assert.Single(document.Fonts);
        Assert.Equal(missing, font.MissingGlyphCodes);
        Assert.Equal(problem is { } expected ? [expected] : [], font.Problems);
    }

    // The form shows its text without a Tr of its own, so it keeps the render mode of the page that draws it.
    [Theory]
    [InlineData("0 Tr", new[] { 0x0005 }, PdfFontProblem.GlyphWidths)]
    [InlineData("3 Tr", new int[0], null)]
    public void Load_ShouldCheckOnlyDrawnText_WhenAFormInheritsInvisibleText(string mode, int[] missing,
        PdfFontProblem? problem)
    {
        var form = "BT /F1 12 Tf 72 700 Td <00010005> Tj ET";

        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> "
                + "/XObject << /Fm1 10 0 R >> >> /Contents 5 0 R >>",
            Type0,
            TestPdf.Stream($"{mode} /Fm1 Do"),
            CMap(""),
            CidFont("/CIDToGIDMap /Identity /W [1 [650]]"),
            Descriptor(4, 9),
            new TestTrueType().ToStream(),
            $"<< /Type /XObject /Subtype /Form /BBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> "
                + $"/Length {form.Length} >>\nstream\n{form}\nendstream");

        var font = Assert.Single(document.Fonts);
        Assert.Equal(missing, font.MissingGlyphCodes);
        Assert.Equal(problem is { } expected ? [expected] : [], font.Problems);
    }

    // The CIDSet covers the program whatever the encoding, here a predefined Japanese CMap.
    [Fact]
    public void Load_ShouldFindACidSetProblem_WhenTheEncodingIsntIdentity()
    {
        var font = Type0.Replace("/Identity-H", "/UniJIS-UCS2-H");
        var descendant = CidFont("/CIDToGIDMap /Identity").Replace("(Identity)", "(Japan1)");
        var descriptor = Descriptor(4, 9).Replace(" >>", " /CIDSet 10 0 R >>");

        var document = OneFont(font, "<0000> Tj", CMap(""), descendant, descriptor, new TestTrueType().ToStream(),
            TestPdf.Stream(new string([(char)0xC0])));

        Assert.Equal([PdfFontProblem.CidSet], Assert.Single(document.Fonts).Problems);
    }

    // With an Identity map, the program's three glyphs are CIDs 0-2; with the stream map, only CIDs 0 and 1.
    [Theory]
    [InlineData("/CIDToGIDMap /Identity", 0xE0, null)]
    [InlineData("/CIDToGIDMap /Identity", 0xC0, PdfFontProblem.CidSet)]
    [InlineData("/CIDToGIDMap 10 0 R", 0xC0, null)]
    [InlineData("/CIDToGIDMap 10 0 R", 0x80, PdfFontProblem.CidSet)]
    public void Load_ShouldFindACidSetProblem_WhenTheCidSetLeavesOutCidsTheProgramHas(string map, int bits,
        PdfFontProblem? problem)
    {
        var descriptor = Descriptor(4, 9).Replace(" >>", " /CIDSet 11 0 R >>");
        var gids = TestPdf.Stream(new string([(char)0, (char)0, (char)0, (char)2]));
        var cidSet = TestPdf.Stream(new string([(char)bits]));

        var document = OneFont(Type0, "<0000> Tj", CMap(""), CidFont(map), descriptor, new TestTrueType().ToStream(),
            gids, cidSet);

        Assert.Equal(problem is { } expected ? [expected] : [], Assert.Single(document.Fonts).Problems);
    }

    private static string SimpleFont(bool symbolic, string widths) =>
        $"<< /Type /Font /Subtype /TrueType /BaseFont /Trail {(symbolic ? "" : "/Encoding /WinAnsiEncoding")} "
        + $"/FirstChar 65 /Widths [{widths}] /FontDescriptor 6 0 R >>";

    private static string CidFont(string entries) =>
        "<< /Type /Font /Subtype /CIDFontType2 /BaseFont /Trail /CIDSystemInfo << /Registry (Adobe) "
        + $"/Ordering (Identity) /Supplement 0 >> /FontDescriptor 8 0 R {entries} >>";

    private static string Descriptor(int flags, int program) =>
        $"<< /Type /FontDescriptor /FontName /Trail /Flags {flags} /FontBBox [0 0 1000 1000] /ItalicAngle 0 /Ascent 800 "
        + $"/Descent -200 /CapHeight 700 /StemV 80 /FontFile2 {program} 0 R >>";
}
