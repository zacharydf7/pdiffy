using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

public class FormsTests
{
    [Fact]
    public void Load_ShouldListEveryAnnotationWithRectFlagsAndRole_WhenPageHasAnnotations()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "example.com", "Link")
            .Annotation("/Subtype /Text /Rect [120 130 100 100] /F 6 /Contents (Check the dose)");
        pdf.Structure(new TestElement("Link", 0, new TestLink("https://example.com", "Example site")));

        var annotations = pdf.Load().Annotations;

        Assert.Equal(2, annotations.Count);
        var (link, note) = (annotations[0], annotations[1]);
        Assert.Equal(("Link", ContentRole.Tagged, "https://example.com"), (link.Subtype, link.Role, link.Uri));
        Assert.Equal(("Text", ContentRole.Untagged, "Check the dose"), (note.Subtype, note.Role, note.Contents));
        Assert.Equal((1, 100, 100, 20, 30), (note.Page!.Value, note.X, note.Y, note.Width, note.Height));
        Assert.Equal(AnnotationFlags.Hidden | AnnotationFlags.Print, note.Flags);
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
            .FormField("toppings", "/FT /Ch /Ff 2097152 /Opt [(Cheese) (Olives)] /V [(Cheese) (Olives)]")
            .FormField("signature", "/FT /Sig");
        pdf.AddPage();

        var fields = pdf.Load().FormFields;

        FormFieldType[] types =
        [
            FormFieldType.Checkbox, FormFieldType.RadioButton, FormFieldType.PushButton, FormFieldType.ComboBox,
            FormFieldType.ListBox, FormFieldType.Signature,
        ];
        Assert.Equal(types, fields.Select(field => field.Type));
        Assert.Equal(["Yes", "Small", null, "Green", "Cheese, Olives", null], fields.Select(field => field.Value));
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
}
