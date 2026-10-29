using System.Text.RegularExpressions;
using PDiffy.Diffing;
using PDiffy.Tests.Unit.Docx;
using PDiffy.Tests.Unit.Producers;

namespace PDiffy.Tests.Unit;

// Makes one known edit to every line of real snapshots and checks that the diff reports that edit and nothing else,
// both ways round, so a matching rule that misreads some kind of line shows up without a test written for that line.
public class SnapshotEditTests
{
    public static TheoryData<string> EditNames => [.. Edits.Keys];

    private static readonly Dictionary<string, (Func<List<string>, int, List<string>?> Apply, ChangeKind[] Expected)>
        Edits = new()
        {
            ["two attributes added"] = (AddAttributes, [ChangeKind.AttributeChanged, ChangeKind.AttributeChanged]),
            ["an attribute changed"] = (ChangeAttribute, [ChangeKind.AttributeChanged]),
            ["the last word changed"] = (ChangeLastWord, [ChangeKind.TextChanged]),
            ["the line removed"] = (RemoveLine, [ChangeKind.Removed]),
            ["the line copied"] = (CopyLine, [ChangeKind.Added]),
            ["the line swapped with the next"] = (SwapWithNext, [ChangeKind.Moved]),
            ["a near copy added before the line, which gained an attribute"] =
                (AddNearCopyBefore, [ChangeKind.Added, ChangeKind.AttributeChanged]),
        };

    [Theory]
    [MemberData(nameof(EditNames))]
    public void Compare_ShouldReportOnlyTheEdit_WhenOneLineOfASnapshotIsEdited(string editName)
    {
        var (apply, expected) = Edits[editName];
        var failures = new List<string>();

        foreach (var (name, snapshot) in Snapshots())
        {
            var lines = snapshot.Split('\n').ToList();
            for (var i = 0; i < lines.Count; i++)
            {
                if (!IsContent(lines, i) || apply(lines, i) is not { } edited) continue;

                var after = string.Join('\n', edited);
                var changes = SnapshotDiff.Compare(snapshot, after).Changes;
                var reversed = SnapshotDiff.Compare(after, snapshot).Changes;
                var problem = Problem(changes, expected) ?? Problem(reversed, [.. expected.Select(Mirror)], "undone, ");
                if (problem is not null) failures.Add($"{name} line {i + 1} `{lines[i].Trim()}`: {problem}");
            }
        }

        Assert.True(failures.Count == 0, $"{editName}:\n" + string.Join('\n', failures));
    }

    private static string? Problem(IReadOnlyList<SnapshotChange> changes, ChangeKind[] expected, string label = "")
    {
        if (!changes.Select(change => change.Kind).SequenceEqual(expected)) return $"{label}got {Describe(changes)}";
        return changes.FirstOrDefault(IsMoveInPlace) is { } move ? $"{label}moved in place: {move.Detail}" : null;
    }

    private static bool IsMoveInPlace(SnapshotChange change) =>
        change.Kind == ChangeKind.Moved && Regex.IsMatch(change.Detail, @"^reading position (\d+) -> \1$");

    private static ChangeKind Mirror(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => ChangeKind.Removed,
        ChangeKind.Removed => ChangeKind.Added,
        _ => kind,
    };

    private static string Describe(IReadOnlyList<SnapshotChange> changes) =>
        changes.Count == 0 ? "no changes" : string.Join("; ", changes.Select(change => $"{change.Kind} {change.Path}"));

    private static List<string> AddAttributes(List<string> lines, int i) =>
        Replace(lines, i, lines[i] + " test-a=1 test-b=2");

    // The first plain value: a quoted value would need its quotes kept, and an empty one has nothing to change.
    private static List<string>? ChangeAttribute(List<string> lines, int i)
    {
        var tokens = SnapshotLine.Tokens(lines[i].Trim()).ToList();
        var index = tokens.FindIndex(IsPlainValue);
        if (index < 0) return null;

        tokens[index] += "9";
        return Replace(lines, i, Indent(lines[i]) + string.Join(' ', tokens));
    }

    // A bookmark's title is how it's told from its siblings, so a new title is a different bookmark.
    private static List<string>? ChangeLastWord(List<string> lines, int i)
    {
        var line = SnapshotLine.Parse(lines[i], 0);
        if (line.QuotedText is not { Length: > 0 } text || lines[i].TrimStart().StartsWith('"')) return null;

        var words = text.Split(' ');
        words[^1] = "Changed";
        var quoted = SnapshotLine.Tokens(lines[i].Trim()).Last(token => token.StartsWith('"'));
        return Replace(lines, i, lines[i].Replace(quoted, $"\"{string.Join(' ', words)}\""));
    }

    // The copy keeps the leading words and changes every plain value, so it's alike enough to pass for the line, but
    // the line's own edit, which only gained an attribute, is more alike.
    private static List<string>? AddNearCopyBefore(List<string> lines, int i)
    {
        var tokens = SnapshotLine.Tokens(lines[i].Trim()).ToList();
        var values = tokens.FindAll(IsPlainValue);
        if (values.Count == 0) return null;

        var changed = tokens.Select(token => values.Contains(token) ? $"{token}7" : token);
        var copy = Indent(lines[i]) + string.Join(' ', changed);
        var end = SubtreeEnd(lines, i);
        return [.. lines[..i], copy, .. lines[(i + 1)..end], lines[i] + " test-a=1", .. lines[(i + 1)..]];
    }

    private static List<string> RemoveLine(List<string> lines, int i) =>
        [.. lines[..i], .. lines[SubtreeEnd(lines, i)..]];

    private static List<string> CopyLine(List<string> lines, int i)
    {
        var end = SubtreeEnd(lines, i);
        return [.. lines[..end], .. lines[i..end], .. lines[end..]];
    }

    private static List<string>? SwapWithNext(List<string> lines, int i)
    {
        var (end, depth) = (SubtreeEnd(lines, i), Depth(lines[i]));
        if (!IsContent(lines, end) || Depth(lines[end]) != depth) return null;

        var nextEnd = SubtreeEnd(lines, end);
        if (lines[i..end].SequenceEqual(lines[end..nextEnd])) return null;
        return [.. lines[..i], .. lines[end..nextEnd], .. lines[i..end], .. lines[nextEnd..]];
    }

    private static IEnumerable<(string Name, string Snapshot)> Snapshots()
    {
        yield return ("PDF", MutationTests.Build(new MutationTests.Sample()).ToSnapshot());
        yield return ("docs sample PDF", DocsSampleTests.Guide(revised: true).Load().ToSnapshot());
        var pdfSharp = new PdfSharpPdf().Artifact("Page 1").Heading("Trail Guide").Paragraph("Pack ", "water")
            .Figure("Trail map").NewPage().Paragraph("Ask a ranger.");
        yield return ("PDFsharp PDF", pdfSharp.Load().ToSnapshot());
        yield return ("DOCX", DocxMutationTests.Build(new DocxMutationTests.Sample()).ToSnapshot());
    }

    // An indented line that isn't a section's "(none)" or "(ignored)" placeholder.
    private static bool IsContent(List<string> lines, int i) =>
        i < lines.Count && lines[i].StartsWith("  ") && lines[i].Trim() is not ("(none)" or "(ignored)");

    // The line after this one's nested lines.
    private static int SubtreeEnd(List<string> lines, int i)
    {
        var end = i + 1;
        while (IsContent(lines, end) && Depth(lines[end]) > Depth(lines[i])) end++;
        return end;
    }

    private static int Depth(string line) => line.Length - line.TrimStart().Length;

    private static string Indent(string line) => line[..Depth(line)];

    private static bool IsKey(string key) => key.All(c => char.IsAsciiLetterLower(c) || c == '-');

    // A key=value token whose value is neither quoted nor empty.
    private static bool IsPlainValue(string token) => token.IndexOf('=') is var equals and > 0
        && equals < token.Length - 1 && !token[(equals + 1)..].StartsWith('"') && IsKey(token[..equals]);

    private static List<string> Replace(List<string> lines, int i, string line) =>
        [.. lines[..i], line, .. lines[(i + 1)..]];
}
