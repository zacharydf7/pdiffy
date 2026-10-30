namespace PDiffy.Tests.Unit;

public class CheckChangesTests
{
    private const string Header = "# pdfua-snapshot v1\n# checks: latest, 12 rules\n";

    [Fact]
    public void Compare_ShouldReportNewAndFixedProblems_WhenTheChecksSectionChanges()
    {
        var before = Checks("FigureAltText p2 structure > Document > Figure[2]",
            "TableHeaders p3 structure > Document > Table > TR > TH[2] > scope");
        var after = Checks("FigureAltText p2 structure > Document > Figure[2]",
            "FigureAltText p4 structure > Document > Figure[3]");

        var diff = SnapshotDiff.CompareSnapshots(before, after);

        Assert.Equal("""
            2 change(s): 1 new problem, 1 fixed
            by section: checks 2
            1 can affect accessibility, listed first
            + new problem FigureAltText (p4) [accessibility]
                structure > Document > Figure[3]
            - fixed TableHeaders (p3)
                structure > Document > Table > TR > TH[2] > scope
            """.ReplaceLineEndings("\n"), diff.Describe());
    }

    [Fact]
    public void Compare_ShouldNotFlagANewProblem_WhenItComesFromAHouseRule()
    {
        var diff = SnapshotDiff.CompareSnapshots(Checks(), Checks("NoActions settings > javascript"));

        var change = Assert.Single(diff.Changes);
        Assert.Equal((ChangeKind.Added, "NoActions", null, "settings > javascript", false),
            (change.Kind, change.Path, change.Page, change.Detail, change.AffectsAccessibility));
    }

    [Fact]
    public void Compare_ShouldReportAFixedAndANewProblem_WhenAFindingMovesToAnotherElement()
    {
        var before = Checks("FigureAltText p2 structure > Document > Figure[2]");
        var after = Checks("FigureAltText p2 structure > Document > Figure[3]");

        var diff = SnapshotDiff.CompareSnapshots(before, after);

        Assert.Equal([ChangeKind.Removed, ChangeKind.Added], diff.Changes.Select(change => change.Kind));
    }

    [Fact]
    public void Compare_ShouldCountEachCopy_WhenAFindingRepeats()
    {
        var link = "LinkDescriptions p1 annotations";

        var diff = SnapshotDiff.CompareSnapshots(Checks(link, link), Checks(link, link, link));

        Assert.Equal(ChangeKind.Added, Assert.Single(diff.Changes).Kind);
    }

    [Fact]
    public void Describe_ShouldSayHowManyRulesWereAdded_WhenTheRuleSetGrows()
    {
        var after = Checks("DocumentLanguage document").Replace("latest, 12 rules", "latest, 14 rules");

        var diff = SnapshotDiff.CompareSnapshots(Checks(), after);

        Assert.Contains("\n2 rule(s) added to the checks, so a new problem can come from a new rule\n",
            diff.Describe());
    }

    [Fact]
    public void Compare_ShouldReportTheFindingsAsNew_WhenTheChecksSectionWasIgnored()
    {
        var before = Checks().Replace("(none)", "(ignored)");

        var diff = SnapshotDiff.CompareSnapshots(before, Checks("DocumentLanguage document"));

        Assert.Equal((ChangeKind.Added, "DocumentLanguage", "document"),
            (Assert.Single(diff.Changes).Kind, diff.Changes[0].Path, diff.Changes[0].Detail));
    }

    [Fact]
    public void Compare_ShouldKeepTheFixedProblemsPage_WhenAPageIsInsertedBeforeIt()
    {
        var before = Pages(2, "LinkDescriptions p2 annotations");
        var after = Pages(3, "FigureAltText p3 structure > Figure");

        var diff = SnapshotDiff.CompareSnapshots(before, after);

        Assert.Equal([(ChangeKind.Removed, 2), (ChangeKind.Added, 3)],
            diff.Changes.Where(change => change.SectionName == "checks").Select(change => (change.Kind, change.Page)));
    }

    // A snapshot whose pages each hold one paragraph, the last with a finding; the later one has a page inserted first.
    private static string Pages(int count, string finding)
    {
        var paragraphs = Enumerable.Range(1, count).Select(page => $"    P p{page} \"Stage {page - (count - 2)}\"");
        return Header + $"document\n  marked=yes pages={count}\nstructure\n  Document\n" + string.Join("\n", paragraphs)
            + $"\nchecks\n  {finding}\n";
    }

    // A snapshot with only a checks section; with no findings it prints "(none)", which isn't a problem.
    private static string Checks(params string[] findings) =>
        Header + "checks\n"
        + (findings.Length == 0 ? "  (none)\n" : string.Concat(findings.Select(line => $"  {line}\n")));
}
