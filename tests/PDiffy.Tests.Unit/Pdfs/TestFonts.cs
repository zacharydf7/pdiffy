namespace PDiffy.Tests.Unit.Pdfs;

/// <summary>A one-page PDF with a single font, and the fonts and ToUnicode maps tests give it.</summary>
internal static class TestFonts
{
    /// <summary>A CID font with an Identity ordering, for <see cref="Type0"/> to use as object 7.</summary>
    public const string CidFont = "<< /Type /Font /Subtype /CIDFontType2 /BaseFont /Cid /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) "
        + "/Supplement 0 >> /FontDescriptor << /Type /FontDescriptor /FontName /Cid /Flags 4 /FontBBox [0 0 1000 1000] /ItalicAngle 0 "
        + "/Ascent 800 /Descent -200 /CapHeight 700 /StemV 80 >> >>";

    /// <summary>
    /// A font whose text comes only from its ToUnicode map, given to <see cref="OneFont"/> with the map as object 6 and
    /// <see cref="CidFont"/> as object 7.
    /// </summary>
    public const string Type0 = "<< /Type /Font /Subtype /Type0 /BaseFont /Cid /Encoding /Identity-H /DescendantFonts [7 0 R] /ToUnicode 6 0 R >>";

    // Objects 1-5 are a one-page document whose page shows the content with font F1 (object 4); extras are numbered from 6.
    public static PdfUaDocument OneFont(string font, string text, params string[] extraObjects) => RawPdf.Load(
    [
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
        font,
        TestPdf.Stream($"BT /F1 12 Tf 72 700 Td {text} ET"),
        .. extraObjects,
    ]);

    public static string CMap(string mappings) => TestPdf.Stream(
        $"/CIDInit /ProcSet findresource begin 12 dict begin begincmap 1 begincodespacerange <00> <FFFF> endcodespacerange {mappings} "
        + "endcmap CMapName currentdict /CMap defineresource pop end end");
}
