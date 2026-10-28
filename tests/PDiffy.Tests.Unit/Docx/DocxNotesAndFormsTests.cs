namespace PDiffy.Tests.Unit.Docx;

public class DocxNotesAndFormsTests
{
    private const string W14 = "xmlns:w14=\"http://schemas.microsoft.com/office/word/2010/wordml\"";

    [Fact]
    public void ToSnapshot_ShouldShowWhereEachNoteIsAndWhatItSays_WhenTheDocumentHasFootnotesAndComments()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:p><w:r><w:t>Warfarin</w:t></w:r><w:r><w:footnoteReference w:id="1"/></w:r>
                  <w:r><w:t xml:space="preserve"> and food</w:t></w:r><w:r><w:commentReference w:id="0"/></w:r></w:p>
                """,
        }.WithXmlPart("notes", "footnotes", "footnotes", """
            <w:footnote w:type="separator" w:id="-1"><w:p><w:r><w:separator/></w:r></w:p></w:footnote>
            <w:footnote w:id="1"><w:p><w:r><w:t>Source: label</w:t></w:r></w:p></w:footnote>
            """).WithXmlPart("remarks", "comments", "comments", """<w:comment w:id="0" w:author="Ann"><w:p><w:r><w:t>Check this</w:t></w:r></w:p></w:comment>""");

        var snapshot = docx.Load().ToSnapshot();

        Assert.Contains("  P \"Warfarin and food\"\n    note footnote 1 at=8\n    comment 0 at=17\n", snapshot);
        Assert.EndsWith("notes\n  footnote 1\n    P \"Source: label\"\n  comment 0 author=\"Ann\"\n    P \"Check this\"\n", snapshot);
    }

    [Fact]
    public void Load_ShouldRecordTrackedChangesAndKeepDeletedTextOutOfTheParagraph_WhenChangesAreTracked()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:p><w:ins w:id="1" w:author="Ann"><w:r><w:t xml:space="preserve">new </w:t></w:r></w:ins>
                  <w:del w:id="2" w:author="Bob"><w:r><w:delText xml:space="preserve">old </w:delText></w:r></w:del><w:r><w:t>text</w:t></w:r></w:p>
                """,
        };

        var document = docx.Load();

        Assert.Equal("new text", Assert.Single(document.Paragraphs()).Text);
        Assert.Equal([new DocxRevision("insert", "new ", "Ann", 0), new DocxRevision("delete", "old ", "Bob", 4)], document.Revisions());
    }

    [Fact]
    public void Load_ShouldRecordMovesAndChangesToParagraphsAndFormatting_WhenTheyAreTracked()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:p><w:pPr><w:pPrChange w:author="Ann"><w:pPr/></w:pPrChange><w:rPr><w:ins w:author="Ann"/></w:rPr></w:pPr>
                  <w:moveFrom w:author="Bob"><w:r><w:t>Up</w:t></w:r></w:moveFrom><w:moveTo w:author="Bob"><w:r><w:t>Down</w:t></w:r></w:moveTo>
                  <w:r><w:rPr><w:b/><w:rPrChange w:author="Cy"><w:rPr/></w:rPrChange></w:rPr><w:t>!</w:t></w:r></w:p>
                """,
        };

        var revisions = docx.Load().Revisions().ToList();

        DocxRevision[] expected =
        [
            new("move-from", "Up", "Bob", 0), new("move-to", "Down", "Bob", 0), new("format", "!", "Cy", 4),
            new("paragraph-insert", "", "Ann", 5), new("paragraph-format", "", "Ann", 0),
        ];
        Assert.Equal(expected, revisions);
    }

    [Fact]
    public void Load_ShouldFindTrackedChangesInsideControlsNotesAndInsertedDocuments_WhenListingRevisions()
    {
        var change = """<w:p><w:ins w:author="Ann"><w:r><w:t>new</w:t></w:r></w:ins></w:p>""";
        var inserted = new TestDocx { Body = "<w:p/>" }.WithXmlPart("notes", "endnotes", "endnotes", $"""<w:endnote w:id="2">{change}</w:endnote>""")
            .Build().ToArray();
        var docx = new TestDocx { Body = $"""<w:sdt><w:sdtContent>{change}</w:sdtContent></w:sdt><w:altChunk r:id="chunk"/>""" }
            .WithXmlPart("notes", "footnotes", "footnotes", $"""<w:footnote w:id="1">{change}</w:footnote>""")
            .WithPart("chunk", "aFChunk", "inserted.docx", inserted);

        var document = docx.Load();

        Assert.Equal(3, document.Revisions().Count());
        Assert.Contains("    notes\n      endnote 2\n        P \"new\"\n", document.ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldNameARepeatingSection_WhenAControlRepeats()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:sdt><w:sdtPr><w15:repeatingSection xmlns:w15="http://schemas.microsoft.com/office/word/2012/wordml"/></w:sdtPr>
                  <w:sdtContent><w:p/></w:sdtContent></w:sdt>
                """,
        };

        Assert.Contains("  Control repeating-section\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void ToSnapshot_ShouldDescribeAContentControl_WhenAParagraphHasADropDown()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:p><w:r><w:t xml:space="preserve">State: </w:t></w:r><w:sdt><w:sdtPr><w:alias w:val="Home state"/><w:tag w:val="State"/>
                  <w:dropDownList><w:listItem w:displayText="Ohio" w:value="OH"/><w:listItem w:displayText="Texas" w:value="TX"/></w:dropDownList></w:sdtPr>
                  <w:sdtContent><w:r><w:t>Ohio</w:t></w:r></w:sdtContent></w:sdt></w:p>
                """,
        };

        Assert.Contains("    form dropdown name=\"State\" title=\"Home state\" value=\"Ohio\" items=\"Ohio\",\"Texas\" at=7\n", docx.Load().ToSnapshot());
    }

    [Fact]
    public void Load_ShouldReadACheckBoxesState_WhenAContentControlIsACheckBox()
    {
        var docx = new TestDocx
        {
            Body = $"""
                <w:p {W14}><w:sdt><w:sdtPr><w:tag w:val="Agree"/><w14:checkbox><w14:checked w14:val="1"/></w14:checkbox></w:sdtPr>
                  <w:sdtContent><w:r><w:t>X</w:t></w:r></w:sdtContent></w:sdt></w:p>
                """,
        };

        var form = Assert.IsType<DocxForm>(Assert.Single(Assert.Single(docx.Load().Paragraphs()).Inlines));

        Assert.Equal(("checkbox", "Agree", "yes"), (form.Kind, form.Name, form.Value));
    }

    [Fact]
    public void ToSnapshot_ShouldKeepTheBlocksInsideAContentControl_WhenTheControlWrapsParagraphs()
    {
        var docx = new TestDocx
        {
            Body = """
                <w:sdt><w:sdtPr><w:tag w:val="Address"/></w:sdtPr><w:sdtContent>
                  <w:p><w:r><w:t>4 Privet Drive</w:t></w:r></w:p></w:sdtContent></w:sdt>
                """,
        };

        Assert.Contains("  Control rich-text name=\"Address\"\n    P \"4 Privet Drive\"\n", docx.Load().ToSnapshot());
    }

    [Theory]
    [InlineData("""<w:checkBox><w:default w:val="0"/><w:checked/></w:checkBox>""", "FORMCHECKBOX", "", "legacy-checkbox", "yes")]
    [InlineData("""<w:ddList><w:result w:val="1"/><w:listEntry w:val="Red"/><w:listEntry w:val="Blue"/></w:ddList>""", "FORMDROPDOWN", "", "legacy-dropdown", "Blue")]
    [InlineData("""<w:textInput/>""", "FORMTEXT", "Jane", "legacy-text", "Jane")]
    public void Load_ShouldReadALegacyFormField_WhenAFieldHasFormData(string data, string code, string result, string kind, string value)
    {
        var docx = new TestDocx
        {
            Body = $"""
                <w:p><w:r><w:fldChar w:fldCharType="begin"><w:ffData><w:name w:val="Field1"/>{data}</w:ffData></w:fldChar></w:r>
                  <w:r><w:instrText>{code}</w:instrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r>
                  <w:r><w:t>{result}</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r></w:p>
                """,
        };

        var inlines = Assert.Single(docx.Load().Paragraphs()).Inlines;

        Assert.Equal(new DocxField(code, result, 0), inlines[0]);
        var form = Assert.IsType<DocxForm>(inlines[1]);
        Assert.Equal((kind, "Field1", value), (form.Kind, form.Name, form.Value));
    }

    [Fact]
    public void ToSnapshot_ShouldLeaveOutNotes_WhenTheNotesSectionIsIgnored()
    {
        var docx = new TestDocx { Body = "<w:p/>" }
            .WithXmlPart("remarks", "comments", "comments", """<w:comment w:id="0" w:author="Ann"><w:p/></w:comment>""");

        Assert.EndsWith("notes\n  (ignored)\n", docx.Load().ToSnapshot(o => o.Ignore(DocxSection.Notes)));
    }
}
