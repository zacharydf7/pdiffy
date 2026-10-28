namespace PDiffy.Tests.Unit;

public class SnapshotDiffTests
{
    // Normalized because Git can check this file out with CRLF line endings.
    private static readonly string Before = """
        structure
          Document lang=en-US
            H1 p1 "Drug Interaction Table"
            Figure p1 alt="Pharmacy logo"
            Table p2
              TR
                TH1 p2 scope=Column "Drug"
                TH2 p2 scope=Column "Interacts with"
              TR
                TD p2 headers=[TH1] "Warfarin"
                TD p2 headers=[TH2] "Take with food"
            P p3 "Call your pharmacist"
        layout
          p1 text tagged at=72,720 font=Arial size=18 color=gray:0 "Drug Interaction Table"
        """.ReplaceLineEndings("\n");

    [Fact]
    public void Compare_ShouldFindNoChanges_WhenSnapshotsAreEqual()
    {
        var diff = SnapshotDiff.Compare(Before, Before.ReplaceLineEndings("\r\n"));

        Assert.True(diff.IsEmpty);
        Assert.Equal("No changes.", diff.Describe());
    }

    [Fact]
    public void Compare_ShouldShowTheChangedWords_WhenAnElementsTextChanges()
    {
        var after = Before.Replace("Take with food", "Take after food");

        var change = Assert.Single(SnapshotDiff.Compare(Before, after).Changes);

        var path = "structure > Document > Table > TR[2] > TD[2]";
        Assert.Equal(new SnapshotChange(ChangeKind.TextChanged, Section.Structure, path, 2, "\"Take [-with-] {+after+} food\""), change);
    }

    [Fact]
    public void Compare_ShouldReportEachAttribute_WhenAttributesChange()
    {
        var after = Before.Replace("TH2 p2 scope=Column", "TH2 p2").Replace("color=gray:0", "color=#ff0000");

        var changes = SnapshotDiff.Compare(Before, after).Changes;

        SnapshotChange[] expected =
        [
            new(ChangeKind.AttributeChanged, Section.Structure, "structure > Document > Table > TR[1] > TH2", 2,
                "scope: Column -> (missing)"),
            new(ChangeKind.AttributeChanged, Section.Layout, "layout > p1", 1, "color: gray:0 -> #ff0000"),
        ];
        Assert.Equal(expected, changes);
    }

    [Fact]
    public void Compare_ShouldReportTheWholeLine_WhenAnElementMovesToAnotherPage()
    {
        var after = Before.Replace("TD p2 headers=[TH1]", "TD p3 headers=[TH1]");

        var change = Assert.Single(SnapshotDiff.Compare(Before, after).Changes);

        var detail = "TD p2 headers=[TH1] \"Warfarin\" -> TD p3 headers=[TH1] \"Warfarin\"";
        Assert.Equal((ChangeKind.Changed, detail), (change.Kind, change.Detail));
    }

    [Fact]
    public void Compare_ShouldCompareEachOccurrence_WhenAKeyRepeats()
    {
        var before = "structure\n  Link p1 annot=Link uri=\"https://a\" annot=Link uri=\"https://b\" \"here\"\n";
        var after = before.Replace("https://b", "https://c");

        var change = Assert.Single(SnapshotDiff.Compare(before, after).Changes);

        Assert.Equal((ChangeKind.AttributeChanged, "uri: \"https://b\" -> \"https://c\""), (change.Kind, change.Detail));
    }

    [Fact]
    public void Compare_ShouldStillReportALine_WhenOnlyTheOrderOfItsKeysChanges()
    {
        var before = "layout\n  p1 path tagged fill=gray:0 stroke=gray:1\n";
        var after = "layout\n  p1 path tagged stroke=gray:1 fill=gray:0\n";

        var change = Assert.Single(SnapshotDiff.Compare(before, after).Changes);

        Assert.Equal(ChangeKind.Changed, change.Kind);
    }

    [Fact]
    public void Compare_ShouldReportTheFirstLines_WhenOnlyTheOptionsChange()
    {
        var after = "# ignored fields: positions\n" + Before;

        var change = Assert.Single(SnapshotDiff.Compare(Before, after).Changes);

        Assert.Equal(new SnapshotChange(ChangeKind.Changed, null, "header", null, "+ # ignored fields: positions"), change);
    }

    [Fact]
    public void Compare_ShouldReportWhitespace_WhenOnlyBlankLinesDiffer()
    {
        var change = Assert.Single(SnapshotDiff.Compare(Before, Before + "\n").Changes);

        Assert.Equal(("whitespace", null), (change.Path, change.Section));
    }

    [Fact]
    public void Compare_ShouldReportAMove_WhenAnElementChangesPlaceInReadingOrder()
    {
        var figure = "    Figure p1 alt=\"Pharmacy logo\"\n";
        var after = Before.Replace(figure, "").Replace("    P p3", figure + "    P p3");

        var change = Assert.Single(SnapshotDiff.Compare(Before, after).Changes);

        var expected = new SnapshotChange(ChangeKind.Moved, Section.Structure, "structure > Document > Figure", 1,
            "reading position 3 -> 10");
        Assert.Equal(expected, change);
    }

    [Fact]
    public void Compare_ShouldReportRemovedAndAdded_WhenAReplacementSharesTooFewWords()
    {
        var after = Before.Replace("P p3 \"Call your pharmacist\"", "P p3 \"Ask questions\"");

        var changes = SnapshotDiff.Compare(Before, after).Changes;

        SnapshotChange[] expected =
        [
            new(ChangeKind.Removed, Section.Structure, "structure > Document > P", 3, "P p3 \"Call your pharmacist\""),
            new(ChangeKind.Added, Section.Structure, "structure > Document > P", 3, "P p3 \"Ask questions\""),
        ];
        Assert.Equal(expected, changes);
    }

    [Fact]
    public void Compare_ShouldNameBookmarksByTitle_WhenABookmarkChanges()
    {
        var before = "bookmarks\n  \"Warnings\" -> p3\n    \"Food\"\n";
        var after = before.Replace("\"Food\"", "\"Food\" -> p4");

        var change = Assert.Single(SnapshotDiff.Compare(before, after).Changes);

        Assert.Equal((ChangeKind.Changed, "bookmarks > Warnings > Food", 4), (change.Kind, change.Path, change.Page));
    }

    [Theory]
    [InlineData("P p1 \"He said \\\"stop\\\"\"", "P p1 \"He said \\\"go\\\"\"", "\"He said [-\"stop\"-] {+\"go\"+}\"")]
    [InlineData("Figure p1", "Figure p1 \"Logo\"", "\"{+Logo+}\"")]
    public void Compare_ShouldShowReadableText_WhenTextHasQuotesOrStartsEmpty(string before, string after, string detail)
    {
        var change = Assert.Single(SnapshotDiff.Compare($"structure\n  {before}\n", $"structure\n  {after}\n").Changes);

        Assert.Equal((ChangeKind.TextChanged, detail), (change.Kind, change.Detail));
    }

    // Two removals far apart leave nearly the whole section between them to align, which takes more than a quick pass.
    [Fact]
    public void Compare_ShouldReportJustTheRemovals_WhenLinesGoFromFarApartInALongSection()
    {
        var lines = Enumerable.Range(1, 3000)
            .Select(i => $"  p{i / 100 + 1} text tagged font=Arial size=12 color=gray:0 \"Line {i}\"").ToList();
        var before = "layout\n" + string.Join("\n", lines) + "\n";
        var after = "layout\n" + string.Join("\n", lines.Where((_, i) => i != 100 && i != 2900)) + "\n";

        var changes = SnapshotDiff.Compare(before, after).Changes;

        string[] expected = [lines[100].TrimStart(), lines[2900].TrimStart()];
        Assert.Equal([ChangeKind.Removed, ChangeKind.Removed], changes.Select(change => change.Kind));
        Assert.Equal(expected, changes.Select(change => change.Detail));
    }

    [Fact]
    public void Describe_ShouldCountChangesByKindAndListThem_WhenThereAreChanges()
    {
        var after = Before.Replace("Take with food", "Take after food").Replace("TH2 p2 scope=Column", "TH2 p2");

        var text = SnapshotDiff.Compare(Before, after).Describe(maxChanges: 1);

        var expected = "2 change(s): 1 attribute, 1 text\n"
            + "~ attribute structure > Document > Table > TR[1] > TH2 (p2)\n    scope: Column -> (missing)\n"
            + "... and 1 more";
        Assert.Equal(expected, text);
    }

    [Fact]
    public void CompareTo_ShouldCompareTheSnapshots_WhenGivenTwoDocuments()
    {
        var paragraph = new StructureElement { Type = "P", FirstPage = 1, Text = "Take with food" };
        var before = Documents.Empty() with { Structure = [paragraph] };
        var after = before with { Structure = [paragraph with { Text = "Take after food" }] };

        var change = Assert.Single(before.CompareTo(after).Changes);

        Assert.Equal((ChangeKind.TextChanged, Section.Structure, "structure > P"), (change.Kind, change.Section, change.Path));
    }
}
