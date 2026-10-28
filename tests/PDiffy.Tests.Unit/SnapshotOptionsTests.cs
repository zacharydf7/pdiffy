using System.Text.RegularExpressions;
using PDiffy;
using static PDiffy.Tests.Unit.SnapshotText;

namespace PDiffy.Tests.Unit;

public class SnapshotOptionsTests
{
    public static TheoryData<Field> AllFields => new(Enum.GetValues<Field>());

    [Fact]
    public void ToSnapshot_ShouldPrintIgnoredAndListTheSections_WhenSectionsAreIgnored()
    {
        var snapshot = Documents.Empty().ToSnapshot(o => o.Ignore(Section.Layout, Section.Metadata));

        Assert.Equal("# ignored sections: metadata, layout", snapshot.Split('\n')[1]);
        Assert.Equal(["  (ignored)"], SectionLines(snapshot, "metadata"));
        Assert.Equal(["  (ignored)"], SectionLines(snapshot, "layout"));
        Assert.NotEmpty(SectionLines(snapshot, "document"));
    }

    [Fact]
    public void ToSnapshot_ShouldLeaveOutOnlyThoseKeys_WhenFieldsAreIgnored()
    {
        var snapshot = Full().ToSnapshot(o => o.Ignore(Field.Positions).Ignore(Field.Colors));

        Assert.Equal("# ignored fields: positions, colors", snapshot.Split('\n')[1]);
        string[] expectedLayout =
        [
            "  p1 text tagged font=Arial size=12 \"Hi\"",
            "  p1 image tagged pixels=1x1 hash=0123456789abcdef",
            "  p1 path artifact width=0.5",
        ];
        Assert.Equal(expectedLayout, SectionLines(snapshot, "layout"));
        Assert.Equal(["  p1 Link tagged uri=\"https://example.com\" contents=\"Example\""], SectionLines(snapshot, "annotations"));
    }

    [Fact]
    public void ToSnapshot_ShouldDropALine_WhenEveryKeyOnItIsIgnored()
    {
        var snapshot = Full().ToSnapshot(o => o.Ignore(Field.Title, Field.Author, Field.Subject, Field.Keywords));

        Assert.Equal("  creation-date=<present> mod-date=<present> producer=<present> creator=<present>", SectionLines(snapshot, "metadata")[0]);
    }

    [Fact]
    public void ToSnapshot_ShouldLeaveOutEveryCopy_WhenAFieldAppearsInSeveralPlaces()
    {
        var snapshot = Full().ToSnapshot(o => o.Ignore(Field.Title, Field.Author, Field.Subject, Field.Language, Field.LinkTargets));

        Assert.DoesNotMatch(@"\s(title|xmp-title|author|xmp-creator|subject|xmp-description|lang|xmp-language|uri|action|dest)=", snapshot);
    }

    [Theory]
    [MemberData(nameof(AllFields))]
    public void ToSnapshot_ShouldChangeTheBody_WhenAnyFieldIsIgnored(Field field)
    {
        var document = Full();

        var ignored = document.ToSnapshot(o => o.Ignore(field));

        Assert.NotEqual(Body(document.ToSnapshot()), Body(ignored));
    }

    [Fact]
    public void ToSnapshot_ShouldReplaceOnlyQuotedText_WhenScrubbingAPattern()
    {
        var paragraph = new StructureElement { Type = "P", Text = "Order 12345 for P. Smith" };
        var document = Documents.Empty() with { Structure = [paragraph] };

        var snapshot = document.ToSnapshot(o => o.ScrubText(new Regex(@"Order (\d+)"), "Order <id>").ScrubText(new Regex("P"), "X"));

        Assert.Equal(["  P \"Order <id> for X. Smith\""], SectionLines(snapshot, "structure"));
    }

    [Theory]
    [InlineData("2026-10-27")]
    [InlineData("2026-10-27T14:03:00Z")]
    [InlineData("10/27/2026")]
    [InlineData("10/27/2026 2:30 PM")]
    [InlineData("October 27, 2026")]
    [InlineData("Oct. 27, 2026 14:03")]
    [InlineData("27 October 2026")]
    [InlineData("D:20261027140300Z")]
    public void ToSnapshot_ShouldReplaceTheDate_WhenScrubbingDates(string date)
    {
        var document = Documents.Empty() with { Structure = [new StructureElement { Type = "P", Text = $"Printed {date}." }] };

        var snapshot = document.ToSnapshot(o => o.ScrubDates());

        Assert.Equal(["  P \"Printed Date_1.\""], SectionLines(snapshot, "structure"));
    }

    [Fact]
    public void ToSnapshot_ShouldGiveTheSameValueTheSameToken_WhenScrubbingDatesAndGuids()
    {
        var first = "0f8fad5b-d9cb-469f-a165-70867728950e";
        var second = "7c9e6679-7425-40de-944b-e07fc1f90ae7";
        var document = Documents.Empty() with
        {
            Structure =
            [
                new StructureElement { Type = "P", Text = $"Issued 2026-10-27 as {first}" },
                new StructureElement { Type = "P", Text = $"Due 11/30/2026, replaces {second}" },
                new StructureElement { Type = "P", Alt = $"Reissued 2026-10-27 as {first}" },
            ],
        };

        var snapshot = document.ToSnapshot(o => o.ScrubDates().ScrubGuids());

        string[] expected =
        [
            "  P \"Issued Date_1 as Guid_1\"",
            "  P \"Due Date_2, replaces Guid_2\"",
            "  P alt=\"Reissued Date_1 as Guid_1\"",
        ];
        Assert.Equal(expected, SectionLines(snapshot, "structure"));
    }

    [Fact]
    public void ToSnapshot_ShouldPrintProducerAndCreatorValues_WhenAskedToShowThem()
    {
        var document = Documents.Empty();

        var snapshot = document.ToSnapshot(o => o.ShowProducerAndCreator());

        Assert.Equal("  creation-date=<present> mod-date=<present> producer=\"Producer 1.0\" creator=<missing>", SectionLines(snapshot, "metadata")[1]);
    }

    [Fact]
    public void ToSnapshot_ShouldKeepOnlyContentOnThosePages_WhenLimitedToPages()
    {
        var document = Documents.Empty() with
        {
            Document = new DocumentInfo(true, "en-US", true, 3),
            Pages = [new PageInfo(1, 612, 792, 0, null, false), new PageInfo(2, 612, 792, 0, null, false), new PageInfo(3, 612, 792, 0, null, false)],
            Bookmarks = [new Bookmark("Intro", 1, []), new Bookmark("Results", null, [new Bookmark("Table", 2, []), new Bookmark("Notes", 3, [])])],
            Structure =
            [
                new StructureElement
                {
                    Type = "Document",
                    Children =
                    [
                        new StructureElement { Type = "H1", FirstPage = 1, Text = "Intro" },
                        new StructureElement { Type = "Table", FirstPage = 1, LastPage = 3, Text = "Spans" },
                        new StructureElement { Type = "P", FirstPage = 2, Text = "Kept" },
                        new StructureElement { Type = "P", FirstPage = 3, Text = "Dropped" },
                    ],
                },
            ],
            Artifacts = [new PageText(1, "Page 1"), new PageText(2, "Page 2")],
            Annotations = [new Annotation { Subtype = "Link", Page = 3 }, new Annotation { Subtype = "Link", Page = 2 }],
            FormFields = [new FormField { Name = "a", Page = 1 }, new FormField { Name = "b", Page = 2 }, new FormField { Name = "c" }],
            TextRuns = [new TextRun(1, 72, 700, "Arial", 12, null, TextRenderMode.Fill, ContentRole.Tagged, "One"),
                new TextRun(2, 72, 700, "Arial", 12, null, TextRenderMode.Fill, ContentRole.Tagged, "Two")],
        };

        var snapshot = document.ToSnapshot(o => o.OnlyPages(2));

        Assert.Equal("# only pages: 2", snapshot.Split('\n')[1]);
        Assert.Equal("  marked=yes lang=en-US display-doc-title=yes pages=3", SectionLines(snapshot, "document")[0]);
        Assert.Equal(["  p2 letter rotate=0"], SectionLines(snapshot, "pages"));
        Assert.Equal(["  \"Results\"", "    \"Table\" -> p2"], SectionLines(snapshot, "bookmarks"));
        Assert.Equal(["  Document", "    Table p1-3 \"Spans\"", "    P p2 \"Kept\""], SectionLines(snapshot, "structure"));
        Assert.Equal(["  p2 \"Page 2\""], SectionLines(snapshot, "artifacts"));
        Assert.Equal(["  p2 Link tagged at=0,0 size=0x0"], SectionLines(snapshot, "annotations"));
        Assert.Equal(["  text \"b\" p2"], SectionLines(snapshot, "forms"));
        Assert.Equal(["  p2 text tagged at=72,700 font=Arial size=12 \"Two\""], SectionLines(snapshot, "layout"));
    }

    [Fact]
    public void ToSnapshot_ShouldNumberOnlyPrintedValues_WhenScrubbingAlongsideIgnoredFields()
    {
        var document = Documents.Empty() with
        {
            Structure = [new StructureElement { Type = "Figure", Alt = "Chart for 2026-01-01" }, new StructureElement { Type = "P", Text = "Printed 2026-10-27" }],
        };

        var snapshot = document.ToSnapshot(o => o.Ignore(Field.AltText).ScrubDates());

        Assert.Equal(["  Figure", "  P \"Printed Date_1\""], SectionLines(snapshot, "structure"));
    }

    [Fact]
    public void ToSnapshot_ShouldLeaveLongerNumbersAlone_WhenScrubbingDates()
    {
        var document = Documents.Empty() with { Structure = [new StructureElement { Type = "P", Text = "Part 1234-56-7890" }] };

        var snapshot = document.ToSnapshot(o => o.ScrubDates());

        Assert.Equal(["  P \"Part 1234-56-7890\""], SectionLines(snapshot, "structure"));
    }

    [Fact]
    public void OnlyPages_ShouldThrow_WhenGivenNoPagesOrPagesBelowOne()
    {
        var options = new SnapshotOptions();

        Assert.Throws<ArgumentException>(() => options.OnlyPages());
        Assert.Throws<ArgumentOutOfRangeException>(() => options.OnlyPages(0));
    }

    [Fact]
    public void ToSnapshot_ShouldThrow_WhenAPageIsPastTheEndOfTheDocument()
    {
        var document = Documents.Empty();

        Assert.Throws<ArgumentOutOfRangeException>(() => document.ToSnapshot(o => o.OnlyPages(2)));
    }

    // A document with every field set, so ignoring any one of them changes its snapshot.
    private static PdfUaDocument Full() => Documents.Empty() with
    {
        Settings = new DocumentSettings
        {
            PdfVersion = "1.7", OpenAction = "p1 Fit", IsEncrypted = true, Permissions = DocumentPermissions.Print,
            EmbeddedFiles = [new EmbeddedFile("a.txt", null, null, null, 5, "2cf24dba5fb0a30e")],
        },
        Metadata = new Metadata("Title", "Ann", "Drugs", "pharmacy", true, true, "Producer 1.0", "Word", "1", "Title")
        {
            OtherInfo = [new Setting("Company", "Example Pharmacy")], XmpCreator = "Ann", XmpDescription = "Drugs", XmpLanguage = "en-US",
        },
        Pages = [new PageInfo(1, 612, 792, 0, null, false) { CropBox = new PageBox(0, 0, 600, 780) }],
        Structure =
        [
            new StructureElement { Type = "Figure", Lang = "en-US", Alt = "Logo", ActualText = "Logo" },
            new StructureElement { Type = "Link", Annotations = [new Annotation { Subtype = "Link", Action = "GoTo", HasDestination = true }] },
        ],
        Annotations =
        [
            new Annotation { Subtype = "Link", Page = 1, X = 72, Y = 600, Width = 50, Height = 12, Uri = "https://example.com", Contents = "Example" },
        ],
        FormFields = [new FormField { Name = "city", Type = FormFieldType.Text, Value = "Tampa" }],
        TextRuns = [new TextRun(1, 72, 700, "Arial", 12, "gray:0", TextRenderMode.Fill, ContentRole.Tagged, "Hi")],
        Images = [new ImageInfo(1, 72, 500, 10, 10, 1, 1, "0123456789abcdef", ContentRole.Tagged)],
        Paths = [new PathInfo(1, 72, 300, 100, 0, null, "#ff0000", 0.5, ContentRole.Artifact)],
    };

    private static string Body(string snapshot) => string.Join("\n", snapshot.Split('\n').Where(line => !line.StartsWith('#')));
}
