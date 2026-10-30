namespace PDiffy.Tests.Unit;

public class LayoutShiftsTests
{
    [Fact]
    public void Describe_ShouldShowTheReflowAsOneShift_WhenAnInsertedLineMovesTheLinesBelowIt()
    {
        var before = Layout(Lines(6, insertAt: null));
        var after = Layout(Lines(6, insertAt: 2));

        var diff = SnapshotDiff.CompareSnapshots(before, after);

        Assert.Equal(5, diff.Changes.Count);
        Assert.Contains("~ shifted layout > p1[4] to p1[7] (p1)\n    4 lines down 16 pt", diff.Describe());
        Assert.Contains("+ added layout > p1[3] (p1)", diff.Describe());
    }

    [Fact]
    public void Describe_ShouldStillListAColorChange_WhenItsLineIsPartOfAShift()
    {
        var before = Layout(Lines(6, insertAt: null));
        var after = Layout(Lines(6, insertAt: 2)).Replace("gray:0 \"Line 5\"", "#ff0000 \"Line 5\"");

        var described = SnapshotDiff.CompareSnapshots(before, after).Describe();

        Assert.Contains("    color: gray:0 -> #ff0000", described);
        Assert.Contains("~ shifted", described);
    }

    [Fact]
    public void Describe_ShouldListEachMove_WhenOnlyTwoLinesMovedTheSameWay()
    {
        var before = Layout([Line(700, "Line 1"), Line(684, "Line 2"), Line(668, "Line 3")]);
        var after = Layout([Line(700, "Line 1"), Line(668, "Line 2"), Line(652, "Line 3")]);

        var described = SnapshotDiff.CompareSnapshots(before, after).Describe();

        Assert.DoesNotContain("~ shifted", described);
        Assert.Contains("    at: 72,684 -> 72,668", described);
        Assert.Contains("    at: 72,668 -> 72,652", described);
    }

    [Fact]
    public void Describe_ShouldCountTheShiftedLinesAsShown_WhenTheShiftFitsInTheList()
    {
        var before = Layout(Lines(30, insertAt: null));
        var after = Layout(Lines(30, insertAt: 0));

        var described = SnapshotDiff.CompareSnapshots(before, after).Describe(maxChanges: 2);

        Assert.Contains("    30 lines down 16 pt", described);
        Assert.DoesNotContain(" more", described);
    }

    [Fact]
    public void Describe_ShouldStartANewShift_WhenTheLinesMoveOnToAnotherPage()
    {
        // Page 1's last moved line is p1[3] and page 2's first is p2[4], so only the page tells the two runs apart.
        var pageOne = Enumerable.Range(1, 3).Select(i => Line(700 - 16 * i, $"Line {i}")).ToList();
        var pageTwo = Enumerable.Range(1, 6).Select(i => Line(700 - 16 * i, $"Later line {i}", page: 2)).ToList();
        var before = Layout([.. pageOne, .. pageTwo]);
        var after = Layout([
            .. Enumerable.Range(1, 3).Select(i => Line(690 - 16 * i, $"Line {i}")),
            .. pageTwo.Take(3),
            .. Enumerable.Range(4, 3).Select(i => Line(690 - 16 * i, $"Later line {i}", page: 2)),
        ]);

        var described = SnapshotDiff.CompareSnapshots(before, after).Describe();

        Assert.Contains("~ shifted layout > p1[1] to p1[3] (p1)\n    3 lines down 10 pt", described);
        Assert.Contains("~ shifted layout > p2[4] to p2[6] (p2)\n    3 lines down 10 pt", described);
    }

    [Fact]
    public void Describe_ShouldNameEveryDirection_WhenTheLinesMoveUpAndSideways()
    {
        var before = Layout([Line(600, "Line 1"), Line(584, "Line 2"), Line(568, "Line 3")]);
        var after = Layout([Line(616, "Line 1", x: 74), Line(600, "Line 2", x: 74), Line(584, "Line 3", x: 74)]);

        var described = SnapshotDiff.CompareSnapshots(before, after).Describe();

        Assert.Contains("    3 lines up 16 pt and right 2 pt", described);
    }

    [Fact]
    public void Describe_ShouldEndTheShift_WhenAnUnchangedLineSitsBetweenMovedOnes()
    {
        var before = Layout([.. Enumerable.Range(1, 7).Select(i => Line(800 - 20 * i, $"Line {i}"))]);
        var after = Layout([.. Enumerable.Range(1, 7).Select(i => Line(i == 4 ? 720 : 790 - 20 * i, $"Line {i}"))]);

        var described = SnapshotDiff.CompareSnapshots(before, after).Describe();

        Assert.Contains("~ shifted layout > p1[1] to p1[3] (p1)", described);
        Assert.Contains("~ shifted layout > p1[5] to p1[7] (p1)", described);
    }

    // Text lines 16 pt apart from the top of the page, with "New line" inserted before the line at that index.
    private static List<string> Lines(int count, int? insertAt)
    {
        var texts = Enumerable.Range(1, count).Select(i => $"Line {i}").ToList();
        if (insertAt is int index) texts.Insert(index, "New line");
        return texts.Select((text, i) => Line(700 - 16 * i, text)).ToList();
    }

    private static string Line(int y, string text, int x = 72, int page = 1) =>
        $"  p{page} text tagged at={x},{y} font=Arial size=12 color=gray:0 \"{text}\"";

    private static string Layout(List<string> lines) =>
        SnapshotText.Header + "layout\n" + string.Join("\n", lines) + "\n";
}
