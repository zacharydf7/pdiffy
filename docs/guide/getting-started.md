# Getting started

This page takes you from an empty test project to an approved snapshot in about five minutes.

## Requirements

- .NET 10 SDK.
- Any test framework. The examples use xUnit; NUnit and MSTest work the same way.
- Git, if you want snapshot changes to show up in code review (recommended).

## 1. Install

Add the library to your test project:

```text
dotnet add package PDiffy
```

Optionally, install the command-line tool, used to accept snapshots, compare files and summarize a CI run:

```text
dotnet tool install --global PDiffy.Cli
```

## 2. Write a test

`PdfSnapshot.VerifyAsync` takes the PDF as a stream, prints its snapshot and compares it with the approved one.

```csharp
using PDiffy;

public class TrailGuideTests
{
    [Fact]
    public async Task TrailGuide_ShouldMatchApprovedSnapshot_WhenGenerated()
    {
        // Your code that produces a PDF. A FileStream works too.
        using Stream pdf = await ReportGenerator.CreateTrailGuideAsync();

        await PdfSnapshot.VerifyAsync(pdf);
    }
}
```

The snapshot is named after the test file and method, and sits next to the test file:
`TrailGuideTests.TrailGuide_ShouldMatchApprovedSnapshot_WhenGenerated.verified.pdfua.txt`.

## 3. Run it and approve the snapshot

The first run has nothing to compare with, so it fails with "New PDF snapshot" and writes what it got to a
`.received.pdfua.txt` file. Open it, check that it describes your document, then accept it in any of these ways:

- Run `pdiffy accept --all` to accept every received snapshot under the current folder.
- Run the tests in accept mode: `PDIFFY_ACCEPT=1 dotnet test`, or in PowerShell `$env:PDIFFY_ACCEPT=1; dotnet test`.
- Rename the `.received.pdfua.txt` file to `.verified.pdfua.txt` by hand.

Run the tests again: they pass. Commit the `.verified.pdfua.txt` file. From now on, any change to what the document
says fails the test, and accepting it shows up in code review as a diff of that file.

> [!NOTE]
> Accept mode is ignored on a build server (when `CI` or `TF_BUILD` is set), so a pipeline can never approve its own
> snapshots.

## 4. Set up the repository

Add these lines to `.gitattributes`, so snapshots keep LF line endings on every OS:

```text
*.verified.* text eol=lf
*.received.* text eol=lf
```

Add these to `.gitignore`, since they only exist while a test is failing:

```text
*.received.*
*.diff.html
```

## What happens on each run

| Situation | PDiffy does | Test |
|---|---|---|
| No verified file yet | Writes the received file | Fails: "New PDF snapshot" |
| Snapshot differs | Writes the received file and an HTML report (`.diff.html`) | Fails with a summary of what changed and where |
| Snapshot matches | Deletes any received file or report left from an earlier run | Passes |
| `PDIFFY_ACCEPT=1` is set | Writes the snapshot as the new verified file | Passes |
| `PDIFFY_ACCEPT=1` is set on a build server | Ignores it | Fails as usual |

## Keep snapshots in one folder

Snapshots sit next to the test file by default. To keep them all in one folder, set it once, for example in a module
initializer:

```csharp
using System.Runtime.CompilerServices;
using PDiffy;

static class SnapshotSetup
{
    [ModuleInitializer]
    public static void Initialize() =>
        Snapshot.Directory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Snapshots");
}
```

> [!IMPORTANT]
> Set `Snapshot.Directory` if your build maps source paths (deterministic builds, common on CI). The test file's path
> then doesn't exist on disk, and PDiffy says so in the failure.

## Without a snapshot file

Everything PDiffy reads is also available as properties, so you can assert on the parts you care about:

```csharp
PdfUaDocument document = PdfUaDocument.Load("trail-guide.pdf");

Assert.True(document.Document.Marked);
Assert.Equal("en-US", document.Document.Lang);
Assert.All(document.Elements(StructureType.Figure), figure => Assert.False(string.IsNullOrWhiteSpace(figure.Alt)));

// The same text VerifyAsync compares.
string snapshot = document.ToSnapshot();
```

## Next steps

- [PDF snapshots](pdf-snapshots.md): reading a failure, the HTML report, naming and folders.
- [Ignore and scrub](options.md): leave out dates, IDs, layout or whole sections.
- [Continuous integration](ci.md): keep the received files and reports from a failed run.
