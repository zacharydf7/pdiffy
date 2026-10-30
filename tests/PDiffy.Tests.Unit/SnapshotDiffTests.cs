using System.Text.RegularExpressions;
using PDiffy.Diffing;
using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

public class SnapshotDiffTests
{
    // Normalized because Git can check this file out with CRLF line endings.
    private static readonly string Before = """
        # pdfua-snapshot v1
        structure
          Document lang=en-US
            H1 p1 "Trail Table"
            Figure p1 alt="Park logo"
            Table p2
              TR
                TH1 p2 scope=Column "Trail"
                TH2 p2 scope=Column "Length"
              TR
                TD p2 headers=[TH1] "Ridgeline"
                TD p2 headers=[TH2] "Go with a guide"
            P p3 "Call your ranger"
        layout
          p1 text tagged at=72,720 font=Arial size=18 color=gray:0 "Trail Table"
        """.ReplaceLineEndings("\n");

    [Fact]
    public void Compare_ShouldFindNoChanges_WhenSnapshotsAreEqual()
    {
        var diff = SnapshotDiff.CompareSnapshots(Before, Before.ReplaceLineEndings("\r\n"));

        Assert.True(diff.IsEmpty);
        Assert.Equal("No changes.", diff.Describe());
    }

    [Fact]
    public void Compare_ShouldShowTheChangedWords_WhenAnElementsTextChanges()
    {
        var after = Before.Replace("Go with a guide", "Go without a guide");

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(Before, after).Changes);

        var path = "structure > Document > Table > TR[2] > TD[2]";
        Assert.Equal(new SnapshotChange
        {
            Kind = ChangeKind.TextChanged, SectionName = "structure", Format = SnapshotFormat.Pdf, Path = path,
            Page = 2,
            Detail = "\"Go [-with-] {+without+} a guide\"",
        }, change);
    }

    [Fact]
    public void Compare_ShouldReportEachAttribute_WhenAttributesChange()
    {
        var after = Before.Replace("TH2 p2 scope=Column", "TH2 p2").Replace("color=gray:0", "color=#ff0000");

        var changes = SnapshotDiff.CompareSnapshots(Before, after).Changes;

        SnapshotChange[] expected =
        [
            new()
            {
                Kind = ChangeKind.AttributeChanged, SectionName = "structure", Format = SnapshotFormat.Pdf,
                Path = "structure > Document > Table > TR[1] > TH2", Page = 2, Detail = "scope: Column -> (missing)",
            },
            new()
            {
                Kind = ChangeKind.AttributeChanged, SectionName = "layout", Format = SnapshotFormat.Pdf,
                Path = "layout > p1", Page = 1,
                Detail = "color: gray:0 -> #ff0000",
            },
        ];
        Assert.Equal(expected, changes);
    }

    [Fact]
    public void Compare_ShouldReportTheWholeLine_WhenAnElementMovesToAnotherPage()
    {
        var after = Before.Replace("TD p2 headers=[TH1]", "TD p3 headers=[TH1]");

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(Before, after).Changes);

        var detail = "TD p2 headers=[TH1] \"Ridgeline\" -> TD p3 headers=[TH1] \"Ridgeline\"";
        Assert.Equal((ChangeKind.Changed, detail), (change.Kind, change.Detail));
    }

    [Fact]
    public void Compare_ShouldCompareEachOccurrence_WhenAKeyRepeats()
    {
        var before = SnapshotText.Header + "structure\n  Link p1 annot=Link uri=\"https://a\" annot=Link uri=\"https://b\" \"here\"\n";
        var after = before.Replace("https://b", "https://c");

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(before, after).Changes);

        Assert.Equal((ChangeKind.AttributeChanged, "uri: \"https://b\" -> \"https://c\""), (change.Kind, change.Detail));
    }

    [Fact]
    public void Compare_ShouldStillReportALine_WhenOnlyTheOrderOfItsKeysChanges()
    {
        var before = SnapshotText.Header + "layout\n  p1 path tagged fill=gray:0 stroke=gray:1\n";
        var after = SnapshotText.Header + "layout\n  p1 path tagged stroke=gray:1 fill=gray:0\n";

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(before, after).Changes);

        Assert.Equal(ChangeKind.Changed, change.Kind);
    }

    [Fact]
    public void Compare_ShouldReportTheFirstLines_WhenOnlyTheOptionsChange()
    {
        var after = Before.Replace(SnapshotText.Header, SnapshotText.Header + "# ignored fields: positions\n");

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(Before, after).Changes);

        Assert.Equal(new SnapshotChange
        {
            Kind = ChangeKind.Changed, SectionName = "header", Path = "header", Page = null,
            Format = SnapshotFormat.Pdf,
            Detail = "+ # ignored fields: positions",
        }, change);
    }

    [Fact]
    public void Compare_ShouldReportWhitespace_WhenOnlyBlankLinesDiffer()
    {
        var change = Assert.Single(SnapshotDiff.CompareSnapshots(Before, Before + "\n").Changes);

        Assert.Equal(("whitespace", null), (change.Path, change.PdfSection));
    }

    [Fact]
    public void Compare_ShouldReportAMove_WhenAnElementChangesPlaceInReadingOrder()
    {
        var figure = "    Figure p1 alt=\"Park logo\"\n";
        var after = Before.Replace(figure, "").Replace("    P p3", figure + "    P p3");

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(Before, after).Changes);

        var expected = new SnapshotChange
        {
            Kind = ChangeKind.Moved, SectionName = "structure", Format = SnapshotFormat.Pdf,
            Path = "structure > Document > Figure", Page = 1,
            Detail = "reading position 3 -> 10",
        };
        Assert.Equal(expected, change);
    }

    [Fact]
    public void Compare_ShouldReportRemovedAndAdded_WhenAReplacementSharesTooFewWords()
    {
        var after = Before.Replace("P p3 \"Call your ranger\"", "P p3 \"Ask questions\"");

        var changes = SnapshotDiff.CompareSnapshots(Before, after).Changes;

        SnapshotChange[] expected =
        [
            new()
            {
                Kind = ChangeKind.Removed, SectionName = "structure", Format = SnapshotFormat.Pdf,
                Path = "structure > Document > P", Page = 3,
                Detail = "P p3 \"Call your ranger\"",
            },
            new()
            {
                Kind = ChangeKind.Added, SectionName = "structure", Format = SnapshotFormat.Pdf,
                Path = "structure > Document > P", Page = 3,
                Detail = "P p3 \"Ask questions\"",
            },
        ];
        Assert.Equal(expected, changes);
    }

    [Theory]
    [InlineData("structure", "P p1 \"Walk daily\"", "P p1 \"Avoid shortcuts\"")]
    [InlineData("layout", "p1 text tagged at=72,700 font=Arial size=12 color=gray:0 \"Ridgeline\"",
        "p1 text tagged at=400,300 font=Arial size=12 color=gray:0 \"Signature line\"")]
    [InlineData("layout", "p1 text tagged at=72,700 font=Arial size=12 color=gray:0 \"Ridgeline\"",
        "p1 text tagged at=400,300 font=Arial size=12 color=gray:0 \"Summit\"")]
    public void Compare_ShouldReportRemovedAndAdded_WhenTheTextSharesNoWords(string section, string before, string after)
    {
        var changes = SnapshotDiff.CompareSnapshots($"{SnapshotText.Header}{section}\n  {before}\n",
            $"{SnapshotText.Header}{section}\n  {after}\n").Changes;

        Assert.Equal([ChangeKind.Removed, ChangeKind.Added], changes.Select(change => change.Kind));
    }

    [Fact]
    public void Compare_ShouldReportOneTypeChange_WhenAStructureElementsTypeChangesButItsTextDoesNot()
    {
        var after = Before.Replace("H1 p1 \"Trail Table\"", "H2 p1 \"Trail Table\"");

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(Before, after).Changes);

        Assert.Equal(new SnapshotChange
        {
            Kind = ChangeKind.Changed, SectionName = "structure", Format = SnapshotFormat.Pdf,
            Path = "structure > Document > H2", Page = 1,
            Detail = "type: H1 -> H2",
        }, change);
    }

    [Fact]
    public void Compare_ShouldPairALineWithOneOfItsOwnType_WhenAnotherTypeHasTheSameTextFirst()
    {
        var before = SnapshotText.Header + "structure\n  P p1 lang=en \"Walk\"\n";
        var after = SnapshotText.Header + "structure\n  H3 p1 \"Walk\"\n  P p1 lang=fr \"Walk\"\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.Equal([(ChangeKind.Added, "structure > H3"), (ChangeKind.AttributeChanged, "structure > P")],
            changes.Select(change => (change.Kind, change.Path)));
    }

    [Fact]
    public void Compare_ShouldReportRemovedAndAdded_WhenALineOutsideTheStructureChangesItsFirstWord()
    {
        var after = Before.Replace("  p1 text tagged at=72,720", "  p2 text tagged at=72,720");

        var changes = SnapshotDiff.CompareSnapshots(Before, after).Changes;

        Assert.Equal([ChangeKind.Removed, ChangeKind.Added], changes.Select(change => change.Kind));
    }

    [Fact]
    public void Compare_ShouldReportATextChange_WhenAOneWordValueIsReplaced()
    {
        var after = Before.Replace("\"Ridgeline\"", "\"Cedars\"");

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(Before, after).Changes);

        Assert.Equal((ChangeKind.TextChanged, "\"[-Ridgeline-] {+Cedars+}\""), (change.Kind, change.Detail));
    }

    [Fact]
    public void Compare_ShouldReportAMoveAndTheEdit_WhenAnElementMovesAndChanges()
    {
        var figure = "    Figure p1 alt=\"Park logo\"\n";
        var after = Before.Replace(figure, "").Replace("    P p3 \"Call your ranger\"",
            "    Figure p1 alt=\"Store logo\"\n    P p3 \"Call your warden\"");

        var changes = SnapshotDiff.CompareSnapshots(Before, after).Changes;

        SnapshotChange[] expected =
        [
            new()
            {
                Kind = ChangeKind.Moved, SectionName = "structure", Format = SnapshotFormat.Pdf,
                Path = "structure > Document > Figure",
                Page = 1, Detail = "reading position 3 -> 10",
            },
            new()
            {
                Kind = ChangeKind.AttributeChanged, SectionName = "structure", Format = SnapshotFormat.Pdf,
                Path = "structure > Document > Figure", Page = 1, Detail = "alt: \"Park logo\" -> \"Store logo\"",
            },
            new()
            {
                Kind = ChangeKind.TextChanged, SectionName = "structure", Format = SnapshotFormat.Pdf,
                Path = "structure > Document > P",
                Page = 3, Detail = "\"Call your [-ranger-] {+warden+}\"",
            },
        ];
        Assert.Equal(expected, changes);
    }

    [Fact]
    public void Compare_ShouldSayTheParentChanged_WhenAnElementMovesWithoutChangingReadingPosition()
    {
        var before = SnapshotText.Header + """
            structure
              Document
                P p1 "See the park map for parking."
                  Link p1 "park map"
            """.ReplaceLineEndings("\n");
        var after = SnapshotText.Header + """
            structure
              Document
                P p1 "See the park map for parking."
                Link p1 "park map"
            """.ReplaceLineEndings("\n");

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(before, after).Changes);

        Assert.Equal((ChangeKind.Moved, "structure > Document > Link", "parent: Document > P -> Document"),
            (change.Kind, change.Path, change.Detail));
    }

    [Fact]
    public void Compare_ShouldReportOnlyTheEdit_WhenAnElementKeepsItsParentAndPlaceButGetsANumberedLabel()
    {
        var before = SnapshotText.Header + """
            structure
              Document
                P p1 "hello world"
                  Span p1 "kids words"
            """.ReplaceLineEndings("\n");
        var after = SnapshotText.Header + """
            structure
              Document
                P p1 "totally different"
                  Span p1 "kids words"
                P p1 "other"
            """.ReplaceLineEndings("\n");

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.DoesNotContain(changes, change => change.Kind == ChangeKind.Moved);
        Assert.Contains(changes, change => change is { Kind: ChangeKind.TextChanged, Path: "structure > Document > P[1]" });
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

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        SnapshotChange[] expected =
        [
            new()
            {
                Kind = ChangeKind.Moved, SectionName = "structure", Format = SnapshotFormat.Pdf, Path = "structure > L",
                Page = 1,
                Detail = "reading position 2 -> 1",
            },
            new()
            {
                Kind = ChangeKind.Moved, SectionName = "structure", Format = SnapshotFormat.Pdf,
                Path = "structure > LI", Page = 1,
                Detail = "reading position 4 -> 5\nparent: L -> (top level)",
            },
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
                  TD p1 "Ridgeline"
                  TD p1 "Go with a guide"
              Table p2
                TR
                  TD p2 "Stage"
            """.ReplaceLineEndings("\n");
        var after = SnapshotText.Header + """
            structure
              Table p1
              Table p2
                TR
                  TD p2 "Stage"
                TR
                  TD p2 "Meadows"
                  TD p2 "Avoid shortcuts"
            """.ReplaceLineEndings("\n");

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.Equal([(ChangeKind.Removed, "structure > Table[1] > TR"), (ChangeKind.Added, "structure > Table[2] > TR[2]")],
            changes.Select(change => (change.Kind, change.Path)));
    }

    [Fact]
    public void Compare_ShouldMoveTheNearestCopy_WhenTheSameLineIsAddedInSeveralPlaces()
    {
        var before = SnapshotText.Header + "structure\n" + string.Concat("ABCSDE".Select(name => $"  P p1 \"{name}\"\n"));
        var after = SnapshotText.Header + "structure\n" + string.Concat("SABCDSE".Select(name => $"  P p1 \"{name}\"\n"));

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        SnapshotChange[] expected =
        [
            new()
            {
                Kind = ChangeKind.Added, SectionName = "structure", Format = SnapshotFormat.Pdf,
                Path = "structure > P[1]", Page = 1,
                Detail = "P p1 \"S\"",
            },
            new()
            {
                Kind = ChangeKind.Moved, SectionName = "structure", Format = SnapshotFormat.Pdf,
                Path = "structure > P[6]", Page = 1,
                Detail = "reading position 4 -> 6",
            },
        ];
        Assert.Equal(expected, changes);
    }

    [Fact]
    public void Compare_ShouldNameBookmarksByTitle_WhenABookmarkChanges()
    {
        var before = SnapshotText.Header + "bookmarks\n  \"Safety\" -> p3\n    \"Gear\"\n";
        var after = before.Replace("\"Gear\"", "\"Gear\" -> p4");

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(before, after).Changes);

        Assert.Equal((ChangeKind.Changed, "bookmarks > Safety > Gear", 4), (change.Kind, change.Path, change.Page));
    }

    [Theory]
    [InlineData("P p1 \"He said \\\"stop\\\"\"", "P p1 \"He said \\\"go\\\"\"", "\"He said [-\"stop\"-] {+\"go\"+}\"")]
    [InlineData("Figure p1", "Figure p1 \"Logo\"", "\"{+Logo+}\"")]
    public void Compare_ShouldShowReadableText_WhenTextHasQuotesOrStartsEmpty(string before, string after, string detail)
    {
        var change = Assert.Single(SnapshotDiff.CompareSnapshots($"{SnapshotText.Header}structure\n  {before}\n",
            $"{SnapshotText.Header}structure\n  {after}\n").Changes);

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

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        string[] expected = [lines[100].TrimStart(), lines[2900].TrimStart()];
        Assert.Equal([ChangeKind.Removed, ChangeKind.Removed], changes.Select(change => change.Kind));
        Assert.Equal(expected, changes.Select(change => change.Detail));
    }

    [Fact]
    public void Describe_ShouldCountChangesByKindAndListThem_WhenThereAreChanges()
    {
        var after = Before.Replace("Go with a guide", "Go without a guide").Replace("TH2 p2 scope=Column", "TH2 p2");

        var text = SnapshotDiff.CompareSnapshots(Before, after).Describe(maxChanges: 1);

        var expected = "2 change(s): 1 attribute, 1 text\nby section: structure 2\n1 can affect accessibility, listed first\n"
            + "~ attribute structure > Document > Table > TR[1] > TH2 (p2) [accessibility]\n    scope: Column -> (missing)\n"
            + "... and 1 more";
        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData("(none)", "p1 \"Draft\"", ChangeKind.Added)]
    [InlineData("p1 \"Draft\"", "(none)", ChangeKind.Removed)]
    [InlineData("(ignored)", "p1 \"Draft\"", ChangeKind.Added)]
    public void Compare_ShouldReportOnlyTheContent_WhenItTakesThePlaceOfASectionsPlaceholder(string before, string after,
        ChangeKind kind)
    {
        var diff = SnapshotDiff.CompareSnapshots(SnapshotText.Header + $"untagged\n  {before}\n",
            SnapshotText.Header + $"untagged\n  {after}\n");

        var change = Assert.Single(diff.Changes);
        Assert.Equal((kind, "untagged > p1"), (change.Kind, change.Path));
    }

    [Theory]
    [InlineData("layout\n  p1 text tagged \"Hi\"\nfonts\n  (none)\n", "fonts\n  (none)\nlayout\n  p1 text tagged \"Hi\"\n")]
    [InlineData("layout\n  p1 text tagged \"Hi\"\n", "layout\n   p1 text tagged \"Hi\"\n")]
    public void Compare_ShouldStillFindADifference_WhenOnlySectionOrderOrAnIndentDiffers(string before, string after)
    {
        var header = "# pdfua-snapshot v1\n# tolerances: positions 1pt\n";

        var diff = SnapshotDiff.CompareSnapshots(header + before, header + after);

        Assert.False(diff.IsEmpty);
    }

    [Fact]
    public void Compare_ShouldCompareTheChildren_WhenAnElementWithNoTypePrintsAsNone()
    {
        var before = SnapshotText.Header + "structure\n  Document\n    (none)\n      Figure p1 alt=\"Logo\"\n";
        var after = SnapshotText.Header + "structure\n  Document\n    (none)\n      Figure p1\n";

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(before, after).Changes);

        Assert.Equal((ChangeKind.AttributeChanged, "structure > Document > (none) > Figure"), (change.Kind, change.Path));
    }

    [Theory]
    [InlineData("positions 1pt", "p1 text tagged at=72,700 size=12 \"Hi\"", "p1 text tagged at=73,699 size=12 \"Hi\"", 0)]
    [InlineData("positions 1pt", "p1 text tagged at=72,700 size=12 \"Hi\"", "p1 text tagged at=74,700 size=12 \"Hi\"", 1)]
    [InlineData("positions 1pt", "p1 text tagged at=72,700 size=12 \"Hi\"", "p1 text tagged at=72,700 size=12.5 \"Hi\"", 1)]
    [InlineData("font-sizes 0.3pt", "p1 text tagged at=72,700 size=12 \"Hi\"", "p1 text tagged at=72,700 size=12.3 \"Hi\"", 0)]
    [InlineData("positions 1pt", "p1 image tagged at=72,600 size=120x40", "p1 image tagged at=72,601 size=121x40", 0)]
    [InlineData("line-widths 0.2pt", "p1 path artifact width=0.5", "p1 path artifact width=0.7", 0)]
    [InlineData("positions 1pt", "embedded-file \"a.txt\" size=100 hash=x",
        "embedded-file \"a.txt\" size=101 hash=x", 1)]
    [InlineData("positions 1pt", "p1 text tagged at=72,700 \"Go with a guide\"",
        "p1 text tagged at=73,700 \"Go without a guide\"", 1)]
    [InlineData("positions 1pt", "p1 Link untagged dest=p2 view=XYZ view-at=72,720", "p1 Link untagged dest=p2 view=XYZ view-at=72,721", 0)]
    [InlineData("positions 1pt", "p1 Link untagged dest=p2 view=XYZ view-at=72,720", "p1 Link untagged dest=p2 view=XYZ view-at=72,740", 1)]
    [InlineData("positions 1pt", "p1 Link untagged dest=p2 view=XYZ view-at=72,-", "p1 Link untagged dest=p2 view=XYZ view-at=73,-", 0)]
    [InlineData("positions 1pt", "p1 Link untagged dest=p2 view=XYZ view-at=72,-", "p1 Link untagged dest=p2 view=XYZ view-at=72,720", 1)]
    [InlineData("positions 1pt", "\"Atlas\" file=\"atlas.pdf\" dest=p1 view=XYZ view-at=72,720",
        "\"Atlas\" file=\"atlas.pdf\" dest=p1 view=XYZ view-at=72,721", 0)]
    public void Compare_ShouldCountOnlyNumbersBeyondTheTolerance_WhenTheHeaderGivesOne(string tolerance, string before,
        string after, int changes)
    {
        var header = $"# pdfua-snapshot v1\n# tolerances: {tolerance}\nlayout\n  ";

        var diff = SnapshotDiff.CompareSnapshots(header + before + "\n", header + after + "\n");

        Assert.Equal(changes, diff.Changes.Count);
    }

    [Fact]
    public void Compare_ShouldShowOnlyTheNewKeys_WhenASnapshotFromBeforeLinkTargetsMeetsOneWithThem()
    {
        var before = SnapshotText.Header + "bookmarks\n  \"Safety\" -> p2\n"
            + "annotations\n  p1 Link untagged at=72,200 size=100x20 dest=yes contents=\"Safety\"\n";
        var after = SnapshotText.Header + "bookmarks\n  \"Safety\" -> p2 view=Fit\n"
            + "annotations\n  p1 Link untagged at=72,200 size=100x20 dest=p2 view=XYZ view-at=72,720 contents=\"Safety\"\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.All(changes, change => Assert.Equal(ChangeKind.AttributeChanged, change.Kind));
        Assert.Equal(["view: (missing) -> Fit", "dest: yes -> p2", "view: (missing) -> XYZ", "view-at: (missing) -> 72,720"],
            changes.Select(change => change.Detail));
    }

    [Fact]
    public void Compare_ShouldShowOnlyTheNewSpanAndKey_WhenADocxSnapshotFromBeforeHiddenTextMeetsOneWithIt()
    {
        var before = "# docx-snapshot v1\nstyles\n  French paragraph\nbody\n  P \"Walk with care\"\n";
        var after = "# docx-snapshot v1\nstyles\n  French paragraph lang=fr-FR\nbody\n  P \"Walk with care\"\n"
            + "    span \"care\" hidden\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.Equal(["AttributeChanged lang: (missing) -> fr-FR", "Added span \"care\" hidden"],
            changes.Select(change => $"{change.Kind} {change.Detail}"));
    }

    [Fact]
    public void Compare_ShouldShowOnlyTheNewKeys_WhenADocxSnapshotFromBeforeParagraphBordersMeetsOneWithThem()
    {
        var before = "# docx-snapshot v1\nbody\n  P style=Note \"Walk with care\"\n";
        var after = "# docx-snapshot v1\nbody\n  P style=Note rtl-paragraph borders=top:single:0.5 shading=FFFF00 \"Walk with care\"\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.All(changes, change => Assert.Equal("body > P", change.Path));
        Assert.Equal(["AttributeChanged borders: (missing) -> top:single:0.5", "AttributeChanged shading: (missing) -> FFFF00",
            "Changed P style=Note \"Walk with care\" -> P style=Note rtl-paragraph borders=top:single:0.5 shading=FFFF00 \"Walk with care\""],
            changes.Select(change => $"{change.Kind} {change.Detail}"));
    }

    [Fact]
    public void Compare_ShouldShowOnlyTheNewKeys_WhenADocxSnapshotFromBeforeTableAltTextMeetsOneWithIt()
    {
        var before = "# docx-snapshot v1\nbody\n  Table widths=144\n    Row\n      Cell\n        P \"Trail\"\n"
            + "    Row\n      Cell\n        P \"Length\"\n";
        var after = "# docx-snapshot v1\nbody\n  Table widths=144 caption=\"Trails\"\n    Row height=20 split=no\n      Cell valign=center\n"
            + "        P \"Trail\"\n    Row height=20\n      Cell\n        P \"Length\"\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.Equal(["caption: (missing) -> \"Trails\"", "height: (missing) -> 20", "split: (missing) -> no",
            "valign: (missing) -> center", "height: (missing) -> 20"], changes.Select(change => change.Detail));
        Assert.All(changes, change => Assert.Equal(ChangeKind.AttributeChanged, change.Kind));
    }

    [Fact]
    public void Compare_ShouldPairATableWithItsEdit_WhenItGainsAPageAndMostOfItsTextChanges()
    {
        var before = SnapshotText.Header + "structure\n  Table p2-3 summary=\"Trail details\"\n    TR\n      TD p2 \"Ridgeline loop\"\n"
            + "    TR\n      TD p3 \"Cedars\"\n";
        var after = SnapshotText.Header + "structure\n  Table p2-4 summary=\"Trail details\"\n    TR\n      TD p2 \"Lakeshore walk\"\n"
            + "    TR\n      TD p4 \"Meadows path\"\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.Contains(changes, change => change.Kind == ChangeKind.Changed && change.Path == "structure > Table");
        Assert.DoesNotContain(changes, change => change.Kind is ChangeKind.Removed or ChangeKind.Added && change.Path == "structure > Table");
    }

    [Fact]
    public void Compare_ShouldPairTheRewrittenTableWithItsOldSelf_WhenADeletedTableComesFirst()
    {
        var before = SnapshotText.Header + "structure\n  Table p2-3\n    TR\n      TD p2 \"Ridgeline loop\"\n"
            + "  Table p5-6\n    TR\n      TD p5 \"Lakeshore walk north shore trail\"\n";
        var after = SnapshotText.Header + "structure\n  Table p5-7\n    TR\n      TD p5 \"Lakeshore path south bay ford\"\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.Contains(changes, change => change.Kind == ChangeKind.Removed && change.Detail.StartsWith("Table p2-3"));
        Assert.Contains(changes, change => change.Kind == ChangeKind.Changed && change.Detail == "Table p5-6 -> Table p5-7");
    }

    [Fact]
    public void Compare_ShouldShowOnlyTheNewKey_WhenASnapshotFromBeforeRotatedTextMeetsOneWithIt()
    {
        var before = SnapshotText.Header + "layout\n  p1 text tagged at=300,400 font=Helvetica size=12 \"Hello world\"\n";
        var after = SnapshotText.Header + "layout\n  p1 text tagged at=300,400 font=Helvetica size=12 angle=90 \"Hello world\"\n";

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(before, after).Changes);

        Assert.Equal((ChangeKind.AttributeChanged, "angle: (missing) -> 90"), (change.Kind, change.Detail));
    }

    [Fact]
    public void Compare_ShouldShowOnlyTheNewKeys_WhenASnapshotFromBeforeMarkedSpansMeetsOneWithThem()
    {
        var before = SnapshotText.Header + "structure\n  P p1 \"Say bonjour\"\nartifacts\n  p1 \"Page 1\"\n";
        var after = SnapshotText.Header + "structure\n  P p1 span-lang=[fr-FR:\"bonjour\"] \"Say bonjour\"\n"
            + "artifacts\n  p1 type=Pagination subtype=Footer \"Page 1\"\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.All(changes, change => Assert.Equal(ChangeKind.AttributeChanged, change.Kind));
        Assert.Equal(["span-lang: (missing) -> [fr-FR:\"bonjour\"]", "type: (missing) -> Pagination",
            "subtype: (missing) -> Footer"], changes.Select(change => change.Detail));
        Assert.Equal([true, false, false], changes.Select(change => change.AffectsAccessibility));
    }

    [Fact]
    public void Compare_ShouldShowTheFormLineAndJavaScript_WhenASnapshotFromBeforeFormSettingsMeetsOneWithThem()
    {
        var before = SnapshotText.Header + "settings\n  javascript=no additional-actions=no\nforms\n  text \"total\" p1\n";
        var after = SnapshotText.Header + "settings\n  javascript=yes additional-actions=no\nforms\n  form need-appearances=yes\n"
            + "  text \"total\" p1 scripts=calculate:3f9c0a1b2d4e5f60\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.Equal(["AttributeChanged javascript: no -> yes", "Added form need-appearances=yes",
            "AttributeChanged scripts: (missing) -> calculate:3f9c0a1b2d4e5f60"], changes.Select(change => $"{change.Kind} {change.Detail}"));
    }

    [Fact]
    public void Compare_ShouldShowOnlyTheNewKeys_WhenASnapshotFromBeforeFieldBehaviorMeetsOneWithIt()
    {
        var before = SnapshotText.Header + "forms\n  text \"total\" p1 flags=read-only\n";
        var after = SnapshotText.Header + "forms\n  text \"total\" p1 flags=read-only default=\"0\" align=right "
            + "text-font=Helvetica text-size=auto text-color=gray:0 scripts=calculate:3f9c0a1b2d4e5f60\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.All(changes, change => Assert.Equal(ChangeKind.AttributeChanged, change.Kind));
        Assert.Equal(["default: (missing) -> \"0\"", "align: (missing) -> right", "text-font: (missing) -> Helvetica",
            "text-size: (missing) -> auto", "text-color: (missing) -> gray:0", "scripts: (missing) -> calculate:3f9c0a1b2d4e5f60"],
            changes.Select(change => change.Detail));
    }

    [Fact]
    public void Compare_ShouldShowOnlyTheNewKeys_WhenASnapshotFromBeforeSignaturesMeetsOneWithThem()
    {
        var before = SnapshotText.Header + "forms\n  signature \"approval\" p1\n";
        var after = SnapshotText.Header + "forms\n  signature \"approval\" p1 signed=yes subfilter=adbe.pkcs7.detached "
            + "covers-file=yes certification=form-filling\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.All(changes, change => Assert.Equal(ChangeKind.AttributeChanged, change.Kind));
        Assert.Equal(["signed: (missing) -> yes", "subfilter: (missing) -> adbe.pkcs7.detached", "covers-file: (missing) -> yes",
            "certification: (missing) -> form-filling"], changes.Select(change => change.Detail));
    }

    [Fact]
    public void Compare_ShouldShowOnlyTheNewKey_WhenASnapshotFromBeforeLayersMeetsOneWithThem()
    {
        var before = SnapshotText.Header + "layout\n  p1 path untagged at=72,500 size=100x20 fill=gray:0\n";
        var after = SnapshotText.Header
            + "layout\n  p1 path untagged at=72,500 size=100x20 fill=gray:0 layers=[\"Draft\"]\n";

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(before, after).Changes);

        Assert.Equal((ChangeKind.AttributeChanged, "layers: (missing) -> [\"Draft\"]"), (change.Kind, change.Detail));
    }

    [Fact]
    public void Compare_ShouldShowTheRunsALayerSplits_WhenASnapshotFromBeforeLayersMeetsOneWithThem()
    {
        var before = SnapshotText.Header + "layout\n"
            + "  p1 text untagged at=72,400 font=Helvetica size=12 color=gray:0 \"Trail open until May\"\n";
        var after = SnapshotText.Header + "layout\n"
            + "  p1 text untagged at=72,400 font=Helvetica size=12 color=gray:0 \"Trail open\"\n"
            + "  p1 text untagged at=140,400 font=Helvetica size=12 color=gray:0 layers=[\"Draft\"] \"until May\"\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.Equal([ChangeKind.TextChanged, ChangeKind.Added], changes.Select(change => change.Kind));
        Assert.Equal("\"Trail open [-until May-]\"", changes[0].Detail);
    }

    [Fact]
    public void Compare_ShouldShowTheLinesThePageSplitInto_WhenASnapshotFromBeforeUntaggedLinesMeetsOneWithThem()
    {
        var before = SnapshotText.Header + "untagged\n  p1 \"Trail closed Bring water Ranger on duty\"\n";
        var after = SnapshotText.Header + "untagged\n  p1 \"Trail closed\"\n  p1 \"Bring water\"\n  p1 \"Ranger on duty\"\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.Equal([ChangeKind.Removed, ChangeKind.Added, ChangeKind.Added, ChangeKind.Added],
            changes.Select(change => change.Kind));
        Assert.Equal([false, true, true, true], changes.Select(change => change.AffectsAccessibility));
    }

    [Fact]
    public void Compare_ShouldShowOnlyJavaScript_WhenASnapshotFromBeforeLinkScriptsMeetsOneWithThem()
    {
        var before = SnapshotText.Header + "settings\n  version=1.7 javascript=no additional-actions=no\n";
        var after = SnapshotText.Header + "settings\n  version=1.7 javascript=yes additional-actions=no\n";

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(before, after).Changes);

        Assert.Equal((ChangeKind.AttributeChanged, "javascript: no -> yes"), (change.Kind, change.Detail));
    }

    [Fact]
    public void Compare_ShouldShowOnlyTheOpenAction_WhenASnapshotFromBeforeNamedOpenActionsMeetsOneWithThem()
    {
        var before = SnapshotText.Header + "settings\n  version=1.7 open-action=\"GoTo named intro\" javascript=no\n";
        var after = SnapshotText.Header + "settings\n  version=1.7 open-action=\"GoTo p2 Fit\" javascript=no\n";

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(before, after).Changes);

        Assert.Equal((ChangeKind.AttributeChanged, "open-action: \"GoTo named intro\" -> \"GoTo p2 Fit\""),
            (change.Kind, change.Detail));
    }

    [Fact]
    public void Compare_ShouldShowOnlyTheReorderedText_WhenASnapshotFromBeforeNestedArtifactTextMeetsOneWithIt()
    {
        // The old reader put the nested text last and fused the two words where it jumped back.
        var before = SnapshotText.Header + "artifacts\n  p1 \"page 1Trail Guide\"\n";
        var after = SnapshotText.Header + "artifacts\n  p1 \"Trail Guide page 1\"\n";

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(before, after).Changes);

        Assert.Equal((ChangeKind.TextChanged, PdfSection.Artifacts), (change.Kind, change.PdfSection));
    }

    [Fact]
    public void Compare_ShouldShowOnlyTheNewKey_WhenASnapshotFromBeforeFigureImagesMeetsOneWithThem()
    {
        var before = SnapshotText.Header + "structure\n  Figure p1 alt=\"Logo\"\n";
        var after = SnapshotText.Header + "structure\n  Figure p1 alt=\"Logo\" images=[76be8b528d0075f7]\n";

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(before, after).Changes);

        Assert.Equal((ChangeKind.AttributeChanged, "images: (missing) -> [76be8b528d0075f7]"), (change.Kind, change.Detail));
    }

    [Fact]
    public void Compare_ShouldShowOnlyTheNewKey_WhenASnapshotFromBeforeFormReferencesMeetsOneWithThem()
    {
        var before = SnapshotText.Header + "structure\n  P p1 \"Stamp\"\n";
        var after = SnapshotText.Header + "structure\n  P p1 mcids-in-xobject=1 \"Stamp\"\n";

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(before, after).Changes);

        Assert.Equal((ChangeKind.AttributeChanged, "mcids-in-xobject: (missing) -> 1"), (change.Kind, change.Detail));
    }

    [Fact]
    public void Compare_ShouldReportOneBookmarkChanged_WhenABookmarkToAnotherFileLosesItsPage()
    {
        var before = SnapshotText.Header + "bookmarks\n  \"Atlas\" -> p1\n";
        var after = SnapshotText.Header + "bookmarks\n  \"Atlas\" file=\"atlas.pdf\" dest=p1 view=Fit\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.All(changes, change => Assert.Equal("bookmarks > Atlas", change.Path));
        Assert.Equal(["AttributeChanged file: (missing) -> \"atlas.pdf\"", "AttributeChanged dest: (missing) -> p1",
            "AttributeChanged view: (missing) -> Fit", "Changed \"Atlas\" -> p1 -> \"Atlas\" file=\"atlas.pdf\" dest=p1 view=Fit"],
            changes.Select(change => $"{change.Kind} {change.Detail}"));
    }

    [Fact]
    public void Describe_ShouldListAccessibilityChangesFirst_WhenTheyComeLaterInTheSnapshot()
    {
        var before = SnapshotText.Header + "structure\n  P p1 \"Go with a guide\"\nuntagged\n  (none)\n";
        var after = SnapshotText.Header + "structure\n  P p1 \"Go without a guide\"\nuntagged\n  p1 \"Stray text\"\n";

        var lines = SnapshotDiff.CompareSnapshots(before, after).Describe().Split('\n');

        Assert.Equal("by section: structure 1, untagged 1", lines[1]);
        Assert.Equal("+ added untagged > p1 (p1) [accessibility]", lines[3]);
        Assert.Contains("~ text structure > P (p1)", lines);
    }

    [Theory]
    [InlineData("structure", "Figure p1 alt=\"Logo\"", "Figure p1", true)]
    [InlineData("structure", "Figure p1 alt=\"Logo\"", "Figure p1 alt=\"\"", true)]
    [InlineData("structure", "Figure p1 alt=\"Logo\"", "Figure p1 alt=\"Store logo\"", false)]
    [InlineData("structure", "H2 p1 \"Elevation\"", "P p1 \"Elevation\"", true)]
    [InlineData("structure", "TH1 p1 scope=Column \"Trail\"", "TH1 p1 \"Trail\"", true)]
    [InlineData("structure", "TD p1 headers=[TH1] \"Ridgeline\"", "TD p1 \"Ridgeline\"", true)]
    [InlineData("structure", "P p1 lang=es-ES \"Hola\"", "P p1 lang=fr-FR \"Hola\"", true)]
    [InlineData("structure", "P p1 \"Go with a guide\"", "P p1 \"Go without a guide\"", false)]
    [InlineData("structure", "TH1 p1 scope=Column \"Trail\"", "TH2 p1 scope=Column \"Trail\"", false)]
    [InlineData("structure", "TD p1 headers=[TH1] \"Ridgeline\"", "TD p1 headers=[?h-other] \"Ridgeline\"", true)]
    [InlineData("document", "marked=yes lang=en-US", "marked=yes lang=en-GB", true)]
    [InlineData("structure", "P p1 span-lang=[fr-FR:\"bonjour\"] \"Say bonjour\"", "P p1 span-lang=[de-DE:\"bonjour\"] \"Say bonjour\"", true)]
    [InlineData("structure", "P p1 span-lang=[fr-FR:\"bonjuor\"] \"Say bonjuor\"", "P p1 span-lang=[fr-FR:\"bonjour\"] \"Say bonjour\"", false)]
    [InlineData("structure", "P p1 span-lang=[fr-FR:\"a \\\"b\"] \"a b\"", "P p1 span-lang=[fr-FR:\"a\"] \"a b\"", false)]
    [InlineData("artifacts", "p1 type=Pagination subtype=Footer \"Page 1\"", "p1 type=Pagination subtype=Header \"Page 1\"", false)]
    [InlineData("structure", "P p1 span-actual=[\"fi\":\"x\"] \"xne\"", "P p1 \"xne\"", true)]
    [InlineData("untagged", "p1 images=1 paths=1", "p1 images=2 paths=1", true)]
    [InlineData("untagged", "p1 images=1 paths=1", "p1 images=2 paths=2", true)]
    [InlineData("untagged", "p1 images=2 paths=3", "p1 images=1 paths=1", false)]
    [InlineData("untagged", "p1 \"Page 1\"", "p1 \"Page 1 Draft\"", true)]
    [InlineData("untagged", "p1 \"Draft copy for review only Page 3\"", "p1 \"Draft copy for review only\"", false)]
    [InlineData("structure", "Custom->H2 p1 \"Elevation\"", "Custom->H3 p1 \"Elevation\"", true)]
    [InlineData("annotations", "p1 Link tagged at=0,0 size=10x10", "p1 Link untagged at=0,0 size=10x10", true)]
    [InlineData("annotations", "p1 Link tagged", "p1 Link untagged", true)]
    [InlineData("annotations", "p1 Link untagged at=0,0 size=10x10", "p1 Link tagged at=0,0 size=10x10", false)]
    [InlineData("forms", "text \"hiker\" p1 tu=\"Hiker name\"", "text \"hiker\" p1", true)]
    [InlineData("layout", "p1 text tagged at=72,700 color=gray:0 \"Elevation\"", "p1 text tagged at=72,700 color=#ff0000 \"Elevation\"", false)]
    [InlineData("layout", "p1 text tagged at=72,700 color=gray:0 \"Elevation\"", "p1 text artifact at=72,700 color=gray:0 \"Elevation\"", true)]
    [InlineData("layout", "p1 image tagged at=72,600 size=120x40 hash=3f9c", "p1 image artifact at=72,600 size=120x40 hash=3f9c", true)]
    [InlineData("layout", "p1 text artifact at=72,40 color=gray:0 \"Page 1\"", "p1 text tagged at=72,40 color=gray:0 \"Page 1\"", false)]
    [InlineData("document", "marked=yes lang=en-US display-doc-title=yes pages=1", "marked=no lang=en-US display-doc-title=yes pages=1", true)]
    [InlineData("document", "marked=yes lang=en-US display-doc-title=yes pages=1", "marked=yes lang=en-US display-doc-title=no pages=1", true)]
    [InlineData("document", "marked=no lang=en-US display-doc-title=no pages=1", "marked=yes lang=en-US display-doc-title=yes pages=1", false)]
    [InlineData("metadata", "title=\"Trail Guide\" author=(missing)", "title=(missing) author=(missing)", true)]
    [InlineData("metadata", "pdfuaid-part=1 xmp-title=\"Trail Guide\"", "pdfuaid-part=1 xmp-title=\"\"", true)]
    [InlineData("metadata", "title=\"Trail Guide\" author=(missing)", "title=\"Trail Guide 2026\" author=(missing)", false)]
    [InlineData("structure", "Span p1 actual=\"fi\" \"fi\"", "Span p1 \"fi\"", true)]
    [InlineData("structure", "Span p1 expansion=\"Mount\" \"Mt\"", "Span p1 expansion=\"\" \"Mt\"", true)]
    [InlineData("structure", "Sect p1 title=\"Routes\"", "Sect p1", false)]
    [InlineData("pages", "p1 letter rotate=0 tabs=S", "p1 letter rotate=0 tabs=R", true)]
    [InlineData("pages", "p1 letter rotate=0 tabs=S", "p1 letter rotate=0 tabs=(missing)", true)]
    [InlineData("pages", "p1 letter rotate=0 tabs=R", "p1 letter rotate=0 tabs=S", false)]
    [InlineData("fonts", "Arial TrueType embedded=yes to-unicode=yes", "Arial TrueType embedded=yes to-unicode=no", true)]
    [InlineData("fonts", "Arial TrueType embedded=yes to-unicode=yes", "Arial TrueType embedded=yes to-unicode=yes unmapped=[41]", true)]
    [InlineData("fonts", "Arial TrueType embedded=yes to-unicode=yes unmapped=[41]", "Arial TrueType embedded=yes to-unicode=yes unmapped=[41-43]", true)]
    [InlineData("fonts", "Arial TrueType embedded=yes to-unicode=yes unmapped=[41-43,50]", "Arial TrueType embedded=yes to-unicode=yes unmapped=[42,50]", false)]
    [InlineData("fonts", "Arial TrueType embedded=yes to-unicode=no", "Arial TrueType embedded=no to-unicode=no", false)]
    [InlineData("settings", "version=1.7 encrypted=yes permissions=print,extract-for-accessibility", "version=1.7 encrypted=yes permissions=print", true)]
    [InlineData("settings", "version=1.7 encrypted=yes permissions=extract-for-accessibility", "version=1.7 encrypted=yes permissions=none", true)]
    [InlineData("settings", "version=1.7 encrypted=yes permissions=print", "version=1.7 encrypted=yes permissions=print,copy", false)]
    [InlineData("settings", "version=1.7 encrypted=yes permissions=extract-for-accessibility", "version=1.7 encrypted=no", false)]
    [InlineData("settings", "version=1.7 encrypted=no", "version=1.7 encrypted=yes permissions=print", true)]
    [InlineData("settings", "version=1.7 encrypted=no", "version=1.7 encrypted=yes permissions=print,extract-for-accessibility", false)]
    public void AffectsAccessibility_ShouldFollowTheFixedRules_WhenGivenAChange(string section, string before, string after,
        bool affects)
    {
        var changes = SnapshotDiff.CompareSnapshots($"{SnapshotText.Header}{section}\n  {before}\n",
            $"{SnapshotText.Header}{section}\n  {after}\n").Changes;

        Assert.NotEmpty(changes);
        Assert.Equal(affects, changes.Any(change => change.AffectsAccessibility == true));
    }

    [Fact]
    public void AffectsAccessibility_ShouldBeNull_WhenTheChangeIsInADocxSnapshot()
    {
        var before = "# docx-snapshot v1\nsections\n  s1 letter headers=default\nbody\n  P lang=en-US \"Walk\"\n";
        var after = "# docx-snapshot v1\nsections\n  s1 letter\nbody\n  P lang=fr-FR \"Walk\"\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.Equal(2, changes.Count);
        Assert.All(changes, change => Assert.Null(change.AffectsAccessibility));
    }

    [Fact]
    public void Compare_ShouldNameTheDocxSection_WhenTheChangeIsInADocxSnapshot()
    {
        var before = "# docx-snapshot v1\nsections\n  s1 letter headers=default\nbody\n  P lang=en-US \"Walk\"\n";
        var after = "# docx-snapshot v1\nsections\n  s1 letter\nbody\n  P lang=fr-FR \"Walk\"\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.Equal(["sections", "body"], changes.Select(change => change.SectionName));
        Assert.Equal([DocxSection.Sections, DocxSection.Body], changes.Select(change => change.DocxSection));
        Assert.All(changes, change => Assert.Null(change.PdfSection));
    }

    [Fact]
    public void Compare_ShouldLeaveTheSectionsEmpty_WhenTheTextsAreNotSnapshots()
    {
        var change = Assert.Single(SnapshotDiff.CompareSnapshots("a\nb\n", "a\nc\n").Changes);

        Assert.Null(change.SectionName);
        Assert.Null(change.PdfSection);
        Assert.Null(change.DocxSection);
        Assert.Null(change.AffectsAccessibility);
    }

    [Fact]
    public void Compare_ShouldReportOnlyTheNewAttributes_WhenLinesWithoutTextGainSome()
    {
        var before = "# docx-snapshot v1\nbody\n  Table\n    Row\n      Cell\n        P \"Name\"\n      Cell\n";
        var cell = "Cell margins=top:2 borders=top:none:0";
        var after = $"# docx-snapshot v1\nbody\n  Table\n    Row\n      {cell}\n        P \"Name\"\n      {cell}\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        (ChangeKind, string)[] expected =
        [
            (ChangeKind.AttributeChanged, "body > Table > Row > Cell[1]"),
            (ChangeKind.AttributeChanged, "body > Table > Row > Cell[1]"),
            (ChangeKind.AttributeChanged, "body > Table > Row > Cell[2]"),
            (ChangeKind.AttributeChanged, "body > Table > Row > Cell[2]"),
        ];
        Assert.Equal(expected, changes.Select(change => (change.Kind, change.Path)));
    }

    [Fact]
    public void Compare_ShouldPairTheEditedLine_WhenANewLineWithTheSameLeadingWordsComesFirst()
    {
        var image = "p1 image tagged at=72,600 size=120x40 pixels=600x200 hash=aaa";
        var added = "p1 image tagged at=300,100 size=50x50 pixels=10x10 hash=bbb";
        var before = $"{SnapshotText.Header}layout\n  {image}\n";
        var after = $"{SnapshotText.Header}layout\n  {added}\n  {image.Replace("aaa", "ccc")}\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.Equal([ChangeKind.Added, ChangeKind.AttributeChanged], changes.Select(change => change.Kind));
    }

    // The later line is as like, or more like, another added line, so each line keeps its own edit.
    [Theory]
    [InlineData("p1 image tagged at=10,20 size=100x50 hash=aaa", "p1 image tagged at=10,80 size=200x50 hash=bbb",
        "p1 image tagged at=10,80 size=200x50 hash=aaa", "p1 image tagged at=10,80 size=200x60 hash=bbb", 3)]
    [InlineData("p1 path tagged a=1 b=1 c=1 d=1 e=1 f=1", "p1 path tagged a=1 b=1 c=1 d=1 g=1 h=1",
        "p1 path tagged a=1 b=1 c=1 d=1 g=1", "p1 path tagged a=1 b=1 c=1 d=1 g=1 h=1 i=1", 4)]
    [InlineData("p1 path tagged a=1 e=1 f=1 g=1 h=1", "p1 path tagged a=1 b=2 c=2 d=2 i=2",
        "p1 path tagged a=1 b=2 c=2 p=1 r=1", "p1 path tagged a=1 e=1 b=2 c=2 q=1", 12)]
    public void Compare_ShouldPairEachLineWithItsOwnEdit_WhenTheLaterLineMatchesAnotherAsWell(string first,
        string second, string firstEdited, string secondEdited, int attributeChanges)
    {
        var before = $"{SnapshotText.Header}layout\n  {first}\n  {second}\n";
        var after = $"{SnapshotText.Header}layout\n  {firstEdited}\n  {secondEdited}\n";

        var diff = SnapshotDiff.CompareSnapshots(before, after);

        var expected = Enumerable.Repeat(ChangeKind.AttributeChanged, attributeChanges);
        Assert.Equal(expected, diff.Changes.Select(change => change.Kind));
        Assert.DoesNotContain("<td></td><td class=\"added\">", diff.ToHtml());
    }

    // Three added lines for two removed ones: at best one added line is left over.
    [Theory]
    [InlineData("a=1 b=1 c=1 d=1 x=1", "a=1 b=1 c=1 d=1 e=1",
        "a=1 x=1 p=1 q=1 r=1", "a=1 b=1 c=1 d=1 e=1 f=1", "a=1 b=1 x=1 y=1 z=1")]
    [InlineData("a=1 b=1 c=1 d=1 y=1", "a=1 b=1 c=1 d=1 e=1 f=1",
        "a=1 b=1 c=1 d=1 e=1", "a=1 b=1 y=1 q=1", "c=1 d=1 e=1 f=1 g=1")]
    public void Compare_ShouldLeaveOneAddedLineUnpaired_WhenThreeLinesReplaceTwo(string first, string second,
        string firstAdded, string secondAdded, string thirdAdded)
    {
        string Layout(params string[] lines) =>
            $"{SnapshotText.Header}layout\n" + string.Concat(lines.Select(line => $"  p1 path tagged {line}\n"));

        var diff = SnapshotDiff.CompareSnapshots(Layout(first, second), Layout(firstAdded, secondAdded, thirdAdded));

        Assert.Equal(1, diff.Changes.Count(change => change.Kind == ChangeKind.Added));
        Assert.DoesNotContain(diff.Changes, change => change.Kind is ChangeKind.Removed or ChangeKind.Moved);
        Assert.Single(Regex.Matches(diff.ToHtml(), Regex.Escape("<td></td><td class=\"added\">")));
    }

    [Fact]
    public void Compare_ShouldPairTheLineWithTheSameText_WhenANewLineSharesAsManyWords()
    {
        var before = $"{SnapshotText.Header}structure\n  H2 p1 \"Trail map\"\n";
        var after = $"{SnapshotText.Header}structure\n  H2 p1 \"Trail guide\"\n"
            + "  H2 p1 lang=en title=Map \"Trail map\"\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        ChangeKind[] expected = [ChangeKind.Added, ChangeKind.AttributeChanged, ChangeKind.AttributeChanged];
        Assert.Equal(expected, changes.Select(change => change.Kind));
    }

    [Fact]
    public void Compare_ShouldPairTheNextMostAlike_WhenTheMostAlikeIsALaterLinesEdit()
    {
        var before = $"{SnapshotText.Header}layout\n  p1 path tagged a=1 b=1 c=1 d=1 e=1 f=1\n"
            + "  p1 path tagged a=1 b=1 c=1 d=1 g=1 h=1\n";
        var after = $"{SnapshotText.Header}layout\n  p1 path tagged a=1 b=1 c=1 x=1 y=1 z=1\n"
            + "  p1 path tagged a=1 b=1 c=1 d=1 g=1 h=1 e=1\n";

        var diff = SnapshotDiff.CompareSnapshots(before, after);

        Assert.Equal(Enumerable.Repeat(ChangeKind.AttributeChanged, 7), diff.Changes.Select(change => change.Kind));
        Assert.DoesNotContain("<td></td><td class=\"added\">", diff.ToHtml());
    }

    [Fact]
    public void Compare_ShouldNotPairLinesOfKeys_WhenTheyStartWithDifferentKeys()
    {
        var before = $"{SnapshotText.Header}metadata\n  creation-date=<present> producer=<present>\n";
        var after = $"{SnapshotText.Header}metadata\n  pdfuaid-part=1 xmp-title=\"Guide\"\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.Equal([ChangeKind.Removed, ChangeKind.Added], changes.Select(change => change.Kind));
    }

    [Fact]
    public void Compare_ShouldNotReportAMove_WhenAnEditedLineIsStillInItsPlace()
    {
        var before = "# docx-snapshot v1\nbody\n  Table\n    Row\n      Cell\n        P \"Name\"\n";
        var after = "# docx-snapshot v1\nbody\n  Table\n    Row header height=20\n      Cell\n        P \"Name\"\n";

        var changes = SnapshotDiff.CompareSnapshots(before, after).Changes;

        Assert.Equal([ChangeKind.AttributeChanged, ChangeKind.Changed], changes.Select(change => change.Kind));
    }

    [Fact]
    public void Compare_ShouldFlagAMoveInTheStructure_WhenAnElementChangesPlaceInReadingOrder()
    {
        var figure = "    Figure p1 alt=\"Park logo\"\n";
        var after = Before.Replace(figure, "").Replace("    P p3", figure + "    P p3");

        Assert.True(Assert.Single(SnapshotDiff.CompareSnapshots(Before, after).Changes).AffectsAccessibility);
    }

    [Fact]
    public void Compare_ShouldReportEachChangedLineByNumber_WhenTheTextIsNotASnapshot()
    {
        var changes = SnapshotDiff.CompareSnapshots("{\n  \"a\": 1,\n  \"b\": 2,\n  \"c\": 3\n}\n",
            "{\n  \"a\": 1,\n  \"c\": 4\n}\n").Changes;

        SnapshotChange[] expected =
        [
            new()
            {
                Kind = ChangeKind.TextChanged, SectionName = null, Path = "line 3", Page = null,
                Detail = "[-\"b\": 2,-] {+\"c\": 4+}",
            },
            new()
            {
                Kind = ChangeKind.Removed, SectionName = null, Path = "line 4", Page = null, Detail = "  \"c\": 3",
            },
        ];
        Assert.Equal(expected, changes);
    }

    [Fact]
    public void Compare_ShouldShowBothLinesInQuotes_WhenAPlainLineChangesOnlyInSpaces()
    {
        var change = Assert.Single(SnapshotDiff.CompareSnapshots("a\n", " a\n").Changes);

        Assert.Equal((ChangeKind.TextChanged, "- \"a\"\n+ \" a\""), (change.Kind, change.Detail));
    }

    [Fact]
    public void Compare_ShouldKeepEachChangesSection_WhenTheVerifiedSnapshotHasAnOlderFormatVersion()
    {
        var older = Before.Replace("# pdfua-snapshot v1", "# pdfua-snapshot v0");

        var changes = SnapshotDiff.CompareSnapshots(older, Before.Replace("Go with a guide",
            "Go without a guide")).Changes;

        Assert.Equal([null, PdfSection.Structure], changes.Select(change => change.PdfSection));
        Assert.Equal("header", changes[0].Path);
    }

    // What an upgrading user sees for a document with suspect tags, an element without a parent entry and a PDF 2.0
    // header: the earlier release printed neither key, and read the version as 1.4.
    [Fact]
    public void Compare_ShouldReportOnlyTheNewKeysAndTheVersion_WhenASnapshotFromBeforeTheDocumentSetupReadsIsCompared()
    {
        var pdf = new TestPdf { MarkInfo = "/Marked true /Suspects true" };
        pdf.AddPage().Tagged(0, "Hello");
        pdf.Structure(new TestElement("P", 0) { WithoutParent = true });
        var bytes = pdf.Build();
        "%PDF-2.0"u8.ToArray().CopyTo(bytes, 0);
        var current = PdfUaDocument.Load(bytes).ToSnapshot();
        var earlier = current.Replace(" suspects=yes", "").Replace(" parent=missing", "")
            .Replace("version=2.0", "version=1.4");

        var changes = SnapshotDiff.CompareSnapshots(earlier, current).Changes;

        Assert.Equal([PdfSection.Document, PdfSection.Settings, PdfSection.Structure],
            changes.Select(change => change.PdfSection));
        Assert.All(changes, change => Assert.Equal(ChangeKind.AttributeChanged, change.Kind));
    }

    // The earlier release didn't print a Form element's field role.
    [Fact]
    public void Compare_ShouldReportOnlyTheFieldRole_WhenASnapshotFromBeforeFieldRolesIsCompared()
    {
        var pdf = new TestPdf();
        pdf.AddPage();
        pdf.Structure(new TestElement("Form") { Entries = "/A << /O /PrintField /Role /cb >>" });
        var current = pdf.Load().ToSnapshot();
        var earlier = current.Replace(" field-role=cb", "");

        var change = Assert.Single(SnapshotDiff.CompareSnapshots(earlier, current).Changes);

        Assert.Equal((PdfSection.Structure, ChangeKind.AttributeChanged), (change.PdfSection, change.Kind));
    }

    // The earlier release didn't print a Screen annotation's media clip, so its two keys show as added, on the
    // annotation's line and on the line of the Annot element that tags it.
    [Fact]
    public void Compare_ShouldReportOnlyTheMediaClipsKeys_WhenASnapshotFromBeforeMediaClipsIsCompared()
    {
        var pdf = new TestPdf();
        pdf.AddPage();
        pdf.Structure(new TestElement("Annot", new TestAnnotation("/Subtype /Screen /A << /S /Rendition /R << /S /MR "
            + "/C << /S /MCD /CT (video/mp4) /Alt [(en-US) (Trail tour)] >> >> >>")));
        var current = pdf.Load().ToSnapshot();
        var earlier = current.Replace(" media-type=video/mp4 media-alt=\"Trail tour\"", "");

        var changes = SnapshotDiff.CompareSnapshots(earlier, current).Changes;

        Assert.All(changes, change => Assert.Equal(ChangeKind.AttributeChanged, change.Kind));
        Assert.Equal([PdfSection.Structure, PdfSection.Structure, PdfSection.Annotations, PdfSection.Annotations],
            changes.Select(change => change.PdfSection));
    }

    // The earlier release didn't count artifacts and tagged content nested in each other, so both counts show as added
    // on the page's line.
    [Fact]
    public void Compare_ShouldReportOnlyTheNestingCounts_WhenASnapshotFromBeforeTheCountsIsCompared()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Raw("/P << /MCID 0 >> BDC BT /F1 12 Tf 72 700 Td (Ridge) Tj ET /Artifact BMC EMC EMC")
            .Raw("/Artifact BMC /P << /MCID 1 >> BDC EMC EMC");
        pdf.Structure(new TestElement("P", 0));
        var current = pdf.Load().ToSnapshot();
        var earlier = current.Replace(" artifacts-in-tagged=1", "").Replace(" artifacts-holding-tagged=1", "");

        var changes = SnapshotDiff.CompareSnapshots(earlier, current).Changes;

        Assert.Equal([PdfSection.Pages, PdfSection.Pages], changes.Select(change => change.PdfSection));
        Assert.All(changes, change => Assert.Equal(ChangeKind.AttributeChanged, change.Kind));
    }

    [Fact]
    public void Compare_ShouldReportTheShiftOnceAndOnlyTheNewPagesContent_WhenAPageIsInserted()
    {
        var changes = SnapshotDiff.CompareSnapshots(Pages(10), Pages(10, insertedAfter: 1)).Changes;

        SnapshotChange[] expected =
        [
            new()
            {
                Kind = ChangeKind.Changed, SectionName = "pages", Format = SnapshotFormat.Pdf, Path = "pages", Page = 2,
                Detail = "p2 inserted; p2-10 -> p3-11",
            },
            new()
            {
                Kind = ChangeKind.AttributeChanged, SectionName = "document", Format = SnapshotFormat.Pdf,
                Path = "document > marked",
                Page = null, Detail = "pages: 10 -> 11",
            },
            new()
            {
                Kind = ChangeKind.Added, SectionName = "structure", Format = SnapshotFormat.Pdf,
                Path = "structure > Document > P[2]", Page = 2,
                Detail = "P p2 \"A new page\"",
            },
            new()
            {
                Kind = ChangeKind.Added, SectionName = "layout", Format = SnapshotFormat.Pdf, Path = "layout > p2",
                Page = 2,
                Detail = "p2 text tagged at=72,700 \"A new page\"",
            },
        ];
        Assert.Equal(expected, changes);
    }

    [Fact]
    public void Compare_ShouldReportTheShiftOnceAndOnlyTheRemovedPagesContent_WhenAPageIsRemoved()
    {
        var changes = SnapshotDiff.CompareSnapshots(Pages(10, insertedAfter: 1), Pages(10)).Changes;

        Assert.Equal("p2 removed; p3-11 -> p2-10", changes[0].Detail);
        Assert.Equal([ChangeKind.Changed, ChangeKind.AttributeChanged, ChangeKind.Removed, ChangeKind.Removed],
            changes.Select(change => change.Kind));
    }

    [Fact]
    public void Compare_ShouldNotLineUpPages_WhenNoPageHasTheSameText()
    {
        var after = Pages(10, insertedAfter: 1).Replace("Paragraph", "Chapter").Replace("of the guide", "about gardening");

        var changes = SnapshotDiff.CompareSnapshots(Pages(10), after).Changes;

        Assert.DoesNotContain(changes, change => change.Path == "pages");
    }

    [Fact]
    public void Compare_ShouldCarryTheShiftPastABlankPage_WhenAPageIsInsertedBeforeIt()
    {
        var changes = SnapshotDiff.CompareSnapshots(Pages(10, blank: 5), Pages(10, insertedAfter: 1, blank: 5)).Changes;

        Assert.Equal("p2 inserted; p2-10 -> p3-11", changes[0].Detail);
        Assert.Equal(4, changes.Count);
    }

    [Fact]
    public void Compare_ShouldNotLineUpPages_WhenPagesAreBothInsertedAndRewritten()
    {
        var after = Pages(10, insertedAfter: 1).Replace("Paragraph 5 of the guide", "Entirely rewritten text");

        var changes = SnapshotDiff.CompareSnapshots(Pages(10), after).Changes;

        Assert.DoesNotContain(changes, change => change.Path == "pages");
    }

    [Fact]
    public void Compare_ShouldShowAWrittenPageNumber_WhenALaterLineIsRemovedAlongWithAPage()
    {
        var note = "    P p8 \"Paragraph 7 of the guide\"\n";
        var before = Pages(10, insertedAfter: 1).Replace(note, note + "    P p8 \"A note\"\n");

        var changes = SnapshotDiff.CompareSnapshots(before, Pages(10)).Changes;

        var removed = Assert.Single(changes, change => change.Detail == "P p8 \"A note\"");
        Assert.Equal(8, removed.Page);
    }

    [Theory]
    [InlineData("    P p3 \"Paragraph 2 of the guide\"", "    P p2 \"Paragraph 2 of the guide\"")]
    [InlineData("    Table p3-5", "    Table p2-4")]
    [InlineData("    Table p2-3", "    Table p2")]
    [InlineData("  \"Trails\" -> p4", "  \"Trails\" -> p3")]
    [InlineData("  label from p3 style=decimal", "  label from p2 style=decimal")]
    [InlineData("    P p3 \"See p3 and \\\"p4\\\"\"", "    P p2 \"See p3 and \\\"p4\\\"\"")]
    [InlineData("  info p3=\"x\"", "  info p3=\"x\"")]
    [InlineData("# only pages: 3", "# only pages: 3")]
    [InlineData("  Note -> ", "  Note -> ")]
    public void Renumber_ShouldChangeOnlyPageWords_WhenAPageWasRemoved(string line, string expected)
    {
        var before = SnapshotTree.Parse(Pages(10, insertedAfter: 1));
        var alignment = PageAlignment.Find(before, SnapshotTree.Parse(Pages(10)))!;

        var renumbered = alignment.Renumber(line);

        Assert.Equal(expected, renumbered);
    }

    [Fact]
    public void ToHtml_ShouldNotMarkShiftedLinesAsChanged_WhenAPageIsInserted()
    {
        var html = SnapshotDiff.CompareSnapshots(Pages(10), Pages(10, insertedAfter: 1)).ToHtml();

        // Only the document line's page count is an edited row; everything else is unchanged or the new page's lines.
        Assert.Single(Regex.Matches(html, "<td class=\"removed\">"));
        Assert.Equal(2, Regex.Matches(html, "<td></td><td class=\"added\">").Count);
    }

    // One paragraph per page, in the structure and the layout, with an optional page added after the given one and an
    // optional page left blank.
    private static string Pages(int count, int? insertedAfter = null, int? blank = null)
    {
        var texts = Enumerable.Range(1, count).Select(i => i == blank ? null : $"Paragraph {i} of the guide").ToList();
        if (insertedAfter is int index) texts.Insert(index, "A new page");
        var pages = texts.Select((text, i) => (Text: text, Page: i + 1)).Where(page => page.Text is not null).ToList();
        var structure = pages.Select(page => $"    P p{page.Page} \"{page.Text}\"");
        var layout = pages.Select(page => $"  p{page.Page} text tagged at=72,700 \"{page.Text}\"");
        return SnapshotText.Header + $"document\n  marked=yes pages={texts.Count}\nstructure\n  Document\n"
            + string.Join("\n", structure) + "\nlayout\n" + string.Join("\n", layout) + "\n";
    }

    [Fact]
    public void ToJson_ShouldListEachChangeWithItsSectionAndPage_WhenGivenASnapshot()
    {
        var diff = SnapshotDiff.CompareSnapshots(Before, Before.Replace("H1 p1 \"Trail", "P p1 \"Trail"));

        var json = diff.ToJson();

        Assert.Equal("""
            {
              "version": 2,
              "changes": [
                {
                  "kind": "Changed",
                  "section": "structure",
                  "path": "structure > Document > P[1]",
                  "page": 1,
                  "detail": "type: H1 -> P",
                  "affectsAccessibility": true
                }
              ]
            }

            """.ReplaceLineEndings("\n"), json);
    }

    [Fact]
    public void ToJson_ShouldWriteANullSectionPageAndFlag_WhenTheTextIsNotASnapshot()
    {
        var diff = SnapshotDiff.CompareSnapshots("a\n", "b\n");

        var json = diff.ToJson();

        Assert.Contains("\"kind\": \"TextChanged\",\n      \"section\": null,\n      \"path\": \"line 1\",\n"
            + "      \"page\": null,", json);
        Assert.Contains("\"affectsAccessibility\": null", json);
    }

    [Fact]
    public void ToJson_ShouldNameTheSectionWhitespace_WhenOnlyBlankLinesDiffer()
    {
        var diff = SnapshotDiff.CompareSnapshots(Before, Before + "\n");

        var json = diff.ToJson();

        Assert.Contains("\"section\": \"whitespace\",\n      \"path\": \"whitespace\",", json);
    }

    [Fact]
    public void CompareTo_ShouldCompareTheSnapshots_WhenGivenTwoDocuments()
    {
        var paragraph = new PdfStructureElement { Type = "P", FirstPage = 1, Text = "Go with a guide" };
        var before = Documents.Empty() with { Structure = [paragraph] };
        var after = before with { Structure = [paragraph with { Text = "Go without a guide" }] };

        var change = Assert.Single(before.CompareTo(after).Changes);

        Assert.Equal((ChangeKind.TextChanged, PdfSection.Structure, "structure > P"),
            (change.Kind, change.PdfSection, change.Path));
    }
}
