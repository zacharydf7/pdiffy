using PDiffy.Tests.Unit.Pdfs;
using static PDiffy.Tests.Unit.Pdfs.TestFonts;
using static PDiffy.Tests.Unit.SnapshotText;

namespace PDiffy.Tests.Unit;

public class FontProblemTests
{
    private const string CMapData = "/CIDInit /ProcSet findresource begin 12 dict begin begincmap /CMapName /Trail def "
        + "{0} 1 begincodespacerange <0000> <FFFF> endcodespacerange 1 begincidrange <0000> <FFFF> 0 endcidrange "
        + "endcmap CMapName currentdict /CMap defineresource pop end end";

    [Theory]
    [InlineData(32, "/Encoding /WinAnsiEncoding", null)]
    [InlineData(32, "/Encoding /MacRomanEncoding", null)]
    [InlineData(32, "/Encoding << /BaseEncoding /WinAnsiEncoding /Differences [65 /B] >>", null)]
    [InlineData(32, "", PdfFontProblem.TrueTypeEncoding)]
    [InlineData(32, "/Encoding /StandardEncoding", PdfFontProblem.TrueTypeEncoding)]
    [InlineData(32, "/Encoding << /Differences [65 /B] >>", PdfFontProblem.TrueTypeEncoding)]
    [InlineData(4, "", null)]
    [InlineData(4, "/Encoding /WinAnsiEncoding", PdfFontProblem.SymbolicEncoding)]
    public void Load_ShouldReportATrueTypeEncodingProblem_WhenTheEncodingDoesntSuitTheFont(int flags, string encoding,
        PdfFontProblem? expected)
    {
        var font = $"<< /Type /Font /Subtype /TrueType /BaseFont /Helvetica {encoding} /FontDescriptor << /Type "
            + $"/FontDescriptor /FontName /Helvetica /Flags {flags} /FontBBox [0 0 1000 1000] /ItalicAngle 0 "
            + "/Ascent 800 /Descent -200 /CapHeight 700 /StemV 80 >> >>";

        var document = OneFont(font, "(A) Tj");

        Assert.Equal(expected is { } problem ? [problem] : [], Assert.Single(document.Fonts).Problems);
    }

    [Theory]
    [InlineData("/Identity-H", "Identity", null)]
    [InlineData("/UniJIS-UCS2-H", "Japan1", null)]
    [InlineData("/UniJIS-UCS2-H", "Identity", PdfFontProblem.CidSystemInfo)]
    [InlineData("/UniKS-UCS2-H", "Japan1", PdfFontProblem.CidSystemInfo)]
    [InlineData("/Trail-H", "Identity", PdfFontProblem.CmapNotEmbedded)]
    public void Load_ShouldReportACMapProblem_WhenAType0FontNamesItsCMap(string encoding, string ordering,
        PdfFontProblem? expected)
    {
        var font = Type0.Replace("/Identity-H", encoding);
        var descendant = CidFont.Replace("/Ordering (Identity)", $"/Ordering ({ordering})");

        var document = OneFont(font, "<0041> Tj", CMap(""), descendant);

        Assert.Equal(expected is { } problem ? [problem] : [], Assert.Single(document.Fonts).Problems);
    }

    [Theory]
    [InlineData("/CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >>", "/WMode 0 def", null)]
    [InlineData("/CIDSystemInfo [<< /Registry (Adobe) /Ordering (Japan1) /Supplement 0 >>]", "",
        PdfFontProblem.CidSystemInfo)]
    [InlineData("/WMode 1", "/WMode 0 def", PdfFontProblem.CmapWritingMode)]
    [InlineData("", "/WMode 1 def", PdfFontProblem.CmapWritingMode)]
    [InlineData("", "/UniJIS-UCS2-H usecmap", null)]
    [InlineData("", "/Ridge-H usecmap", PdfFontProblem.CmapReference)]
    [InlineData("/UseCMap /Ridge-H", "", PdfFontProblem.CmapReference)]
    [InlineData("/UseCMap 9 0 R", "", PdfFontProblem.CmapReference)]
    [InlineData("", "/WMode 99999999999 def", PdfFontProblem.CmapWritingMode)]
    public void Load_ShouldReportACMapProblem_WhenAType0FontEmbedsItsCMap(string entries, string data,
        PdfFontProblem? expected)
    {
        var font = Type0.Replace("/Identity-H", "8 0 R");
        var cmap = EmbeddedCMap(entries, string.Format(CMapData, data));
        var usedCMap = EmbeddedCMap("", string.Format(CMapData, ""));

        var document = OneFont(font, "<0041> Tj", CMap(""), CidFont, cmap, usedCMap);

        Assert.Equal(expected is { } problem ? [problem] : [], Assert.Single(document.Fonts).Problems);
    }

    [Theory]
    [InlineData("/FontFile2 9 0 R", "", PdfFontProblem.CidToGidMap)]
    [InlineData("/FontFile3 9 0 R", "", PdfFontProblem.CidToGidMap)]
    [InlineData("/FontFile2 9 0 R", "/CIDToGIDMap /Identity-H", PdfFontProblem.CidToGidMap)]
    [InlineData("/FontFile2 9 0 R", "/CIDToGIDMap /Identity", null)]
    [InlineData("/FontFile2 9 0 R", "/CIDToGIDMap 9 0 R", null)]
    [InlineData("", "", null)]
    public void Load_ShouldReportAMissingCidToGidMap_WhenAnEmbeddedTrueTypeCidFontHasNone(string fontFile, string map,
        PdfFontProblem? expected)
    {
        var descendant = CidFont.Replace("/StemV 80 >>", $"/StemV 80 {fontFile} >> {map}");

        var document = OneFont(Type0, "<0041> Tj", CMap(""), descendant, "<< >>", TestPdf.Stream("not a font program"));

        Assert.Equal(expected is { } problem ? [problem] : [], Assert.Single(document.Fonts).Problems);
    }

    // Found as a CMap problem first, but listed in PdfFontProblem order.
    [Fact]
    public void Load_ShouldListTheProblemsInTheirEnumOrder_WhenAFontHasSeveral()
    {
        var font = Type0.Replace("/Identity-H", "/Trail-H");
        var descendant = CidFont.Replace("/StemV 80 >>", "/StemV 80 /FontFile2 8 0 R >>");

        var document = OneFont(font, "<0041> Tj", CMap(""), descendant, TestPdf.Stream("not a font program"));

        PdfFontProblem[] expected = [PdfFontProblem.CidToGidMap, PdfFontProblem.CmapNotEmbedded];
        Assert.Equal(expected, Assert.Single(document.Fonts).Problems);
    }

    [Fact]
    public void Write_ShouldPrintTheProblems_WhenAFontHasThem()
    {
        var font = new PdfFont
        {
            Name = "Trail", Subtype = "Type0", Embedded = true, HasToUnicode = true,
            Problems = [PdfFontProblem.CidToGidMap, PdfFontProblem.CmapWritingMode],
        };
        var document = Documents.Empty() with { Fonts = [font] };

        var snapshot = document.ToSnapshot();

        Assert.Equal(["  Trail Type0 embedded=yes to-unicode=yes problems=[cid-to-gid-map,cmap-writing-mode]"],
            SectionLines(snapshot, "fonts"));
    }

    private static string EmbeddedCMap(string entries, string data) =>
        $"<< /Type /CMap {entries} /Length {data.Length} >>\nstream\n{data}\nendstream";
}
