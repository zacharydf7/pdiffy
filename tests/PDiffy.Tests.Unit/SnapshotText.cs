namespace PDiffy.Tests.Unit;

internal static class SnapshotText
{
    // The first line of every PDF snapshot, which tells SnapshotDiff to compare by section rather than line by line.
    public const string Header = "# pdfua-snapshot v1\n";

    // The indented lines under a section header, up to the next section.
    public static List<string> SectionLines(string snapshot, string section)
    {
        var lines = snapshot.Split('\n');
        var start = Array.IndexOf(lines, section) + 1;
        return lines.Skip(start).TakeWhile(line => line.StartsWith(' ')).ToList();
    }
}
