using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

public class DocumentSettingsTests
{
    // A comment's attached file follows the document's own list; one that only names a file outside embeds nothing.
    [Fact]
    public void Load_ShouldReadFilesAttachedOnPages_WhenFileAttachmentAnnotationsHoldThem()
    {
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(data.xml) "
                + "<< /F (data.xml) /UF (data.xml) >>] >> >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 5 0 R 7 0 R] >>",
            "<< /Type /Annot /Subtype /FileAttachment /Rect [0 0 10 10] /FS << /Type /Filespec /F (photo.jpg) "
                + "/Desc (Ridge view) /EF << /F 6 0 R >> >> >>",
            "<< /Type /Annot /Subtype /FileAttachment /Rect [0 20 10 30] /FS (notes.txt) >>",
            TestPdf.Stream("hello"),
            "<< /Type /Annot /Subtype /FileAttachment /Rect [0 40 10 50] /FS << /F (map.pdf) /UF (map.pdf) >> >>");

        var files = document.Settings.EmbeddedFiles;

        PdfEmbeddedFile[] expected =
        [
            new()
            {
                Name = "data.xml", FileName = "data.xml", Description = null, Relationship = null, Size = null,
                Hash = null, NamedBothWays = true,
            },
            new()
            {
                Name = "photo.jpg", Page = 1, FileName = "photo.jpg", Description = "Ridge view", Relationship = null,
                Size = 5, Hash = "2cf24dba5fb0a30e",
            },
        ];
        Assert.Equal(expected, files);
        Assert.Equal(["  embedded-file \"photo.jpg\" page=p1 file=\"photo.jpg\" both-names=no "
            + "description=\"Ridge view\" size=5 hash=2cf24dba5fb0a30e"],
            document.ToSnapshot().Split('\n').Where(line => line.Contains("photo.jpg")));
    }

    // The document's own data.xml is on no page, so it stays whichever pages are printed; route.gpx is on page 1.
    [Theory]
    [InlineData(1, new[] { "data.xml", "route.gpx" })]
    [InlineData(2, new[] { "data.xml" })]
    public void ToSnapshot_ShouldKeepAPagesAttachments_WhenOnlySomePagesArePrinted(int page, string[] files)
    {
        var pdf = new TestPdf().CatalogEntries(" /Names << /EmbeddedFiles << /Names [(data.xml) "
            + "<< /F (data.xml) /UF (data.xml) >>] >> >>");
        pdf.AddPage().FileAttachment("route.gpx");
        pdf.AddPage();

        var snapshot = pdf.Load().ToSnapshot(options => options.OnlyPages(page));

        Assert.Equal(files, snapshot.Split('\n').Where(line => line.Contains("embedded-file"))
            .Select(line => line.Split('"')[1]));
    }

    // An annotation's appearance can be one stream, or one per state; either can be a reference XObject, or hold one in
    // its resources. Objects 5 and 6 are forms with the given entries.
    [Theory]
    [InlineData("/AP << /N 5 0 R >>", "/Ref << /F (map.pdf) /Page 0 >>", "", 1)]
    [InlineData("/AP << /N << /On 6 0 R /Off 5 0 R >> >>", "", "/Ref << /F (map.pdf) /Page 0 >>", 1)]
    [InlineData("/AP << /D 5 0 R >>", "/Resources << /XObject << /Map 6 0 R >> >>", "/Ref << /F (map.pdf) /Page 0 >>", 1)]
    [InlineData("/AP << /N 5 0 R >>", "", "", 0)]
    public void Load_ShouldCountReferenceXObjectsInAppearances_WhenGivenAnAnnotationAppearance(string appearance,
        string first, string second, int expected)
    {
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>",
            $"<< /Type /Annot /Subtype /Stamp /Rect [0 0 10 10] {appearance} >>",
            $"<< /Type /XObject /Subtype /Form /BBox [0 0 10 10] {first} /Length 0 >>\nstream\n\nendstream",
            $"<< /Type /XObject /Subtype /Form /BBox [0 0 10 10] {second} /Length 0 >>\nstream\n\nendstream");

        Assert.Equal(expected, document.Settings.ReferenceXObjects);
    }

    [Fact]
    public void Load_ShouldReadViewerSettingsLayersAndEmbeddedFiles_WhenCatalogSetsThem()
    {
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R /Version /2.0 /PageMode /UseOutlines /PageLayout /TwoColumnLeft /OpenAction [3 0 R /Fit] "
                + "/AA << /WC << /S /JavaScript /JS (cleanup) >> >> "
                + "/ViewerPreferences << /DisplayDocTitle true /CenterWindow true /Direction /R2L >> "
                + "/OCProperties << /OCGs [5 0 R 6 0 R] /D << /OFF [6 0 R] >> >> "
                + "/Names << /EmbeddedFiles << /Names [(data.xml) 7 0 R] >> >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
            TestPdf.Stream(""),
            "<< /Type /OCG /Name (Watermark) >>",
            "<< /Type /OCG /Name (Draft) >>",
            "<< /Type /Filespec /F (data.xml) /UF (data.xml) /Desc (Lab data) /AFRelationship /Data /EF << /F 8 0 R >> >>",
            TestPdf.Stream("hello"));

        var settings = document.Settings;

        Assert.Equal(("1.7", "2.0", "UseOutlines", "TwoColumnLeft", "p1 Fit"),
            (settings.PdfVersion, settings.CatalogVersion, settings.PageMode, settings.PageLayout, settings.OpenAction));
        Assert.Equal((true, true, false), (settings.HasJavaScript, settings.HasAdditionalActions, settings.IsEncrypted));
        Assert.Null(settings.Permissions);
        PdfSetting[] preferences =
        [
            new() { Name = "CenterWindow", Value = "true" },
            new() { Name = "Direction", Value = "R2L" },
        ];
        Assert.Equal(preferences, settings.ViewerPreferences);
        PdfLayer[] layers =
        [
            new() { Name = "Watermark", VisibleByDefault = true },
            new() { Name = "Draft", VisibleByDefault = false },
        ];
        Assert.Equal(layers, settings.Layers);
        Assert.Equal([new PdfEmbeddedFile
        {
            Name = "data.xml", FileName = "data.xml", Description = "Lab data", Relationship = "Data", Size = 5,
            Hash = "2cf24dba5fb0a30e", NamedBothWays = true,
        }], settings.EmbeddedFiles);
        Assert.Equal([new PdfLayerConfiguration()], settings.LayerConfigurations);
    }

    [Fact]
    public void Load_ShouldReadHeaderVersionGoToDestinationAndAllOffLayers_WhenCatalogUsesThem()
    {
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R /OpenAction 7 0 R "
                + "/OCProperties << /OCGs [5 0 R 6 0 R] /D << /BaseState /OFF /ON [5 0 R] >> >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
            TestPdf.Stream(""),
            "<< /Type /OCG /Name (Watermark) >>",
            "<< /Type /OCG /Name (Draft) >>",
            "<< /S /GoTo /D 8 0 R >>",
            "[3 0 R /XYZ 0 792 0.5]");

        var settings = document.Settings;

        Assert.Equal(("1.7", null, "GoTo p1 XYZ 0 792 0.5"), (settings.PdfVersion, settings.CatalogVersion, settings.OpenAction));
        PdfLayer[] layers =
        [
            new() { Name = "Watermark", VisibleByDefault = true },
            new() { Name = "Draft", VisibleByDefault = false },
        ];
        Assert.Equal(layers, settings.Layers);
    }

    [Fact]
    public void Load_ShouldHashStoredBytesOrMarkUnreadable_WhenAttachmentCantBeDecoded()
    {
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(photo.jpg) 5 0 R (data.bin) 6 0 R] >> >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
            TestPdf.Stream(""),
            "<< /Type /Filespec /F (photo.jpg) /EF << /F 7 0 R >> >>",
            "<< /Type /Filespec /F (data.bin) /EF << /F 8 0 R >> >>",
            "<< /Length 5 /Filter /DCTDecode >>\nstream\nhello\nendstream",
            "<< /Length 5 /Filter /LZWDecode >>\nstream\nhello\nendstream");

        var files = document.Settings.EmbeddedFiles;

        Assert.Equal((5, "2cf24dba5fb0a30e", false), (files[0].Size, files[0].Hash, files[0].Unreadable));
        Assert.Equal((null, null, true), (files[1].Size, files[1].Hash, files[1].Unreadable));
    }

    [Theory]
    [InlineData("/Names << /JavaScript << /Names [(init) << /S /JavaScript /JS (x) >>] >> >>")]
    [InlineData("/OpenAction << /S /URI /URI (https://example.com) /Next << /S /JavaScript /JS (x) >> >>")]
    [InlineData("/OpenAction << /S /Rendition /OP 0 /JS (x) >>")]
    public void Load_ShouldReportJavaScript_WhenANameTreeOrOpenActionHasIt(string catalogEntries)
    {
        var pdf = new TestPdf().CatalogEntries(catalogEntries);
        pdf.AddPage();

        var settings = pdf.Load().Settings;

        Assert.Equal((true, false), (settings.HasJavaScript, settings.HasAdditionalActions));
    }

    [Theory]
    [InlineData("", "/Subtype /Link /Rect [0 0 9 9] /A << /S /JavaScript /JS (x) >>", true)]
    [InlineData("", "/Subtype /Link /Rect [0 0 9 9] /A << /S /URI /URI (https://example.com) "
        + "/Next [<< /S /JavaScript /JS (x) >>] >>", true)]
    [InlineData("", "/Subtype /Screen /Rect [0 0 9 9] /AA << /PO << /S /JavaScript /JS (x) >> >>", true)]
    [InlineData("", "/Subtype /Screen /Rect [0 0 9 9] /A << /S /Rendition /OP 0 /JS (x) >>", true)]
    [InlineData("", "/Subtype /Screen /Rect [0 0 9 9] /A << /S /Rendition /OP 0 >>", false)]
    [InlineData("/AA << /O << /S /JavaScript /JS (x) >> >>", null, true)]
    [InlineData("", "/Subtype /Link /Rect [0 0 9 9] /A << /S /URI /URI (https://example.com) >>", false)]
    public void Load_ShouldReportJavaScript_WhenGivenAnAnnotationOrPageAction(string pageEntries, string? annotation,
        bool expected)
    {
        var pdf = new TestPdf();
        var page = pdf.AddPage().PageEntries(pageEntries);
        if (annotation is not null) page.Annotation(annotation);

        var settings = pdf.Load().Settings;

        Assert.Equal(expected, settings.HasJavaScript);
    }

    [Fact]
    public void Load_ShouldReportJavaScript_WhenALaterBookmarkRunsIt()
    {
        var pdf = new TestPdf().Bookmark("Trails", 1).Bookmark("Map", "/A << /S /JavaScript /JS (x) >>");
        pdf.AddPage();

        var settings = pdf.Load().Settings;

        Assert.True(settings.HasJavaScript);
    }

    // Built by hand, since TestPdf doesn't nest bookmarks.
    [Fact]
    public void Load_ShouldReportJavaScript_WhenANestedBookmarkRunsIt()
    {
        var document = RawPdf.Load("<< /Type /Catalog /Pages 2 0 R /Outlines << /First 5 0 R /Last 5 0 R >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
            TestPdf.Stream(""),
            "<< /Title (Trails) /First 6 0 R /Last 6 0 R /Dest [3 0 R /Fit] >>",
            "<< /Title (Map) /Parent 5 0 R /A << /S /JavaScript /JS (x) >> >>");

        var settings = document.Settings;

        Assert.True(settings.HasJavaScript);
    }

    [Theory]
    [InlineData("/OpenAction << /S /URI /URI (https://example.com) >>", "URI https://example.com", false)]
    [InlineData("/OpenAction << /S /JavaScript /JS (app.alert(1)) >>", "JavaScript", true)]
    [InlineData("/OpenAction << /S /URI /URI (https://example.com) /Next [<< /S /Launch /Next << /S /JavaScript /JS (x) >> >> "
        + "<< /S /Named /N /NextPage >>] >>", "URI https://example.com, Launch, JavaScript, Named", true)]
    public void Load_ShouldDescribeTheOpenAction_WhenItIsAnAction(string catalogEntries, string expected, bool hasJavaScript)
    {
        var pdf = new TestPdf().CatalogEntries(catalogEntries);
        pdf.AddPage();

        var settings = pdf.Load().Settings;

        Assert.Equal((expected, hasJavaScript), (settings.OpenAction, settings.HasJavaScript));
    }

    [Theory]
    [InlineData("/OpenAction (intro)", "p1 FitH 700")]
    [InlineData("/OpenAction << /S /GoTo /D /intro >>", "GoTo p1 FitH 700")]
    [InlineData("/OpenAction (outro)", "named outro")]
    [InlineData("/OpenAction [{page1} /XYZ 72.6 null 0]", "p1 XYZ 73 -")]
    [InlineData("/OpenAction [0 /Fit]", "p1 Fit")]
    public void Load_ShouldReadTheOpenActionsDestinationLikeALink_WhenItIsNamedUnroundedOrNumbered(string openAction,
        string expected)
    {
        var pdf = new TestPdf().CatalogEntries(openAction + " /Names << /Dests << /Names [(intro) [{page1} /FitH 700.4]] >> >>");
        pdf.AddPage();

        var settings = pdf.Load().Settings;

        Assert.Equal(expected, settings.OpenAction);
    }

    // /O and /U are the standard security handler's hashes (revision 3, 128-bit RC4) for empty user and owner passwords,
    // this /P and this /ID, so PdfPig can open the file. The file has no strings or streams, so nothing else is encrypted.
    [Fact]
    public void Load_ShouldReadPermissions_WhenDocumentIsEncrypted()
    {
        var id = string.Concat(Enumerable.Repeat("01", 16));
        var document = RawPdf.LoadWithTrailer($"/Encrypt 4 0 R /ID [<{id}> <{id}>]",
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
            "<< /Filter /Standard /V 2 /R 3 /Length 128 /P -3388 "
                + "/O <36451BD39D753B7C1D10922C28E6665AA4F3353FB0348B536893E3B1DB5C579B> "
                + "/U <E2B62D625B527B35FC4A69438DF367F800000000000000000000000000000000> >>");

        var settings = document.Settings;

        Assert.True(settings.IsEncrypted);
        Assert.Equal(PdfPermissions.Print | PdfPermissions.ExtractForAccessibility, settings.Permissions);
    }

    [Fact]
    public void Load_ShouldReadOtherInfoAndXmpDetails_WhenMetadataHasThem()
    {
        var pdf = new TestPdf
        {
            InfoEntries = "/Company (Example Park) /Trapped /False /SourceModified (D:20210101)",
            Xmp = "<dc:creator><rdf:Seq><rdf:li>Ann</rdf:li><rdf:li>Bo</rdf:li></rdf:Seq></dc:creator>"
                + "<dc:description><rdf:Alt><rdf:li xml:lang=\"x-default\">Trail guide</rdf:li></rdf:Alt></dc:description>"
                + "<dc:language><rdf:Bag><rdf:li>en-US</rdf:li></rdf:Bag></dc:language>"
                + "<pdfaid:part>2</pdfaid:part><pdfaid:conformance>B</pdfaid:conformance>",
        };
        pdf.AddPage();

        var metadata = pdf.Load().Metadata;

        PdfSetting[] expectedInfo =
        [
            new() { Name = "Company", Value = "Example Park" },
            new() { Name = "SourceModified", Value = "D:20210101" },
            new() { Name = "Trapped", Value = "False" },
        ];
        Assert.Equal(expectedInfo, metadata.OtherInfo);
        Assert.Equal(("Ann; Bo", "Trail guide", "en-US", "2B"),
            (metadata.XmpCreator, metadata.XmpDescription, metadata.XmpLanguage, metadata.PdfAConformance));
    }

    [Fact]
    public void Load_ShouldReadPageBoxesUserUnitAndGroup_WhenPageDeclaresThem()
    {
        var pdf = new TestPdf();
        pdf.AddPage().PageEntries("/CropBox [10 10 602 782] /BleedBox [0 0 612 792] /TrimBox [20 20 592 772] "
            + "/ArtBox [30 30 582 762] /UserUnit 2 /Group << /S /Transparency /CS /DeviceRGB /I true >>");
        pdf.AddPage();

        var pages = pdf.Load().Pages;

        var boxed = pages[0];
        Assert.Equal(new PdfBox { X = 10, Y = 10, Width = 592, Height = 772 }, boxed.CropBox);
        Assert.Equal(new PdfBox { X = 0, Y = 0, Width = 612, Height = 792 }, boxed.BleedBox);
        Assert.Equal(new PdfBox { X = 20, Y = 20, Width = 572, Height = 752 }, boxed.TrimBox);
        Assert.Equal(new PdfBox { X = 30, Y = 30, Width = 552, Height = 732 }, boxed.ArtBox);
        Assert.Equal(2.0, boxed.UserUnit);
        Assert.Equal(new PdfPageGroup
        {
            Type = "Transparency", ColorSpace = "DeviceRGB", Isolated = true, Knockout = false,
        }, boxed.Group);
        Assert.Equal((null, null, null), (pages[1].CropBox, pages[1].TrimBox, pages[1].Group));
    }

    [Fact]
    public void Load_ShouldReadTheCropBox_WhenThePageInheritsItFromThePagesNode()
    {
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 /CropBox [5 5 600 780] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
            TestPdf.Stream(""));

        Assert.Equal(new PdfBox { X = 5, Y = 5, Width = 595, Height = 775 }, Assert.Single(document.Pages).CropBox);
    }
}
