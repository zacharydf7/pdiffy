using System.Text.RegularExpressions;
using PDiffy;
using static PDiffy.Tests.Unit.SnapshotText;

namespace PDiffy.Tests.Unit;

public class SnapshotOptionsTests
{
    public static TheoryData<PdfDetail> AllFields => new(Enum.GetValues<PdfDetail>());

    [Fact]
    public void ToSnapshot_ShouldPrintIgnoredAndListTheSections_WhenSectionsAreIgnored()
    {
        var snapshot = Documents.Empty().ToSnapshot(o => o.Ignore(PdfSection.Layout, PdfSection.Metadata));

        Assert.Equal("# ignored sections: metadata, layout", snapshot.Split('\n')[1]);
        Assert.Equal(["  (ignored)"], SectionLines(snapshot, "metadata"));
        Assert.Equal(["  (ignored)"], SectionLines(snapshot, "layout"));
        Assert.NotEmpty(SectionLines(snapshot, "document"));
    }

    [Theory]
    [InlineData(PdfProfile.Content, "# ignored sections: settings, metadata, fonts, layout",
        "# ignored fields: page-boxes, positions")]
    [InlineData(PdfProfile.Accessibility, "# ignored fields: pdf-version, open-action, embedded-file-contents, author, "
        + "subject, keywords, dates, producer, creator, custom-info, page-boxes, positions, colors, font-names, font-sizes, "
        + "line-widths, image-contents")]
    [InlineData(PdfProfile.Layout, "# ignored sections: settings, metadata")]
    public void ToSnapshot_ShouldListWhatTheProfileLeavesOut_WhenAProfileIsUsed(PdfProfile profile, params string[] header)
    {
        var snapshot = Full().ToSnapshot(o => o.Use(profile));

        Assert.Equal(["# pdfua-snapshot v1", .. header], snapshot.Split('\n').TakeWhile(line => line.StartsWith('#')));
    }

    [Fact]
    public void ToSnapshot_ShouldLeaveOutNothing_WhenTheStrictProfileIsUsed()
    {
        var snapshot = Full().ToSnapshot(o => o.Use(PdfProfile.Strict));

        Assert.Equal(Full().ToSnapshot(), snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldLeaveOutWhatIgnoreAdds_WhenAProfileIsUsedWithIgnore()
    {
        var snapshot = Full().ToSnapshot(o => o.Use(PdfProfile.Layout).Ignore(PdfDetail.AltText));

        Assert.StartsWith("# pdfua-snapshot v1\n# ignored sections: settings, metadata\n# ignored fields: alt-text\n", snapshot);
    }

    [Fact]
    public void Use_ShouldThrow_WhenGivenAValueThatIsNotAProfile()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfSnapshotOptions().Use((PdfProfile)99));
    }

    [Fact]
    public void ToSnapshot_ShouldListTheTolerances_WhenNumbersAreTolerated()
    {
        var snapshot = Full().ToSnapshot(o => o.Tolerate(PdfTolerance.FontSizes, 0.5)
            .Tolerate(PdfTolerance.Positions, 1));

        Assert.Equal("# tolerances: positions 1pt, font-sizes 0.5pt", snapshot.Split('\n')[1]);
        Assert.Equal(Full().ToSnapshot().Split('\n').Skip(1), snapshot.Split('\n').Skip(2));
    }

    [Theory]
    [InlineData(PdfTolerance.Positions, "positions")]
    [InlineData(PdfTolerance.FontSizes, "font-sizes")]
    [InlineData(PdfTolerance.LineWidths, "line-widths")]
    public void ToSnapshot_ShouldListTheToleratedNumbers_WhenGivenEachTolerance(PdfTolerance tolerance, string name)
    {
        var snapshot = Full().ToSnapshot(o => o.Tolerate(tolerance, 2));

        Assert.Equal($"# tolerances: {name} 2pt", snapshot.Split('\n')[1]);
    }

    [Theory]
    [InlineData((PdfTolerance)3, 1)]
    [InlineData(PdfTolerance.Positions, 0)]
    [InlineData(PdfTolerance.Positions, -1)]
    [InlineData(PdfTolerance.Positions, double.NaN)]
    [InlineData(PdfTolerance.Positions, double.PositiveInfinity)]
    public void Tolerate_ShouldThrow_WhenTheToleranceIsUnknownOrThePointsAreNotPositive(PdfTolerance tolerance,
        double points)
    {
        Assert.ThrowsAny<ArgumentException>(() => new PdfSnapshotOptions().Tolerate(tolerance, points));
    }

    [Fact]
    public void ToSnapshot_ShouldLeaveOutOnlyThoseKeys_WhenFieldsAreIgnored()
    {
        var snapshot = Full().ToSnapshot(o => o.Ignore(PdfDetail.Positions).Ignore(PdfDetail.Colors));

        Assert.Equal("# ignored fields: positions, colors", snapshot.Split('\n')[1]);
        string[] expectedLayout =
        [
            "  p1 text tagged font=Arial size=12 \"Hi\"",
            "  p1 image tagged pixels=1x1 hash=0123456789abcdef",
            "  p1 path artifact width=0.5",
        ];
        Assert.Equal(expectedLayout, SectionLines(snapshot, "layout"));
        Assert.Equal(["  p1 Link tagged uri=\"https://example.com\" contents=\"Example\""], SectionLines(snapshot, "annotations"));
        Assert.Equal(["  \"Intro\" -> p1 view=FitH", "  \"Map\" uri=\"https://example.com/map\""], SectionLines(snapshot, "bookmarks"));
        Assert.DoesNotContain("view-at=", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldDropALine_WhenEveryKeyOnItIsIgnored()
    {
        var snapshot = Full().ToSnapshot(o =>
            o.Ignore(PdfDetail.Title, PdfDetail.Author, PdfDetail.Subject, PdfDetail.Keywords));

        Assert.Equal("  creation-date=<present> mod-date=<present> producer=<present> creator=<present>", SectionLines(snapshot, "metadata")[0]);
    }

    [Fact]
    public void ToSnapshot_ShouldLeaveOutEveryCopy_WhenAFieldAppearsInSeveralPlaces()
    {
        var snapshot = Full().ToSnapshot(o => o.Ignore(PdfDetail.Title, PdfDetail.Author, PdfDetail.Subject, PdfDetail.Language, PdfDetail.LinkTargets,
            PdfDetail.AltText, PdfDetail.ActualText, PdfDetail.ImageContents, PdfDetail.FontNames, PdfDetail.FontSizes, PdfDetail.Colors, PdfDetail.FormValues));

        Assert.DoesNotMatch(@"\s(title|xmp-title|author|xmp-creator|subject|xmp-description|lang|span-lang|xmp-language|uri|action|file|dest|view|view-at|alt|actual|span-alt|span-actual|images|pixels|text-font|text-size|text-color|value|default)=", snapshot);
    }

    [Theory]
    [MemberData(nameof(AllFields))]
    public void ToSnapshot_ShouldChangeTheBody_WhenAnyFieldIsIgnored(PdfDetail field)
    {
        var document = Full();

        var ignored = document.ToSnapshot(o => o.Ignore(field));

        Assert.NotEqual(Body(document.ToSnapshot()), Body(ignored));
    }

    [Fact]
    public void ToSnapshot_ShouldReplaceOnlyQuotedText_WhenScrubbingAPattern()
    {
        var paragraph = new PdfStructureElement { Type = "P", Text = "Ticket 12345 for P. Smith" };
        var document = Documents.Empty() with { Structure = [paragraph] };

        var snapshot = document.ToSnapshot(o => o.ScrubText(new Regex(@"Ticket (\d+)"), "Ticket <id>").ScrubText(new Regex("P"), "X"));

        Assert.Equal(["  P \"Ticket <id> for X. Smith\""], SectionLines(snapshot, "structure"));
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
        var document = Documents.Empty() with { Structure = [new PdfStructureElement { Type = "P", Text = $"Printed {date}." }] };

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
                new PdfStructureElement { Type = "P", Text = $"Issued 2026-10-27 as {first}" },
                new PdfStructureElement { Type = "P", Text = $"Due 11/30/2026, replaces {second}" },
                new PdfStructureElement { Type = "P", AltText = $"Reissued 2026-10-27 as {first}" },
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
            Document = new PdfDocumentInfo(true, "en-US", true, 3),
            Pages = [new PdfPage(1, 612, 792, 0, null, false), new PdfPage(2, 612, 792, 0, null, false), new PdfPage(3, 612, 792, 0, null, false)],
            Bookmarks = [new PdfBookmark("Intro", 1, []), new PdfBookmark("Results", null, [new PdfBookmark("Table", 2, []), new PdfBookmark("Notes", 3, [])])],
            Structure =
            [
                new PdfStructureElement
                {
                    Type = "Document",
                    Children =
                    [
                        new PdfStructureElement { Type = "H1", FirstPage = 1, Text = "Intro" },
                        new PdfStructureElement { Type = "Table", FirstPage = 1, LastPage = 3, Text = "Spans" },
                        new PdfStructureElement { Type = "P", FirstPage = 2, Text = "Kept" },
                        new PdfStructureElement { Type = "P", FirstPage = 3, Text = "Dropped" },
                    ],
                },
            ],
            Artifacts = [new PdfPageText(1, "Page 1"), new PdfPageText(2, "Page 2")],
            Annotations = [new PdfAnnotation { Subtype = "Link", Page = 3 }, new PdfAnnotation { Subtype = "Link", Page = 2 }],
            FormFields = [new PdfFormField { Name = "a", Page = 1 }, new PdfFormField { Name = "b", Page = 2 }, new PdfFormField { Name = "c" }],
            TextRuns = [new PdfTextRun(1, 72, 700, "Arial", 12, null, PdfTextRenderMode.Fill, PdfContentRole.Tagged, "One"),
                new PdfTextRun(2, 72, 700, "Arial", 12, null, PdfTextRenderMode.Fill, PdfContentRole.Tagged, "Two")],
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
            Structure = [new PdfStructureElement { Type = "Figure", AltText = "Chart for 2026-01-01" }, new PdfStructureElement { Type = "P", Text = "Printed 2026-10-27" }],
        };

        var snapshot = document.ToSnapshot(o => o.Ignore(PdfDetail.AltText).ScrubDates());

        Assert.Equal(["  Figure", "  P \"Printed Date_1\""], SectionLines(snapshot, "structure"));
    }

    [Fact]
    public void ToSnapshot_ShouldLeaveLongerNumbersAlone_WhenScrubbingDates()
    {
        var document = Documents.Empty() with { Structure = [new PdfStructureElement { Type = "P", Text = "Part 1234-56-7890" }] };

        var snapshot = document.ToSnapshot(o => o.ScrubDates());

        Assert.Equal(["  P \"Part 1234-56-7890\""], SectionLines(snapshot, "structure"));
    }

    [Fact]
    public void OnlyPages_ShouldThrow_WhenGivenNoPagesOrPagesBelowOne()
    {
        var options = new PdfSnapshotOptions();

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
        Settings = new PdfSettings
        {
            PdfVersion = "1.7", OpenAction = "p1 Fit", IsEncrypted = true, Permissions = PdfPermissions.Print,
            EmbeddedFiles = [new PdfEmbeddedFile("a.txt", null, null, null, 5, "2cf24dba5fb0a30e")],
        },
        Metadata = new PdfMetadata("Title", "Ann", "Trails", "park", true, true, "Producer 1.0", "Word", "1", "Title")
        {
            OtherInfo = [new PdfSetting("Company", "Example Park")], XmpCreator = "Ann", XmpDescription = "Trails", XmpLanguage = "en-US",
        },
        Pages = [new PdfPage(1, 612, 792, 0, null, false) { CropBox = new PdfBox(0, 0, 600, 780) }],
        Bookmarks =
        [
            new PdfBookmark("Intro", 1, []) { Destination = new PdfDestination { Page = 1, View = "FitH", Positions = [700] } },
            new PdfBookmark("Map", null, []) { Uri = "https://example.com/map" },
        ],
        Structure =
        [
            new PdfStructureElement
            {
                Type = "Figure", Language = "en-US", AltText = "Logo", ActualText = "Logo", ImageHashes = ["0123456789abcdef"],
                MarkedSpans = [new PdfMarkedSpan { Language = "fr-FR", ActualText = "fi", AltText = "ligature" }],
            },
            new PdfStructureElement
            {
                Type = "Link",
                Annotations =
                [
                    new PdfAnnotation
                    {
                        Subtype = "Link", Action = "GoToR", File = "maps.pdf",
                        Destination = new PdfDestination { Page = 1, View = "XYZ", Positions = [72, 700] },
                    },
                ],
            },
        ],
        Annotations =
        [
            new PdfAnnotation { Subtype = "Link", Page = 1, X = 72, Y = 600, Width = 50, Height = 12, Uri = "https://example.com", Contents = "Example" },
        ],
        FormFields =
        [
            new PdfFormField
            {
                Name = "city", Type = PdfFormFieldType.Text, Value = "Tampa", DefaultValue = "Miami", TextFont = "Helvetica",
                TextSize = 10, TextColor = "gray:0",
            },
        ],
        TextRuns = [new PdfTextRun(1, 72, 700, "Arial", 12, "gray:0", PdfTextRenderMode.Fill, PdfContentRole.Tagged, "Hi")],
        Images = [new PdfImage(1, 72, 500, 10, 10, 1, 1, "0123456789abcdef", PdfContentRole.Tagged)],
        Paths = [new PdfVectorPath(1, 72, 300, 100, 0, null, "#ff0000", 0.5, PdfContentRole.Artifact)],
    };

    private static string Body(string snapshot) => string.Join("\n", snapshot.Split('\n').Where(line => !line.StartsWith('#')));
}
