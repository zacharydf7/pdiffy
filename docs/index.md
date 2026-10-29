---
_layout: landing
---

<div class="hero">
  <img src="images/logo.svg" alt="" width="96" height="96">
  <div>
    <h1>PDiffy</h1>
    <p class="lead">Document-aware testing for PDF and Word files in .NET. Semantic snapshots, comparisons and checks that look at what a document says, not how the file happens to be encoded.</p>
    <p>
      <a class="btn btn-primary" href="guide/getting-started.md">Get started</a>
      <a class="btn btn-outline-secondary" href="api/index.md">API reference</a>
      <a class="btn btn-outline-secondary" href="https://www.nuget.org/packages/PDiffy">NuGet</a>
    </p>
  </div>
</div>

PDiffy reads a PDF and writes a plain-text snapshot of everything a reader, a screen reader or a viewer gets from it:
the tag structure, text, alt text, reading order, language, tables, links, bookmarks, fonts, form fields, document
settings, and the position and style of everything drawn on each page. A test passes when the snapshot matches the
approved one. When it doesn't, the failure reads like a change to the document.

| Use it for | With | Learn more |
|---|---|---|
| **Snapshot tests**: approve a document once, then see exactly what changed in it | `PdfSnapshot.VerifyAsync`, `DocxSnapshot.VerifyAsync` | [PDF snapshots](guide/pdf-snapshots.md) |
| **Comparisons**: compare two documents directly, with no snapshot files | `CompareTo`, `pdiffy before.pdf after.pdf` | [Compare two documents](guide/comparing.md) |
| **Checks**: assert on alt text, forms, fonts or settings without knowing PDF internals | `PdfUaDocument`, `DocxDocument` | [Check documents directly](guide/checking-documents.md) |

```csharp
[Fact]
public async Task DrugGuide_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateDrugGuideAsync();

    await PdfSnapshot.VerifyAsync(pdf);
}
```

```text
3 change(s): 1 moved, 1 attribute, 1 text
by section: structure 3
2 can affect accessibility, listed first
↕ moved structure > Document > Figure (p1) [accessibility]
    reading position 3 -> 10
~ attribute structure > Document > Table > TR[1] > TH2 (p2) [accessibility]
    scope: Column -> (missing)
~ text structure > Document > Table > TR[2] > TD[2] (p2)
    "Take [-with-] {+after+} food"
```

A failing test also writes a side-by-side HTML report. Open the
[sample report](samples/DrugGuide.diff.html) to see one.

## Why not compare bytes

Byte-for-byte PDF snapshots fail for reasons that don't matter, and explain nothing when they do:

- Any change to how the file is written (compression, object order, font subsetting, a library upgrade) fails every
  test, even when the content is identical.
- The same output differs between operating systems, so every test needs one snapshot per OS.
- A failure only says the bytes differ. Finding out what changed means opening both files in a PDF checker.

PDiffy records the content and leaves out the encoding. One snapshot covers Windows and Linux, and a failing test shows
which element changed, on which page.

## Find what you need

| I want to... | Go to |
|---|---|
| Add PDiffy to a test project and write the first test | [Getting started](guide/getting-started.md) |
| Snapshot a PDF and understand a failure | [PDF snapshots](guide/pdf-snapshots.md) |
| Snapshot a Word document | [Word documents](guide/word-documents.md) |
| Snapshot plain text or JSON | [Text and JSON](guide/text-and-json.md) |
| Leave out dates, IDs, layout or whole sections | [Ignore and scrub](guide/options.md) |
| Compare two documents without snapshot files | [Compare two documents](guide/comparing.md) |
| Assert on alt text, forms, fonts or settings directly | [Check documents directly](guide/checking-documents.md) |
| Run snapshot tests in GitHub Actions or another CI | [Continuous integration](guide/ci.md) |
| Move existing Verify snapshots over | [Moving from Verify](guide/migrating-from-verify.md) |
| Look up a command-line option | [Command line](reference/command-line.md) |
| Read a snapshot line by line | [PDF format](reference/pdf-snapshot-format.md), [Word format](reference/docx-snapshot-format.md) |
| See every class, property and option | [API reference](api/index.md) |

## Packages

| Package | What it is | Install |
|---|---|---|
| [PDiffy](https://www.nuget.org/packages/PDiffy) | The library for your test project | `dotnet add package PDiffy` |
| [PDiffy.Cli](https://www.nuget.org/packages/PDiffy.Cli) | The `pdiffy` command-line tool | `dotnet tool install --global PDiffy.Cli` |

Both target .NET 10 and are fully managed, with no native binaries, so a snapshot made on Windows matches one made on
Linux. They work with any test framework: the examples on this site use xUnit.
