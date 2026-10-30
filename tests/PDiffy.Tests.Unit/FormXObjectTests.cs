using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

// A form XObject numbers its marked content on its own, so a form drawn on a page can reuse the page's MCIDs.
public class FormXObjectTests
{
    [Fact]
    public void Load_ShouldGiveEachElementItsOwnText_WhenAFormReusesThePagesMcid()
    {
        var document = Load(page: "/P << /MCID 0 >> BDC BT /F1 12 Tf 72 700 Td (Trail notes) Tj ET EMC /Fm1 Do",
            elements: ["<< /Type /StructElem /S /P /P 6 0 R /Pg 3 0 R /K 0 >>", Into(form: 9, mcid: 0)],
            forms: [Form("/P << /MCID 0 >> BDC BT /F1 12 Tf 72 600 Td (Ranger stamp) Tj ET EMC")]);

        Assert.Equal(["Trail notes", "Ranger stamp"], document.Structure.Select(element => element.Text));
        Assert.Empty(document.Untagged);
    }

    [Fact]
    public void Load_ShouldReadTheInnerFormsText_WhenAFormDrawsAnotherThatReusesItsMcid()
    {
        var document = Load(page: "/Fm1 Do",
            elements: [Into(form: 9, mcid: 0), Into(form: 10, mcid: 0)],
            forms:
            [
                Form("/P << /MCID 0 >> BDC BT /F1 12 Tf 72 700 Td (Outer) Tj ET EMC /Fm2 Do",
                    "/Font << /F1 5 0 R >> /XObject << /Fm2 10 0 R >>"),
                Form("/P << /MCID 0 >> BDC BT /F1 12 Tf 72 600 Td (Inner) Tj ET EMC"),
            ]);

        Assert.Equal(["Outer", "Inner"], document.Structure.Select(element => element.Text));
    }

    [Fact]
    public void Load_ShouldGiveTheFormsTextToThePagesElement_WhenTheFormIsDrawnInsideThePagesMarkedContent()
    {
        var document = Load(page: "/P << /MCID 0 >> BDC /Fm1 Do EMC",
            elements: ["<< /Type /StructElem /S /P /P 6 0 R /Pg 3 0 R /K 0 >>"],
            forms: [Form("BT /F1 12 Tf 72 600 Td (Ranger stamp) Tj ET")]);

        Assert.Equal("Ranger stamp", Assert.Single(document.Structure).Text);
    }

    [Fact]
    public void Load_ShouldGiveEachElementItsOwnText_WhenAFormInsideThePagesMarkedContentReusesItsMcid()
    {
        var document = Load(page: "/P << /MCID 0 >> BDC BT /F1 12 Tf 72 700 Td (Trail notes) Tj ET /Fm1 Do EMC",
            elements: ["<< /Type /StructElem /S /P /P 6 0 R /Pg 3 0 R /K 0 >>", Into(form: 9, mcid: 0)],
            forms: [Form("/P << /MCID 0 >> BDC BT /F1 12 Tf 72 600 Td (Ranger stamp) Tj ET EMC")]);

        Assert.Equal(["Trail notes", "Ranger stamp"], document.Structure.Select(element => element.Text));
    }

    [Fact]
    public void Load_ShouldReadTheFormsTextAsThePagesMcid_WhenFormsNestDeeperThanPDiffyFollows()
    {
        // Eleven forms, each drawing the next; the last one's MCID is past the ten PDiffy follows.
        var forms = Enumerable.Range(1, 11).Select(i => i < 11
            ? Form($"/Fm{i + 1} Do", $"/Font << /F1 5 0 R >> /XObject << /Fm{i + 1} {8 + i} 0 R >>")
            : Form("/P << /MCID 0 >> BDC BT /F1 12 Tf 72 600 Td (Ranger stamp) Tj ET EMC")).ToArray();

        var document = Load(page: "/Fm1 Do", elements: [Into(form: 18, mcid: 0)], forms: forms);

        Assert.Equal("Ranger stamp", Assert.Single(document.Structure).Text);
        Assert.Empty(document.Untagged);
    }

    [Fact]
    public void Load_ShouldListTheFormsTextAsUntagged_WhenOnlyThePagesMcidIsReferenced()
    {
        var document = Load(page: "/P << /MCID 3 >> BDC BT /F1 12 Tf 72 700 Td (Trail notes) Tj ET EMC /Fm1 Do",
            elements: ["<< /Type /StructElem /S /P /P 6 0 R /Pg 3 0 R /K 3 >>"],
            forms: [Form("/P << /MCID 3 >> BDC BT /F1 12 Tf 72 600 Td (Ranger stamp) Tj ET EMC")]);

        Assert.Equal("Trail notes", Assert.Single(document.Structure).Text);
        Assert.Equal(["Ranger stamp"], document.Untagged.Select(text => text.Text));
    }

    // The page's marked content goes on around the form, so the form's artifact is inside it.
    [Fact]
    public void Load_ShouldCountTheFormsArtifactAsInsideTaggedContent_WhenTheFormIsDrawnInsideThePagesMarkedContent()
    {
        var document = Load(page: "/P << /MCID 0 >> BDC BT /F1 12 Tf 72 700 Td (Trail notes) Tj ET /Fm1 Do EMC",
            elements: ["<< /Type /StructElem /S /P /P 6 0 R /Pg 3 0 R /K 0 >>"],
            forms: [Form("/Artifact BMC BT /F1 12 Tf 72 600 Td (Page 1) Tj ET EMC")]);

        Assert.Equal(1, Assert.Single(document.Pages).ArtifactsInTaggedContent);
    }

    // Objects 1 to 6 are the catalog, pages, page, its content, a font and the structure root; the elements follow,
    // numbered from 7, then the forms, so with two elements the first form is object 9.
    private static PdfUaDocument Load(string page, string[] elements, string[] forms)
    {
        var kids = string.Join(" ", elements.Select((_, i) => $"{7 + i} 0 R"));
        var formNames = string.Join(" ", forms.Select((_, i) => $"/Fm{i + 1} {7 + elements.Length + i} 0 R"));
        return RawPdf.Load(
        [
            "<< /Type /Catalog /Pages 2 0 R /MarkInfo << /Marked true >> /StructTreeRoot 6 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
                + $"/Resources << /Font << /F1 5 0 R >> /XObject << {formNames} >> >> /Contents 4 0 R >>",
            TestPdf.Stream(page),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            $"<< /Type /StructTreeRoot /K [{kids}] >>",
            .. elements,
            .. forms,
        ]);
    }

    private static string Into(int form, int mcid) =>
        $"<< /Type /StructElem /S /P /P 6 0 R /K [<< /Type /MCR /MCID {mcid} /Pg 3 0 R /Stm {form} 0 R >>] >>";

    private static string Form(string content, string resources = "/Font << /F1 5 0 R >>") =>
        $"<< /Type /XObject /Subtype /Form /BBox [0 0 612 792] /Resources << {resources} >> "
        + $"/Length {content.Length} >>\nstream\n{content}\nendstream";
}
