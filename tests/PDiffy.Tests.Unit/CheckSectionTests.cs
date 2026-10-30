using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

public class CheckSectionTests
{
    [Fact]
    public void ToSnapshot_ShouldRecordEachFindingByRulePageAndPath_WhenAskedToCheck()
    {
        var document = Guide(lang: null).Load();

        var snapshot = document.ToSnapshot(options => options.Check(Rules));

        Assert.Contains("# checks: latest, 2 rules\n", snapshot);
        Assert.EndsWith("checks\n  DocumentLanguage document\n  FigureAltText p2 structure > Figure\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldPrintNone_WhenTheChecksFindNothing()
    {
        var document = Guide(lang: "en-US").Load();

        var snapshot = document.ToSnapshot(options => options.Check(Rules.Except(PdfCheck.FigureAltText)));

        Assert.EndsWith("checks\n  (none)\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldLeaveOutTheChecks_WhenNotAskedTo()
    {
        var snapshot = Guide(lang: null).Load().ToSnapshot();

        Assert.DoesNotContain("checks", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldKeepOnlyTheFindingsOnThosePagesAndTheWholeDocuments_WhenLimitedToPages()
    {
        var document = Guide(lang: null).Load();

        var snapshot = document.ToSnapshot(options => options.Check(Rules).OnlyPages(1));

        Assert.EndsWith("checks\n  DocumentLanguage document\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldStillCheckTheWholeDocument_WhenSectionsAreIgnored()
    {
        var document = Guide(lang: "en-US").Load();

        var snapshot = document.ToSnapshot(options => options.Check(Rules).Ignore(PdfSection.Structure));

        Assert.EndsWith("checks\n  FigureAltText p2 structure > Figure\n", snapshot);
    }

    // A run on page 2 alone would see an H2 before any H1.
    [Fact]
    public void ToSnapshot_ShouldCheckTheWholeDocument_WhenLimitedToALaterPage()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Trails");
        pdf.AddPage().Tagged(0, "Ridge loop");
        pdf.Structure(new TestElement("H1", 0), new TestElement("H2", 0) { Page = 2 });

        var snapshot = pdf.Load().ToSnapshot(options =>
            options.Check(PdfCheckSet.Latest.Only(PdfCheckCategory.Headings)).OnlyPages(2));

        Assert.EndsWith("checks\n  (none)\n", snapshot);
    }

    // An upgrading user who opts in should see only the new header line and the new section.
    [Fact]
    public void Compare_ShouldReportOnlyTheHeaderLineAndTheNewSection_WhenATestStartsCheckingTheDocument()
    {
        var document = Guide(lang: null).Load();

        var diff = SnapshotDiff.CompareSnapshots(document.ToSnapshot(),
            document.ToSnapshot(options => options.Check(PdfCheckSet.Latest)));

        Assert.Equal(["header", "checks"], diff.Changes.Select(change => change.SectionName).Distinct());
        Assert.Equal($"+ # checks: {PdfCheckSet.Latest}", diff.Changes[0].Detail);
        Assert.All(diff.Changes.Skip(1), change => Assert.Equal(ChangeKind.Added, change.Kind));
    }

    // The test PDF's font isn't embedded, so these tests apply only the rules they're about.
    private static readonly PdfCheckSet Rules =
        PdfCheckSet.Latest.Only(PdfCheckCategory.Language, PdfCheckCategory.AltText);

    // A paragraph on page 1, and a figure with no alt text on page 2.
    private static TestPdf Guide(string? lang)
    {
        var pdf = new TestPdf { Lang = lang };
        pdf.AddPage().Tagged(0, "Ridge loop");
        pdf.AddPage().TaggedImage(0);
        pdf.Structure(new TestElement("P", 0), new TestElement("Figure", 0) { Page = 2 });
        return pdf;
    }
}
