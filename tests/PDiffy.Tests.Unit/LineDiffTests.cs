using PDiffy.Diffing;

namespace PDiffy.Tests.Unit;

public class LineDiffTests
{
    [Fact]
    public void Compare_ShouldKeepCommonLinesAndMarkTheRest_WhenLinesChange()
    {
        string[] before = ["a", "b", "c", "d"];
        string[] after = ["a", "c", "x", "d"];

        var changes = LineDiff.Compare(before, after);

        string[] expected = [" a", "-b", " c", "+x", " d"];
        Assert.Equal(expected, changes.Select(change => $"{change.Kind}{change.Line}"));
    }

    // Random lists over a small alphabet, checked against a plain longest-common-subsequence count: the diff must rebuild
    // both lists and keep as many lines as possible.
    [Fact]
    public void Compare_ShouldRebuildBothListsWithTheFewestEdits_WhenListsDifferRandomly()
    {
        var random = new Random(42);
        for (var run = 0; run < 500; run++)
        {
            var before = Enumerable.Range(0, random.Next(0, 15)).Select(_ => ((char)('a' + random.Next(4))).ToString()).ToList();
            var after = Enumerable.Range(0, random.Next(0, 15)).Select(_ => ((char)('a' + random.Next(4))).ToString()).ToList();

            var changes = LineDiff.Compare(before, after);

            Assert.Equal(before, changes.Where(change => change.Kind != '+').Select(change => change.Line));
            Assert.Equal(after, changes.Where(change => change.Kind != '-').Select(change => change.Line));
            Assert.Equal(CommonLength(before, after), changes.Count(change => change.Kind == ' '));
        }
    }

    private static int CommonLength(List<string> before, List<string> after)
    {
        var common = new int[before.Count + 1, after.Count + 1];
        for (var i = before.Count - 1; i >= 0; i--)
        {
            for (var j = after.Count - 1; j >= 0; j--)
            {
                common[i, j] = before[i] == after[j] ? common[i + 1, j + 1] + 1 : Math.Max(common[i + 1, j], common[i, j + 1]);
            }
        }

        return common[0, 0];
    }
}
