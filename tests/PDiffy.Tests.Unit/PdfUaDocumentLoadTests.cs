using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

public class PdfUaDocumentLoadTests
{
    [Fact]
    public void Load_ShouldReadDocumentInfoAndMetadata_WhenCatalogDeclaresThem()
    {
        var pdf = new TestPdf { Lang = "en-US", Title = "Drug Guide", PdfUaPart = "1" };
        pdf.AddPage();

        var document = pdf.Load();

        Assert.Equal(new DocumentInfo(Marked: true, Lang: "en-US", DisplayDocTitle: true, PageCount: 1), document.Document);
        Assert.Equal("Drug Guide", document.Metadata.Title);
        Assert.Equal("1", document.Metadata.PdfUaPart);
    }

    [Fact]
    public void Load_ShouldReportMissingLang_WhenCatalogHasNoLang()
    {
        var pdf = new TestPdf { Lang = null };
        pdf.AddPage();

        var document = pdf.Load();

        Assert.Null(document.Document.Lang);
    }

    [Fact]
    public void Load_ShouldJoinTextOfEveryMcid_WhenElementPointsToSeveral()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Take with").Tagged(1, "food");
        pdf.Structure(new TestElement("P", 0, 1));

        var paragraph = Assert.Single(pdf.Load().Structure);

        Assert.Equal("Take with food", paragraph.Text);
        Assert.Equal(1, paragraph.FirstPage);
    }

    [Fact]
    public void Load_ShouldKeepTextInContentOrder_WhenUntaggedMarkedContentIsNestedInsideAnMcid()
    {
        var pdf = new TestPdf();
        var page = pdf.AddPage();
        page.Raw($"/P << /MCID 0 >> BDC {page.TextLine("Section")} /Span BMC {page.TextLine("1")} EMC {page.TextLine("Intro")} EMC");
        pdf.Structure(new TestElement("P", 0));

        var paragraph = Assert.Single(pdf.Load().Structure);

        Assert.Equal("Section 1 Intro", paragraph.Text);
    }

    [Fact]
    public void Load_ShouldReadNestingAttributesAndRoleMap_WhenStructureHasThem()
    {
        var pdf = new TestPdf().RoleMap("Chart", "Diagram").RoleMap("Diagram", "Figure");
        pdf.AddPage().Tagged(0, "Logo").Tagged(1, "Hola", "Span");
        pdf.Structure(new TestElement("Document",
            new TestElement("Chart", 0) { Entries = "/Alt (Pharmacy logo) /ActualText (Logo)" },
            new TestElement("P", 1) { Entries = "/Lang (es-ES) /E (Hello) /T (Greeting)" },
            new TestElement("L") { Entries = "/A << /O /List /ListNumbering /Decimal >>" }));

        var document = pdf.Load();

        var root = Assert.Single(document.Structure);
        Assert.Equal("Document", root.Type);
        var (chart, paragraph, list) = (root.Children[0], root.Children[1], root.Children[2]);
        Assert.Equal("Figure", chart.MappedType);
        Assert.Equal("Pharmacy logo", chart.Alt);
        Assert.Equal("Logo", chart.ActualText);
        Assert.Equal(("es-ES", "Hello", "Greeting"), (paragraph.Lang, paragraph.Expansion, paragraph.Title));
        Assert.Equal("Decimal", list.ListNumbering);
        Assert.Equal([chart], document.Elements("Figure"));
        Assert.Equal([new RoleMapping("Chart", "Diagram"), new RoleMapping("Diagram", "Figure")], document.RoleMap);
    }

    [Fact]
    public void Load_ShouldStopAtTheLoop_WhenRoleMapIsCircular()
    {
        var pdf = new TestPdf().RoleMap("Note1", "Note2").RoleMap("Note2", "Note1");
        pdf.AddPage().Tagged(0, "See below");
        pdf.Structure(new TestElement("Note1", 0));

        var note = Assert.Single(pdf.Load().Structure);

        Assert.Equal("Note2", note.MappedType);
    }

    [Fact]
    public void Load_ShouldReadTextFromOtherPages_WhenElementUsesMarkedContentReferences()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Continued");
        pdf.AddPage().Tagged(0, "overleaf");
        pdf.Structure(new TestElement("P", 0, new TestMcr(0, Page: 2)));

        var paragraph = Assert.Single(pdf.Load().Structure);

        Assert.Equal("Continued overleaf", paragraph.Text);
        Assert.Equal((1, 2), (paragraph.FirstPage, paragraph.LastPage));
    }

    [Fact]
    public void Load_ShouldUseTheParentsPage_WhenElementHasNoPageOfItsOwn()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "First page");
        pdf.AddPage().Tagged(0, "Second page");
        pdf.Structure(new TestElement("Sect", new TestElement("P", 0) { Page = null }) { Page = 2 });

        var paragraph = Assert.Single(Assert.Single(pdf.Load().Structure).Children);

        Assert.Equal("Second page", paragraph.Text);
    }

    [Fact]
    public void Load_ShouldReportTextAsUntagged_WhenNoElementPointsToItsMcid()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Reachable").Tagged(1, "Orphaned");
        pdf.Structure(new TestElement("P", 0));

        var document = pdf.Load();

        Assert.Equal([new PageText(1, "Orphaned")], document.Untagged);
    }

    [Fact]
    public void Load_ShouldNameOnlyReferencedHeaderCells_WhenCellsUseHeaders()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Drug").Tagged(1, "Dose").Tagged(2, "Warfarin");
        var headerRow = new TestElement("TR",
            new TestElement("TH", 0) { Entries = "/ID (h-drug) /A << /O /Table /Scope /Column >>" },
            new TestElement("TH", 1) { Entries = "/ID (h-dose) /A << /O /Table /Scope /Column >>" });
        var bodyRow = new TestElement("TR",
            new TestElement("TD", 2) { Entries = "/A << /O /Table /Headers [(h-drug) (missing)] /RowSpan 2 /ColSpan 3 >>" });
        pdf.Structure(new TestElement("Table", headerRow, bodyRow));

        var table = Assert.Single(pdf.Load().Structure);

        var drugHeader = table.Children[0].Children[0];
        var doseHeader = table.Children[0].Children[1];
        var cell = table.Children[1].Children[0];
        Assert.Equal(("TH1", "Column"), (drugHeader.Name, drugHeader.Scope));
        Assert.Null(doseHeader.Name);
        Assert.Equal(["TH1", "?missing"], cell.Headers);
        Assert.Equal((2, 3), (cell.RowSpan, cell.ColSpan));
    }

    [Fact]
    public void Load_ShouldReadLinkAnnotation_WhenElementHasObjectReference()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "example.com", "Link");
        pdf.Structure(new TestElement("Link", 0, new TestLink("https://example.com", "Example site")));

        var link = Assert.Single(pdf.Load().Structure);

        var annotation = Assert.Single(link.Annotations);
        Assert.Equal(("Link", "https://example.com", "Example site"), (annotation.Subtype, annotation.Uri, annotation.Contents));
        Assert.Equal("example.com", link.Text);
    }

    [Fact]
    public void Load_ShouldSeparateArtifactsAndUntaggedText_WhenContentIsNotTagged()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Body").Artifact("Page 1").Untagged("Stray text");
        pdf.Structure(new TestElement("P", 0));

        var document = pdf.Load();

        Assert.Equal([new PageText(1, "Page 1")], document.Artifacts);
        Assert.Equal([new PageText(1, "Stray text")], document.Untagged);
    }

    [Fact]
    public void Load_ShouldCountUntaggedImagesAndPaths_WhenGraphicsAreNeitherTaggedNorArtifacts()
    {
        var pdf = new TestPdf();
        pdf.AddPage().TaggedImage(0).UntaggedImage().UntaggedPath().ArtifactPath().ClippingPath();
        pdf.Structure(new TestElement("Figure", 0) { Entries = "/Alt (Logo)" });

        var document = pdf.Load();

        Assert.Equal((Images: 1, Paths: 1), UntaggedGraphics(document));
    }

    [Fact]
    public void Load_ShouldCountImageAsUntagged_WhenNoElementPointsToItsMcid()
    {
        var pdf = new TestPdf();
        pdf.AddPage().TaggedImage(0);

        var document = pdf.Load();

        Assert.Equal((Images: 1, Paths: 0), UntaggedGraphics(document));
    }

    [Fact]
    public void Load_ShouldCountPathAsUntagged_WhenItIsFilledAndAlsoSetsTheClip()
    {
        var pdf = new TestPdf();
        pdf.AddPage().FilledClippingPath();

        Assert.Equal((Images: 0, Paths: 1), UntaggedGraphics(pdf.Load()));
    }

    [Fact]
    public void Load_ShouldNotCountNestedGraphics_WhenTheyAreInsideAnArtifactOrAnMcid()
    {
        var pdf = new TestPdf();
        pdf.AddPage()
            .Raw("/Artifact BMC /Span BMC 72 480 100 1 re f EMC EMC")
            .Raw("/Figure << /MCID 0 >> BDC /Span BMC 72 500 100 20 re f EMC EMC");
        pdf.Structure(new TestElement("Figure", 0) { Entries = "/Alt (Bar)" });

        Assert.Equal((Images: 0, Paths: 0), UntaggedGraphics(pdf.Load()));
    }

    [Fact]
    public void Load_ShouldReportNoUntaggedGraphics_WhenEveryGraphicIsTaggedOrArtifact()
    {
        var pdf = new TestPdf();
        pdf.AddPage().TaggedImage(0).ArtifactPath().ClippingPath();
        pdf.Structure(new TestElement("Figure", 0) { Entries = "/Alt (Logo)" });

        Assert.Equal((Images: 0, Paths: 0), UntaggedGraphics(pdf.Load()));
    }

    [Fact]
    public void Load_ShouldKeepArtifactOutOfElementText_WhenArtifactIsNestedInsideAnMcid()
    {
        var pdf = new TestPdf();
        var page = pdf.AddPage();
        page.Raw($"/P << /MCID 0 >> BDC {page.TextLine("Body")} /Artifact BMC {page.TextLine("Page 1")} EMC EMC");
        pdf.Structure(new TestElement("P", 0));

        var document = pdf.Load();

        Assert.Equal("Body", Assert.Single(document.Structure).Text);
        Assert.Equal([new PageText(1, "Page 1")], document.Artifacts);
    }

    [Fact]
    public void Load_ShouldAddSpaceOnlyForVisibleGaps_WhenTextIsDrawnInPieces()
    {
        var pdf = new TestPdf();
        pdf.AddPage()
            .Raw("/P << /MCID 0 >> BDC BT /F1 12 Tf 72 400 Td (Ta) Tj (ke) Tj ET EMC")
            .Raw("/P << /MCID 1 >> BDC BT /F1 12 Tf 72 380 Td (with) Tj 40 0 Td (food) Tj ET EMC");
        pdf.Structure(new TestElement("P", 0), new TestElement("P", 1));

        var structure = pdf.Load().Structure;

        Assert.Equal(["Take", "with food"], structure.Select(element => element.Text));
    }

    [Fact]
    public void Load_ShouldMarkPageUnreadableAndReadTheRest_WhenPageHasDamagedFont()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Readable");
        pdf.AddPage().WithDamagedFont().Tagged(0, "Lost");
        pdf.Structure(new TestElement("P", 0), new TestElement("P", 0) { Page = 2 });

        var document = pdf.Load();

        Assert.False(document.Pages[0].Unreadable);
        Assert.True(document.Pages[1].Unreadable);
        Assert.Equal("Readable", document.Structure[0].Text);
        Assert.Null(document.Structure[1].Text);
        Assert.Contains("  p2 unreadable\n", document.ToSnapshot());
        Assert.Contains(new FontInfo("Damaged", "Type0", Embedded: true, HasToUnicode: true), document.Fonts);
    }

    [Fact]
    public void Load_ShouldReadPageSizeRotationAndTabs_WhenPagesDiffer()
    {
        var pdf = new TestPdf();
        pdf.AddPage();
        pdf.AddPage(width: 595, height: 842, rotate: 90, tabs: null);

        var pages = pdf.Load().Pages;

        Assert.Equal(new PageInfo(1, 612, 792, 0, "S", false), pages[0]);
        Assert.Equal(new PageInfo(2, 595, 842, 90, null, false), pages[1]);
    }

    [Fact]
    public void Load_ShouldReadBookmarksAndPageLabels_WhenCatalogHasThem()
    {
        var pdf = new TestPdf().Bookmark("Intro", 1).Bookmark("Warnings", 2)
            .CatalogEntries("/PageLabels << /Nums [0 << /S /r >> 1 << /S /D /P (A-) /St 5 >>] >>");
        pdf.AddPage();
        pdf.AddPage();

        var document = pdf.Load();

        Assert.Equal(["Intro", "Warnings"], document.Bookmarks.Select(b => b.Title));
        Assert.Equal([1, 2], document.Bookmarks.Select(b => b.Page));
        Assert.Equal([new PageLabelRange(1, "r", null, 1), new PageLabelRange(2, "D", "A-", 5)], document.PageLabels);
    }

    [Fact]
    public void Load_ShouldReadPageLabelsFromEveryNode_WhenLabelTreeHasKids()
    {
        var pdf = new TestPdf()
            .CatalogEntries("/PageLabels << /Kids [<< /Nums [0 << /S /r >>] >> << /Nums [1 << /S /D >>] >>] >>");
        pdf.AddPage();
        pdf.AddPage();

        var labels = pdf.Load().PageLabels;

        Assert.Equal([new PageLabelRange(1, "r", null, 1), new PageLabelRange(2, "D", null, 1)], labels);
    }

    [Fact]
    public void Load_ShouldStripSubsetPrefix_WhenReadingFonts()
    {
        var pdf = new TestPdf { BaseFont = "ABCDEF+Helvetica" };
        pdf.AddPage().Untagged("Text");

        var font = Assert.Single(pdf.Load().Fonts);

        Assert.Equal(new FontInfo("Helvetica", "Type1", Embedded: false, HasToUnicode: false), font);
    }

    [Fact]
    public void Load_ShouldReadInheritedFonts_WhenResourcesAreOnThePagesNode()
    {
        var pdf = new TestPdf { ResourcesOnPagesNode = true };
        pdf.AddPage().Tagged(0, "Inherited");
        pdf.Structure(new TestElement("P", 0));

        var document = pdf.Load();

        Assert.Equal("Inherited", Assert.Single(document.Structure).Text);
        Assert.Equal("Helvetica", Assert.Single(document.Fonts).Name);
    }

    private static (int Images, int Paths) UntaggedGraphics(PdfUaDocument document) =>
        (document.Images.Count(image => image.Role == ContentRole.Untagged), document.Paths.Count(path => path.Role == ContentRole.Untagged));
}
