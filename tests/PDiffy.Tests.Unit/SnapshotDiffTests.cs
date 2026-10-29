namespace PDiffy.Tests.Unit;

public class SnapshotDiffTests
{
    // Normalized because Git can check this file out with CRLF line endings.
    private static readonly string Before = """
        # pdfua-snapshot v1
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
        var before = SnapshotText.Header + "structure\n  Link p1 annot=Link uri=\"https://a\" annot=Link uri=\"https://b\" \"here\"\n";
        var after = before.Replace("https://b", "https://c");

        var change = Assert.Single(SnapshotDiff.Compare(before, after).Changes);

        Assert.Equal((ChangeKind.AttributeChanged, "uri: \"https://b\" -> \"https://c\""), (change.Kind, change.Detail));
    }

    [Fact]
    public void Compare_ShouldStillReportALine_WhenOnlyTheOrderOfItsKeysChanges()
    {
        var before = SnapshotText.Header + "layout\n  p1 path tagged fill=gray:0 stroke=gray:1\n";
        var after = SnapshotText.Header + "layout\n  p1 path tagged stroke=gray:1 fill=gray:0\n";

        var change = Assert.Single(SnapshotDiff.Compare(before, after).Changes);

        Assert.Equal(ChangeKind.Changed, change.Kind);
    }

    [Fact]
    public void Compare_ShouldReportTheFirstLines_WhenOnlyTheOptionsChange()
    {
        var after = Before.Replace(SnapshotText.Header, SnapshotText.Header + "# ignored fields: positions\n");

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

    [Theory]
    [InlineData("structure", "P p1 \"Take daily\"", "P p1 \"Avoid alcohol\"")]
    [InlineData("layout", "p1 text tagged at=72,700 font=Arial size=12 color=gray:0 \"Warfarin\"",
        "p1 text tagged at=400,300 font=Arial size=12 color=gray:0 \"Signature line\"")]
    [InlineData("layout", "p1 text tagged at=72,700 font=Arial size=12 color=gray:0 \"Warfarin\"",
        "p1 text tagged at=400,300 font=Arial size=12 color=gray:0 \"Total\"")]
    public void Compare_ShouldReportRemovedAndAdded_WhenTheTextSharesNoWords(string section, string before, string after)
    {
        var changes = SnapshotDiff.Compare($"{SnapshotText.Header}{section}\n  {before}\n", $"{SnapshotText.Header}{section}\n  {after}\n").Changes;

        Assert.Equal([ChangeKind.Removed, ChangeKind.Added], changes.Select(change => change.Kind));
    }

    [Fact]
    public void Compare_ShouldReportOneTypeChange_WhenAStructureElementsTypeChangesButItsTextDoesNot()
    {
        var after = Before.Replace("H1 p1 \"Drug Interaction Table\"", "H2 p1 \"Drug Interaction Table\"");

        var change = Assert.Single(SnapshotDiff.Compare(Before, after).Changes);

        Assert.Equal(new SnapshotChange(ChangeKind.Changed, Section.Structure, "structure > Document > H2", 1, "type: H1 -> H2"), change);
    }

    [Fact]
    public void Compare_ShouldPairALineWithOneOfItsOwnType_WhenAnotherTypeHasTheSameTextFirst()
    {
        var before = SnapshotText.Header + "structure\n  P p1 lang=en \"Take\"\n";
        var after = SnapshotText.Header + "structure\n  H3 p1 \"Take\"\n  P p1 lang=fr \"Take\"\n";

        var changes = SnapshotDiff.Compare(before, after).Changes;

        Assert.Equal([(ChangeKind.Added, "structure > H3"), (ChangeKind.AttributeChanged, "structure > P")],
            changes.Select(change => (change.Kind, change.Path)));
    }

    [Fact]
    public void Compare_ShouldReportRemovedAndAdded_WhenALineOutsideTheStructureChangesItsFirstWord()
    {
        var after = Before.Replace("  p1 text tagged at=72,720", "  p2 text tagged at=72,720");

        var changes = SnapshotDiff.Compare(Before, after).Changes;

        Assert.Equal([ChangeKind.Removed, ChangeKind.Added], changes.Select(change => change.Kind));
    }

    [Fact]
    public void Compare_ShouldReportATextChange_WhenAOneWordValueIsReplaced()
    {
        var after = Before.Replace("\"Warfarin\"", "\"Aspirin\"");

        var change = Assert.Single(SnapshotDiff.Compare(Before, after).Changes);

        Assert.Equal((ChangeKind.TextChanged, "\"[-Warfarin-] {+Aspirin+}\""), (change.Kind, change.Detail));
    }

    [Fact]
    public void Compare_ShouldReportAMoveAndTheEdit_WhenAnElementMovesAndChanges()
    {
        var figure = "    Figure p1 alt=\"Pharmacy logo\"\n";
        var after = Before.Replace(figure, "").Replace("    P p3 \"Call your pharmacist\"",
            "    Figure p1 alt=\"Store logo\"\n    P p3 \"Call your doctor\"");

        var changes = SnapshotDiff.Compare(Before, after).Changes;

        SnapshotChange[] expected =
        [
            new(ChangeKind.Moved, Section.Structure, "structure > Document > Figure", 1, "reading position 3 -> 10"),
            new(ChangeKind.AttributeChanged, Section.Structure, "structure > Document > Figure", 1,
                "alt: \"Pharmacy logo\" -> \"Store logo\""),
            new(ChangeKind.TextChanged, Section.Structure, "structure > Document > P", 3, "\"Call your [-pharmacist-] {+doctor+}\""),
        ];
        Assert.Equal(expected, changes);
    }

    [Fact]
    public void Compare_ShouldReportAMoveInsideAMove_WhenAChildLeavesAnElementThatAlsoMoved()
    {
        var before = SnapshotText.Header + """
            structure
              H1 p1 "Title"
              L p1
                LI p1 "Apples are red"
                LI p1 "Pears are green"
              P p1 "Note text here"
            """.ReplaceLineEndings("\n");
        var after = SnapshotText.Header + """
            structure
              L p1
                LI p1 "Apples are red"
              H1 p1 "Title"
              P p1 "Note text here"
              LI p1 "Pears are green"
            """.ReplaceLineEndings("\n");

        var changes = SnapshotDiff.Compare(before, after).Changes;

        SnapshotChange[] expected =
        [
            new(ChangeKind.Moved, Section.Structure, "structure > L", 1, "reading position 2 -> 1"),
            new(ChangeKind.Moved, Section.Structure, "structure > LI", 1, "reading position 4 -> 5"),
        ];
        Assert.Equal(expected, changes);
    }

    [Fact]
    public void Compare_ShouldReportRemovedAndAdded_WhenARowGoesAndAnUnrelatedRowArrivesInAnotherTable()
    {
        var before = SnapshotText.Header + """
            structure
              Table p1
                TR
                  TD p1 "Warfarin"
                  TD p1 "Take with food"
              Table p2
                TR
                  TD p2 "Dose"
            """.ReplaceLineEndings("\n");
        var after = SnapshotText.Header + """
            structure
              Table p1
              Table p2
                TR
                  TD p2 "Dose"
                TR
                  TD p2 "Ibuprofen"
                  TD p2 "Avoid alcohol"
            """.ReplaceLineEndings("\n");

        var changes = SnapshotDiff.Compare(before, after).Changes;

        Assert.Equal([(ChangeKind.Removed, "structure > Table[1] > TR"), (ChangeKind.Added, "structure > Table[2] > TR[2]")],
            changes.Select(change => (change.Kind, change.Path)));
    }

    [Fact]
    public void Compare_ShouldMoveTheNearestCopy_WhenTheSameLineIsAddedInSeveralPlaces()
    {
        var before = SnapshotText.Header + "structure\n" + string.Concat("ABCSDE".Select(name => $"  P p1 \"{name}\"\n"));
        var after = SnapshotText.Header + "structure\n" + string.Concat("SABCDSE".Select(name => $"  P p1 \"{name}\"\n"));

        var changes = SnapshotDiff.Compare(before, after).Changes;

        SnapshotChange[] expected =
        [
            new(ChangeKind.Added, Section.Structure, "structure > P[1]", 1, "P p1 \"S\""),
            new(ChangeKind.Moved, Section.Structure, "structure > P[6]", 1, "reading position 4 -> 6"),
        ];
        Assert.Equal(expected, changes);
    }

    [Fact]
    public void Compare_ShouldNameBookmarksByTitle_WhenABookmarkChanges()
    {
        var before = SnapshotText.Header + "bookmarks\n  \"Warnings\" -> p3\n    \"Food\"\n";
        var after = before.Replace("\"Food\"", "\"Food\" -> p4");

        var change = Assert.Single(SnapshotDiff.Compare(before, after).Changes);

        Assert.Equal((ChangeKind.Changed, "bookmarks > Warnings > Food", 4), (change.Kind, change.Path, change.Page));
    }

    [Theory]
    [InlineData("P p1 \"He said \\\"stop\\\"\"", "P p1 \"He said \\\"go\\\"\"", "\"He said [-\"stop\"-] {+\"go\"+}\"")]
    [InlineData("Figure p1", "Figure p1 \"Logo\"", "\"{+Logo+}\"")]
    public void Compare_ShouldShowReadableText_WhenTextHasQuotesOrStartsEmpty(string before, string after, string detail)
    {
        var change = Assert.Single(SnapshotDiff.Compare($"{SnapshotText.Header}structure\n  {before}\n", $"{SnapshotText.Header}structure\n  {after}\n").Changes);

        Assert.Equal((ChangeKind.TextChanged, detail), (change.Kind, change.Detail));
    }

    // Two removals far apart leave nearly the whole section between them to align, which takes more than a quick pass.
    [Fact]
    public void Compare_ShouldReportJustTheRemovals_WhenLinesGoFromFarApartInALongSection()
    {
        var lines = Enumerable.Range(1, 3000)
            .Select(i => $"  p{i / 100 + 1} text tagged font=Arial size=12 color=gray:0 \"Line {i}\"").ToList();
        var before = SnapshotText.Header + "layout\n" + string.Join("\n", lines) + "\n";
        var after = SnapshotText.Header + "layout\n" + string.Join("\n", lines.Where((_, i) => i != 100 && i != 2900)) + "\n";

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

        var expected = "2 change(s): 1 attribute, 1 text\nby section: structure 2\n1 can affect accessibility, listed first\n"
            + "~ attribute structure > Document > Table > TR[1] > TH2 (p2) [accessibility]\n    scope: Column -> (missing)\n"
            + "... and 1 more";
        Assert.Equal(expected, text);
    }

    [Fact]
    public void Describe_ShouldListAccessibilityChangesFirst_WhenTheyComeLaterInTheSnapshot()
    {
        var before = SnapshotText.Header + "structure\n  P p1 \"Take with food\"\nuntagged\n  (none)\n";
        var after = SnapshotText.Header + "structure\n  P p1 \"Take after food\"\nuntagged\n  p1 \"Stray text\"\n";

        var lines = SnapshotDiff.Compare(before, after).Describe().Split('\n');

        Assert.Equal("by section: structure 1, untagged 2", lines[1]);
        Assert.Equal("+ added untagged > p1 (p1) [accessibility]", lines[3]);
        Assert.Contains("~ text structure > P (p1)", lines);
    }

    [Theory]
    [InlineData("structure", "Figure p1 alt=\"Logo\"", "Figure p1", true)]
    [InlineData("structure", "Figure p1 alt=\"Logo\"", "Figure p1 alt=\"\"", true)]
    [InlineData("structure", "Figure p1 alt=\"Logo\"", "Figure p1 alt=\"Store logo\"", false)]
    [InlineData("structure", "H2 p1 \"Dosage\"", "P p1 \"Dosage\"", true)]
    [InlineData("structure", "TH1 p1 scope=Column \"Drug\"", "TH1 p1 \"Drug\"", true)]
    [InlineData("structure", "TD p1 headers=[TH1] \"Warfarin\"", "TD p1 \"Warfarin\"", true)]
    [InlineData("structure", "P p1 lang=es-ES \"Hola\"", "P p1 lang=fr-FR \"Hola\"", true)]
    [InlineData("structure", "P p1 \"Take with food\"", "P p1 \"Take after food\"", false)]
    [InlineData("structure", "TH1 p1 scope=Column \"Drug\"", "TH2 p1 scope=Column \"Drug\"", false)]
    [InlineData("structure", "TD p1 headers=[TH1] \"Warfarin\"", "TD p1 headers=[?h-other] \"Warfarin\"", true)]
    [InlineData("document", "marked=yes lang=en-US", "marked=yes lang=en-GB", true)]
    [InlineData("untagged", "p1 images=1", "p1 images=2", true)]
    [InlineData("untagged", "p1 \"Page 1\"", "p1 \"Page 1 Draft\"", true)]
    [InlineData("annotations", "p1 Link tagged at=0,0 size=10x10", "p1 Link untagged at=0,0 size=10x10", true)]
    [InlineData("annotations", "p1 Link tagged", "p1 Link untagged", true)]
    [InlineData("annotations", "p1 Link untagged at=0,0 size=10x10", "p1 Link tagged at=0,0 size=10x10", false)]
    [InlineData("forms", "text \"patient\" p1 tu=\"Patient name\"", "text \"patient\" p1", true)]
    [InlineData("layout", "p1 text tagged at=72,700 color=gray:0 \"Dosage\"", "p1 text tagged at=72,700 color=#ff0000 \"Dosage\"", false)]
    public void AffectsAccessibility_ShouldFollowTheFixedRules_WhenGivenAChange(string section, string before, string after,
        bool affects)
    {
        var changes = SnapshotDiff.Compare($"{SnapshotText.Header}{section}\n  {before}\n", $"{SnapshotText.Header}{section}\n  {after}\n").Changes;

        Assert.NotEmpty(changes);
        Assert.Equal(affects, changes.Any(change => change.AffectsAccessibility));
    }

    [Fact]
    public void AffectsAccessibility_ShouldBeFalse_WhenTheChangeIsInADocxSnapshot()
    {
        var before = "# docx-snapshot v1\nsections\n  s1 letter headers=default\nbody\n  P lang=en-US \"Take\"\n";
        var after = "# docx-snapshot v1\nsections\n  s1 letter\nbody\n  P lang=fr-FR \"Take\"\n";

        var changes = SnapshotDiff.Compare(before, after).Changes;

        Assert.Equal(2, changes.Count);
        Assert.DoesNotContain(changes, change => change.AffectsAccessibility);
    }

    [Fact]
    public void Compare_ShouldFlagAMoveInTheStructure_WhenAnElementChangesPlaceInReadingOrder()
    {
        var figure = "    Figure p1 alt=\"Pharmacy logo\"\n";
        var after = Before.Replace(figure, "").Replace("    P p3", figure + "    P p3");

        Assert.True(Assert.Single(SnapshotDiff.Compare(Before, after).Changes).AffectsAccessibility);
    }

    [Fact]
    public void Compare_ShouldReportEachChangedLineByNumber_WhenTheTextIsNotASnapshot()
    {
        var changes = SnapshotDiff.Compare("{\n  \"a\": 1,\n  \"b\": 2,\n  \"c\": 3\n}\n", "{\n  \"a\": 1,\n  \"c\": 4\n}\n").Changes;

        SnapshotChange[] expected =
        [
            new(ChangeKind.TextChanged, null, "line 3", null, "[-\"b\": 2,-] {+\"c\": 4+}"),
            new(ChangeKind.Removed, null, "line 4", null, "  \"c\": 3"),
        ];
        Assert.Equal(expected, changes);
    }

    [Fact]
    public void Compare_ShouldShowBothLinesInQuotes_WhenAPlainLineChangesOnlyInSpaces()
    {
        var change = Assert.Single(SnapshotDiff.Compare("a\n", " a\n").Changes);

        Assert.Equal((ChangeKind.TextChanged, "- \"a\"\n+ \" a\""), (change.Kind, change.Detail));
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
