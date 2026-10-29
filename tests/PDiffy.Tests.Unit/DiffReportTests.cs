using System.Text.RegularExpressions;

namespace PDiffy.Tests.Unit;

public class DiffReportTests
{
    private static readonly string Before = string.Join("\n",
    [
        "# pdfua-snapshot v1", "pages", "  p1 letter rotate=0", "  p2 letter rotate=0", "  p3 letter rotate=0",
        "structure", .. Enumerable.Range(1, 20).Select(i => $"  P p{i / 10 + 1} \"Line {i}\""), "  P p3 \"Take with food\"", "",
    ]);

    [Fact]
    public void ToHtml_ShouldShowBothSidesWithTheChangedWordsMarked_WhenALineChanges()
    {
        var html = SnapshotDiff.Compare(Before, Before.Replace("Take with food", "Take after food")).ToHtml();

        Assert.Contains("<th>Before</th><th>After</th>", html);
        Assert.Contains("<td class=\"removed\">  P p3 &quot;Take <mark>with </mark>food&quot;</td>", html);
        Assert.Contains("<td class=\"added\">  P p3 &quot;Take <mark>after </mark>food&quot;</td>", html);
    }

    [Fact]
    public void ToHtml_ShouldListAccessibilityChangesFirstWithAFilter_WhenSomeAffectAccessibility()
    {
        var after = Before.Replace("  P p1 \"Line 1\"", "  P p1 \"Line one\"").Replace("  P p3 \"Take with food\"", "  H2 p3 \"Take with food\"");

        var html = SnapshotDiff.Compare(Before, after).ToHtml();

        Assert.Contains("2 change(s), by section: structure 2", html);
        Assert.Contains("<input type=\"checkbox\" data-accessibility> Only accessibility (1)", html);
        var flagged = html.IndexOf("<li data-kind=\"Changed\" data-accessibility>", StringComparison.Ordinal);
        Assert.InRange(flagged, 0, html.IndexOf("<li data-kind=\"TextChanged\">", StringComparison.Ordinal));
    }

    [Fact]
    public void ToHtml_ShouldShowARetypedElementOnOneRow_WhenOnlyItsTypeChanges()
    {
        var html = SnapshotDiff.Compare(Before, Before.Replace("  P p3 \"Take with food\"", "  H2 p3 \"Take with food\"")).ToHtml();

        Assert.Contains("<td class=\"removed\">  <mark>P </mark>p3 &quot;Take with food&quot;</td>"
            + "<td class=\"added\">  <mark>H2 </mark>p3 &quot;Take with food&quot;</td>", html);
    }

    [Fact]
    public void ToHtml_ShouldPairLinesLikeTheChangeList_WhenALineIsInsertedBeforeAnEditedOne()
    {
        var after = Before.Replace("  P p3 \"Take with food\"", "  P p3 \"New warning\"\n  P p3 \"Take after food\"");

        var html = SnapshotDiff.Compare(Before, after).ToHtml();

        Assert.Contains("<td></td><td class=\"added\">  P p3 &quot;New warning&quot;</td>", html);
        Assert.Contains("<mark>with </mark>", html);
    }

    [Fact]
    public void ToHtml_ShouldLinkEachPageToItsFirstChangeAndFoldUnchangedLines_WhenMostLinesMatch()
    {
        var html = SnapshotDiff.Compare(Before, Before.Replace("Take with food", "Take after food")).ToHtml();

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
        var html = SnapshotDiff.Compare(SnapshotText.Header + SwapBefore, SnapshotText.Header + SwapAfter).ToHtml();

        var links = Regex.Matches(html, "href=\"#(p\\d+)\"").Select(match => match.Groups[1].Value).Distinct().ToList();
        Assert.NotEmpty(links);
        Assert.All(links, page => Assert.Contains($"id=\"{page}\"", html));
    }

    [Fact]
    public void ToHtml_ShouldAnchorAPageInTheSectionItChangedIn_WhenTheLineViewShowsNoChangeOnThatPage()
    {
        var header = SnapshotText.Header + "pages\n  p1 letter rotate=0\n  p2 letter rotate=0\n";

        var html = SnapshotDiff.Compare(header + SwapBefore, header + SwapAfter).ToHtml();

        Assert.Contains("<tr id=\"p1\"><td>    Sect p1</td>", html);
    }

    [Fact]
    public void ToHtml_ShouldShowTheWholeTextAsOneSection_WhenTheTextIsNotASnapshot()
    {
        var html = SnapshotDiff.Compare("structure\nTake with food\n", "structure\nTake after food\n").ToHtml();

        Assert.Single(html.Split("<details class=\"section\"").Skip(1));
        Assert.Contains("<summary>text</summary>", html);
        Assert.Contains("<td class=\"added\">Take <mark>after </mark>food</td>", html);
    }

    [Fact]
    public void ToHtml_ShouldEncodeTextAndLoadNothingFromOutside_WhenTextLooksLikeMarkup()
    {
        var html = SnapshotDiff.Compare(Before, Before.Replace("Take with food", "<script>alert(1)</script>")).ToHtml();

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("src=", html);
        Assert.DoesNotContain("<link", html);
    }
}
