using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

public class FormsTests
{
    [Fact]
    public void Load_ShouldListEveryAnnotationWithRectFlagsAndRole_WhenPageHasAnnotations()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "example.com", "Link")
            .Annotation("/Subtype /Text /Rect [120 130 100 100] /F 6 /Contents (Check the stage)");
        pdf.Structure(new TestElement("Link", 0, new TestLink("https://example.com", "Example site")));

        var annotations = pdf.Load().Annotations;

        Assert.Equal(2, annotations.Count);
        var (link, note) = (annotations[0], annotations[1]);
        Assert.Equal(("Link", ContentRole.Tagged, "https://example.com"), (link.Subtype, link.Role, link.Uri));
        Assert.Equal(("Text", ContentRole.Untagged, "Check the stage"), (note.Subtype, note.Role, note.Contents));
        Assert.Equal((1, 100, 100, 20, 30), (note.Page!.Value, note.X, note.Y, note.Width, note.Height));
        Assert.Equal(AnnotationFlags.Hidden | AnnotationFlags.Print, note.Flags);
    }

    [Theory]
    [InlineData("/Dest [{page2} /XYZ 72.4 720 0]", 2, "XYZ", new[] { 72, 720 })]
    [InlineData("/A << /S /GoTo /D [{page2} /FitH 500] >>", 2, "FitH", new[] { 500 })]
    [InlineData("/Dest (trails)", 2, "Fit", new int[0])]
    [InlineData("/Dest /safety", 1, "FitR", new[] { 10, 20, 300, 400 })]
    public void Load_ShouldReadWhereALinkGoes_WhenItHasADestination(string target, int page, string view, int[] positions)
    {
        var pdf = new TestPdf().CatalogEntries("/Names << /Dests << /Names [(trails) [{page2} /Fit]] >> >> "
            + "/Dests << /safety << /D [{page1} /FitR 10 20 300 400] >> >>");
        pdf.AddPage().Annotation($"/Subtype /Link /Rect [72 700 172 720] {target}");
        pdf.AddPage();

        var destination = Assert.Single(pdf.Load().Annotations).Destination;

        Assert.NotNull(destination);
        Assert.Equal((page, view), (destination.Page, destination.View));
        Assert.Equal(positions.Select(p => (int?)p), destination.Positions);
    }

    [Theory]
    [InlineData("/XYZ 72 720 2", 2.0)]
    [InlineData("/XYZ 72 720 0", null)]
    [InlineData("/XYZ 72 720 null", null)]
    public void Load_ShouldReadTheZoom_WhenALinkOpensAtAPosition(string view, double? zoom)
    {
        var pdf = new TestPdf();
        pdf.AddPage().Annotation($"/Subtype /Link /Rect [72 700 172 720] /Dest [{{page1}} {view}]");

        var destination = Assert.Single(pdf.Load().Annotations).Destination;

        Assert.Equal(zoom, destination?.Zoom);
    }

    [Fact]
    public void Load_ShouldKeepTheName_WhenANamedDestinationIsNotDefined()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Annotation("/Subtype /Link /Rect [72 700 172 720] /Dest (missing)");

        var destination = Assert.Single(pdf.Load().Annotations).Destination;

        Assert.Equal(("missing", null, null), (destination?.Name, destination?.Page, destination?.View));
    }

    [Fact]
    public void Load_ShouldReadTheFileAndPage_WhenALinkGoesToAnotherFile()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Annotation("/Subtype /Link /Rect [72 700 172 720] /A << /S /GoToR /F (maps.pdf) /D [1 /Fit] >>")
            .Annotation("/Subtype /Link /Rect [72 600 172 620] /A << /S /Launch /F << /Type /Filespec /UF (guide.docx) >> >>");

        var annotations = pdf.Load().Annotations;

        Assert.Equal(("GoToR", "maps.pdf", 2), (annotations[0].Action, annotations[0].File, annotations[0].Destination?.Page));
        Assert.Equal(("Launch", "guide.docx"), (annotations[1].Action, annotations[1].File));
    }

    [Fact]
    public void Load_ShouldReadTextFieldNameValueAndFlags_WhenFormHasATextField()
    {
        var pdf = new TestPdf().FormField("street", "/FT /Tx /TU (Street address) /V (12 Main St) /Ff 4098 /MaxLen 40");
        pdf.AddPage();

        var field = Assert.Single(pdf.Load().FormFields);

        Assert.Equal(("street", FormFieldType.Text, 1), (field.Name, field.Type, field.Page!.Value));
        Assert.Equal(("Street address", "12 Main St", 40), (field.AccessibleName, field.Value, field.MaxLength!.Value));
        Assert.Equal(FormFieldFlags.Required | FormFieldFlags.Multiline, field.Flags);
    }

    [Fact]
    public void Load_ShouldTellButtonAndChoiceFieldsApart_WhenFormHasEachKind()
    {
        var pdf = new TestPdf()
            .FormField("agree", "/FT /Btn /V /Yes")
            .FormField("size", "/FT /Btn /Ff 32768 /V /Small")
            .FormField("submit", "/FT /Btn /Ff 65536")
            .FormField("color", "/FT /Ch /Ff 131072 /Opt [(Red) [(g) (Green)]] /V (Green)")
            .FormField("toppings", "/FT /Ch /Ff 2097152 /Opt [(Mushrooms) (Olives)] /V [(Mushrooms) (Olives)]")
            .FormField("signature", "/FT /Sig");
        pdf.AddPage();

        var fields = pdf.Load().FormFields;

        FormFieldType[] types =
        [
            FormFieldType.Checkbox, FormFieldType.RadioButton, FormFieldType.PushButton, FormFieldType.ComboBox,
            FormFieldType.ListBox, FormFieldType.Signature,
        ];
        Assert.Equal(types, fields.Select(field => field.Type));
        Assert.Equal(["Yes", "Small", null, "Green", "Mushrooms, Olives", null], fields.Select(field => field.Value));
        Assert.Equal(["Red", "Green"], fields[3].Options);
        Assert.Equal(FormFieldFlags.MultiSelect, fields[4].Flags);
    }

    [Fact]
    public void Load_ShouldJoinParentNamesAndInheritType_WhenFieldsAreNested()
    {
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R] >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [6 0 R] /Contents 4 0 R >>",
            TestPdf.Stream(""),
            "<< /T (address) /FT /Tx /Ff 2 /Kids [6 0 R] >>",
            "<< /Type /Annot /Subtype /Widget /T (city) /Parent 5 0 R /Rect [72 300 272 320] /TU (City) >>");

        var field = Assert.Single(document.FormFields);

        Assert.Equal(("address.city", FormFieldType.Text, FormFieldFlags.Required), (field.Name, field.Type, field.Flags));
        Assert.Equal((1, "City"), (field.Page!.Value, field.AccessibleName));
    }

    [Fact]
    public void Load_ShouldInheritValueAndMaxLengthAndFindTheWidgetPage_WhenWidgetsAreSeparateKids()
    {
        var document = RawPdf.Load(
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [6 0 R 7 0 R] >> >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [8 0 R] /Contents 5 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [9 0 R 11 0 R] /Contents 5 0 R >>",
            TestPdf.Stream(""),
            "<< /T (size) /FT /Btn /Ff 32768 /V /Small /Kids [8 0 R 9 0 R] >>",
            "<< /T (phone) /FT /Tx /V (555) /MaxLen 10 /Kids [10 0 R] >>",
            "<< /Type /Annot /Subtype /Widget /Parent 6 0 R /Rect [72 300 92 320] >>",
            "<< /Type /Annot /Subtype /Widget /Parent 6 0 R /Rect [72 300 92 320] >>",
            "<< /T (number) /Parent 7 0 R /Kids [11 0 R] >>",
            "<< /Type /Annot /Subtype /Widget /Parent 10 0 R /Rect [72 200 272 220] >>");

        var fields = document.FormFields;

        Assert.Equal(2, fields.Count);
        Assert.Equal(("size", FormFieldType.RadioButton, "Small", 1),
            (fields[0].Name, fields[0].Type, fields[0].Value, fields[0].Page!.Value));
        Assert.Equal(("phone.number", FormFieldType.Text, "555", 10, 2),
            (fields[1].Name, fields[1].Type, fields[1].Value, fields[1].MaxLength!.Value, fields[1].Page!.Value));
    }

    [Fact]
    public void Load_ShouldRecordTheSignatureButNotItsBytes_WhenASignatureFieldIsSigned()
    {
        var document = LoadSigned("/SubFilter /adbe.pkcs7.detached /Contents <0102> /M (D:20261030120000Z) /Reason (Approved) "
            + "/Location (Trailhead office)");

        var expected = new SignatureInfo
        {
            SubFilter = "adbe.pkcs7.detached", CoversWholeFile = true, HasReason = true, HasLocation = true, HasSigningTime = true,
        };
        Assert.Equal(expected, Assert.Single(document.FormFields).Signature);
        Assert.Contains("  signature \"approval\" p1 signed=yes subfilter=adbe.pkcs7.detached covers-file=yes reason=<present> "
            + "location=<present> signing-time=<present>\n", document.ToSnapshot());
    }

    [Fact]
    public void Load_ShouldSayTheSignatureDoesNotCoverTheFile_WhenAnUpdateWasSavedAfterSigning()
    {
        var document = LoadSigned("/SubFilter /adbe.pkcs7.detached", updatedAfterSigning: true);

        Assert.False(Assert.Single(document.FormFields).Signature!.CoversWholeFile);
    }

    [Theory]
    [InlineData("/TransformParams << /P 1 >>", SignatureCertification.NoChanges)]
    [InlineData("/TransformParams << /P 3 >>", SignatureCertification.FormFillingAndAnnotations)]
    [InlineData("", SignatureCertification.FormFilling)]
    public void Load_ShouldReadTheCertificationLevel_WhenTheSignatureCertifiesTheDocument(string parameters,
        SignatureCertification level)
    {
        var document = LoadSigned($"/Reference [<< /Type /SigRef /TransformMethod /DocMDP {parameters} >>]");

        Assert.Equal(level, Assert.Single(document.FormFields).Signature!.Certification);
    }

    [Fact]
    public void Load_ShouldLeaveOutTheSignature_WhenASignatureFieldIsUnsigned()
    {
        var pdf = new TestPdf().FormField("blank", "/FT /Sig");
        pdf.AddPage();

        Assert.Null(Assert.Single(pdf.Load().FormFields).Signature);
    }

    [Fact]
    public void Load_ShouldLeaveOutTheCertification_WhenTheSignatureOnlyLocksFields()
    {
        var document = LoadSigned("/Reference [<< /TransformMethod /FieldMDP >>]");

        Assert.Null(Assert.Single(document.FormFields).Signature!.Certification);
    }

    // Built twice: once to learn the file's length, then with a byte range that runs to the end. The range's numbers are
    // padded to a fixed width, so the second build is as long as the first. An update saved after signing comes after it.
    private static PdfUaDocument LoadSigned(string signatureEntries, bool updatedAfterSigning = false)
    {
        TestPdf Build(long end, bool update)
        {
            var pdf = new TestPdf { Layout = new PdfLayout(EmptyUpdate: update) }.FormField("approval",
                $"/FT /Sig /V << /Type /Sig /ByteRange [0 0000000010 0000000020 {end - 20:D10}] {signatureEntries} >>");
            pdf.AddPage();
            return pdf;
        }

        var length = Build(20, update: false).Build().Length;
        return Build(length, updatedAfterSigning).Load();
    }
}
