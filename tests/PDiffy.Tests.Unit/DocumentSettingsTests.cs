using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

public class DocumentSettingsTests
{
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
            "<< /Type /OCG /Name (Braille) >>",
            "<< /Type /OCG /Name (Draft) >>",
            "<< /Type /Filespec /F (data.xml) /UF (data.xml) /Desc (Lab data) /AFRelationship /Data /EF << /F 8 0 R >> >>",
            TestPdf.Stream("hello"));

        var settings = document.Settings;

        Assert.Equal(("1.7", "2.0", "UseOutlines", "TwoColumnLeft", "p1 Fit"),
            (settings.PdfVersion, settings.CatalogVersion, settings.PageMode, settings.PageLayout, settings.OpenAction));
        Assert.Equal((true, true, false), (settings.HasJavaScript, settings.HasAdditionalActions, settings.IsEncrypted));
        Assert.Null(settings.Permissions);
        Assert.Equal([new Setting("CenterWindow", "true"), new Setting("Direction", "R2L")], settings.ViewerPreferences);
        Assert.Equal([new Layer("Braille", true), new Layer("Draft", false)], settings.Layers);
        Assert.Equal([new EmbeddedFile("data.xml", "data.xml", "Lab data", "Data", 5, "2cf24dba5fb0a30e")], settings.EmbeddedFiles);
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
            "<< /Type /OCG /Name (Braille) >>",
            "<< /Type /OCG /Name (Draft) >>",
            "<< /S /GoTo /D 8 0 R >>",
            "[3 0 R /XYZ 0 792 0.5]");

        var settings = document.Settings;

        Assert.Equal(("1.7", null, "GoTo p1 XYZ 0 792 0.5"), (settings.PdfVersion, settings.CatalogVersion, settings.OpenAction));
        Assert.Equal([new Layer("Braille", true), new Layer("Draft", false)], settings.Layers);
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
    public void Load_ShouldReportJavaScript_WhenANameTreeOrChainedActionHasIt(string catalogEntries)
    {
        var pdf = new TestPdf().CatalogEntries(catalogEntries);
        pdf.AddPage();

        var settings = pdf.Load().Settings;

        Assert.Equal((true, false), (settings.HasJavaScript, settings.HasAdditionalActions));
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
        Assert.Equal(DocumentPermissions.Print | DocumentPermissions.ExtractForAccessibility, settings.Permissions);
    }

    [Fact]
    public void Load_ShouldReadOtherInfoAndXmpDetails_WhenMetadataHasThem()
    {
        var pdf = new TestPdf
        {
            InfoEntries = "/Company (Example Pharmacy) /Trapped /False /SourceModified (D:20210101)",
            Xmp = "<dc:creator><rdf:Seq><rdf:li>Ann</rdf:li><rdf:li>Bo</rdf:li></rdf:Seq></dc:creator>"
                + "<dc:description><rdf:Alt><rdf:li xml:lang=\"x-default\">Drug guide</rdf:li></rdf:Alt></dc:description>"
                + "<dc:language><rdf:Bag><rdf:li>en-US</rdf:li></rdf:Bag></dc:language>"
                + "<pdfaid:part>2</pdfaid:part><pdfaid:conformance>B</pdfaid:conformance>",
        };
        pdf.AddPage();

        var metadata = pdf.Load().Metadata;

        Setting[] expectedInfo = [new("Company", "Example Pharmacy"), new("SourceModified", "D:20210101"), new("Trapped", "False")];
        Assert.Equal(expectedInfo, metadata.OtherInfo);
        Assert.Equal(("Ann; Bo", "Drug guide", "en-US", "2B"),
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
        Assert.Equal(new PageBox(10, 10, 592, 772), boxed.CropBox);
        Assert.Equal(new PageBox(0, 0, 612, 792), boxed.BleedBox);
        Assert.Equal(new PageBox(20, 20, 572, 752), boxed.TrimBox);
        Assert.Equal(new PageBox(30, 30, 552, 732), boxed.ArtBox);
        Assert.Equal(2.0, boxed.UserUnit);
        Assert.Equal(new PageGroup("Transparency", "DeviceRGB", Isolated: true, Knockout: false), boxed.Group);
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

        Assert.Equal(new PageBox(5, 5, 595, 775), Assert.Single(document.Pages).CropBox);
    }
}
