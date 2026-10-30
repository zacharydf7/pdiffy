using System.Text;
using System.Text.RegularExpressions;

namespace PDiffy.Tests.Unit;

[Collection(nameof(SnapshotFolderTests))]
public sealed class SnapshotTests : SnapshotFolderTests
{
    [Fact]
    public async Task VerifyTextAsync_ShouldWriteTheTextExactlyAndFail_WhenThereIsNoVerifiedFile()
    {
        var error = await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyTextAsync(" a  \r\n\tb ",
            "Export"));

        Assert.StartsWith("New text snapshot.", error.Message);
        Assert.Equal(" a  \n\tb ", File.ReadAllText(Path.Combine(Folder, "Export.received.txt")));
    }

    [Fact]
    public void VerifyText_ShouldThrowRightAway_WhenTheTextOrJsonDiffers()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Export.verified.txt"), "one\n");
        File.WriteAllText(Path.Combine(Folder, "Job.verified.json"), "{\n  \"a\": 1\n}");

        var text = Assert.Throws<SnapshotException>(() => Snapshot.VerifyText("two\n", "Export"));
        var json = Assert.Throws<SnapshotException>(() => Snapshot.VerifyJson("""{"a":2}""", "Job"));

        Assert.StartsWith("The text's snapshot differs from the verified one.", text.Message);
        Assert.StartsWith("The JSON's snapshot differs from the verified one.", json.Message);
    }

    [Fact]
    public void VerifyText_ShouldLogEachVerifiedFileItUses_WhenTheRunLogIsSet()
    {
        var runLog = Path.Combine(Folder, "run-log");
        Environment.SetEnvironmentVariable(Snapshot.RunLogVariable, runLog);
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Export.verified.txt"), "one\n");

        Snapshot.VerifyText("one\n", "Export");
        Assert.Throws<SnapshotException>(() => Snapshot.VerifyText("two\n", "Report"));

        var log = Assert.Single(Directory.GetFiles(runLog));
        Assert.Equal($"pdiffy-run-{Environment.ProcessId}.log", Path.GetFileName(log));
        Assert.Equal([Path.Combine(Folder, "Export.verified.txt"), Path.Combine(Folder, "Report.verified.txt")],
            File.ReadAllLines(log));
    }

    [Theory]
    [InlineData("1", null, true, true)]
    [InlineData(null, null, true, false)]
    [InlineData("1", "true", true, false)]
    [InlineData("1", null, false, false)]
    public void VerifyText_ShouldOpenTheReportOnlyWhenAskedToLocally_WhenTheSnapshotFails(string? openReport,
        string? ci,
        bool hasVerified, bool opens)
    {
        var opened = new List<string>();
        SnapshotWorkflow.OpenReport = opened.Add;
        Environment.SetEnvironmentVariable(Snapshot.OpenReportVariable, openReport);
        Environment.SetEnvironmentVariable("CI", ci);
        Directory.CreateDirectory(Folder);
        if (hasVerified) File.WriteAllText(Path.Combine(Folder, "Export.verified.txt"), "one\n");

        Assert.Throws<SnapshotException>(() => Snapshot.VerifyText("two\n", "Export"));

        Assert.Equal(opens ? [Path.Combine(Folder, "Export.diff.html")] : [], opened);
    }

    [Fact]
    public void VerifyText_ShouldFailWithItsMessageAndSayWhy_WhenTheReportCannotBeOpened()
    {
        SnapshotWorkflow.OpenReport = _ => throw new System.ComponentModel.Win32Exception("No browser");
        Environment.SetEnvironmentVariable(Snapshot.OpenReportVariable, "1");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Export.verified.txt"), "one\n");

        var error = Assert.Throws<SnapshotException>(() => Snapshot.VerifyText("two\n", "Export"));

        Assert.StartsWith("The text's snapshot differs from the verified one.", error.Message);
        Assert.EndsWith($"{Environment.NewLine}Couldn't open the report: No browser", error.Message);
    }

    [Fact]
    public async Task VerifyTextAsync_ShouldPass_WhenTheVerifiedFileHasAByteOrderMarkAndCrlfLineEndings()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Export.verified.txt"), "one\r\ntwo\r\n", new UTF8Encoding(true));

        await Snapshot.VerifyTextAsync("one\ntwo\n", "Export");
    }

    [Fact]
    public async Task VerifyTextAsync_ShouldFailWithTheChangedLines_WhenTheTextDiffers()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Export.verified.txt"), "one\ntwo\nthree\n");

        var error = await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyTextAsync("one\n2\nthree\n",
            "Export"));

        Assert.StartsWith("The text's snapshot differs from the verified one.", error.Message);
        Assert.Contains("~ text line 2\n    [-two-] {+2+}", error.Message);
        Assert.Contains("<mark>", File.ReadAllText(Path.Combine(Folder, "Export.diff.html")));
    }

    [Fact]
    public async Task VerifyTextAsync_ShouldFailShowingBothLines_WhenOnlySpacesDiffer()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Export.verified.txt"), "a b\n");

        var error = await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyTextAsync("a b  \n", "Export"));

        Assert.Contains("~ text line 1\n    - \"a b\"\n    + \"a b  \"", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public async Task VerifyTextAsync_ShouldPassAndKeepTheVerifiedFile_WhenNumbersDifferWithinTheTolerance(
        string? accept)
    {
        Environment.SetEnvironmentVariable(Snapshot.AcceptVariable, accept);
        const string Verified = "# pdfua-snapshot v1\n# tolerances: positions 1pt\nlayout\n  p1 text tagged at=72,700 \"Hi\"\n";
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Layout.verified.txt"), Verified);

        await Snapshot.VerifyTextAsync(Verified.Replace("72,700", "73,700"), "Layout");

        Assert.Equal(Verified, File.ReadAllText(Path.Combine(Folder, "Layout.verified.txt")));
        Assert.False(File.Exists(Path.Combine(Folder, "Layout.received.txt")));
    }

    [Fact]
    public async Task VerifyTextAsync_ShouldUseTheExtensionAndFolder_WhenGivenThem()
    {
        var other = Path.Combine(Folder, "other");

        await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyTextAsync("text", "Export", extension: "csv",
            directory: other));

        Assert.True(File.Exists(Path.Combine(other, "Export.received.csv")));
    }

    [Fact]
    public async Task VerifyTextAsync_ShouldNameTheSnapshotAfterTheTestFileAndMethod_WhenNoNameIsGiven()
    {
        await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyTextAsync("text"));

        var method = nameof(VerifyTextAsync_ShouldNameTheSnapshotAfterTheTestFileAndMethod_WhenNoNameIsGiven);
        Assert.True(File.Exists(Path.Combine(Folder, $"{nameof(SnapshotTests)}.{method}.received.txt")));
    }

    [Fact]
    public async Task VerifyTextAsync_ShouldApplyTheDefaultsThenTheCallsOwnOptions_WhenDefaultOptionsAreSet()
    {
        Snapshot.DefaultTextOptions = options => options.ScrubText(new Regex("trail"), "path");

        await Assert.ThrowsAsync<SnapshotException>(
            () => Snapshot.VerifyTextAsync("trail 12", "Export", o => o.ScrubText(new Regex("path"), "route")));

        Assert.Equal("route 12", File.ReadAllText(Path.Combine(Folder, "Export.received.txt")));
    }

    [Fact]
    public async Task VerifyJsonAsync_ShouldLetTheDefaultsKeepGuids_WhenDefaultOptionsSayTo()
    {
        Snapshot.DefaultTextOptions = options => options.KeepGuids();

        await Assert.ThrowsAsync<SnapshotException>(
            () => Snapshot.VerifyJsonAsync("""{"id":"0f8fad5b-d9cb-469f-a165-70867728950e"}""", "Export"));

        var received = File.ReadAllText(Path.Combine(Folder, "Export.received.json"));
        Assert.Contains("0f8fad5b-d9cb-469f-a165-70867728950e", received);
    }

    [Fact]
    public async Task VerifyTextAsync_ShouldAddTheVariantToTheName_WhenGivenOne()
    {
        await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyTextAsync("one", variant: "Ridgeline"));
        await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyTextAsync("two", "Export", variant: "Cedars"));

        var method = nameof(VerifyTextAsync_ShouldAddTheVariantToTheName_WhenGivenOne);
        Assert.True(File.Exists(Path.Combine(Folder, $"{nameof(SnapshotTests)}.{method}.Ridgeline.received.txt")));
        Assert.True(File.Exists(Path.Combine(Folder, "Export.Cedars.received.txt")));
    }

    [Theory]
    [InlineData("Export")]
    [InlineData("export")]
    public async Task VerifyTextAsync_ShouldSayTheNameIsTaken_WhenAnotherCallInThisRunGaveItDifferentContent(
        string secondName)
    {
        Environment.SetEnvironmentVariable(Snapshot.AcceptVariable, "1");
        await Snapshot.VerifyTextAsync("first", "Export");

        var error = await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyTextAsync("second", secondName));

        Assert.StartsWith($"Snapshot '{secondName}' was already verified in this run with different content", error.Message);
        Assert.Equal("first", File.ReadAllText(Path.Combine(Folder, "Export.verified.txt")));
    }

    [Fact]
    public async Task VerifyTextAsync_ShouldPass_WhenAnotherCallInThisRunGaveTheNameTheSameContent()
    {
        Environment.SetEnvironmentVariable(Snapshot.AcceptVariable, "1");
        await Snapshot.VerifyTextAsync("same", "Export");
        Environment.SetEnvironmentVariable(Snapshot.AcceptVariable, null);

        await Snapshot.VerifyTextAsync("same", "Export");
    }

    [Fact]
    public async Task VerifyTextAsync_ShouldScrubTheWholeText_WhenAskedTo()
    {
        var text = "Permit 1234 for 0f8fad5b-d9cb-469f-a165-70867728950e on 2026-10-28\n";

        await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyTextAsync(text, "Permit",
            o => o.ScrubText(new Regex(@"Permit \d+"), "Permit N").ScrubGuids().ScrubDates()));

        Assert.Equal("Permit N for Guid_1 on Date_1\n", File.ReadAllText(Path.Combine(Folder, "Permit.received.txt")));
    }

    [Fact]
    public async Task VerifyTextAsync_ShouldOverwriteTheVerifiedFile_WhenAcceptModeIsOn()
    {
        Environment.SetEnvironmentVariable(Snapshot.AcceptVariable, "1");

        await Snapshot.VerifyTextAsync("accepted\n", "Export");

        Assert.Equal("accepted\n", File.ReadAllText(Path.Combine(Folder, "Export.verified.txt")));
    }

    [Fact]
    public async Task VerifyJsonAsync_ShouldIndentWithKeysInTheOrderWritten_WhenGivenCompactJson()
    {
        await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyJsonAsync("""{"b":1.50,"a":["é","<x>"]}""", "Job"));

        var expected = "{\n  \"b\": 1.50,\n  \"a\": [\n    \"é\",\n    \"<x>\"\n  ]\n}\n";
        Assert.Equal(expected, File.ReadAllText(Path.Combine(Folder, "Job.received.json")));
    }

    [Fact]
    public async Task VerifyJsonAsync_ShouldScrubGuidsAndDates_WhenNotToldToKeepThem()
    {
        var guid = "0f8fad5b-d9cb-469f-a165-70867728950e";
        var json = $$"""{"Id":"{{guid}}","ParentId":"{{guid}}","Created":"2026-10-28T09:15:00Z"}""";

        await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyJsonAsync(json, "Job"));

        var expected = "{\n  \"Id\": \"Guid_1\",\n  \"ParentId\": \"Guid_1\",\n  \"Created\": \"Date_1\"\n}\n";
        Assert.Equal(expected, File.ReadAllText(Path.Combine(Folder, "Job.received.json")));
    }

    [Fact]
    public async Task VerifyJsonAsync_ShouldKeepADate_WhenItIsWrittenForPeopleOrPartOfLongerText()
    {
        var json = """{"Date":"January 12, 2022","Text":"Sent 2020-11-14 to 0f8fad5b-d9cb-469f-a165-70867728950e"}""";

        await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyJsonAsync(json, "Guide"));

        var expected = "{\n  \"Date\": \"January 12, 2022\",\n  \"Text\": \"Sent 2020-11-14 to 0f8fad5b-d9cb-469f-a165-70867728950e\"\n}\n";
        Assert.Equal(expected, File.ReadAllText(Path.Combine(Folder, "Guide.received.json")));
    }

    [Fact]
    public async Task VerifyJsonAsync_ShouldKeepADate_WhenItFollowsAQuoteMarkInsideText()
    {
        await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyJsonAsync("""{"Text":"He wrote \"2020-11-14"}""", "Guide"));

        Assert.Contains("2020-11-14", File.ReadAllText(Path.Combine(Folder, "Guide.received.json")));
    }

    [Fact]
    public async Task VerifyJsonAsync_ShouldScrubDatesAndGuidsInsideText_WhenToldToScrubThem()
    {
        var guid = "0f8fad5b-d9cb-469f-a165-70867728950e";
        var json = $$"""{"Created":"2020-11-14","Text":"Sent 2020-11-14 to {{guid}}"}""";

        await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyJsonAsync(json, "Guide", o => o.ScrubDates().ScrubGuids()));

        var expected = "{\n  \"Created\": \"Date_1\",\n  \"Text\": \"Sent Date_1 to Guid_1\"\n}\n";
        Assert.Equal(expected, File.ReadAllText(Path.Combine(Folder, "Guide.received.json")));
    }

    [Fact]
    public async Task VerifyJsonAsync_ShouldLeaveGuidsAndDates_WhenToldToKeepThem()
    {
        var json = """{"Id":"0f8fad5b-d9cb-469f-a165-70867728950e","Created":"2026-10-28"}""";

        await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyJsonAsync(json, "Job", o => o.KeepGuids().KeepDates()));

        Assert.Contains("0f8fad5b-d9cb-469f-a165-70867728950e", File.ReadAllText(Path.Combine(Folder, "Job.received.json")));
        Assert.Contains("2026-10-28", File.ReadAllText(Path.Combine(Folder, "Job.received.json")));
    }

    [Fact]
    public async Task VerifyJsonAsync_ShouldThrowNamingTheSnapshotAndWriteNothing_WhenTheJsonIsInvalid()
    {
        var error = await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyJsonAsync("{\"a\": ", "Job"));

        Assert.StartsWith("Job: the text isn't valid JSON.", error.Message);
        Assert.False(Directory.Exists(Folder));
    }
}
