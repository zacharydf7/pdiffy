namespace PDiffy.Tests.Unit;

internal static class SnapshotText
{
    // The indented lines under a section header, up to the next section.
    public static List<string> SectionLines(string snapshot, string section)
    {
        var lines = snapshot.Split('\n');
        var start = Array.IndexOf(lines, section) + 1;
        return lines.Skip(start).TakeWhile(line => line.StartsWith(' ')).ToList();
    }
}
