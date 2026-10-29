namespace PDiffy.Tests.Unit.Docx;

// Each case makes one small change that matters to a known DOCX and checks that the snapshot catches it, in the right
// section and as the right kind of change. DOCX changes have no Section value, so the path is matched.
public class DocxMutationTests
{
    private static readonly byte[] Logo = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    public static TheoryData<string, Sample, DocxSection, ChangeKind> Mutations => new()
    {
        { "paragraph style", new Sample { Style = "Quote" }, DocxSection.Body, ChangeKind.AttributeChanged },
        { "alignment", new Sample { Alignment = "center" }, DocxSection.Body, ChangeKind.AttributeChanged },
        { "indent", new Sample { IndentTwips = 1440 }, DocxSection.Body, ChangeKind.AttributeChanged },
        { "spacing", new Sample { SpacingAfterTwips = 240 }, DocxSection.Body, ChangeKind.AttributeChanged },
        { "bold word", new Sample { WordFormat = "<w:b/>" }, DocxSection.Body, ChangeKind.Added },
        { "italic word", new Sample { WordFormat = "<w:i/>" }, DocxSection.Body, ChangeKind.Added },
        { "word size", new Sample { WordFormat = "<w:sz w:val=\"32\"/>" }, DocxSection.Body, ChangeKind.Added },
        { "list level", new Sample { SecondItemLevel = 1 }, DocxSection.Body, ChangeKind.AttributeChanged },
        { "list restart", new Sample { SecondItemList = 2 }, DocxSection.Body, ChangeKind.AttributeChanged },
        { "header row flag", new Sample { HeaderRow = false }, DocxSection.Body, ChangeKind.Changed },
        { "cell merge", new Sample { CellSpan = 2 }, DocxSection.Body, ChangeKind.AttributeChanged },
        { "image alt text", new Sample { ImageAlt = "Store logo" }, DocxSection.Body, ChangeKind.AttributeChanged },
        { "image bytes", new Sample { ImageBytes = [0x89, 0x50, 0x4E, 0x47, 9, 9, 9] }, DocxSection.Body, ChangeKind.AttributeChanged },
        { "field result", new Sample { PageResult = "4" }, DocxSection.Body, ChangeKind.AttributeChanged },
        { "header text", new Sample { HeaderText = "Trail leaflet" }, DocxSection.Headers, ChangeKind.TextChanged },
        { "footnote text", new Sample { FootnoteText = "Source: leaflet" }, DocxSection.Notes, ChangeKind.TextChanged },
        { "tracked change added", new Sample { TrackedInsert = true }, DocxSection.Body, ChangeKind.Added },
        { "comment added", new Sample { Comment = true }, DocxSection.Body, ChangeKind.Added },
    };

    [Theory]
    [MemberData(nameof(Mutations))]
    public void CompareTo_ShouldReportTheChange_WhenAMeaningfulDetailChanges(string mutation, Sample changed,
        DocxSection section, ChangeKind kind)
    {
        var original = Build(new Sample());
        var mutated = Build(changed);

        var diff = original.CompareTo(mutated);

        Assert.NotEqual(original.ToSnapshot(), mutated.ToSnapshot());
        var path = SnapshotWriter.Name(section) + " > ";
        Assert.True(diff.Changes.Any(item => item.Path.StartsWith(path) && item.Kind == kind), $"{mutation}:\n{diff.Describe()}");
    }

    /// <summary>The details of the base document that a mutation can change. Lengths are in twentieths of a point.</summary>
    public sealed record Sample
    {
        public string Style { get; init; } = "BodyText";
        public string Alignment { get; init; } = "left";
        public int IndentTwips { get; init; } = 720;
        public int SpacingAfterTwips { get; init; } = 120;
        public string WordFormat { get; init; } = "";
        public int SecondItemLevel { get; init; }
        public int SecondItemList { get; init; } = 1;
        public bool HeaderRow { get; init; } = true;
        public int CellSpan { get; init; } = 1;
        public string ImageAlt { get; init; } = "Park logo";
        public byte[] ImageBytes { get; init; } = Logo;
        public string PageResult { get; init; } = "3";
        public string HeaderText { get; init; } = "Trail guide";
        public string FootnoteText { get; init; } = "Source: label";
        public bool TrackedInsert { get; init; }
        public bool Comment { get; init; }
    }

    private const string Styles = """
        <w:style w:type="paragraph" w:styleId="BodyText"><w:name w:val="Body Text"/></w:style>
        <w:style w:type="paragraph" w:styleId="Quote"><w:name w:val="Quote"/><w:rPr><w:i/></w:rPr></w:style>
        """;

    private const string Numbering = """
        <w:abstractNum w:abstractNumId="1">
          <w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%1."/></w:lvl>
          <w:lvl w:ilvl="1"><w:start w:val="1"/><w:numFmt w:val="lowerLetter"/><w:lvlText w:val="%2)"/></w:lvl>
        </w:abstractNum>
        <w:num w:numId="1"><w:abstractNumId w:val="1"/></w:num>
        <w:num w:numId="2"><w:abstractNumId w:val="1"/><w:lvlOverride w:ilvl="0"><w:startOverride w:val="1"/></w:lvlOverride></w:num>
        """;

    // A styled paragraph with a formatted word, a footnote and a page field; a two-item list; a table; an image; and
    // a section with a header.
    internal static DocxDocument Build(Sample sample)
    {
        var insert = sample.TrackedInsert ? """<w:ins w:id="10" w:author="Ann"><w:r><w:t xml:space="preserve"> daily</w:t></w:r></w:ins>""" : "";
        var comment = sample.Comment ? """<w:r><w:commentReference w:id="0"/></w:r>""" : "";
        // A cell spanning both columns replaces the second cell, as Word merges them.
        var span = sample.CellSpan == 1 ? "" : $"""<w:tcPr><w:gridSpan w:val="{sample.CellSpan}"/></w:tcPr>""";
        var secondCell = sample.CellSpan == 1 ? "<w:tc><w:p><w:r><w:t>Stage</w:t></w:r></w:p></w:tc>" : "";
        var header = sample.HeaderRow ? "<w:trPr><w:tblHeader/></w:trPr>" : "";
        var body = $"""
            <w:p><w:pPr><w:pStyle w:val="{sample.Style}"/><w:jc w:val="{sample.Alignment}"/><w:ind w:left="{sample.IndentTwips}"/>
                <w:spacing w:after="{sample.SpacingAfterTwips}"/></w:pPr>
              <w:r><w:t xml:space="preserve">Go </w:t></w:r><w:r><w:rPr>{sample.WordFormat}</w:rPr><w:t>with</w:t></w:r>
              <w:r><w:t xml:space="preserve"> a guide</w:t></w:r>{insert}<w:r><w:footnoteReference w:id="1"/></w:r>{comment}
              <w:r><w:t xml:space="preserve"> page </w:t></w:r><w:fldSimple w:instr="PAGE"><w:r><w:t>{sample.PageResult}</w:t></w:r></w:fldSimple></w:p>
            <w:p><w:pPr><w:numPr><w:ilvl w:val="0"/><w:numId w:val="1"/></w:numPr></w:pPr><w:r><w:t>Ridgeline</w:t></w:r></w:p>
            <w:p><w:pPr><w:numPr><w:ilvl w:val="{sample.SecondItemLevel}"/><w:numId w:val="{sample.SecondItemList}"/></w:numPr></w:pPr>
              <w:r><w:t>Cedars</w:t></w:r></w:p>
            <w:tbl><w:tblGrid><w:gridCol w:w="2880"/><w:gridCol w:w="2880"/></w:tblGrid>
              <w:tr>{header}<w:tc>{span}<w:p><w:r><w:t>Trail</w:t></w:r></w:p></w:tc>{secondCell}</w:tr></w:tbl>
            <w:p><w:r><w:drawing xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
              <wp:inline><wp:extent cx="1524000" cy="508000"/><wp:docPr id="1" name="Picture 1" descr="{sample.ImageAlt}"/>
                <a:graphic><a:graphicData><a:blip r:embed="logo"/></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>
            <w:sectPr><w:headerReference w:type="default" r:id="top"/></w:sectPr>
            """;
        var docx = new TestDocx { Styles = Styles, Numbering = Numbering, Body = body }
            .WithImage("logo", "image1.png", sample.ImageBytes)
            .WithHeader("top", $"<w:p><w:r><w:t>{sample.HeaderText}</w:t></w:r></w:p>")
            .WithXmlPart("notes", "footnotes", "footnotes", $"""<w:footnote w:id="1"><w:p><w:r><w:t>{sample.FootnoteText}</w:t></w:r></w:p></w:footnote>""");
        if (sample.Comment)
        {
            docx.WithXmlPart("remarks", "comments", "comments", """<w:comment w:id="0" w:author="Ann"><w:p><w:r><w:t>Check</w:t></w:r></w:p></w:comment>""");
        }

        return docx.Load();
    }
}
