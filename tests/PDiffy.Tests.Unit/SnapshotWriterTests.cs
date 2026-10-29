using PDiffy;
using static PDiffy.Tests.Unit.SnapshotText;

namespace PDiffy.Tests.Unit;

public class SnapshotWriterTests
{
    [Fact]
    public void Write_ShouldProduceCanonicalSnapshot_WhenGivenFullDocument()
    {
        var headerRow = new StructureElement
        {
            Type = "TR",
            Children =
            [
                new StructureElement { Type = "TH", Name = "TH1", FirstPage = 2, Scope = "Column", Text = "Trail" },
                new StructureElement { Type = "TH", Name = "TH2", FirstPage = 2, Scope = "Column", ColSpan = 2, Text = "Length" },
            ],
        };
        var bodyRow = new StructureElement
        {
            Type = "TR",
            Children =
            [
                new StructureElement { Type = "TD", FirstPage = 2, Headers = ["TH1"], Text = "Ridgeline" },
                new StructureElement { Type = "TD", FirstPage = 2, LastPage = 3, Headers = ["TH1", "TH2"], RowSpan = 2, Text = "Cedars" },
            ],
        };
        var link = new StructureElement
        {
            Type = "Link",
            FirstPage = 3,
            Annotations =
            [
                new Annotation { Subtype = "Link", Uri = "https://example.com", Contents = "Example" },
                new Annotation { Subtype = "Link", Action = "GoTo", HasDestination = true, AccessibleName = "Go to warnings" },
            ],
            Text = "example.com",
        };
        var root = new StructureElement
        {
            Type = "Document",
            Lang = "en-US",
            Children =
            [
                new StructureElement
                {
                    Type = "Heading",
                    MappedType = "H1",
                    FirstPage = 1,
                    ActualText = "Trail table",
                    Expansion = "DIT",
                    Title = "Intro",
                    Text = "Trail Table",
                },
                new StructureElement { Type = "Figure", FirstPage = 1, Alt = "Park logo" },
                new StructureElement { Type = "Table", FirstPage = 2, LastPage = 3, Summary = "Trail details", Children = [headerRow, bodyRow] },
                new StructureElement { Type = "L", FirstPage = 3, LastPage = 3, ListNumbering = "Decimal" },
                link,
            ],
        };
        var document = Documents.Empty() with
        {
            Document = new DocumentInfo(true, "en-US", true, 3),
            RoleMap = [new RoleMapping("Heading", "H1")],
            Pages =
            [
                new PageInfo(1, 612, 792, 0, null, false),
                new PageInfo(2, 792, 612, 90, "S", true),
                new PageInfo(3, 595, 842, 0, null, false),
            ],
            PageLabels = [new PageLabelRange(1, "r", "A-", 1)],
            Bookmarks = [new Bookmark("Safety", 3, [new Bookmark("Gear", null, [])])],
            Structure = [root],
            Artifacts = [new PageText(1, "Page i")],
            Fonts = [new FontInfo("Symbol", null, false, false), new FontInfo("Arial", "TrueType", true, true)],
        };

        var snapshot = document.ToSnapshot();

        var expected = """
            # pdfua-snapshot v1
            document
              marked=yes lang=en-US display-doc-title=yes pages=3
            settings
              version=(missing) javascript=no additional-actions=no encrypted=no
            metadata
              title="Title" author=(missing) subject=(missing) keywords=(missing)
              creation-date=<present> mod-date=<present> producer=<present> creator=<missing>
              pdfuaid-part=1 xmp-title="Title"
            role-map
              Heading -> H1
            pages
              p1 letter rotate=0
              p2 letter-landscape rotate=90 tabs=S
              p3 a4 rotate=0
              label from p1 style=r prefix="A-" start=1
            bookmarks
              "Safety" -> p3
                "Gear"
            structure
              Document lang=en-US
                Heading->H1 p1 actual="Trail table" expansion="DIT" title="Intro" "Trail Table"
                Figure p1 alt="Park logo"
                Table p2-3 summary="Trail details"
                  TR
                    TH1 p2 scope=Column "Trail"
                    TH2 p2 scope=Column colspan=2 "Length"
                  TR
                    TD p2 headers=[TH1] "Ridgeline"
                    TD p2-3 headers=[TH1,TH2] rowspan=2 "Cedars"
                L p3 numbering=Decimal
                Link p3 annot=Link uri="https://example.com" contents="Example" annot=Link action=GoTo dest=yes tu="Go to warnings" "example.com"
            artifacts
              p1 "Page i"
            untagged
              (none)
            fonts
              Arial TrueType embedded=yes to-unicode=yes
              Symbol embedded=no to-unicode=no
            annotations
              (none)
            forms
              (none)
            layout
              (none)

            """;
        Assert.Equal(expected.ReplaceLineEndings("\n"), snapshot);
    }

    [Theory]
    [InlineData("role-map")]
    [InlineData("pages")]
    [InlineData("bookmarks")]
    [InlineData("structure")]
    [InlineData("artifacts")]
    [InlineData("untagged")]
    [InlineData("fonts")]
    [InlineData("annotations")]
    [InlineData("forms")]
    [InlineData("layout")]
    public void Write_ShouldPrintNone_WhenSectionIsEmpty(string section)
    {
        var snapshot = Documents.Empty().ToSnapshot();

        Assert.Equal(["  (none)"], SectionLines(snapshot, section));
    }

    [Fact]
    public void Write_ShouldEscapeQuotesBackslashesAndControlCharacters_WhenTextIsQuoted()
    {
        var paragraph = new StructureElement { Type = "P", Text = "say \"hi\" \\ now\ttab" };
        var document = Documents.Empty() with { Structure = [paragraph] };

        var snapshot = document.ToSnapshot();

        Assert.Equal(["  P \"say \\\"hi\\\" \\\\ now\\u0009tab\""], SectionLines(snapshot, "structure"));
    }

    [Fact]
    public void Write_ShouldEscapeControlCharacters_WhenUnquotedValueContainsNewline()
    {
        var paragraph = new StructureElement { Type = "P", Lang = "en\nUS" };
        var document = Documents.Empty() with { Structure = [paragraph] };

        var snapshot = document.ToSnapshot();

        Assert.Equal(["  P lang=en\\u000aUS"], SectionLines(snapshot, "structure"));
    }

    [Fact]
    public void Write_ShouldSortRoleMap_WhenGivenInSourceOrder()
    {
        var document = Documents.Empty() with { RoleMap = [new RoleMapping("Zeta", "P"), new RoleMapping("Alpha", "Span")] };

        var snapshot = document.ToSnapshot();

        Assert.Equal(["  Alpha -> Span", "  Zeta -> P"], SectionLines(snapshot, "role-map"));
    }

    [Fact]
    public void Write_ShouldPrintEachFontOnceInSortedOrder_WhenFontsRepeatOrShareAName()
    {
        var document = Documents.Empty() with
        {
            Fonts =
            [
                new FontInfo("Times", "Type1", false, false),
                new FontInfo("Arial", "TrueType", true, true),
                new FontInfo("Arial", "TrueType", true, false),
                new FontInfo("Arial", "TrueType", true, true),
            ],
        };

        var snapshot = document.ToSnapshot();

        string[] expected =
        [
            "  Arial TrueType embedded=yes to-unicode=no",
            "  Arial TrueType embedded=yes to-unicode=yes",
            "  Times Type1 embedded=no to-unicode=no",
        ];
        Assert.Equal(expected, SectionLines(snapshot, "fonts"));
    }

    [Fact]
    public void Write_ShouldPrintUntaggedTextThenGraphicsPageByPage_WhenPagesHaveUntaggedContent()
    {
        var document = Documents.Empty() with
        {
            Untagged = [new PageText(2, "Footer"), new PageText(1, "Stray")],
            Images = [new ImageInfo(1, 0, 0, 10, 10, 1, 1, "0123456789abcdef", ContentRole.Untagged)],
            Paths =
            [
                new PathInfo(1, 0, 0, 5, 5, "gray:0", null, null, ContentRole.Untagged),
                new PathInfo(1, 0, 0, 5, 5, "gray:0", null, null, ContentRole.Artifact),
            ],
        };

        var snapshot = document.ToSnapshot();

        Assert.Equal(["  p1 \"Stray\"", "  p1 images=1 paths=1", "  p2 \"Footer\""], SectionLines(snapshot, "untagged"));
    }

    [Fact]
    public void Write_ShouldPrintSettingsOtherInfoAndPageBoxes_WhenDocumentHasThem()
    {
        var document = Documents.Empty() with
        {
            Settings = new DocumentSettings
            {
                PdfVersion = "1.7", CatalogVersion = "2.0", PageLayout = PageLayouts.OneColumn, OpenAction = "p1 Fit", IsEncrypted = true,
                Permissions = DocumentPermissions.Print | DocumentPermissions.ExtractForAccessibility,
                ViewerPreferences = [new Setting("CenterWindow", "true")], Layers = [new Layer("Draft", false)],
                EmbeddedFiles = [new EmbeddedFile("data.xml", "data.xml", null, "Data", 5, "2cf24dba5fb0a30e")],
            },
            Metadata = Documents.Empty().Metadata with { OtherInfo = [new Setting("Company", "Example Park")], PdfAConformance = "2B" },
            Pages =
            [
                new PageInfo(1, 612, 792, 0, "S", false)
                {
                    CropBox = new PageBox(10, 10, 592, 772), Group = new PageGroup("Transparency", "DeviceRGB", Isolated: true, Knockout: false),
                },
            ],
        };

        var snapshot = document.ToSnapshot();

        string[] expectedSettings =
        [
            "  version=1.7 catalog-version=2.0 page-layout=OneColumn open-action=\"p1 Fit\" javascript=no additional-actions=no "
                + "encrypted=yes permissions=print,extract-for-accessibility",
            "  viewer CenterWindow=true",
            "  layer \"Draft\" hidden",
            "  embedded-file \"data.xml\" file=\"data.xml\" relationship=Data size=5 hash=2cf24dba5fb0a30e",
        ];
        Assert.Equal(expectedSettings, SectionLines(snapshot, "settings"));
        string[] expectedMetadataTail = ["  pdfuaid-part=1 xmp-title=\"Title\" pdfa=2B", "  info Company=\"Example Park\""];
        Assert.Equal(expectedMetadataTail, SectionLines(snapshot, "metadata")[2..]);
        var expectedPage = "  p1 letter rotate=0 tabs=S crop=10,10,592,772 group=Transparency group-color-space=DeviceRGB isolated=yes";
        Assert.Equal([expectedPage], SectionLines(snapshot, "pages"));
    }

    [Fact]
    public void Write_ShouldPrintAnnotationsAndFormFields_WhenDocumentHasThem()
    {
        var document = Documents.Empty() with
        {
            Annotations =
            [
                new Annotation
                {
                    Subtype = "Widget", Page = 1, X = 72, Y = 300, Width = 200, Height = 20,
                    Flags = AnnotationFlags.Print | AnnotationFlags.NoZoom, Role = ContentRole.Untagged, AccessibleName = "City",
                },
            ],
            FormFields =
            [
                new FormField
                {
                    Name = "address.city", Type = FormFieldType.ComboBox, Page = 1, AccessibleName = "City", Value = "Tampa",
                    Options = ["Miami", "Tampa"], MaxLength = 20, Flags = FormFieldFlags.Required | FormFieldFlags.Editable,
                },
            ],
        };

        var snapshot = document.ToSnapshot();

        Assert.Equal(["  p1 Widget untagged at=72,300 size=200x20 flags=print,no-zoom tu=\"City\""],
            SectionLines(snapshot, "annotations"));
        var expectedField = "  combo-box \"address.city\" p1 tu=\"City\" value=\"Tampa\" options=[\"Miami\",\"Tampa\"] maxlen=20 "
            + "flags=required,editable";
        Assert.Equal([expectedField], SectionLines(snapshot, "forms"));
    }

    [Fact]
    public void Write_ShouldPrintTextImagesThenPathsPerPage_WhenDocumentHasLayout()
    {
        var document = Documents.Empty() with
        {
            TextRuns =
            [
                new TextRun(2, 72, 700, "Arial", 10.5, "#ff0000", TextRenderMode.Fill, ContentRole.Tagged, "Next page"),
                new TextRun(1, 72, 720, "Arial", 12, "gray:0", TextRenderMode.Invisible, ContentRole.Untagged, "OCR text"),
            ],
            Images = [new ImageInfo(1, 72, 600, 10, 10, 1, 1, "0123456789abcdef", ContentRole.Tagged)],
            Paths = [new PathInfo(1, 72, 300, 100, 0, null, "#ff0000", 0.5, ContentRole.Artifact)],
        };

        var snapshot = document.ToSnapshot();

        string[] expected =
        [
            "  p1 text untagged at=72,720 font=Arial size=12 color=gray:0 mode=invisible \"OCR text\"",
            "  p1 image tagged at=72,600 size=10x10 pixels=1x1 hash=0123456789abcdef",
            "  p1 path artifact at=72,300 size=100x0 stroke=#ff0000 width=0.5",
            "  p2 text tagged at=72,700 font=Arial size=10.5 color=#ff0000 \"Next page\"",
        ];
        Assert.Equal(expected, SectionLines(snapshot, "layout"));
    }

    [Fact]
    public void Write_ShouldPrintTabsMissing_WhenPageHasAnnotationsButNoTabsEntry()
    {
        var document = Documents.Empty() with { Pages = [new PageInfo(1, 612, 792, 0, null, HasAnnotations: true)] };

        var snapshot = document.ToSnapshot();

        Assert.Equal(["  p1 letter rotate=0 tabs=(missing)"], SectionLines(snapshot, "pages"));
    }

    [Fact]
    public void Write_ShouldPrintMissingLang_WhenDocumentHasNoLanguage()
    {
        var document = Documents.Empty() with { Document = new DocumentInfo(true, null, false, 1) };

        var snapshot = document.ToSnapshot();

        Assert.Equal("  marked=yes lang=(missing) display-doc-title=no pages=1", SectionLines(snapshot, "document")[0]);
    }
}
