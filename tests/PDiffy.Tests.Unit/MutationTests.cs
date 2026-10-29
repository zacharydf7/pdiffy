using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

// Each case makes one small change that matters to a known PDF and checks that the snapshot catches it, in the right
// section, as the right kind of change, and flagged for accessibility when it should be. Adding a new fact to the
// snapshot means adding a case here.
public class MutationTests
{
    public static TheoryData<string, Sample, Section, ChangeKind, bool> Mutations => new()
    {
        { "heading level", new Sample { Heading = "H3" }, Section.Structure, ChangeKind.Changed, true },
        { "heading made a paragraph", new Sample { Heading = "P" }, Section.Structure, ChangeKind.Changed, true },
        { "alt text removed", new Sample { Alt = null }, Section.Structure, ChangeKind.AttributeChanged, true },
        { "alt text emptied", new Sample { Alt = "" }, Section.Structure, ChangeKind.AttributeChanged, true },
        { "alt text edited", new Sample { Alt = "Store logo" }, Section.Structure, ChangeKind.AttributeChanged, false },
        { "reading order swapped", new Sample { SwapParagraphs = true }, Section.Structure, ChangeKind.Moved, true },
        { "header scope removed", new Sample { Scope = null }, Section.Structure, ChangeKind.AttributeChanged, true },
        { "cell headers changed", new Sample { Headers = "(h-other)" }, Section.Structure, ChangeKind.AttributeChanged, true },
        { "tagged text made untagged", new Sample { NoteTagged = false }, Section.Untagged, ChangeKind.Added, true },
        { "artifact made tagged", new Sample { FooterTagged = true }, Section.Artifacts, ChangeKind.Removed, false },
        { "document language", new Sample { DocumentLang = "en-GB" }, Section.Document, ChangeKind.AttributeChanged, true },
        { "element language", new Sample { ElementLang = "fr-FR" }, Section.Structure, ChangeKind.AttributeChanged, true },
        { "link target", new Sample { LinkUri = "https://example.com/b" }, Section.Annotations, ChangeKind.AttributeChanged, false },
        { "image pixels", new Sample { ImageGray = "81" }, Section.Layout, ChangeKind.AttributeChanged, false },
        { "text moved by 1 pt", new Sample { TextAt = "73 400" }, Section.Layout, ChangeKind.AttributeChanged, false },
        { "font size by 0.1 pt", new Sample { FontSize = "12.1" }, Section.Layout, ChangeKind.AttributeChanged, false },
        { "text color", new Sample { TextColor = "1 0 0 rg" }, Section.Layout, ChangeKind.AttributeChanged, false },
        { "field's accessible name removed", new Sample { FieldEntries = "/FT /Tx /Ff 2" }, Section.Forms, ChangeKind.AttributeChanged, true },
        { "field no longer required", new Sample { FieldEntries = "/FT /Tx /TU (Patient name)" }, Section.Forms, ChangeKind.AttributeChanged, false },
        { "bookmark target page", new Sample { BookmarkPage = 1 }, Section.Bookmarks, ChangeKind.Changed, false },
    };

    [Theory]
    [MemberData(nameof(Mutations))]
    public void CompareTo_ShouldReportTheChange_WhenAMeaningfulDetailChanges(string mutation, Sample changed,
        Section section, ChangeKind kind, bool affectsAccessibility)
    {
        var original = Build(new Sample());
        var mutated = Build(changed);

        var diff = original.CompareTo(mutated);

        Assert.NotEqual(original.ToSnapshot(), mutated.ToSnapshot());
        Assert.True(diff.Changes.Any(item => item.Section == section && item.Kind == kind), $"{mutation}:\n{diff.Describe()}");
        Assert.Equal(affectsAccessibility, diff.Changes.Any(item => item.AffectsAccessibility));
    }

    /// <summary>The details of the base document that a mutation can change.</summary>
    public sealed record Sample
    {
        public string Heading { get; init; } = "H2";
        public string? Alt { get; init; } = "Pharmacy logo";
        public bool SwapParagraphs { get; init; }
        public string? Scope { get; init; } = "Column";
        public string Headers { get; init; } = "(h-drug)";
        public bool NoteTagged { get; init; } = true;
        public bool FooterTagged { get; init; }
        public string DocumentLang { get; init; } = "en-US";
        public string ElementLang { get; init; } = "es-ES";
        public string LinkUri { get; init; } = "https://example.com/a";
        public string ImageGray { get; init; } = "80";
        public string TextAt { get; init; } = "72 400";
        public string FontSize { get; init; } = "12";
        public string TextColor { get; init; } = "0 g";
        public string FieldEntries { get; init; } = "/FT /Tx /TU (Patient name) /Ff 2";
        public int BookmarkPage { get; init; } = 2;
    }

    // Page 1 has a heading, two paragraphs, a figure, a note, a footer, a styled line, a table, a link and a form field;
    // page 2 has the paragraph the bookmark points to.
    private static PdfUaDocument Build(Sample sample)
    {
        var pdf = new TestPdf { Lang = sample.DocumentLang };
        var page = pdf.AddPage().Tagged(0, "Dosage", sample.Heading).Tagged(1, "Take with food").Tagged(2, "Avoid alcohol")
            .TaggedImage(3, sample.ImageGray)
            .Raw($"/P << /MCID 6 >> BDC q BT /F1 {sample.FontSize} Tf {sample.TextColor} {sample.TextAt} Td (Styled) Tj ET Q EMC")
            .Tagged(7, "Drug", "TH").Tagged(8, "Warfarin", "TD").Tagged(9, "example.com", "Link");
        if (sample.NoteTagged) page.Tagged(4, "Store cold");
        else page.Untagged("Store cold");
        if (sample.FooterTagged) page.Tagged(5, "Page 1");
        else page.Artifact("Page 1");
        pdf.AddPage().Tagged(0, "Warnings");

        var paragraphs = new[] { new TestElement("P", 1), new TestElement("P", 2) { Entries = $"/Lang ({sample.ElementLang})" } };
        if (sample.SwapParagraphs) paragraphs = [paragraphs[1], paragraphs[0]];
        var alt = sample.Alt is null ? "" : $"/Alt ({sample.Alt})";
        var scope = sample.Scope is null ? "" : $"/Scope /{sample.Scope}";
        var elements = new List<TestElement> { new(sample.Heading, 0) };
        elements.AddRange(paragraphs);
        elements.Add(new TestElement("Figure", 3) { Entries = alt });
        elements.Add(new TestElement("P", 6));
        elements.Add(new TestElement("Table",
            new TestElement("TR", new TestElement("TH", 7) { Entries = $"/ID (h-drug) /A << /O /Table {scope} >>" }),
            new TestElement("TR", new TestElement("TD", 8) { Entries = $"/A << /O /Table /Headers [{sample.Headers}] >>" })));
        elements.Add(new TestElement("Link", 9, new TestLink(sample.LinkUri, "Drug database")));
        if (sample.NoteTagged) elements.Add(new TestElement("Note", 4));
        if (sample.FooterTagged) elements.Add(new TestElement("P", 5));
        elements.Add(new TestElement("P", 0) { Page = 2 });
        pdf.Structure(new TestElement("Document", [.. elements]));
        pdf.Bookmark("Warnings", sample.BookmarkPage);
        pdf.FormField("patient", sample.FieldEntries);
        return pdf.Load();
    }
}
