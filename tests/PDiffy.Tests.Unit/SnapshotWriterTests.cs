using PDiffy;
using static PDiffy.Tests.Unit.SnapshotText;

namespace PDiffy.Tests.Unit;

public class SnapshotWriterTests
{
    [Fact]
    public void Write_ShouldProduceCanonicalSnapshot_WhenGivenFullDocument()
    {
        var headerRow = new PdfStructureElement
        {
            Type = "TR",
            Children =
            [
                new PdfStructureElement { Type = "TH", Name = "TH1", FirstPage = 2, Scope = "Column", Text = "Trail" },
                new PdfStructureElement { Type = "TH", Name = "TH2", FirstPage = 2, Scope = "Column", ColSpan = 2, Text = "Length" },
            ],
        };
        var bodyRow = new PdfStructureElement
        {
            Type = "TR",
            Children =
            [
                new PdfStructureElement { Type = "TD", FirstPage = 2, Headers = ["TH1"], Text = "Ridgeline" },
                new PdfStructureElement { Type = "TD", FirstPage = 2, LastPage = 3, Headers = ["TH1", "TH2"], RowSpan = 2, Text = "Cedars" },
            ],
        };
        var link = new PdfStructureElement
        {
            Type = "Link",
            FirstPage = 3,
            Annotations =
            [
                new PdfAnnotation { Subtype = "Link", Uri = "https://example.com", Contents = "Example" },
                new PdfAnnotation
                {
                    Subtype = "Link", Action = "GoTo", AccessibleName = "Go to warnings",
                    Destination = new PdfDestination { Page = 3, View = "XYZ", Positions = [72, null], Zoom = 1.5 },
                },
            ],
            Text = "example.com",
        };
        var root = new PdfStructureElement
        {
            Type = "Document",
            Language = "en-US",
            Children =
            [
                new PdfStructureElement
                {
                    Type = "Heading",
                    MappedType = "H1",
                    FirstPage = 1,
                    ActualText = "Trail table",
                    Expansion = "DIT",
                    Title = "Intro",
                    Text = "Trail Table",
                },
                new PdfStructureElement { Type = "Figure", FirstPage = 1, AltText = "Park logo" },
                new PdfStructureElement { Type = "Table", FirstPage = 2, LastPage = 3, Summary = "Trail details", Children = [headerRow, bodyRow] },
                new PdfStructureElement { Type = "L", FirstPage = 3, LastPage = 3, ListNumbering = "Decimal" },
                link,
            ],
        };
        var document = Documents.Empty() with
        {
            Document = new PdfDocumentInfo
            {
                Marked = true, Language = "en-US", DisplayDocTitle = true, PageCount = 3,
            },
            RoleMap = [new PdfRoleMapping { Custom = "Heading", Standard = "H1" }],
            Pages =
            [
                new PdfPage
                {
                    Number = 1, Width = 612, Height = 792, Rotation = 0, Tabs = null, HasAnnotations = false,
                },
                new PdfPage
                {
                    Number = 2, Width = 792, Height = 612, Rotation = 90, Tabs = "S", HasAnnotations = true,
                },
                new PdfPage
                {
                    Number = 3, Width = 595, Height = 842, Rotation = 0, Tabs = null, HasAnnotations = false,
                },
            ],
            PageLabels = [new PdfPageLabelRange { FirstPage = 1, Style = "r", Prefix = "A-", Start = 1 }],
            Bookmarks =
            [
                new PdfBookmark
                {
                    Title = "Safety", Page = 3,
                    Children =
                    [
                        new PdfBookmark { Title = "Gear", Page = null, Children = [], Uri = "https://example.com/gear" },
                    ],
                    Destination = new PdfDestination { Page = 3, View = "FitH", Positions = [700] },
                },
            ],
            Structure = [root],
            Artifacts = [new PdfPageText { Page = 1, Text = "Page i" }],
            Fonts =
            [
                new PdfFont { Name = "Symbol", Subtype = null, Embedded = false, HasToUnicode = false },
                new PdfFont { Name = "Arial", Subtype = "TrueType", Embedded = true, HasToUnicode = true },
            ],
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
              "Safety" -> p3 view=FitH view-at=700
                "Gear" uri="https://example.com/gear"
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
                Link p3 annot=Link uri="https://example.com" contents="Example" annot=Link action=GoTo dest=p3 view=XYZ view-at=72,- zoom=1.5 tu="Go to warnings" "example.com"
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
        var paragraph = new PdfStructureElement { Type = "P", Text = "say \"hi\" \\ now\ttab" };
        var document = Documents.Empty() with { Structure = [paragraph] };

        var snapshot = document.ToSnapshot();

        Assert.Equal(["  P \"say \\\"hi\\\" \\\\ now\\u0009tab\""], SectionLines(snapshot, "structure"));
    }

    [Fact]
    public void Write_ShouldEscapeControlCharacters_WhenUnquotedValueContainsNewline()
    {
        var paragraph = new PdfStructureElement { Type = "P", Language = "en\nUS" };
        var document = Documents.Empty() with { Structure = [paragraph] };

        var snapshot = document.ToSnapshot();

        Assert.Equal(["  P lang=en\\u000aUS"], SectionLines(snapshot, "structure"));
    }

    [Fact]
    public void Write_ShouldSortRoleMap_WhenGivenInSourceOrder()
    {
        PdfRoleMapping[] roleMap =
        [
            new() { Custom = "Zeta", Standard = "P" },
            new() { Custom = "Alpha", Standard = "Span" },
        ];
        var document = Documents.Empty() with { RoleMap = roleMap };

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
                new PdfFont { Name = "Times", Subtype = "Type1", Embedded = false, HasToUnicode = false },
                new PdfFont { Name = "Arial", Subtype = "TrueType", Embedded = true, HasToUnicode = true },
                new PdfFont { Name = "Arial", Subtype = "TrueType", Embedded = true, HasToUnicode = false },
                new PdfFont { Name = "Arial", Subtype = "TrueType", Embedded = true, HasToUnicode = true },
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
            Untagged = [new PdfPageText { Page = 2, Text = "Footer" }, new PdfPageText { Page = 1, Text = "Stray" }],
            Images = [new PdfImage
            {
                Page = 1, X = 0, Y = 0, Width = 10, Height = 10, PixelWidth = 1, PixelHeight = 1,
                Hash = "0123456789abcdef", Role = PdfContentRole.Untagged,
            }],
            Paths =
            [
                new PdfVectorPath
                {
                    Page = 1, X = 0, Y = 0, Width = 5, Height = 5, Fill = "gray:0", Stroke = null, LineWidth = null,
                    Role = PdfContentRole.Untagged,
                },
                new PdfVectorPath
                {
                    Page = 1, X = 0, Y = 0, Width = 5, Height = 5, Fill = "gray:0", Stroke = null, LineWidth = null,
                    Role = PdfContentRole.Artifact,
                },
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
            Settings = new PdfSettings
            {
                PdfVersion = "1.7", CatalogVersion = "2.0", PageLayout = PdfPageLayouts.OneColumn, OpenAction = "p1 Fit", IsEncrypted = true,
                Permissions = PdfPermissions.Print | PdfPermissions.ExtractForAccessibility,
                ViewerPreferences = [new PdfSetting { Name = "CenterWindow", Value = "true" }], Layers = [new PdfLayer
                {
                    Name = "Draft", VisibleByDefault = false,
                }],
                EmbeddedFiles = [new PdfEmbeddedFile
                {
                    Name = "data.xml", FileName = "data.xml", Description = null, Relationship = "Data", Size = 5,
                    Hash = "2cf24dba5fb0a30e",
                }],
            },
            Metadata = Documents.Empty().Metadata with { OtherInfo = [new PdfSetting
            {
                Name = "Company", Value = "Example Park",
            }], PdfAConformance = "2B" },
            Pages =
            [
                new PdfPage
                {
                    Number = 1, Width = 612, Height = 792, Rotation = 0, Tabs = "S", HasAnnotations = false,
                    CropBox = new PdfBox { X = 10, Y = 10, Width = 592, Height = 772 },
                    Group = new PdfPageGroup
                    {
                        Type = "Transparency", ColorSpace = "DeviceRGB", Isolated = true, Knockout = false,
                    },
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
                new PdfAnnotation
                {
                    Subtype = "Widget", Page = 1, X = 72, Y = 300, Width = 200, Height = 20,
                    Flags = PdfAnnotationFlags.Print | PdfAnnotationFlags.NoZoom, Role = PdfContentRole.Untagged, AccessibleName = "City",
                },
            ],
            FormFields =
            [
                new PdfFormField
                {
                    Name = "address.city", Type = PdfFormFieldType.ComboBox, Page = 1, AccessibleName = "City", Value = "Tampa",
                    Options = ["Miami", "Tampa"], MaxLength = 20, Flags = PdfFormFieldFlags.Required | PdfFormFieldFlags.Editable,
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
                new PdfTextRun
                {
                    Page = 2, X = 72, Y = 700, Font = "Arial", Size = 10.5, Color = "#ff0000",
                    RenderMode = PdfTextRenderMode.Fill, Role = PdfContentRole.Tagged, Text = "Next page",
                },
                new PdfTextRun
                {
                    Page = 1, X = 72, Y = 720, Font = "Arial", Size = 12, Color = "gray:0",
                    RenderMode = PdfTextRenderMode.Invisible, Role = PdfContentRole.Untagged, Text = "OCR text",
                },
            ],
            Images = [new PdfImage
            {
                Page = 1, X = 72, Y = 600, Width = 10, Height = 10, PixelWidth = 1, PixelHeight = 1,
                Hash = "0123456789abcdef", Role = PdfContentRole.Tagged,
            }],
            Paths = [new PdfVectorPath
            {
                Page = 1, X = 72, Y = 300, Width = 100, Height = 0, Fill = null, Stroke = "#ff0000", LineWidth = 0.5,
                Role = PdfContentRole.Artifact,
            }],
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
        var document = Documents.Empty() with { Pages = [new PdfPage
        {
            Number = 1, Width = 612, Height = 792, Rotation = 0, Tabs = null, HasAnnotations = true,
        }] };

        var snapshot = document.ToSnapshot();

        Assert.Equal(["  p1 letter rotate=0 tabs=(missing)"], SectionLines(snapshot, "pages"));
    }

    [Fact]
    public void Write_ShouldPrintMissingLang_WhenDocumentHasNoLanguage()
    {
        var document = Documents.Empty() with { Document = new PdfDocumentInfo
        {
            Marked = true, Language = null, DisplayDocTitle = false, PageCount = 1,
        } };

        var snapshot = document.ToSnapshot();

        Assert.Equal("  marked=yes lang=(missing) display-doc-title=no pages=1", SectionLines(snapshot, "document")[0]);
    }
}
