using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

// Each case makes one small change that matters to a known PDF and checks that the snapshot catches it, in the right
// section, as the right kind of change, and flagged for accessibility when it should be. Adding a new fact to the
// snapshot means adding a case here.
public class MutationTests
{
    public static TheoryData<string, Sample, PdfSection, ChangeKind, bool> Mutations => new()
    {
        { "heading level", new Sample { Heading = "H3" }, PdfSection.Structure, ChangeKind.Changed, true },
        { "heading made a paragraph", new Sample { Heading = "P" }, PdfSection.Structure, ChangeKind.Changed, true },
        { "alt text removed", new Sample { Alt = null }, PdfSection.Structure, ChangeKind.AttributeChanged, true },
        { "alt text emptied", new Sample { Alt = "" }, PdfSection.Structure, ChangeKind.AttributeChanged, true },
        { "alt text edited", new Sample { Alt = "Store logo" }, PdfSection.Structure, ChangeKind.AttributeChanged, false },
        { "reading order swapped", new Sample { SwapParagraphs = true }, PdfSection.Structure, ChangeKind.Moved, true },
        { "header scope removed", new Sample { Scope = null }, PdfSection.Structure, ChangeKind.AttributeChanged, true },
        { "cell headers changed", new Sample { Headers = "(h-other)" }, PdfSection.Structure, ChangeKind.AttributeChanged, true },
        { "tagged text made untagged", new Sample { NoteTagged = false }, PdfSection.Untagged, ChangeKind.Added, true },
        { "tagged text made an artifact", new Sample { NoteArtifact = true }, PdfSection.Layout, ChangeKind.Changed, true },
        { "artifact made tagged", new Sample { FooterTagged = true }, PdfSection.Artifacts, ChangeKind.Removed, false },
        { "artifact kind", new Sample { FooterKind = "Header" }, PdfSection.Artifacts, ChangeKind.AttributeChanged, false },
        { "span language", new Sample { SpanLang = "de-DE" }, PdfSection.Structure, ChangeKind.AttributeChanged, true },
        { "document language", new Sample { DocumentLang = "en-GB" }, PdfSection.Document, ChangeKind.AttributeChanged, true },
        { "element language", new Sample { ElementLang = "fr-FR" }, PdfSection.Structure, ChangeKind.AttributeChanged, true },
        { "link target", new Sample { LinkUri = "https://example.com/b" }, PdfSection.Annotations, ChangeKind.AttributeChanged, false },
        { "link destination page", new Sample { LinkPage = 1 }, PdfSection.Annotations, ChangeKind.AttributeChanged, false },
        { "image pixels", new Sample { ImageGray = "81" }, PdfSection.Layout, ChangeKind.AttributeChanged, false },
        { "figure's image", new Sample { ImageGray = "81" }, PdfSection.Structure, ChangeKind.AttributeChanged, false },
        { "text moved by 1 pt", new Sample { TextAt = "73 400" }, PdfSection.Layout, ChangeKind.AttributeChanged, false },
        { "font size by 0.1 pt", new Sample { FontSize = "12.1" }, PdfSection.Layout, ChangeKind.AttributeChanged, false },
        { "text color", new Sample { TextColor = "1 0 0 rg" }, PdfSection.Layout, ChangeKind.AttributeChanged, false },
        { "field's accessible name removed", new Sample { FieldEntries = "/FT /Tx /Ff 2" }, PdfSection.Forms, ChangeKind.AttributeChanged, true },
        { "field no longer required", new Sample { FieldEntries = "/FT /Tx /TU (Hiker name)" }, PdfSection.Forms, ChangeKind.AttributeChanged, false },
        { "field text size", new Sample { FieldAppearance = "/F1 14 Tf 0 g" }, PdfSection.Forms, ChangeKind.AttributeChanged, false },
        { "field script edited", new Sample { FieldScript = "AFNumber_Format(2)" }, PdfSection.Forms, ChangeKind.AttributeChanged, false },
        { "form no longer redrawn by the viewer", new Sample { FormEntries = "" }, PdfSection.Forms, ChangeKind.Removed, false },
        { "signature removed", new Sample { SignatureEntries = "/FT /Sig" }, PdfSection.Forms, ChangeKind.AttributeChanged, false },
        { "bookmark target page", new Sample { BookmarkPage = 1 }, PdfSection.Bookmarks, ChangeKind.Changed, false },
        { "tags marked as unreliable", new Sample { MarkInfo = "/Marked true /Suspects true" }, PdfSection.Document, ChangeKind.AttributeChanged, false },
        { "parent entry left out", new Sample { FigureWithoutParent = true }, PdfSection.Structure, ChangeKind.AttributeChanged, false },
        { "form field role", new Sample { FieldRole = "cb" }, PdfSection.Structure, ChangeKind.AttributeChanged, true },
        { "media clip alt text", new Sample { MediaAlt = "Ridge tour" }, PdfSection.Annotations, ChangeKind.AttributeChanged, false },
        { "media clip alt text removed", new Sample { MediaAlt = null }, PdfSection.Annotations, ChangeKind.AttributeChanged, true },
        { "artifact moved inside tagged content", new Sample { ArtifactInTagged = true }, PdfSection.Pages, ChangeKind.AttributeChanged, false },
        { "tagged content moved inside an artifact", new Sample { ArtifactHoldsTagged = true }, PdfSection.Pages, ChangeKind.AttributeChanged, false },
        { "layer configuration renamed", new Sample { LayerConfig = "Print" }, PdfSection.Settings, ChangeKind.TextChanged, false },
        { "reference XObject added", new Sample { ReferenceForm = true }, PdfSection.Settings, ChangeKind.AttributeChanged, false },
        { "embedded file named one way", new Sample { AttachmentNames = "/F (data.xml)" }, PdfSection.Settings, ChangeKind.AttributeChanged, false },
        { "TrueType font's encoding removed", new Sample { FontEncoding = "" }, PdfSection.Fonts, ChangeKind.AttributeChanged, false },
    };

    [Theory]
    [MemberData(nameof(Mutations))]
    public void CompareTo_ShouldReportTheChange_WhenAMeaningfulDetailChanges(string mutation, Sample changed,
        PdfSection section, ChangeKind kind, bool affectsAccessibility)
    {
        var original = Build(new Sample());
        var mutated = Build(changed);

        var diff = original.CompareTo(mutated);

        Assert.NotEqual(original.ToSnapshot(), mutated.ToSnapshot());
        Assert.True(diff.Changes.Any(item => item.PdfSection == section && item.Kind == kind),
            $"{mutation}:\n{diff.Describe()}");
        Assert.Equal(affectsAccessibility, diff.Changes.Any(item => item.AffectsAccessibility == true));
    }

    /// <summary>The details of the base document that a mutation can change.</summary>
    public sealed record Sample
    {
        public string Heading { get; init; } = "H2";
        public string? Alt { get; init; } = "Park logo";
        public bool SwapParagraphs { get; init; }
        public string? Scope { get; init; } = "Column";
        public string Headers { get; init; } = "(h-trail)";
        public bool NoteTagged { get; init; } = true;
        public bool NoteArtifact { get; init; }
        public bool FooterTagged { get; init; }
        public string FooterKind { get; init; } = "Footer";
        public string SpanLang { get; init; } = "fr-FR";
        public string DocumentLang { get; init; } = "en-US";
        public string ElementLang { get; init; } = "es-ES";
        public string LinkUri { get; init; } = "https://example.com/a";
        public int LinkPage { get; init; } = 2;
        public string ImageGray { get; init; } = "80";
        public string TextAt { get; init; } = "72 400";
        public string FontSize { get; init; } = "12";
        public string TextColor { get; init; } = "0 g";
        public string FieldEntries { get; init; } = "/FT /Tx /TU (Hiker name) /Ff 2";
        public string FieldAppearance { get; init; } = "/F1 12 Tf 0 g";
        public string FieldScript { get; init; } = "AFNumber_Format(0)";
        public string FormEntries { get; init; } = "/NeedAppearances true";
        public string SignatureEntries { get; init; } =
            "/FT /Sig /V << /Type /Sig /SubFilter /adbe.pkcs7.detached /Reason (Approved) /Reference [<< /TransformMethod /DocMDP >>] >>";
        public int BookmarkPage { get; init; } = 2;
        public string MarkInfo { get; init; } = "/Marked true";
        public bool FigureWithoutParent { get; init; }
        public string FieldRole { get; init; } = "rb";
        public string? MediaAlt { get; init; } = "Trail tour";
        public bool ArtifactInTagged { get; init; }
        public bool ArtifactHoldsTagged { get; init; }
        public string LayerConfig { get; init; } = "Screen";
        public bool ReferenceForm { get; init; }
        public string? AttachmentNames { get; init; } = "/F (data.xml) /UF (data.xml)";
        public string FontEncoding { get; init; } = "/Encoding /WinAnsiEncoding";
    }

    // Page 1 has a heading, two paragraphs, a figure, a note, a footer, a styled line, a table, a link, a link to page 2
    // and two form fields, one signed; page 2 has the paragraph the bookmark points to and a Screen annotation's video.
    internal static PdfUaDocument Build(Sample sample)
    {
        var pdf = new TestPdf
            {
                Lang = sample.DocumentLang, MarkInfo = sample.MarkInfo, FontType = "TrueType",
                FontEncoding = sample.FontEncoding,
            }
            .CatalogEntries($" /OCProperties << /OCGs [] /D << /Name ({sample.LayerConfig}) >> >>");
        if (sample.AttachmentNames is not null)
        {
            pdf.CatalogEntries(" /Names << /EmbeddedFiles << /Names [(data.xml) << /Type /Filespec "
                + $"{sample.AttachmentNames} /Desc (Trail counts by month) /AFRelationship /Data >>] >> >>");
        }
        var page = pdf.AddPage().Tagged(0, "Elevation", sample.Heading).Tagged(1, "Go with a guide").Tagged(2, "Avoid shortcuts")
            .TaggedImage(3, sample.ImageGray)
            .Raw($"/P << /MCID 6 >> BDC q BT /F1 {sample.FontSize} Tf {sample.TextColor} {sample.TextAt} Td (Styled) Tj ET Q EMC")
            .Tagged(7, "Trail", "TH").Tagged(8, "Ridgeline", "TD").Tagged(9, "example.com", "Link")
            .Annotation($"/Subtype /Link /Rect [72 200 172 220] /Dest [{{page{sample.LinkPage}}} /XYZ 72 720 0] /Contents (Safety)");
        if (sample.ReferenceForm) page.ReferenceForm();
        if (sample.NoteArtifact) page.Artifact("Pack light");
        else if (sample.NoteTagged) page.Tagged(4, "Pack light");
        else page.Untagged("Pack light");
        if (sample.FooterTagged) page.Tagged(5, "Page 1");
        else page.Artifact("Page 1", $"/Type /Pagination /Subtype /{sample.FooterKind}");
        page.Raw($"/P << /MCID 10 >> BDC BT /F1 12 Tf 72 150 Td (Say ) Tj /Span << /Lang ({sample.SpanLang}) >> BDC (bonjour) Tj EMC ET EMC");
        var mediaAlt = sample.MediaAlt is null ? "" : $"/Alt [(en-US) ({sample.MediaAlt})]";
        var watermark = sample.ArtifactInTagged ? "/P << /MCID 1 >> BDC /Artifact BMC EMC EMC"
            : sample.ArtifactHoldsTagged ? "/Artifact BMC /P << /MCID 1 >> BDC EMC EMC" : "/Artifact BMC EMC";
        pdf.AddPage().Tagged(0, "Safety").Annotation("/Subtype /Screen /Rect [0 0 10 10] /A << /S /Rendition "
            + $"/R << /S /MR /C << /S /MCD /CT (video/mp4) {mediaAlt} >> >> >>").Raw(watermark);

        var paragraphs = new[] { new TestElement("P", 1), new TestElement("P", 2) { Entries = $"/Lang ({sample.ElementLang})" } };
        if (sample.SwapParagraphs) paragraphs = [paragraphs[1], paragraphs[0]];
        var alt = sample.Alt is null ? "" : $"/Alt ({sample.Alt})";
        var scope = sample.Scope is null ? "" : $"/Scope /{sample.Scope}";
        var elements = new List<TestElement> { new(sample.Heading, 0) };
        elements.AddRange(paragraphs);
        elements.Add(new TestElement("Figure", 3) { Entries = alt, WithoutParent = sample.FigureWithoutParent });
        elements.Add(new TestElement("P", 6));
        elements.Add(new TestElement("P", 10));
        elements.Add(new TestElement("Table",
            new TestElement("TR", new TestElement("TH", 7) { Entries = $"/ID (h-trail) /A << /O /Table {scope} >>" }),
            new TestElement("TR", new TestElement("TD", 8) { Entries = $"/A << /O /Table /Headers [{sample.Headers}] >>" })));
        elements.Add(new TestElement("Link", 9, new TestLink(sample.LinkUri, "Trail database")));
        if (sample.NoteTagged && !sample.NoteArtifact) elements.Add(new TestElement("Note", 4));
        if (sample.FooterTagged) elements.Add(new TestElement("P", 5));
        elements.Add(new TestElement("Form") { Entries = $"/A << /O /PrintField /Role /{sample.FieldRole} >>" });
        elements.Add(new TestElement("P", 0) { Page = 2 });
        pdf.Structure(new TestElement("Document", [.. elements]));
        pdf.Bookmark("Safety", sample.BookmarkPage);
        pdf.FormField("hiker", $"{sample.FieldEntries} /DA ({sample.FieldAppearance}) /AA << /F << /S /JavaScript /JS ({sample.FieldScript}) >> >>").FormField("approval", sample.SignatureEntries)
            .FormEntries(sample.FormEntries);
        return pdf.Load();
    }
}
