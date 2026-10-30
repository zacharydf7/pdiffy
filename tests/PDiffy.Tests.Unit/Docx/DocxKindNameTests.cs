namespace PDiffy.Tests.Unit.Docx;

// A snapshot prints each kind by its enum member's name, so renaming a member would change every approved snapshot.
public class DocxKindNameTests
{
    [Fact]
    public void Name_ShouldPrintTheSnapshotsSpelling_WhenGivenEachRevisionKind()
    {
        var names = Enum.GetValues<DocxRevisionKind>().Select(kind => SnapshotWriter.Name(kind));

        Assert.Equal(
        [
            "insert", "delete", "move-to", "move-from", "format", "paragraph-insert", "paragraph-delete",
            "paragraph-move-to", "paragraph-move-from", "paragraph-format", "paragraph-mark-format", "table-format",
            "table-grid", "row-insert", "row-delete", "row-format", "row-table-format", "cell-insert", "cell-delete",
            "cell-merge", "cell-format", "section-format",
        ], names);
    }

    [Fact]
    public void Name_ShouldPrintTheSnapshotsSpelling_WhenGivenEachFormKind()
    {
        var names = Enum.GetValues<DocxFormKind>().Select(kind => SnapshotWriter.Name(kind));

        Assert.Equal(
        [
            "text", "rich-text", "dropdown", "combobox", "date", "checkbox", "picture", "gallery", "group", "citation",
            "bibliography", "equation", "repeating-section", "repeating-section-item", "legacy-text", "legacy-checkbox",
            "legacy-dropdown",
        ], names);
    }

    [Fact]
    public void Name_ShouldPrintTheSnapshotsSpelling_WhenGivenEachNoteKind()
    {
        var names = Enum.GetValues<DocxNoteKind>().Select(kind => SnapshotWriter.Name(kind));

        Assert.Equal(["footnote", "endnote", "comment"], names);
    }
}
