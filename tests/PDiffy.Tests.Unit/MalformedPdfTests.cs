using System.Security.Cryptography;
using System.Text;
using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

/// <summary>
/// Malformed files, such as references that loop back on themselves or name objects the file doesn't have. Each must
/// load, or fail with <see cref="InvalidDataException"/> when it can't be opened at all, instead of hanging or
/// overflowing the stack.
/// </summary>
public class MalformedPdfTests
{
    [Fact]
    public void Load_ShouldReadEachElementOnce_WhenStructureElementContainsItself()
    {
        var document = OnePage("/StructTreeRoot 6 0 R",
            extraObjects: ["<< /Type /StructTreeRoot /K [7 0 R] >>", "<< /Type /StructElem /S /P /Pg 3 0 R /K [0 7 0 R] >>"]);

        var paragraph = Assert.Single(document.Structure);
        Assert.Equal("Loop", paragraph.Text);
        Assert.Empty(paragraph.Children);
    }

    [Fact]
    public void Load_ShouldReadEachLabelNodeOnce_WhenPageLabelTreeContainsItself()
    {
        var document = OnePage("/PageLabels 6 0 R", extraObjects: ["<< /Kids [6 0 R] /Nums [0 << /S /D >>] >>"]);

        Assert.Equal([new PdfPageLabelRange(1, "D", null, 1)], document.PageLabels);
    }

    [Fact]
    public void Load_ShouldReadEachFieldOnce_WhenFormFieldContainsItself()
    {
        var document = OnePage("/AcroForm << /Fields [6 0 R] >>", extraObjects: ["<< /T (loop) /FT /Tx /Kids [6 0 R] >>"]);

        var field = Assert.Single(document.FormFields);
        Assert.Equal(("loop", PdfFormFieldType.Text), (field.Name, field.Type));
    }

    [Fact]
    public void Load_ShouldReadEachNameTreeNodeOnce_WhenEmbeddedFilesTreeContainsItself()
    {
        var document = OnePage("/Names << /EmbeddedFiles 6 0 R >>", extraObjects: ["<< /Kids [6 0 R] /Names [(a.txt) << >>] >>"]);

        Assert.Equal(["a.txt"], document.Settings.EmbeddedFiles.Select(file => file.Name));
    }

    [Fact]
    public void Load_ShouldStopFollowing_WhenActionsOrArraysReferToThemselves()
    {
        var document = OnePage("/OpenAction 6 0 R /ViewerPreferences << /PrintPageRange 7 0 R >>",
            extraObjects: ["<< /S /GoTo /D 6 0 R /Next 6 0 R >>", "[7 0 R]"]);

        Assert.Equal("GoTo", document.Settings.OpenAction);
        Assert.False(document.Settings.HasJavaScript);
        Assert.Equal("[[[[[...]]]]]", Assert.Single(document.Settings.ViewerPreferences).Value);
    }

    [Fact]
    public void Load_ShouldMarkTheBookmarksUnreadable_WhenAnItemContainsItself()
    {
        var document = OnePage("/Outlines 6 0 R", extraObjects: ["<< /Type /Outlines /First 7 0 R /Last 7 0 R /Count 1 >>",
            "<< /Title (Loop) /Parent 6 0 R /Dest [3 0 R /Fit] /First 7 0 R /Last 7 0 R /Count 1 >>"]);

        Assert.Equal((true, 0), (document.BookmarksUnreadable, document.Bookmarks.Count));
    }

    [Fact]
    public void Load_ShouldStopFollowing_WhenAFieldValueContainsItself()
    {
        var document = OnePage("/AcroForm << /Fields [6 0 R] >>", extraObjects: ["<< /T (pick) /FT /Ch /V 7 0 R >>", "[(a) 7 0 R]"]);

        Assert.Equal("a, a, a, a, ...", Assert.Single(document.FormFields).Value);
    }

    [Fact]
    public void Load_ShouldCountEachPageOnce_WhenPageTreeContainsItself()
    {
        var document = OnePage(pagesKids: "3 0 R 2 0 R");

        Assert.Single(document.Pages);
        Assert.Single(document.Fonts);
    }

    [Fact]
    public void Load_ShouldCountAMarkedContentReferenceWithoutAPage_WhenNeitherItNorItsElementNamesOne()
    {
        var document = OnePage(catalogEntries: "/StructTreeRoot 6 0 R",
            extraObjects: ["<< /Type /StructTreeRoot /K [7 0 R] >>", "<< /Type /StructElem /S /P /P 6 0 R /K [<< /Type /MCR /MCID 0 >>] >>"]);

        var paragraph = Assert.Single(document.Structure);

        Assert.Equal((1, null), (paragraph.McidsWithoutPage, paragraph.Text));
    }

    [Fact]
    public void Load_ShouldCountAMarkedContentReferenceIntoAForm_WhenItNamesAFormXObjectStream()
    {
        var form = "<< /Type /XObject /Subtype /Form /BBox [0 0 612 792] /Length 0 >>\nstream\n\nendstream";
        var document = OnePage(catalogEntries: "/StructTreeRoot 6 0 R",
            extraObjects: ["<< /Type /StructTreeRoot /K [7 0 R] >>",
                "<< /Type /StructElem /S /P /P 6 0 R /K [<< /Type /MCR /MCID 0 /Pg 3 0 R /Stm 8 0 R >>] >>", form]);

        var paragraph = Assert.Single(document.Structure);

        Assert.Equal(1, paragraph.McidsInXObject);
        Assert.Contains("  P p1 mcids-in-xobject=1 \"Loop\"\n", document.ToSnapshot());
    }

    [Fact]
    public void Load_ShouldReadAFormsTaggedText_WhenAReferenceIntoTheFormSharesNoMcidWithThePage()
    {
        var content = "/P << /MCID 5 >> BDC BT /F1 12 Tf 72 600 Td (Stamp) Tj ET EMC";
        var form = "<< /Type /XObject /Subtype /Form /BBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> "
            + $"/Length {content.Length} >>\nstream\n{content}\nendstream";
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R /MarkInfo << /Marked true >> /StructTreeRoot 6 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> /XObject << /Fm1 8 0 R >> >> "
                + "/Contents 4 0 R >>",
            TestPdf.Stream("/Fm1 Do"),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /StructTreeRoot /K [7 0 R] >>",
            "<< /Type /StructElem /S /P /P 6 0 R /K [<< /Type /MCR /MCID 5 /Pg 3 0 R /Stm 8 0 R >>] >>",
            form);

        var paragraph = Assert.Single(document.Structure);

        Assert.Equal((1, "Stamp"), (paragraph.McidsInXObject, paragraph.Text));
        Assert.Empty(document.Untagged);
    }

    [Theory]
    [InlineData("/StructTreeRoot 9 0 R", "3 0 R")]
    [InlineData("", "3 0 R 9 0 R")]
    public void Load_ShouldReadTheRest_WhenAReferenceNamesAnObjectTheFileDoesNotHave(string catalogEntries,
        string pagesKids)
    {
        var document = OnePage(catalogEntries, pagesKids);

        Assert.Contains(new PdfPageText(1, "Loop"), document.Untagged);
    }

    [Fact]
    public void Load_ShouldReadTheRest_WhenAnObjectsPlaceInTheFileIsDamaged()
    {
        var document = WithDamagedPlace(6, "/StructTreeRoot 6 0 R", "<< /Type /StructTreeRoot >>");

        Assert.Equal((1, 0), (document.Pages.Count, document.Structure.Count));
    }

    [Fact]
    public void Load_ShouldMarkTheBookmarksUnreadable_WhenABookmarkHasNoTitle()
    {
        var document = OnePage("/Outlines 6 0 R", extraObjects:
            ["<< /Type /Outlines /First 7 0 R /Last 7 0 R /Count 1 >>", "<< /Parent 6 0 R /Dest [3 0 R /Fit] >>"]);

        Assert.Equal((true, 1), (document.BookmarksUnreadable, document.Pages.Count));
    }

    [Fact]
    public void Load_ShouldReadABookmarkWithoutItsChildren_WhenItsFirstChildIsNotInTheFile()
    {
        var document = OnePage("/Outlines 6 0 R", extraObjects:
        [
            "<< /Type /Outlines /First 7 0 R /Last 7 0 R /Count 1 >>",
            "<< /Title (Trails) /Parent 6 0 R /First 9 0 R /Dest [3 0 R /Fit] >>",
        ]);

        var bookmark = Assert.Single(document.Bookmarks);
        Assert.Equal(("Trails", 0, false), (bookmark.Title, bookmark.Children.Count, document.BookmarksUnreadable));
    }

    // Like a reference to an object the file doesn't have, a bookmark the file can't find reads as none.
    [Fact]
    public void Load_ShouldReadNoBookmarks_WhenTheOnlyBookmarksPlaceInTheFileIsDamaged()
    {
        var document = WithDamagedPlace(7, "/Outlines 6 0 R", "<< /Type /Outlines /First 7 0 R /Last 7 0 R /Count 1 >>",
            "<< /Title (Trails) /Parent 6 0 R /Dest [3 0 R /Fit] >>");

        Assert.Equal((0, false, 1), (document.Bookmarks.Count, document.BookmarksUnreadable, document.Pages.Count));
    }

    [Theory]
    [InlineData("no page tree")]
    [InlineData("no catalog")]
    [InlineData("cut off in the cross-reference table")]
    public void Load_ShouldThrowInvalidDataException_WhenThePdfIsTooDamagedToOpen(string damage)
    {
        var pdf = Encoding.Latin1.GetString(RawPdf.Build(OnePageObjects("", "3 0 R"), 1, ""));
        var damaged = damage switch
        {
            "no page tree" => RawPdf.Build(["<< /Type /Catalog >>"], 1, ""),
            "no catalog" => RawPdf.Build(OnePageObjects("", "3 0 R"), 9, ""),
            _ => Encoding.Latin1.GetBytes(pdf[..(pdf.IndexOf("xref", StringComparison.Ordinal) + 30)]),
        };

        var error = Assert.Throws<InvalidDataException>(() => PdfUaDocument.Load(damaged));

        Assert.StartsWith("The PDF is too damaged to open: ", error.Message);
    }

    [Fact]
    public void Load_ShouldThrowInvalidDataException_WhenTheCatalogsPlaceInTheFileIsDamaged()
    {
        var error = Assert.Throws<InvalidDataException>(() => WithDamagedPlace(1, ""));

        Assert.Equal("The PDF is too damaged to open: a position in the file points outside it.", error.Message);
    }

    [Fact]
    public void Load_ShouldThrowInvalidDataException_WhenThePdfIsEncryptedWithoutAPasswordPDiffyHas()
    {
        var pdf = RawPdf.Build([.. OnePageObjects("", "3 0 R"), "<< /Filter /Standard /V 1 /R 2 /O <00> /U <00> /P -4 >>"], 1,
            "/Encrypt 6 0 R /ID [<00> <00>]");

        var error = Assert.Throws<InvalidDataException>(() => PdfUaDocument.Load(pdf));

        Assert.StartsWith("The PDF is encrypted, and PDiffy can't open it: ", error.Message);
    }

    [Fact]
    public void Load_ShouldHashAnImageAsStored_WhenItsDataCannotBeDecoded()
    {
        var document = RawPdf.Load("<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Im1 5 0 R >> >> /Contents 4 0 R >>",
            TestPdf.Stream("q 10 0 0 10 72 600 cm /Im1 Do Q"),
            "<< /Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 "
                + "/Filter /LZWDecode /Length 5 >>\nstream\nhello\nendstream");

        var image = Assert.Single(document.Images);

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData("hello"u8))[..16], image.Hash);
    }

    // Objects 1-5 are a one-page document whose page shows "Loop" as MCID 0; extra objects are numbered from 6.
    private static PdfUaDocument OnePage(string catalogEntries = "", string pagesKids = "3 0 R",
        string[]? extraObjects = null) =>
        RawPdf.Load([.. OnePageObjects(catalogEntries, pagesKids), .. extraObjects ?? []]);

    // The same document, with one object's cross-reference entry pointing far past the end of the file.
    private static PdfUaDocument WithDamagedPlace(int number, string catalogEntries, params string[] extraObjects)
    {
        var objects = OnePageObjects(catalogEntries, "3 0 R").Concat(extraObjects).ToList();
        var text = Encoding.Latin1.GetString(RawPdf.Build(objects, catalog: 1, ""));
        var entry = $"{text.IndexOf($"\n{number} 0 obj", StringComparison.Ordinal) + 1:D10} 00000 n";
        return PdfUaDocument.Load(Encoding.Latin1.GetBytes(text.Replace(entry, "9999999999 00000 n")));
    }

    private static string[] OnePageObjects(string catalogEntries, string pagesKids) =>
    [
        $"<< /Type /Catalog /Pages 2 0 R /MarkInfo << /Marked true >> {catalogEntries} >>",
        $"<< /Type /Pages /Kids [{pagesKids}] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
        TestPdf.Stream("/P << /MCID 0 >> BDC BT /F1 12 Tf 72 700 Td (Loop) Tj ET EMC"),
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
    ];
}
