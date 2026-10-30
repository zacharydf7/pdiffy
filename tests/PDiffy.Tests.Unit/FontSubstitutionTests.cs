using PDiffy.Diffing;

namespace PDiffy.Tests.Unit;

[Collection(nameof(SnapshotFolderTests))]
public sealed class FontSubstitutionTests : SnapshotFolderTests
{
    // Snapshots always use LF, whatever line endings Git gives this file.
    private static readonly string Before = """
        # pdfua-snapshot v1
        structure
          P p1 "Trail one"
        fonts
          Arial TrueType embedded=no to-unicode=yes
        layout
          p1 text tagged at=72,700 font=Arial size=12 color=gray:0 "Trail one"
          p1 text tagged at=72,680 font=Arial size=12 color=gray:0 "Trail two"
          p1 text tagged at=72,660 font=Arial size=12 color=gray:0 "Trail three"

        """.ReplaceLineEndings("\n");

    private static readonly string Substituted = Before
        .Replace("Arial TrueType embedded=no", "DejaVuSans TrueType embedded=yes")
        .Replace("font=Arial", "font=DejaVuSans")
        .Replace("at=72,680", "at=72,679");

    [Fact]
    public void Hint_ShouldNameBothFontsAndTheLinesThatSwitched_WhenOneFontReplacedAnotherEverywhere()
    {
        var hint = FontSubstitution.Hint(SnapshotDiff.CompareSnapshots(Before, Substituted));

        Assert.Equal("Arial was replaced by DejaVuSans in 3 line(s): the machine running this test probably doesn't have "
            + "Arial. Embed the font in the PDF, or install it on the build agent.", hint);
    }

    [Fact]
    public void Hint_ShouldGiveNothing_WhenOtherChangesAreMoreThanTheFontSwitchExplains()
    {
        var edited = Substituted.Replace("\"Trail one\"", "\"Lake one\"").Replace("\"Trail two\"", "\"Lake two\"");

        var hint = FontSubstitution.Hint(SnapshotDiff.CompareSnapshots(Before, edited));

        Assert.Null(hint);
    }

    [Fact]
    public void Hint_ShouldGiveNothing_WhenTheTextAlsoChangedSize()
    {
        var restyled = Substituted.Replace("size=12", "size=14");

        var hint = FontSubstitution.Hint(SnapshotDiff.CompareSnapshots(Before, restyled));

        Assert.Null(hint);
    }

    [Fact]
    public void Hint_ShouldGiveNothing_WhenAFontIsAddedWithoutAnotherBeingRemoved()
    {
        var after = Before.Replace("fonts\n", "fonts\n  Verdana TrueType embedded=yes to-unicode=yes\n");

        var hint = FontSubstitution.Hint(SnapshotDiff.CompareSnapshots(Before, after));

        Assert.Null(hint);
    }

    [Fact]
    public void VerifyText_ShouldPutTheHintRightAfterTheFirstLine_WhenAFontWasSubstituted()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt"), Before);

        var error = Assert.Throws<SnapshotException>(() => Snapshot.VerifyText(Substituted, "Guide",
            extension: "pdfua.txt"));

        var lines = error.Message.Split(Environment.NewLine);
        Assert.StartsWith("Arial was replaced by DejaVuSans in 3 line(s)", lines[1]);
    }
}
