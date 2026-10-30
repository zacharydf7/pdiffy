using System.Text.RegularExpressions;

namespace PDiffy.Tests.Unit;

public class DiffReportTests
{
    private static readonly string Before = string.Join("\n",
    [
        "# pdfua-snapshot v1", "pages", "  p1 letter rotate=0", "  p2 letter rotate=0", "  p3 letter rotate=0",
        "structure", .. Enumerable.Range(1, 20).Select(i => $"  P p{i / 10 + 1} \"Line {i}\""), "  P p3 \"Go with a guide\"", "",
    ]);

    [Fact]
    public void ToHtml_ShouldListNewProblemsBeforeFixedOnesWithLinks_WhenTheChecksChange()
    {
        var header = "# pdfua-snapshot v1\n# checks: latest, 12 rules\nchecks\n";
        var before = header + "  TableHeaders p3 structure > TH > scope\n";
        var after = header + "  FigureAltText p4 structure > Figure\n";

        var html = SnapshotDiff.CompareSnapshots(before, after).ToHtml();

        Assert.Contains("<div class=\"checks\"><h2>Checks</h2><p class=\"summary\">1 new problem(s), 1 fixed</p>",
            html);
        Assert.Contains("<li class=\"new\"><b>New problem</b> <a href=\"#check1\">FigureAltText p4</a> "
            + "<code>structure &gt; Figure</code> <a href=\"https://zacharydf7.github.io/pdiffy/reference/checks.html"
            + "#figurealttext\">How to fix it</a></li>", html);
        Assert.True(html.IndexOf("<b>New problem</b>", StringComparison.Ordinal)
            < html.IndexOf("<b>Fixed</b> <a href=\"#check2\">TableHeaders p3</a>", StringComparison.Ordinal));
        Assert.Contains("<td class=\"added\" id=\"check1\">", html);
        Assert.Contains("<td class=\"removed\" id=\"check2\">", html);
    }

    [Fact]
    public void ToHtml_ShouldLeaveOutTheChecksPanel_WhenTheChecksDidNotChange()
    {
        var diff = SnapshotDiff.CompareSnapshots(Before, Before.Replace("Go with a guide", "Go without a guide"));

        Assert.DoesNotContain("<div class=\"checks\">", diff.ToHtml());
    }

    [Fact]
    public void ToHtml_ShouldLinkTheFixedAndTheNewFinding_WhenARuleMovesToAnotherPage()
    {
        var header = "# pdfua-snapshot v1\n# checks: latest, 12 rules\nchecks\n";
        var before = header + "  FigureAltText p2 structure > Figure\n";
        var after = header + "  FigureAltText p4 structure > Figure\n";

        var html = SnapshotDiff.CompareSnapshots(before, after).ToHtml();

        Assert.Contains("<a href=\"#check1\">FigureAltText p4</a>", html);
        Assert.Contains("<a href=\"#check2\">FigureAltText p2</a>", html);
        Assert.Contains("<td class=\"added\" id=\"check1\">", html);
        Assert.Contains("<td class=\"removed\" id=\"check2\">", html);
    }

    [Fact]
    public void ToHtml_ShouldGiveEachRepeatedFindingItsOwnLink_WhenTheSameFindingIsNewTwice()
    {
        var header = "# pdfua-snapshot v1\n# checks: latest, 12 rules\nchecks\n";
        var after = header + "  LinkDescriptions p1 structure > Link\n  LinkDescriptions p1 structure > Link\n";

        var html = SnapshotDiff.CompareSnapshots(header + "  (none)\n", after).ToHtml();

        Assert.Contains("<a href=\"#check1\">LinkDescriptions p1</a>", html);
        Assert.Contains("<a href=\"#check2\">LinkDescriptions p1</a>", html);
        Assert.Single(Regex.Matches(html, "id=\"check1\""));
        Assert.Single(Regex.Matches(html, "id=\"check2\""));
    }

    [Fact]
    public void ToHtml_ShouldDrawWhereItWasAndWhereItIs_WhenSomethingMovesOnAPage()
    {
        var before = Layout("p1 letter rotate=0", "p1 image tagged at=72,600 size=10x10 pixels=1x1 hash=x",
            "p1 text tagged at=72,720 size=12 \"Title\"");

        var html = SnapshotDiff.CompareSnapshots(before, before.Replace("at=72,600", "at=72,520")).ToHtml();

        Assert.Contains("<figure class=\"map\"><figcaption>p1</figcaption><svg viewBox=\"0 0 612 792\"", html);
        Assert.Matches("<g class=\"was\"><title>[^<]*at=72,600[^<]*</title><rect x=\"72\" y=\"182\" width=\"10\" "
            + "height=\"10\"/>", html);
        Assert.Matches("<g class=\"now\"><title>[^<]*at=72,520[^<]*</title><rect x=\"72\" y=\"262\" width=\"10\" "
            + "height=\"10\"/>", html);
        Assert.Contains("<g class=\"same\"><title>p1 text tagged at=72,720 size=12 &quot;Title&quot;</title>", html);
    }

    [Theory]
    [InlineData("p1 a4-landscape rotate=0", "0 0 841.9 595.3")]
    [InlineData("p1 500x700 rotate=0", "0 0 500 700")]
    [InlineData("p1 unreadable", "0 0 612 792")]
    public void ToHtml_ShouldDrawThePageAtItsSize_WhenThePagesSectionGivesOne(string page, string viewBox)
    {
        var before = Layout(page, "p1 path artifact at=72,500 size=100x20");

        var html = SnapshotDiff.CompareSnapshots(before, before.Replace("at=72,500", "at=80,500")).ToHtml();

        Assert.Contains($"<svg viewBox=\"{viewBox}\"", html);
    }

    [Fact]
    public void ToHtml_ShouldDrawNoMap_WhenNothingInTheLayoutChanged()
    {
        var before = Layout("p1 letter rotate=0", "p1 text tagged at=72,720 size=12 \"Title\"");

        var html = SnapshotDiff.CompareSnapshots(before, before.Replace("rotate=0", "rotate=90")).ToHtml();

        Assert.DoesNotContain("class=\"maps\"", html);
    }

    [Fact]
    public void ToHtml_ShouldDrawOnlyThePagesThatChanged_WhenOneOfTwoPagesChangesInTheLayout()
    {
        var before = Layout("p1 letter rotate=0\n  p2 letter rotate=0", "p1 path artifact at=72,500 size=100x20",
            "p2 path artifact at=72,500 size=100x20");

        var html = SnapshotDiff.CompareSnapshots(before, before.Replace("p1 path artifact at=72,500",
            "p1 path artifact at=80,500")).ToHtml();

        Assert.Contains("<figcaption>p1</figcaption>", html);
        Assert.DoesNotContain("<figcaption>p2</figcaption>", html);
    }

    [Fact]
    public void ToHtml_ShouldDrawTheRenumberedPage_WhenAPageIsInsertedBeforeIt()
    {
        var before = Layout("p1 letter rotate=0", "p1 text tagged at=72,700 size=12 \"The body of the guide\"");
        var after = Layout("p1 letter rotate=0\n  p2 letter rotate=0", "p1 text tagged at=72,700 size=12 \"A new cover\"",
            "p2 text tagged at=72,690 size=12 \"The body of the guide\"");

        var html = SnapshotDiff.CompareSnapshots(before, after).ToHtml();

        Assert.Matches("<figcaption>p2</figcaption>.*<g class=\"now\"><title>p2 text tagged at=72,690.*"
            + "<g class=\"was\"><title>p2 text tagged at=72,700", html);
    }

    [Fact]
    public void ToHtml_ShouldDrawATextRunAtATypicalSize_WhenItsFontSizeIsLeftOut()
    {
        var before = Layout("p1 letter rotate=0", "p1 text tagged at=72,700 \"Title\"");

        var html = SnapshotDiff.CompareSnapshots(before, before.Replace("at=72,700", "at=72,400")).ToHtml();

        Assert.Contains("<g class=\"now\"><title>p1 text tagged at=72,400 &quot;Title&quot;</title>"
            + "<rect x=\"72\" y=\"384\" width=\"25\" height=\"10\"/>", html);
    }

    [Fact]
    public void ToHtml_ShouldDrawNoMap_WhenTheLayoutLinesHaveNoPositions()
    {
        var before = Layout("p1 letter rotate=0", "p1 text tagged size=12 \"Title\"");

        var html = SnapshotDiff.CompareSnapshots(before, before.Replace("Title", "Title page")).ToHtml();

        Assert.DoesNotContain("class=\"maps\"", html);
    }

    private static string Layout(string page, params string[] layout) =>
        string.Join("\n", ["# pdfua-snapshot v1", "pages", "  " + page, "layout", .. layout.Select(line => "  " + line), ""]);

    [Fact]
    public void ToHtml_ShouldShowBothSidesWithTheChangedWordsMarked_WhenALineChanges()
    {
        var html = SnapshotDiff.CompareSnapshots(Before, Before.Replace("Go with a guide",
            "Go without a guide")).ToHtml();

        Assert.Contains("<th>Before</th><th>After</th>", html);
        Assert.Contains("<td class=\"removed\">  P p3 &quot;Go <mark>with </mark>a guide&quot;</td>", html);
        Assert.Contains("<td class=\"added\">  P p3 &quot;Go <mark>without </mark>a guide&quot;</td>", html);
    }

    [Fact]
    public void ToHtml_ShouldListAccessibilityChangesFirstWithAFilter_WhenSomeAffectAccessibility()
    {
        var after = Before.Replace("  P p1 \"Line 1\"", "  P p1 \"Line one\"").Replace("  P p3 \"Go with a guide\"", "  H2 p3 \"Go with a guide\"");

        var html = SnapshotDiff.CompareSnapshots(Before, after).ToHtml();

        Assert.Contains("2 change(s), by section: structure 2", html);
        Assert.Contains("<input type=\"checkbox\" data-accessibility> Only accessibility (1)", html);
        var flagged = html.IndexOf("<li data-kind=\"Changed\" data-accessibility>", StringComparison.Ordinal);
        Assert.InRange(flagged, 0, html.IndexOf("<li data-kind=\"TextChanged\">", StringComparison.Ordinal));
    }

    [Fact]
    public void ToHtml_ShouldShowARetypedElementOnOneRow_WhenOnlyItsTypeChanges()
    {
        var html = SnapshotDiff.CompareSnapshots(Before, Before.Replace("  P p3 \"Go with a guide\"",
            "  H2 p3 \"Go with a guide\"")).ToHtml();

        Assert.Contains("<td class=\"removed\">  <mark>P </mark>p3 &quot;Go with a guide&quot;</td>"
            + "<td class=\"added\">  <mark>H2 </mark>p3 &quot;Go with a guide&quot;</td>", html);
    }

    [Fact]
    public void ToHtml_ShouldPairLinesLikeTheChangeList_WhenALineIsInsertedBeforeAnEditedOne()
    {
        var after = Before.Replace("  P p3 \"Go with a guide\"", "  P p3 \"New warning\"\n  P p3 \"Go without a guide\"");

        var html = SnapshotDiff.CompareSnapshots(Before, after).ToHtml();

        Assert.Contains("<td></td><td class=\"added\">  P p3 &quot;New warning&quot;</td>", html);
        Assert.Contains("<mark>with </mark>", html);
    }

    [Fact]
    public void ToHtml_ShouldLinkEachPageToItsFirstChangeAndFoldUnchangedLines_WhenMostLinesMatch()
    {
        var html = SnapshotDiff.CompareSnapshots(Before, Before.Replace("Go with a guide",
            "Go without a guide")).ToHtml();

        Assert.Single(html.Split("id=\"p3\"").Skip(1));
        Assert.Contains("<tr id=\"p3\"><td class=\"removed\">", html);
        Assert.Contains("Pages: <a href=\"#p3\">3</a></nav>", html);
        Assert.Contains("<summary>17 unchanged line(s)</summary>", html);
    }

    // The change list calls this a move of the page 1 part; line by line, the page 2 part looks like the one that moved.
    private const string SwapBefore = "structure\n  Document\n    Sect p1\n      P p1 \"a\"\n      P p1 \"b\"\n    Sect p2\n"
        + "      P p2 \"c\"\n";

    private const string SwapAfter = "structure\n  Document\n    Sect p2\n      P p2 \"c\"\n    Sect p1\n      P p1 \"a\"\n"
        + "      P p1 \"b\"\n";

    [Fact]
    public void ToHtml_ShouldGiveEveryLinkedPageAnAnchor_WhenTheTwoViewsDisagreeAboutWhatMoved()
    {
        var html = SnapshotDiff.CompareSnapshots(SnapshotText.Header + SwapBefore,
            SnapshotText.Header + SwapAfter).ToHtml();

        var links = Regex.Matches(html, "href=\"#(p\\d+)\"").Select(match => match.Groups[1].Value).Distinct().ToList();
        Assert.NotEmpty(links);
        Assert.All(links, page => Assert.Contains($"id=\"{page}\"", html));
    }

    [Fact]
    public void ToHtml_ShouldAnchorAPageInTheSectionItChangedIn_WhenTheLineViewShowsNoChangeOnThatPage()
    {
        var header = SnapshotText.Header + "pages\n  p1 letter rotate=0\n  p2 letter rotate=0\n";

        var html = SnapshotDiff.CompareSnapshots(header + SwapBefore, header + SwapAfter).ToHtml();

        Assert.Contains("<tr id=\"p1\"><td>    Sect p1</td>", html);
    }

    [Fact]
    public void ToHtml_ShouldShowTheWholeTextAsOneSection_WhenTheTextIsNotASnapshot()
    {
        var html = SnapshotDiff.CompareSnapshots("structure\nGo with a guide\n",
            "structure\nGo without a guide\n").ToHtml();

        Assert.Single(html.Split("<details class=\"section\"").Skip(1));
        Assert.Contains("<summary>text</summary>", html);
        Assert.Contains("<td class=\"added\">Go <mark>without </mark>a guide</td>", html);
    }

    [Fact]
    public void ToHtml_ShouldEncodeTextAndLoadNothingFromOutside_WhenTextLooksLikeMarkup()
    {
        var html = SnapshotDiff.CompareSnapshots(Before, Before.Replace("Go with a guide",
            "<script>alert(1)</script>")).ToHtml();

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("src=", html);
        Assert.DoesNotContain("<link", html);
    }
}
