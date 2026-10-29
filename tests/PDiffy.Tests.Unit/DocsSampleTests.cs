using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

// The docs site publishes a sample report built from two versions of a small guide, so it shows real PDiffy output.
public class DocsSampleTests
{
    [Fact]
    public void ToHtml_ShouldMatchTheDocsSamples_WhenTheGuideIsRevised()
    {
        var (before, after) = (Guide(revised: false).Load().ToSnapshot(), Guide(revised: true).Load().ToSnapshot());

        var report = SnapshotDiff.Compare(before, after).ToHtml();

        AssertSample("DrugGuide.verified.pdfua.txt", before);
        AssertSample("DrugGuide.received.pdfua.txt", after);
        AssertSample("DrugGuide.diff.html", report);
    }

    // The revision edits a cell, moves the logo after the text in reading order and further down the page, drops its alt
    // text, removes a header's scope, swaps one paragraph for another and stamps untagged "Draft" text on the table page.
    private static TestPdf Guide(bool revised)
    {
        var pdf = new TestPdf { Title = "Drug Interaction Guide" };
        var first = pdf.AddPage().Tagged(0, "Drug Interaction Guide", "H1");
        if (revised)
        {
            first.Tagged(1, "Read this guide before you start a new medicine.")
                .Tagged(2, "Keep all medicines out of reach of children.").TaggedImage(3, y: 520);
        }
        else
        {
            first.TaggedImage(1).Tagged(2, "Read this guide before you start a new medicine.")
                .Tagged(3, "Ask your pharmacist if you have questions.");
        }

        first.Artifact("Example Pharmacy");
        var second = pdf.AddPage().Tagged(0, "Interactions", "H2").Tagged(1, "Drug").Tagged(2, "Advice")
            .Tagged(3, "Warfarin").Tagged(4, revised ? "Take after food" : "Take with food")
            .Tagged(5, "Simvastatin").Tagged(6, "Avoid grapefruit juice");
        if (revised) second.Untagged("Draft");
        second.Artifact("Page 2");

        var heading = new TestElement("H1", 0);
        var logo = new TestElement("Figure", revised ? 3 : 1) { Entries = revised ? "" : "/Alt (Pharmacy logo)" };
        TestElement[] page1 = revised
            ? [heading, new TestElement("P", 1), new TestElement("P", 2), logo]
            : [heading, logo, new TestElement("P", 2), new TestElement("P", 3)];
        pdf.Structure([.. page1, new TestElement("H2", 0) { Page = 2 }, Table()]);
        pdf.Bookmark("Interactions", 2);
        return pdf;

        TestElement Table() => new("Table",
            new TestElement("TR", Header("TH1", 1, scoped: true), Header("TH2", 2, scoped: !revised)) { Page = 2 },
            new TestElement("TR", Cell(3, "TH1"), Cell(4, "TH2")) { Page = 2 },
            new TestElement("TR", Cell(5, "TH1"), Cell(6, "TH2")) { Page = 2 }) { Page = 2 };
    }

    private static TestElement Header(string id, int mcid, bool scoped) =>
        new("TH", mcid) { Page = 2, Entries = $"/ID ({id})" + (scoped ? " /A << /O /Table /Scope /Column >>" : "") };

    private static TestElement Cell(int mcid, string header) =>
        new("TD", mcid) { Page = 2, Entries = $"/A << /O /Table /Headers [({header})] >>" };

    // A sample that differs fails the test, or with PDIFFY_ACCEPT=1 (not on CI) is rewritten, as a snapshot is. The
    // report's page template takes its line endings from the source checkout, so line endings don't count.
    private static void AssertSample(string name, string expected)
    {
        expected = expected.ReplaceLineEndings("\n");
        var path = Path.Combine(SamplesFolder(), name);
        var actual = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : null;
        if (actual != expected && SnapshotWorkflow.AcceptMode()) File.WriteAllText(path, expected);
        else Assert.True(actual == expected, $"docs/samples/{name} is out of date; run the tests with PDIFFY_ACCEPT=1.");
    }

    // Found from the test's output folder, since CI builds map the source paths the compiler records.
    private static string SamplesFolder()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var samples = Path.Combine(folder.FullName, "docs", "samples");
            if (Directory.Exists(samples)) return samples;
        }

        throw new DirectoryNotFoundException("docs/samples wasn't found above the test's output folder.");
    }
}
