namespace PDiffy.Tests.Unit;

public class SnapshotBreakdownTests
{
    // Snapshots always use LF, whatever line endings Git gives this file.
    private static readonly string LayoutHeavy = """
        # pdfua-snapshot v1
        document
          marked=yes lang=en-US pages=2
        structure
          P p1 "Trailhead"
        fonts
          (none)
        layout
          p1 text tagged "Trailhead"
          p1 path artifact
          p2 path artifact

        """.ReplaceLineEndings("\n");

    [Fact]
    public void Of_ShouldCountLinesBySectionAndHintAtAProfile_WhenLayoutIsMostOfAPdfSnapshot()
    {
        var breakdown = SnapshotBreakdown.Of(LayoutHeavy);

        var lines = breakdown!.Split(Environment.NewLine);
        Assert.Equal("2 pages; 5 lines: layout 3 (60%), document 1, structure 1.", lines[0]);
        Assert.StartsWith("Most of it is layout", lines[1]);
    }

    [Fact]
    public void Of_ShouldGiveNoHint_WhenLayoutIsLessThanHalf()
    {
        var snapshot = LayoutHeavy.Replace("  p1 path artifact\n  p2 path artifact\n", "");

        var breakdown = SnapshotBreakdown.Of(snapshot);

        Assert.Equal("2 pages; 3 lines: document 1, structure 1, layout 1 (33%).", breakdown);
    }

    [Fact]
    public void Of_ShouldGiveNoHint_WhenTheSnapshotAlreadyLeavesSomethingOut()
    {
        var snapshot = LayoutHeavy.Replace("# pdfua-snapshot v1\n", "# pdfua-snapshot v1\n# ignored fields: positions\n");

        var breakdown = SnapshotBreakdown.Of(snapshot);

        Assert.Equal("2 pages; 5 lines: layout 3 (60%), document 1, structure 1.", breakdown);
    }

    [Theory]
    [InlineData("trail 12\n")]
    [InlineData("{\n  \"trail\": 12\n}")]
    public void Of_ShouldGiveNothing_WhenTheSnapshotIsTextOrJson(string snapshot)
    {
        Assert.Null(SnapshotBreakdown.Of(snapshot));
    }

    [Fact]
    public void Of_ShouldLeaveOutPagesAndTheHint_WhenTheSnapshotIsADocx()
    {
        var snapshot = "# docx-snapshot v1\ndocument\n  lang=en-US\nbody\n  p \"Trailhead\"\n  p \"Ridgeline\"\n";

        var breakdown = SnapshotBreakdown.Of(snapshot);

        Assert.Equal("3 lines: body 2, document 1.", breakdown);
    }
}
