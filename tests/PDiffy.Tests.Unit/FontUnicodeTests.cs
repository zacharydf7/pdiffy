using PDiffy.Tests.Unit.Pdfs;
using static PDiffy.Tests.Unit.Pdfs.TestFonts;
using static PDiffy.Tests.Unit.SnapshotText;

namespace PDiffy.Tests.Unit;

public class FontUnicodeTests
{
    private const string NoNamesFont = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding << /Differences [65 /g65 /g66] >> >>";

    [Fact]
    public void Load_ShouldReportNoUnmappedCodes_WhenASimpleFontUsesStandardGlyphNames()
    {
        var document = OneFont("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>", "(Hello) Tj");

        Assert.Empty(Assert.Single(document.Fonts).UnmappedCodes);
    }

    [Fact]
    public void Load_ShouldReportCodesWithoutAStandardGlyphName_WhenASimpleFontHasNoToUnicode()
    {
        var font = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding << /Differences [65 /g65 /B] >> >>";

        var document = OneFont(font, "(ABA) Tj");

        Assert.Equal([0x41], Assert.Single(document.Fonts).UnmappedCodes);
    }

    [Fact]
    public void Load_ShouldFallBackToTheEncoding_WhenASimpleFontsToUnicodeLeavesACodeOut()
    {
        var font = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding /ToUnicode 6 0 R >>";

        var document = OneFont(font, "(AB) Tj", CMap("1 beginbfchar <41> <0041> endbfchar"));

        Assert.Empty(Assert.Single(document.Fonts).UnmappedCodes);
    }

    [Fact]
    public void Load_ShouldReportCodesMissingFromToUnicode_WhenACidFontUsesThem()
    {
        var font = Type0;
        var cmap = CMap("1 beginbfchar <0041> <0041> endbfchar 1 beginbfrange <0050> <0052> <0050> endbfrange "
            + "1 beginbfrange <0060> <0061> [<0060> <0000>] endbfrange");

        var document = OneFont(font, "<00410042005000520060006100430044> Tj", cmap, CidFont);

        Assert.Equal([0x42, 0x43, 0x44, 0x61], Assert.Single(document.Fonts).UnmappedCodes);
    }

    // 0052 and 0071 land on U+FEFF and U+FFFE part way through their ranges, and 0080 and 0090 start ranges on U+FEFF
    // and U+0000; 0060 and 0061 map to two and three characters.
    [Fact]
    public void Load_ShouldReportCodesMappedToAByteOrderMarkOrANoncharacter_WhenToUnicodeMapsThemThere()
    {
        var cmap = CMap("4 beginbfchar <0041> <FEFF> <0042> <fffe> <0060> <00660066> <0061> <006600660069> endbfchar "
            + "4 beginbfrange <0050> <0053> <FEFD> <0070> <0071> <FFFD> <0080> <0081> <FEFF> <0090> <0091> <0000> endbfrange");

        var document = OneFont(Type0, "<00410042005000510052005300600061007000710080008100900091> Tj", cmap, CidFont);

        Assert.Equal([0x41, 0x42, 0x52, 0x71, 0x80, 0x90], Assert.Single(document.Fonts).UnmappedCodes);
    }

    // The encoding gives every code a standard name, but a reader uses the ToUnicode map's value instead: none for 41-43
    // and 45-46, three characters for 44.
    [Theory]
    [InlineData("Type1")]
    [InlineData("TrueType")]
    public void Load_ShouldReportCodesMappedToNoText_WhenASimpleFontsEncodingWouldNameThem(string subtype)
    {
        var font = $"<< /Type /Font /Subtype /{subtype} /BaseFont /Helvetica /Encoding /WinAnsiEncoding "
            + "/ToUnicode 6 0 R >>";
        var cmap = CMap("4 beginbfchar <41> <FEFF> <42> <0000> <43> <> <44> <00660066006C> endbfchar "
            + "1 beginbfrange <45> <46> <> endbfrange");

        var document = OneFont(font, "(ABCDEFG) Tj", cmap);

        Assert.Equal([0x41, 0x42, 0x43, 0x45, 0x46], Assert.Single(document.Fonts).UnmappedCodes);
    }

    [Fact]
    public void Load_ShouldReportCodesMappedToNoText_WhenACidFontUsesAnAdobeCollection()
    {
        var font = Type0.Replace("/Identity-H", "/UniJIS-UCS2-H");
        var descendant = CidFont.Replace("/Ordering (Identity)", "/Ordering (Japan1)");

        var document = OneFont(font, "<00410042> Tj", CMap("1 beginbfchar <0041> <FEFF> endbfchar"), descendant);

        Assert.Equal([0x41], Assert.Single(document.Fonts).UnmappedCodes);
    }

    [Fact]
    public void Load_ShouldReadTheText_WhenToUnicodeMapsACodeToACharacterThatCantBeNormalized()
    {
        var cmap = CMap("2 beginbfchar <0041> <FFFE> <0042> <0042> endbfchar");

        var document = OneFont(Type0, "<00410042> Tj", cmap, CidFont);

        Assert.Equal("\uFFFDB", Assert.Single(document.Untagged).Text);
    }

    [Theory]
    [InlineData("/Encoding /WinAnsiEncoding", "<4101>", new[] { 0x01 })]
    [InlineData("/Encoding << /Differences [65 /.notdef] >>", "<4142>", new[] { 0x41 })]
    [InlineData("/Encoding << /BaseEncoding /WinAnsiEncoding /Differences [66 /B] >>", "<0142>", new[] { 0x01 })]
    [InlineData("/Encoding << /Differences [66 /B] >>", "<0142>", new int[0])]
    [InlineData("", "<0141>", new int[0])]
    public void Load_ShouldReportCodesShowingTheMissingGlyph_WhenASimpleFontsEncodingNamesThemNotdef(string encoding,
        string text, int[] expected)
    {
        var font = $"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica {encoding} >>";

        var document = OneFont(font, $"{text} Tj");

        Assert.Equal(expected, Assert.Single(document.Fonts).MissingGlyphCodes);
    }

    [Theory]
    [InlineData("/Identity-H", new[] { 0x0000 })]
    [InlineData("/UniJIS-UCS2-H", new int[0])]
    public void Load_ShouldReportCidZero_WhenACidFontsEncodingIsIdentity(string encoding, int[] expected)
    {
        var font = Type0.Replace("/Identity-H", encoding);

        var document = OneFont(font, "<00000041> Tj", CMap(""), CidFont);

        Assert.Equal(expected, Assert.Single(document.Fonts).MissingGlyphCodes);
    }

    // CID 0 draws glyph 5 and CID 2 glyph 7, but CID 1 draws glyph 0.
    [Fact]
    public void Load_ShouldReportCidsMappedToGlyphZero_WhenACidFontHasACidToGidMap()
    {
        var descendant = CidFont.Replace("/StemV 80 >> >>", "/StemV 80 >> /CIDToGIDMap 8 0 R >>");
        var map = TestPdf.Stream(new string([(char)0, (char)5, (char)0, (char)0, (char)0, (char)7]));

        var document = OneFont(Type0, "<000000010002> Tj", CMap(""), descendant, map);

        Assert.Equal([0x0001], Assert.Single(document.Fonts).MissingGlyphCodes);
    }

    [Fact]
    public void Load_ShouldCountEntriesBeforeTheCut_WhenToUnicodeIsTruncated()
    {
        var font = Type0;
        var truncated = TestPdf.Stream("/CIDInit /ProcSet findresource begin 2 beginbfchar <0041> <0041> <0042> <00");

        var document = OneFont(font, "<00410042> Tj", truncated, CidFont);

        Assert.Equal([0x42], Assert.Single(document.Fonts).UnmappedCodes);
    }

    [Fact]
    public void Load_ShouldUseTheRestoredFont_WhenTextFollowsAGraphicsStateRestore()
    {
        var unmappedFont = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding << /Differences [65 /g65] >> >>";
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>",
            unmappedFont,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            TestPdf.Stream("BT /F1 12 Tf q /F2 12 Tf (A) Tj Q 72 0 Td (A) Tj ET"));

        Assert.Equal([[0x41], []], document.Fonts.Select(font => font.UnmappedCodes));
    }

    // Each font leaves a different code unmapped, so counting the form under only one of them fails.
    [Fact]
    public void Load_ShouldCountAFormsTextUnderEachFontItIsDrawnWith_WhenTheFormSetsNoFontOfItsOwn()
    {
        var content = "BT 72 700 Td (AB) Tj ET";
        var form = $"<< /Type /XObject /Subtype /Form /BBox [0 0 612 792] /Length {content.Length} >>\n"
            + $"stream\n{content}\nendstream";
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
                + "/Resources << /Font << /F1 4 0 R /F2 5 0 R >> /XObject << /X1 6 0 R >> >> /Contents 7 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding << /Differences [66 /g66] >> >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding << /Differences [65 /g65] >> >>",
            form,
            TestPdf.Stream("/F1 12 Tf /X1 Do /F2 12 Tf /X1 Do"));

        Assert.Equal([[0x42], [0x41]], document.Fonts.Select(font => font.UnmappedCodes));
    }

    [Fact]
    public void Load_ShouldReadStringAndHexItems_WhenTextIsShownWithTJ()
    {
        var document = OneFont(NoNamesFont, "[(A) -250 <42>] TJ");

        Assert.Equal([0x41, 0x42], Assert.Single(document.Fonts).UnmappedCodes);
    }

    [Fact]
    public void Load_ShouldListAFontOnceWithCodesFromEveryPage_WhenPagesShareIt()
    {
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 6 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F2 5 0 R >> >> /Contents 7 0 R >>",
            NoNamesFont,
            TestPdf.Stream("BT /F1 12 Tf (A) Tj ET"),
            TestPdf.Stream("BT /F2 12 Tf (B) Tj ET"));

        Assert.Equal([0x41, 0x42], Assert.Single(document.Fonts).UnmappedCodes);
    }

    [Fact]
    public void Load_ShouldReadTextInsideForms_WhenAPageDrawsAFormXObject()
    {
        var form = "<< /Type /XObject /Subtype /Form /BBox [0 0 612 792] /Resources << /Font << /F9 6 0 R >> >> /Length 22 >>\n"
            + "stream\nBT /F9 12 Tf (A) Tj ET\nendstream";
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Fm0 5 0 R >> >> /Contents 4 0 R >>",
            TestPdf.Stream("/Fm0 Do"),
            form,
            NoNamesFont);

        Assert.Equal([0x41], Assert.Single(document.Fonts).UnmappedCodes);
    }

    // Without a guard, each of the eight draws would draw the form eight more times, ten levels deep, and never finish.
    [Fact(Timeout = 10_000)]
    public async Task Load_ShouldReadTheFormOnce_WhenAFormDrawsItself()
    {
        var content = string.Concat(Enumerable.Repeat("/Fm0 Do ", 8)) + "BT /F9 12 Tf (A) Tj ET";
        var form = "<< /Type /XObject /Subtype /Form /BBox [0 0 612 792] "
            + $"/Resources << /Font << /F9 6 0 R >> /XObject << /Fm0 5 0 R >> >> /Length {content.Length} >>\nstream\n{content}\nendstream";

        var document = await Task.Run(() => RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Fm0 5 0 R >> >> /Contents 4 0 R >>",
            TestPdf.Stream("/Fm0 Do"),
            form,
            NoNamesFont), TestContext.Current.CancellationToken);

        Assert.Equal([0x41], Assert.Single(document.Fonts).UnmappedCodes);
    }

    [Theory]
    [InlineData("/Identity-H", "Identity", true)]
    [InlineData("/Identity-H", "Japan1", true)]
    [InlineData("/UniJIS-UCS2-H", "Japan1", false)]
    public void Load_ShouldReportCidCodesAsUnmapped_WhenTheFontLacksAPredefinedCMapOverAnAdobeCollection(string encoding,
        string ordering, bool unmapped)
    {
        var font = $"<< /Type /Font /Subtype /Type0 /BaseFont /Cid /Encoding {encoding} /DescendantFonts [6 0 R] >>";
        var descendant = CidFont.Replace("/Ordering (Identity)", $"/Ordering ({ordering})");

        var document = OneFont(font, "<0041> Tj", descendant);

        Assert.False(document.Pages[0].Unreadable);
        Assert.Equal(unmapped, Assert.Single(document.Fonts).UnmappedCodes.Count > 0);
    }

    [Fact]
    public void Write_ShouldPrintUnmappedCodesAsHexRanges_WhenAFontHasThem()
    {
        var fonts = new[]
        {
            new PdfFont
            {
                Name = "Cid", Subtype = "Type0", Embedded = true, HasToUnicode = true,
                UnmappedCodes = [0x41, 0x42, 0x43, 0x50, 0x175],
            },
            new PdfFont
            {
                Name = "Simple", Subtype = "TrueType", Embedded = true, HasToUnicode = false, UnmappedCodes = [0x2f],
                MissingGlyphCodes = [0x01, 0x02],
            },
        };
        var document = Documents.Empty() with { Fonts = fonts };

        var snapshot = document.ToSnapshot();

        string[] expected =
        [
            "  Cid Type0 embedded=yes to-unicode=yes unmapped=[0041-0043,0050,0175]",
            "  Simple TrueType embedded=yes to-unicode=no unmapped=[2f] missing-glyphs=[01-02]",
        ];
        Assert.Equal(expected, SectionLines(snapshot, "fonts"));
    }
}
