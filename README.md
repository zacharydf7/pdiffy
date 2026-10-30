# PDiffy

Document-aware testing for PDF and Word files in .NET. PDiffy reads what a document says (its structure, text,
accessibility and layout) and gives you semantic snapshots, comparisons and checks that ignore how the file happens to
be encoded.

**Documentation:** [zacharydf7.github.io/pdiffy](https://zacharydf7.github.io/pdiffy/): guides, the command line, the
snapshot formats and the full API reference.

PDiffy reads a PDF and writes a plain-text snapshot of everything a reader, a screen reader or a viewer gets from it: the tag structure, text, alt text, reading order, language, tables, links, bookmarks, artifacts, untagged content, fonts, form fields, document settings, and the position and style of everything drawn on each page. A test passes when the snapshot matches the approved one exactly. When it doesn't, the difference reads like a change to the document.

The same reading of the document supports three kinds of test:

- **Snapshot tests.** `PdfSnapshot.VerifyAsync` compares a document with its approved snapshot and explains any
  difference element by element, on the page where it happened.
- **Comparisons.** `CompareTo` and `pdiffy before.pdf after.pdf` compare two documents directly, with no snapshot files.
- **Checks.** Everything in a snapshot is also a property to assert on, such as each figure's alt text, each form
  field's name or each font's Unicode mapping.

The same files and workflow also work for Word documents (see [Snapshot Word documents](#snapshot-word-documents)) and for text and JSON output (see [Snapshot text and JSON](#snapshot-text-and-json)), so a test project can keep every kind of snapshot in one place.

## Getting started

PDiffy targets .NET 10. Add it to your test project from NuGet:

```
dotnet add package PDiffy
```

Everything starts with `PdfUaDocument.Load`, which reads the whole PDF once. From there you can either print a snapshot or check the parts you care about directly.

```csharp
using PDiffy;

// Read the PDF. Load also takes a Stream, so you can pass your generator's output without saving it first.
PdfUaDocument document = PdfUaDocument.Load("trail-guide.pdf");

// The snapshot is a string: one line per element, in a fixed order, the same on every OS.
string snapshot = document.ToSnapshot();

// Everything in the snapshot is also available as properties, for asserting on directly.
Assert.Equal("en-US", document.Document.Lang);
```

## Examples

The examples below are xUnit tests, but PDiffy works with any test framework. `ReportGenerator` stands in for whatever produces your PDFs.

`StructureType`, `ContentRole`, `TextRenderMode`, `AnnotationFlags`, `FormFieldType`, `FormFieldFlags`, `DocumentPermissions`, `PageModes` and `PageLayouts` list every option, so no PDF knowledge is needed to filter or assert on the model.

### Approve a snapshot and compare against it

The core workflow is one call. `PdfSnapshot.VerifyAsync` prints the snapshot and compares it with the approved one, a `.verified.pdfua.txt` file you check in. The first run has nothing to compare with, so it fails and writes what it got to a `.received.pdfua.txt` file for you to review.

```csharp
[Fact]
public async Task TrailGuide_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    // Generate the PDF in memory.
    using Stream pdf = await ReportGenerator.CreateTrailGuideAsync();

    // Compare it with TrailGuideTests.TrailGuide_ShouldMatchApprovedSnapshot_WhenGenerated.verified.pdfua.txt,
    // named after this test file and method. A difference throws SnapshotException saying what changed.
    await PdfSnapshot.VerifyAsync(pdf);
}
```

Every `VerifyAsync` and `VerifyJsonAsync` has a synchronous `Verify` and `VerifyJson` with the same parameters, for a test that isn't async. `VerifyAsync` does its work before it returns, so a failure throws at the call even if a `void` test forgets the `await`.

A failure explains itself. Elements are matched one to one, so an edited paragraph is one change, not a removed line and an added one, and an element that moved in reading order, or to another parent, is reported as moved, along with anything that changed on it. A structure element whose type changed but whose text didn't, such as a heading going from H2 to H3 or to P, is one change (`type: H2 -> H3`). A paragraph replaced by one with entirely different words is reported as removed and added, but a one-word value replaced on an otherwise unchanged line, such as a number in a table cell, is reported as changed:

```
3 change(s): 1 moved, 1 attribute, 1 text
by section: structure 3
2 can affect accessibility, listed first
↕ moved structure > Document > Figure (p1) [accessibility]
    reading position 3 -> 10
~ attribute structure > Document > Table > TR[1] > TH2 (p2) [accessibility]
    scope: Column -> (missing)
~ text structure > Document > Table > TR[2] > TD[2] (p2)
    "Go [-with-] {+without+} a guide"
```

When pages are inserted or removed, the shift is one change (`p2 inserted; p2-10 -> p3-11`) and each element is compared with the page it lines up with, so a new cover page doesn't report every later element as changed. Within a page, layout lines one after another that all moved the same way, such as everything below an inserted paragraph, are listed as one shift (`~ shifted layout > p1[4] to p1[31] (p1)`, `28 lines down 16 pt`); a line whose color, font or text also changed still lists that change. `Changes`, the JSON and the report keep every line. The second line counts the changes in each section, so a reflow that moves 200 layout lines doesn't hide one change to the structure. Changes that can affect what a screen reader gets come first, tagged `[accessibility]` (see [Changes that affect accessibility](#changes-that-affect-accessibility)).

What happens on each run:

| Situation | PDiffy does | Test |
|---|---|---|
| No verified file yet | Writes the received file | Fails: "New PDF snapshot", with its size by section (`4 pages; 1,362 lines: layout 1,240 (91%), structure 58, ...`) and, when layout is most of it, a hint about the `Content` and `Accessibility` profiles |
| Snapshot differs | Writes the received file and an HTML report (`.diff.html`) | Fails with a summary of what changed and where |
| Snapshot matches | Deletes any received file or report left from an earlier run | Passes |
| `PDIFFY_ACCEPT=1` is set | Writes the snapshot as the new verified file | Passes |
| `PDIFFY_ACCEPT=1` is set on a build server (`CI` or `TF_BUILD` is set) | Ignores it | Fails as usual |
| Snapshot differs and `PDIFFY_OPEN_REPORT=1` is set, not on a build server | Also opens the report in the default browser | Fails as usual |

To accept a change, review the received file, then copy it over the verified one, run the tests again with `PDIFFY_ACCEPT=1`, or run `pdiffy accept` (see [Command line](#command-line)); a failing test prints the `dotnet pdiffy accept` command for its received file, ready to paste. Either way the change shows up in code review as a diff of the `.verified.pdfua.txt` file.

Snapshot files sit next to the test file by default. To keep them all in one folder, set it once, for example in a module initializer. `Snapshot.ProjectFolder` gives a folder in the test project, which PDiffy's build file records when the project builds, so it's right whatever the output folder or runtime, as long as the tests run in the checkout that built them:

```csharp
// Every snapshot, of any kind, goes to and is read from this folder.
Snapshot.Directory = Snapshot.ProjectFolder("Snapshots");
```

If your build maps source paths (deterministic builds, common on CI), the path the compiler records for a test file, such as `/_/tests/...`, isn't on disk. PDiffy then finds the test file's folder in the test project it recorded, so snapshots next to test files still work. A new snapshot on a build server (`CI` or `TF_BUILD` set) asks whether the verified file was committed, since it was probably expected.

A suite whose tests share options, such as a profile or scrubbers, can set them once beside the folder, with `PdfSnapshot.DefaultOptions`, `DocxSnapshot.DefaultOptions` or `Snapshot.DefaultOptions` for text and JSON. Each call's own `configure` runs after them and adds to them. A PDF or Word call can leave out more but can't bring back what the defaults leave out (its `Strict` profile doesn't undo a default one), so keep the defaults to what every test agrees on and set the rest in each test. A text or JSON call's `KeepDates()` or `KeepGuids()` does undo a default scrub. A PDF or Word snapshot's first lines list what's ignored, but not what's scrubbed.

```csharp
PdfSnapshot.DefaultOptions = options => options.Use(SnapshotProfile.Accessibility).ScrubDates();
```

`VerifyAsync` also takes the document as a `byte[]`, or one you've already loaded with `PdfUaDocument.Load`, so a test that asserts on the document and snapshots it reads it once. A stream is read from its start wherever the generator left it, and its position is put back afterwards; one that can't seek, such as a network response, is copied first. An empty stream, a Word document passed as a PDF, or a web page (a generator's error page, say) fails with an `InvalidDataException` that says so.

A test whose snapshots belong somewhere else passes the folder: `await PdfSnapshot.VerifyAsync(pdf, directory: otherFolder)`.

When one test makes several snapshots, as a theory does, give each a variant, which is added to the default name (`TrailMapTests.TrailMap_ShouldMatch.ridgeline`): `await PdfSnapshot.VerifyAsync(pdf, variant: trail)`. Or pass a whole name: `await PdfSnapshot.VerifyAsync(pdf, $"TrailMap.{trail}")`. Two calls in one run that give the same snapshot different content fail, saying the name is taken, instead of overwriting each other's file. A helper that verifies for many tests takes the caller's details and passes them on, so each test keeps its own name:

```csharp
public static Task VerifyTrailMapAsync(string trail, [CallerFilePath] string testFile = "",
    [CallerMemberName] string testName = "") =>
    PdfSnapshot.VerifyAsync(TrailMaps.Create(trail), testFile: testFile, testName: testName, variant: trail);
```

Snapshots use LF line endings. If Git converts line endings on checkout, add `*.verified.txt text eol=lf` and `*.verified.json text eol=lf` (plus a line for any custom extension, such as `*.verified.csv`) to `.gitattributes`; PDiffy also compares a CRLF verified file as LF. Add `*.received.*` and `*.diff.html` to `.gitignore` and to your build server's artifacts, so a failed CI run keeps what it received and the report.

The report is one HTML file with nothing to download, so it opens straight from a build artifact. It lists every change, accessibility changes first (filterable by kind, or to only those that affect accessibility), links to each page, and shows each section with the verified snapshot on the left and the received one on the right, edited lines side by side with the changed words highlighted. For each page with a layout change, it also draws a map of where things are, from the layout lines alone: a dashed outline where something was and a filled box where it is now (a sketch of positions, not a picture of the page). `diff.ToHtml()` gives the same page for any `SnapshotDiff`. See a [sample report](https://zacharydf7.github.io/pdiffy/samples/TrailGuide.diff.html).

### Snapshot text and JSON

`Snapshot.VerifyAsync` and `Snapshot.VerifyJsonAsync` check other output the same way, with the same received and verified files, accept mode and report.

```csharp
[Fact]
public async Task Export_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    string csv = await TrailExporter.CreateCsvAsync();

    // Compared exactly, spaces included; only CRLF line endings are read as LF. Saved as {name}.verified.txt.
    await Snapshot.VerifyAsync(csv);
}

[Fact]
public async Task Job_ShouldMatchApprovedSnapshot_WhenSubmitted()
{
    string json = await client.GetStringAsync("/jobs/latest");

    // Indented with two spaces, keys kept in the order written, id and timestamp values replaced by Guid_1 and Date_1.
    // Saved as {name}.verified.json.
    await Snapshot.VerifyJsonAsync(json);
}
```

A text failure lists each changed line by number, with the changed words marked:

```
2 change(s): 1 removed, 1 text
~ text line 3
    [-"b": 2,-] {+"c": 4+}
- removed line 4
      "c": 3
```

Text options scrub the whole text: `o => o.ScrubText(new Regex(@"Permit \d+"), "Permit N").ScrubDates()`. JSON scrubs strings (values or keys) that are a GUID or an ISO 8601 timestamp (`"2026-10-28T09:15:00Z"`), but leaves dates written for people, such as a guide's "November 14, 2020", and anything inside longer text, because that is content. Turn either off with `o => o.KeepGuids()` or `o => o.KeepDates()`; to scrub ids or dates inside text too, add `o => o.ScrubGuids()` or `o => o.ScrubDates()`. Pass `extension: "csv"` to name text files something other than `.txt`. A verified file that starts with a UTF-8 byte-order mark matches as if it didn't, so files approved by other tools carry over.

### Snapshot Word documents

`DocxSnapshot.VerifyAsync` does the same for DOCX files, with snapshots in `.verified.docx.txt` files. It reads the file directly, with no Word or Open XML SDK needed.

```csharp
[Fact]
public async Task Notes_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    using Stream docx = await NotesGenerator.CreateAsync();

    await DocxSnapshot.VerifyAsync(docx);
}
```

A DOCX snapshot reads like the document: one line per paragraph with its runs merged, and only what differs from its style listed.

```
# docx-snapshot v1
document
  title="Trail notes" lang=en-US compat=15 track-changes=no protection=none even-odd-headers=no created=<present> modified=<present>
sections
  s1 letter margins=72,72,72,72 header=36 footer=36 columns=1 break=nextPage headers=default
styles
  ListParagraph paragraph "List Paragraph" font="Arial" size=12 indent-left=36
  TableGrid table "Table Grid"
numbering
  list 1 definition=3
    level 0 format=decimal text="%1." start=1 indent=36 hanging=18
body
  P style=ListParagraph list=1:0 label="1." "Bring a trail map"
    span "trail" bold
  Table style=TableGrid widths=144,216
    Row header
      Cell
        P "Trail"
      Cell
        P "Length"
  P "Visit example.com"
    link "example.com" -> https://example.com at=6
  P
    image inline size=120x40 alt=(missing) type=png hash=3f9c0a1b2d4e5f60 at=0
headers
  s1 default
    P "Page 3"
      field "PAGE" result="3" at=5
footers
  (none)
notes
  (none)
```

The snapshot records the document settings, each section's page setup, the styles in use with their properties resolved through the styles they're based on, list labels as Word numbers them, paragraph layout and text formatting, tables (grid, header rows, merged cells, shading, the widths, cell margins and borders of tables and cells, a table's alt text, row heights, cell alignment and text direction, and the content controls around rows and cells), images and charts with their alt text and a hash of the image, text boxes (their paragraphs nested under the shape), equations (their text and a hash of how they're built), embedded objects and ActiveX controls (their program or name and a hash of their data, or the file a linked object shows), links, bookmarks (also those between paragraphs, tables, rows and cells), symbols, fields with their results, form controls, tracked changes (to text, paragraphs and their marks, tables, rows, cells and section settings), headers and footers, footnotes, endnotes and comments, and inserted documents. Relationship ids, editing-session ids and the zip's layout are left out, so regenerating the same document gives the same snapshot, and so are styles no paragraph, run or table uses, since they can't change the document.

Anything in a paragraph, or between paragraphs, tables, rows and cells, that PDiffy doesn't read yet, such as a phonetic guide (`w:ruby`), a date or page-number element (`w:dayShort`, `w:pgNum`) or a VML shape that isn't a `v:shape` (such as `v:rect` or `v:oval`), prints as `unsupported <element> hash=...`, so a change to it still fails the test.

`DocxDocument.Load` gives the same content as properties, and `CompareTo` compares two documents. Options leave out sections, leave out one detail everywhere (see [What you can ignore](#what-you-can-ignore)) or scrub text: `o => o.Ignore(DocxSection.Styles).Ignore(DocxDetail.FieldResults).ScrubDates()`.

```csharp
DocxDocument notes = DocxDocument.Load("notes.docx");
Assert.All(notes.Images(), image => Assert.NotNull(image.AltText));
Assert.Empty(notes.Revisions());
```

### Use a profile for what a test guards

Most tests guard one thing: the content, its accessibility or how the page looks. A profile leaves out everything else in one call, and further `Ignore` calls leave out more:

```csharp
await PdfSnapshot.VerifyAsync(pdf, configure: options => options.Use(SnapshotProfile.Accessibility));
```

| SnapshotProfile | For tests that guard | Leaves out |
|---|---|---|
| `Strict` | Everything | Nothing, the same as no profile |
| `Content` | The text and its structure: tags, reading order, bookmarks, annotations and forms | The `Settings`, `Metadata`, `Fonts` and `Layout` sections, `PageBoxes` and `Positions` |
| `Accessibility` | What assistive technology uses: tags, alt text, language, reading order, untagged content, artifacts, annotations, forms, whether fonts map to Unicode, and permissions, which can stop a screen reader from reading the text | How content looks (`PageBoxes`, `Positions`, `Colors`, `FontNames`, `FontSizes`, `LineWidths`, `ImageContents`), settings that don't change what's read out (`PdfVersion`, `OpenAction`, `EmbeddedFileContents`) and metadata other than the title (`Author`, `Subject`, `Keywords`, `Dates`, `Producer`, `Creator`, `CustomInfo`) |
| `Layout` | Everything on the page, including where it's drawn and how it looks | The `Settings` and `Metadata` sections |

For Word documents, use `DocxProfile`:

| DocxProfile | For tests that guard | Leaves out |
|---|---|---|
| `Strict` | Everything | Nothing, the same as no profile |
| `Content` | The text and its structure: paragraphs, tables, lists, headers, footers, notes and inline formatting such as bold | The `Sections` and `Styles` sections, and `Fonts`, `FontSizes`, `Colors`, `Spacing` and `Indents` |

Everything a profile doesn't leave out is compared. The snapshot's header lists the sections and fields a profile leaves out, not the profile's name. If a later release changes what a profile leaves out, the header changes too, so the test fails and says so instead of quietly checking less.

### Leave out sections a test doesn't care about

Some tests only care about part of the document. Pass the sections to leave out, and they print as `(ignored)` instead of their contents. The snapshot's second line lists what was ignored, so nobody reading it mistakes a missing section for an empty one.

```csharp
[Fact]
public async Task TrailMapTemplate_ShouldKeepItsTagStructure_WhenStylingChanges()
{
    using Stream pdf = await ReportGenerator.CreateTrailMapAsync();

    // This test guards the tag tree and the text, so leave out the metadata, the document settings
    // and the layout (positions, fonts and colors), which the design team changes freely.
    await PdfSnapshot.VerifyAsync(pdf, configure: options => options
        .Ignore(Section.Metadata, Section.Settings, Section.Layout));
}
```

The snapshot then starts like this:

```
# pdfua-snapshot v1
# ignored sections: settings, metadata, layout
document
  marked=yes lang=en-US display-doc-title=yes pages=2
settings
  (ignored)
metadata
  (ignored)
...
```

### Leave out a single detail everywhere it appears

For something narrower than a section, ignore a `Field`. It's left out everywhere the table under [What you can ignore](#what-you-can-ignore) lists, and the rest of each line is still compared.

```csharp
[Fact]
public async Task SeasonalGuide_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateSeasonalGuideAsync(month: 6);

    await PdfSnapshot.VerifyAsync(pdf, configure: options => options
        // The title includes the month, so it changes every time.
        .Ignore(Field.Title)
        // Text reflows when the guide has more rows, so where things land doesn't matter here.
        // Fonts, sizes and colors are still compared.
        .Ignore(Field.Positions));
}
```

With `Field.Positions` ignored, a layout line such as `p1 text tagged at=72,720 font=Arial size=18 color=gray:0 "Trails"` becomes `p1 text tagged font=Arial size=18 color=gray:0 "Trails"`.

### Allow small numeric differences

Positions are rounded to whole points, and font sizes and line widths to 0.1 pt. A value close to a rounding boundary can flip after a font or library update: text at 72.49 pt prints `at=72,...`, and moved by 0.02 pt it prints `at=73,...`. Ignoring `Field.Positions` would also hide a heading that moved 40 pt, so tolerate the noise instead:

```csharp
await PdfSnapshot.VerifyAsync(pdf, configure: options => options
    .Tolerate(Field.Positions, points: 1)
    .Tolerate(Field.FontSizes, points: 0.5));
```

A number that differs from the verified one by no more than its tolerance doesn't fail the test, and the verified file stays as it is, even in accept mode, so the baseline doesn't creep a point at a time. `Positions` covers where text, images, paths and annotations are, the size of images, paths and annotations, and the positions a link or bookmark opens its page at, `FontSizes` a text run's size, and `LineWidths` a path's width. The snapshot's header lists each tolerance (`# tolerances: positions 1pt, font-sizes 0.5pt`), so a reader knows its numbers are approximate.

### Scrub values that change on every run

A generated document often prints today's date, a permit number or a GUID. Scrubbers replace those inside quoted text with a stable placeholder, so the rest of the text is still compared.

```csharp
[Fact]
public async Task TrailPass_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateTrailPassAsync();

    await PdfSnapshot.VerifyAsync(pdf, configure: options => options
        // "Printed 10/27/2026" becomes "Printed Date_1". The same text always gets the same number,
        // so the snapshot still shows that the print date and the issue date match.
        .ScrubDates()
        // Booking IDs become Guid_1, Guid_2 and so on.
        .ScrubGuids()
        // Anything else that changes, with your own pattern. "$1" keeps the first group.
        .ScrubText(new Regex(@"(Permit) #\d+"), "$1 #<number>"));
}
```

Scrubbers only touch quoted text: element text, alt text, titles, link addresses, form values, bookmark titles and so on. Tag types, keys and numbers are never changed.

### Snapshot only some pages

For a long document where a test is about one part, keep only those pages. A structure element or bookmark stays when it, or anything under it, is on one of them. The page count, page labels and fonts still describe the whole file, and asking for a page past the end throws.

```csharp
[Fact]
public async Task AnnualReport_ShouldKeepTheSummaryTable_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateAnnualReportAsync();

    // The summary table is on pages 3 and 4; the other 120 pages have their own tests.
    await PdfSnapshot.VerifyAsync(pdf, configure: options => options.OnlyPages(3, 4));
}
```

### Compare two PDFs

`CompareTo` gives the same element-by-element comparison for any two documents, without snapshot files. It suits one-off checks such as before and after a library upgrade, or a customer's file against yours.

```csharp
PdfUaDocument before = PdfUaDocument.Load("guide-v1.pdf");
PdfUaDocument after = PdfUaDocument.Load("guide-v2.pdf");

// The same options as a snapshot: here, only the tag structure and text matter.
SnapshotDiff diff = before.CompareTo(after, options => options.Ignore(Section.Layout, Section.Metadata));

// Each change has a kind (Added, Removed, Moved, AttributeChanged, TextChanged or Changed),
// the section, a path such as "structure > Document > Table > TR[2] > TD[1]", the page and a detail.
Assert.DoesNotContain(diff.Changes, change => change.Kind == ChangeKind.Moved);

// Or allow any change except those that can affect what a screen reader gets.
Assert.DoesNotContain(diff.Changes, change => change.AffectsAccessibility);

// Or print the summary a failing VerifyAsync shows.
Console.WriteLine(diff.Describe());
```

`SnapshotDiff.Compare(before, after)` does the same for two snapshot texts, such as two `.verified.pdfua.txt` files.

For scripts and tools outside .NET, `diff.ToJson()` (or `pdiffy before.pdf after.pdf --json`) gives the changes as JSON:

```json
{
  "version": 1,
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
```

- `kind` is a `ChangeKind` name.
- `section` is the section as the snapshot prints it, such as `structure` or `body`: `header` for the snapshot's first lines, `whitespace` when only blank lines or line endings differ, and `null` for text without sections, such as JSON.
- `page` is `null` when the change isn't on a page.
- `version` goes up only when a field is renamed, removed or changes meaning. New fields can be added without it.

### Catch a generator upgrade that breaks documents

Upgrading the library that writes your PDFs can break them while every page looks the same: a font loses its map to Unicode, so its text can't be searched or read aloud (`to-unicode: yes -> no`), a heading comes out as a paragraph (`type: H1 -> P`), or a post-processing step strips the tags. Snapshot tests catch these the first time the upgraded generator runs, accessibility changes first. To check every document it writes, generate them all before and after the upgrade and compare the folders: `dotnet pdiffy compare before after --html upgrade.html --profile accessibility`. The docs site walks through it in [Catch generator upgrades](https://zacharydf7.github.io/pdiffy/guide/generator-upgrades.html).

### Run the built-in checks

`Check` applies the rules generated documents most often break and lists what it finds, each with the element's path and page, so a failed test says which figure on which page:

```csharp
[Fact]
public async Task TrailMap_ShouldPassTheAccessibilityChecks_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateTrailMapAsync();

    IReadOnlyList<CheckFinding> findings = PdfUaDocument.Load(pdf)
        .Check(PdfCheck.DocumentLanguage, PdfCheck.FigureAltText, PdfCheck.UntaggedContent);

    // On failure: "FigureAltText: Figure on p2 has no alt text (structure > Document > Figure[2])".
    Assert.Empty(findings);
}
```

`Check()` with no arguments applies every check. Pick the ones a test guards; every finding counts the same. A finding's path names the snapshot section and the item, and for a structure element it's the path a snapshot diff shows.

| PdfCheck | Finds |
|---|---|
| `DocumentLanguage` | A document with no language |
| `DocumentTitle` | A document with no title, or one whose viewer shows the file name instead |
| `FigureAltText` | A figure with no alt text or replacement text |
| `UntaggedContent` | Text, images and paths that are neither tagged nor artifacts, page by page |
| `LinkDescriptions` | A link with no description: no contents, and no alt text or text on the element that tags it |
| `FormFieldNames` | A form field with no accessible name (tooltip) |
| `TableHeaders` | A header with no scope that no cell names, and a cell that names a header that doesn't exist |
| `HeadingLevels` | A heading that skips a level, or one that comes before any H1 |
| `FontsEmbedded` | A font that isn't embedded |
| `FontsMapToUnicode` | A font whose text uses codes that don't map to Unicode |
| `NoActions` | JavaScript, actions run on events such as closing or printing, and an open action that does more than go to a page |
| `NoEmbeddedFiles` | Each embedded file |

These are fast regression checks, not a PDF/UA validator. To check conformance, use [veraPDF](https://verapdf.org/) or PAC.

### Check accessibility rules directly

You don't need a snapshot to use PDiffy. The document model answers questions such as "does every image have alt text?" without any PDF knowledge: `StructureType` lists every tag type by name.

```csharp
[Fact]
public async Task TrailMap_ShouldBeReadableByScreenReaders_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateTrailMapAsync();
    PdfUaDocument document = PdfUaDocument.Load(pdf);

    // The document is tagged and declares a language, so a screen reader knows how to pronounce it.
    Assert.True(document.Document.Marked);
    Assert.Equal("en-US", document.Document.Lang);

    // Every figure (logo, chart, signature) has alt text describing it.
    // Elements also finds custom tag types that the PDF maps to Figure.
    Assert.All(document.Elements(StructureType.Figure), figure => Assert.False(string.IsNullOrWhiteSpace(figure.Alt)));

    // Every piece of text is either tagged or marked as decoration. Untagged text is invisible to a screen reader.
    Assert.DoesNotContain(document.TextRuns, run => run.Role == ContentRole.Untagged);

    // Every link has a description a screen reader can announce.
    Assert.All(document.Annotations.Where(annotation => annotation.Subtype == "Link"),
        link => Assert.False(string.IsNullOrWhiteSpace(link.Contents)));
}
```

### Walk the structure tree

`Elements(type)` throws for a type that's neither standard, nor in the role map, nor used in the document, with a hint such as "Did you mean 'Figure'?", so a typo can't make an `Assert.All` over no elements pass. Each element has its own text (`Text`), its text with its descendants' in order (`AllText`, such as a table cell's paragraph or a paragraph's link), the text runs its own marked content draws, with each one's font, size and color (`TextRuns`, such as for checking that every `H1` is 18 pt), its descendants (`Descendants()`, or `Elements(type)` for one type, which doesn't check the type, so use the `StructureType` constants) and its `Path`, the same path a check finding or a diff of whole-document snapshots shows. `Headings()` lists every heading with its level.

```csharp
PdfUaDocument document = PdfUaDocument.Load("trail-guide.pdf");

// Headings go down one level at a time, so a screen reader's outline has no gaps.
var levels = document.Headings().Select(heading => heading.Level ?? 1).ToList();
Assert.All(levels.Zip(levels.Skip(1)), pair => Assert.True(pair.Second <= pair.First + 1));

// Every table cell says something.
StructureElement table = document.Elements(StructureType.Table).Single();
Assert.All(table.Elements(StructureType.TableDataCell), cell => Assert.False(string.IsNullOrWhiteSpace(cell.AllText)));
```

### Check forms

Each form field has its full name, type, accessible name, value and flags. `FormFieldType` and `FormFieldFlags` list every option.

```csharp
[Fact]
public async Task EnrollmentForm_ShouldLabelAndRequireTheRightFields_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateEnrollmentFormAsync();
    PdfUaDocument document = PdfUaDocument.Load(pdf);

    // Every field has a name a screen reader announces (the "tooltip" in most PDF editors).
    Assert.All(document.FormFields, field => Assert.False(string.IsNullOrWhiteSpace(field.AccessibleName)));

    // Find the required fields. Names include their parents, joined with dots.
    var required = document.FormFields.Where(field => field.Flags.HasFlag(FormFieldFlags.Required)).Select(field => field.Name);
    Assert.Equal(["hiker.name", "hiker.dateOfBirth"], required);

    // The state picker is a drop-down with all 50 states.
    FormField state = document.FormFields.Single(field => field.Name == "hiker.address.state");
    Assert.Equal(FormFieldType.ComboBox, state.Type);
    Assert.Equal(50, state.Options.Count);
}
```

### Check document settings

`Settings` covers how the file opens and what it allows. It's where a library upgrade or a configuration change tends to show up first.

```csharp
[Fact]
public async Task ExportedReport_ShouldOpenSafely_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateReportAsync();
    DocumentSettings settings = PdfUaDocument.Load(pdf).Settings;

    // No scripts and no attached files.
    Assert.False(settings.HasJavaScript);
    Assert.Empty(settings.EmbeddedFiles);

    // If the file is ever encrypted, it must still let screen readers extract the text.
    // Permissions is null for an unencrypted file.
    Assert.True(settings.Permissions is null || settings.Permissions.Value.HasFlag(DocumentPermissions.ExtractForAccessibility));

    // Open with bookmarks showing, one page at a time in a continuous column.
    Assert.Equal(PageModes.UseOutlines, settings.PageMode);
    Assert.Equal(PageLayouts.OneColumn, settings.PageLayout);
}
```

### Check how text is drawn

The layout lists every text run, image and path with its position and style. Useful for rules that are hard to see by eye, such as hidden text or off-brand colors.

```csharp
[Fact]
public async Task Brochure_ShouldUseOnlyBrandFontsAndColors_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateBrochureAsync();
    PdfUaDocument document = PdfUaDocument.Load(pdf);

    // No invisible text. It usually means an OCR layer or text hidden behind an image.
    Assert.DoesNotContain(document.TextRuns, run => run.RenderMode == TextRenderMode.Invisible);

    // Only the two brand fonts. Subset prefixes such as "ABCDEF+" are already removed.
    Assert.All(document.TextRuns, run => Assert.Contains(run.Font, new[] { "OpenSans", "OpenSans-Bold" }));

    // Body text is black or brand navy. Colors are written as gray:0 (black), #rrggbb or cmyk:c,m,y,k.
    Assert.All(document.TextRuns.Where(run => run.Size <= 12), run => Assert.Contains(run.Color, new[] { "gray:0", "#1f3a5f" }));
}
```

## Command line

The `pdiffy` tool does the same outside of tests. Install it as a local tool in the repository, at the same version as the PDiffy package your tests use, so `pdiffy snapshot` reads documents exactly as your tests do. The manifest is checked in, and `dotnet tool restore` installs that version anywhere, a build server included; run it as `dotnet pdiffy` (the examples here write `pdiffy`, as a global install runs it). `pdiffy --version` prints the version and the snapshot formats it writes.

```
dotnet new tool-manifest
dotnet tool install PDiffy.Cli --version <the PDiffy package version>
```

A global install (`dotnet tool install --global PDiffy.Cli`) works too, but can drift from the package.

```
# Compare two PDFs, two DOCX files or two snapshot files: prints what changed, exits with 0 when nothing did and 1 when
# something did.
pdiffy guide-v1.pdf guide-v2.pdf

# The same, also writing the side-by-side HTML report.
pdiffy guide-v1.pdf guide-v2.pdf --html report.html

# The changes as JSON for a script: --json alone prints only the JSON, and --json with a file saves it there.
pdiffy guide-v1.pdf guide-v2.pdf --json
pdiffy guide-v1.pdf guide-v2.pdf --json changes.json

# Compare only what a profile guards: content, accessibility, layout or strict (the default).
pdiffy guide-v1.pdf guide-v2.pdf --profile content

# Compare two folders file by file, such as the documents two releases generate. Files pair by their path inside each
# folder; one line per file that changed, was added or removed, or can't be read. --html saves an index page with a
# report for each changed file, and --json and --profile work as above.
pdiffy compare release-1.4/out release-1.5/out
pdiffy compare release-1.4/out release-1.5/out --html compare/index.html

# Run every built-in check on a PDF: prints each finding, exits with 0 when there are none and 1 when there are.
pdiffy check guide.pdf

# Print a PDF's or DOCX's snapshot, or save it to a file.
pdiffy snapshot guide.pdf
pdiffy snapshot guide.pdf guide.pdfua.txt

# Accept every received snapshot (PDF, text or JSON) in a folder after reviewing them: each replaces its verified file,
# keeping its extension, and its report goes.
pdiffy accept tests/Snapshots
pdiffy accept --all    # the current folder and every folder under it

# Summarize every received snapshot, new or changed, with its changes by kind and by section. Exits with 0 when there
# are none and 1 when there are. --markdown adds the table to the end of a file, such as a CI job summary (GitHub's
# $GITHUB_STEP_SUMMARY), and --html saves a page that links each snapshot's report.
pdiffy report --all
pdiffy report --all --markdown "$GITHUB_STEP_SUMMARY" --html snapshot-summary.html

# Count, from the verified snapshots' headers, how many compare each section and how many leave out each field, so a
# suite that has drifted into ignoring layout or fonts everywhere shows it. --markdown works as for report.
pdiffy coverage --all

# List the snapshots no test used, such as those of renamed or deleted tests: run every test, passing, with
# PDIFFY_RUN_LOG set to the full path of an empty folder, then give that folder to unused. --delete deletes them.
PDIFFY_RUN_LOG="$PWD/artifacts/run-log" dotnet test
pdiffy unused tests --all --run-log artifacts/run-log
```

## What a snapshot records

| Section | Contents |
|---|---|
| `document` | Whether the PDF is tagged, its language, whether the viewer shows the title, page count |
| `settings` | PDF version, page mode and layout, what happens when the file opens, whether it has JavaScript (in document-level scripts, the open action, document actions, form fields, links and other annotations, page actions or bookmarks) or document actions, encryption and what it permits; viewer preferences, optional-content layers and embedded files |
| `metadata` | Title, author, subject, keywords; whether dates, producer and creator are present; PDF/UA identifier; XMP title, creator, description, language and PDF/A conformance, or `xmp=unreadable`; every other Info entry (such as a company name or classification label) |
| `role-map` | Each custom tag type and the standard type it maps to |
| `pages` | Size (named when standard, such as `letter` or `a4`), rotation, tab order; crop, bleed, trim and art boxes, user unit and transparency group when set; pages that couldn't be read; page label ranges |
| `bookmarks` | The outline tree, with each bookmark's target: a page and the view it opens at, a web address or another file. Or `unreadable` when it nests more than 256 deep, loops back on itself or has a damaged item, such as one without a title |
| `structure` | One line per structure element, in reading order: type, mapped type, pages, language, alt text, actual text, expansion, title, table attributes (scope, headers, spans, summary), list numbering, links, the hashes of the images it draws (`images=[3f9c0a1b2d4e5f60]`, the same hash as its layout line), the language, replacement text, alt text and expansion that marked content inside it sets, each with the text it covers unless that's all of the element's text (`span-lang=[fr-FR:"bonjour"]`, `span-expansion=["Mount":"Mt."]`), how many of its marked-content references name no page (`mcids-without-page`, whose text can't be found), how many point into a form XObject (`mcids-in-xobject`, whose text may include the page's own text for the same MCID), how many child elements are nested more than 256 deep and left out (`unread-children`), and the element's text |
| `artifacts` | Text marked as an artifact (headers, footers, page numbers), per page, with its kind when it gives one (`type=Pagination subtype=Footer`) |
| `untagged` | Text that is neither tagged nor marked as an artifact, one line per line of text (and tagged text that no structure element points to, one line per marked-content ID), and a count of such images and paths per page |
| `fonts` | Each font: name, type, whether it's embedded, whether it has a Unicode map, and the character codes its text uses that don't map to Unicode (that text can't be copied, searched or read aloud) |
| `annotations` | Every annotation (links, comments, form widgets and so on), page by page in the order each page lists them: type, `tagged` or `untagged`, rectangle, flags (such as `print` or `hidden`), where a link goes (a web address, or a page and the view it opens at, or another file), contents and accessible name |
| `forms` | A `form` line for settings that apply to the whole form, when it sets any: the order calculate scripts run in (`calculation-order=["trip.tax","trip.total"]`), whether the viewer must draw the fields itself (`need-appearances=yes`) and an XFA form (`xfa=static` or `xfa=dynamic`). Then every form field: type, full name (`address.city`), page, accessible name, value, options, maximum length, flags (such as `required` or `read-only`), default value, the value each check box or radio button widget sends (`exports=["Yes"]`), alignment when it isn't left (`align=center`), the font, size and color of typed text (`text-font=Helvetica text-size=auto text-color=gray:0`, from the widget, the field or the form) and the field's JavaScript by when it runs, as a hash (`scripts=format:1a2b3c4d5e6f7081`). A signed signature field prints `signed=yes`, the signature's format (`subfilter=`), whether the signed bytes run to the end of the file (`covers-file=no` after an update saved since signing), whether a reason, location and signing time are given (`reason=<present>`), and what a certifying signature still allows (`certification=form-filling`). The signature's bytes and signing time aren't recorded, so signing the same file again gives the same snapshot |
| `layout` | Every text run, image and painted vector path, page by page, each labeled `tagged`, `artifact` or `untagged`. Text runs: position, font, size, color, render mode, angle. Images: position, size, pixel size, a hash of the pixels. Paths: bounding box, fill, stroke and line width. Any of them drawn inside optional-content marked content (`/OC ... BDC`, a layer a viewer can hide) also lists its layers by name, outermost first (`layers=["Draft"]`) |

## What you can ignore

Each `Section` value leaves out one section from the table above. Each `Field` value leaves out one detail wherever it appears:

| Field | Leaves out |
|---|---|
| `Language` | The document's language, each structure element's language, languages set on marked content inside elements, and the XMP language |
| `PdfVersion` | The PDF version in the file header and the catalog |
| `OpenAction` | What happens when the file opens |
| `Permissions` | What an encrypted document allows |
| `EmbeddedFileContents` | The size and hash of each embedded file, and whether it could be read |
| `Title`, `Author`, `Subject` | The value from the Info dictionary and its XMP counterpart |
| `Keywords` | The keywords |
| `Dates` | Whether the creation and modification dates are present |
| `Producer`, `Creator` | Whether each is present |
| `CustomInfo` | Info entries other than the standard ones |
| `PageBoxes` | The crop, bleed, trim and art boxes and the user unit of each page |
| `AltText`, `ActualText` | Alternate and replacement text on structure elements and on marked content inside them |
| `Positions` | Where text, images, paths and annotations are, the size of images, paths and annotations, and the positions a link or bookmark opens its page at |
| `Colors` | Text color, path fill and stroke colors, and the color of text typed into form fields |
| `FontNames`, `FontSizes` | The font and size of each text run in the layout, and of text typed into form fields |
| `LineWidths` | The stroke width of each path |
| `ImageContents` | The pixel size and hash of each image, and the hashes on each structure element's `images=` |
| `LinkTargets` | Where links, bookmarks and other actions go: web addresses, files, and the view a page opens at (a bookmark's page stays) |
| `AnnotationContents` | The text of annotations |
| `FormValues` | The value and default value of each form field |

The options can also change what's printed:

| Option | Does |
|---|---|
| `ScrubDates()` | Replaces dates in quoted text with `Date_1`, `Date_2`, …: `2026-10-27`, `10/27/2026`, `October 27, 2026`, `27 Oct 2026`, each with an optional time, and PDF dates such as `D:20261027120000` |
| `ScrubGuids()` | Replaces GUIDs in quoted text with `Guid_1`, `Guid_2`, … |
| `ScrubText(regex, replacement)` | Replaces your own pattern in quoted text |
| `ShowProducerAndCreator()` | Prints the producer and creator values instead of only whether they're present |
| `OnlyPages(3, 4)` | Keeps only what is on those pages |
| `Tolerate(Field.Positions, 1)` | Lets positions differ by up to 1 pt without failing the test; also takes `FontSizes` and `LineWidths` |

For Word documents, each `DocxSection` value leaves out one section, and each `DocxDetail` value leaves out one detail wherever it appears:

| DocxDetail | Leaves out |
|---|---|
| `ImageHashes` | The hash of each image, embedded object and control's data |
| `FieldResults` | Each field's `result=` and the result's text in the paragraph, also inside links, formatted text, tracked changes and content controls' values, so a PAGE or DATE field or a table of contents that shows something new doesn't fail the test. Positions after the result move back |
| `Fonts` | The font of styles and of text that differs from its style. A symbol's or a bullet's font stays, since it decides which character shows |
| `FontSizes` | The font size of styles and of text that differs from its style |
| `Colors` | Text color, highlight, cell and paragraph shading, and border colors |
| `Spacing` | Space before and after paragraphs, and line spacing |
| `Indents` | Left, right, first-line and hanging indents of paragraphs, styles and list levels |
| `RevisionAuthors` | Who made each tracked change |
| `CommentAuthors` | Who wrote each comment |

## Changes that affect accessibility

In a PDF snapshot, a change's `AffectsAccessibility` is true, and a failure lists it first, when it is one of these:

| Change | Shows as |
|---|---|
| A structure element's type changed, such as a heading becoming a paragraph (a header cell's generated name, such as `TH1` becoming `TH2`, doesn't count) | `type: H2 -> P` |
| A structure element moved in reading order | `moved` in `structure` |
| Alt text, replacement text or an expansion (on an element or on marked content inside it), a table header's scope, a cell's headers or a form field's or annotation's accessible name was removed or emptied, or a cell's headers point at a header that isn't there | `alt: "Logo" -> (missing)`, `actual: "fi" -> (missing)`, `span-expansion: ["Mount":"Mt."] -> (missing)`, `scope: Column -> (missing)`, `tu: "Name" -> ""`, `headers: [TH1] -> [?h2]` |
| The document's or an element's language changed or was removed, or one set on marked content inside an element (a change to only the words such marked content covers isn't flagged) | `lang: en-US -> en-GB`, `span-lang: [fr-FR:"bonjour"] -> [de-DE:"bonjour"]` |
| Something was added to the untagged content: a line, words in a line, or more images or paths. Less untagged content doesn't count, except that a text line that loses more than half its words shows as removed and added, and the added line counts | `added`, `"Page 1 {+Draft+}"` or `images: 1 -> 2` in `untagged` |
| An annotation became untagged | `... tagged ... -> ... untagged ...` in `annotations` |
| Text, an image or a path that was tagged is now an artifact (decoration) or untagged, so a screen reader skips it | `p1 text tagged ... -> p1 text artifact ...` in `layout` |
| The document is no longer marked as tagged, or the viewer no longer shows its title | `marked: yes -> no`, `display-doc-title: yes -> no` |
| The document's title was removed or emptied | `title: "Trail Guide" -> (missing)`, `xmp-title: "Trail Guide" -> ""` |
| A page's tab order is no longer the structure order | `tabs: S -> R` |
| A font's text no longer maps to Unicode, or more of its character codes don't | `to-unicode: yes -> no`, `unmapped: [41] -> [41-43]` |
| A document is newly encrypted, or its permissions changed, so screen readers can no longer extract its text | `permissions: (missing) -> print`, `permissions: print,extract-for-accessibility -> print` |

The rules are the same for every project. A release can add rules, so an upgrade can flag a change that wasn't flagged before; [Format changes](#format-changes) lists each one. Content made an artifact is found through the layout section, so with layout left out (as the `Content` profile does) it shows as words leaving its structure element and, for text, a line added to `artifacts`, neither of which is flagged. DOCX and text snapshots have no flags. A test passes or fails on the whole snapshot as before; the flag only orders and marks the explanation. To fail only on these, compare with `CompareTo` and assert on `AffectsAccessibility`.

## Snapshot format

```
# pdfua-snapshot v1
document
  marked=yes lang=en-US display-doc-title=yes pages=3
...
structure
  Document lang=en-US
    H1 p1 "Trail Table"
    Figure p1 alt="Park logo" images=[3f9c0a1b2d4e5f60]
    Table p2-3 summary="Trail details"
      TR
        TH1 p2 scope=Column "Trail"
        TH2 p2 scope=Column "Length"
      TR
        TD p2 headers=[TH1] "Ridgeline"
        TD p2-3 headers=[TH2] "Cedars"
untagged
  p1 "Stray text"
  p1 images=1 paths=2
fonts
  Arial TrueType embedded=yes to-unicode=yes
layout
  p1 text tagged at=72,720 font=Arial size=18 color=gray:0 "Trail Table"
  p1 text untagged at=72,90 font=Arial size=9 color=gray:0 mode=invisible "Stray text"
  p1 image tagged at=72,600 size=120x40 pixels=600x200 hash=3f9c0a1b2d4e5f60
  p1 path artifact at=72,60 size=468x1 stroke=cmyk:0,0,0,1 width=0.5
```

- The first line is the format version. See [Format changes](#format-changes) for when it changes.
- When anything is ignored, the next lines list it: `# ignored sections: ...` and `# ignored fields: ...`. With `Tolerate`, `# tolerances: ...` follows, and with `OnlyPages`, `# only pages: ...`.
- Sections always appear in the same order. An empty section prints `(none)` and an ignored one prints `(ignored)`, so something disappearing shows up as a change.
- Two spaces of indent per nesting level. Keys appear in a fixed order and are left out when they have no value.
- Text is quoted; `"` and `\` are escaped, and control characters print as `\uXXXX`.
- Table header cells that other cells refer to get stable names (`TH1`, `TH2`) instead of the PDF's internal IDs.
- In `layout`, `angle=` appears only for text that doesn't run across the page, in degrees counterclockwise (`angle=90` reads upward); a page's `rotate=` is the PDF's own clockwise setting.
- In `layout`, positions and sizes are whole points from the page's bottom-left corner, and font sizes and line widths are rounded to 0.1 pt. Colors name their color space: `gray:0.5`, `#ff0000` (RGB), `cmyk:0,0,0,1`, or `pattern:shading` / `pattern:tiling` for gradients and tiled fills. `mode=` appears only for text that isn't drawn the usual way (`invisible`, `stroke`, `fill-stroke`, …). `fill=`, `stroke=` and `width=` appear only for what a path actually paints.
- An image's hash is taken from its decoded pixels where possible, so recompressing an image doesn't change it.
- Annotation rectangles use the same convention as `layout`: the bottom-left corner and size, in whole points.
- In `fonts`, `unmapped=[0041-0043,0050]` lists hex character codes, with runs shortened to first-last. A code counts as mapped when the font's ToUnicode map gives it text; for a simple font, also when its encoding gives it a standard glyph name; and for a CID font, when it uses a predefined CMap other than Identity-H or Identity-V over one of Adobe's character collections (Japanese, Chinese or Korean). Text inside form XObjects counts too.
- Files are UTF-8 with LF line endings and no BOM.

## What's left out on purpose

These change without changing what anyone reading the document gets:

- Object numbers, compression, object order, stream lengths and cross-reference layout
- MCIDs and structure element IDs (references are resolved to names)
- Font subset prefixes (`ABCDEF+Arial` is recorded as `Arial`)
- Dates, document IDs and the producer string (only whether they're present; `ShowProducerAndCreator()` prints the producer and creator)

Text is normalized to Unicode NFC, with whitespace runs collapsed to one space and trimmed. A space is added between two pieces of text when the baseline changes or the gap is wider than 0.2 em beyond the line's letter spacing, which is read from its narrowest gaps. The same rule joins the pieces of an element's text, so a word a PDF splits across two tagged pieces stays one word. Pieces on different pages, and a piece that starts well to the left of where the last one ended on the same line, are always separated by a space.

## Format changes

A newer PDiffy can print some snapshots differently, when it records something new or reads something more accurately. Releases that change what a snapshot prints are minor versions (0.3.0, 0.4.0), and each one is listed below, so a failure after an upgrade can be told apart from a change to your document.

- **Some snapshots change:** a new line or key that only some documents have (one that prints only when set), or a reader fix. The format version stays the same, and the entry below says which snapshots change.
- **Nearly every snapshot changes:** a key renamed, a value printed another way, or a section added, removed or moved. The format version goes up, at most once per format per release. A failing test then says first that the verified file uses the old version, and still lists the document's changes below.

Either way, review the received files and accept them (`pdiffy accept --all`, or run the tests with `PDIFFY_ACCEPT=1`).

| Release | Change |
|---|---|
| Next | `pdfua-snapshot v1`, unchanged version: a PDF that refers to an object it doesn't have, has a bookmark without a title, or has an image that can't be decoded now loads (the bookmarks print `unreadable`). Such files used to throw, so no approved snapshot changes |
| Next | `pdfua-snapshot v1`, unchanged version: text, images and paths drawn inside optional-content marked content (a layer a viewer can hide) list their layers on their layout lines (`layers=["Draft"]`), and text runs end where a layer starts or ends, so a run a layer splits shows as changed with the rest added. Only snapshots of documents with such content change |
| Next | `pdfua-snapshot v1`, unchanged version: untagged text prints one line per line of text instead of one per page, so an added line shows alone. Snapshots with untagged text on more than one line of a page change: the old page line usually shows as removed and each line as added, flagged as affecting accessibility. `pdiffy check` reports a page's untagged text, including tagged text no element points to, as one finding |
| Next | `pdfua-snapshot v1`, unchanged version: `javascript=yes` now also covers JavaScript in links and other annotations, page actions and bookmarks, and a Rendition action's own script (`/JS`), which a form field also lists in `scripts=`, so `NoActions` and `pdiffy check` report them. Only snapshots of documents with such scripts change, in `settings` and on such form fields |
| Next | `pdfua-snapshot v1`, unchanged version: the open action reads its destination the way links do, so a named one prints its page (`open-action="GoTo p2 Fit"`), positions are whole points, an unset zoom is left out and a page given as a number counts from 0 (`p1 Fit`, not `p? Fit`). Only snapshots of documents whose open action is named, has fractional positions, keeps a position or the zoom, gives its page as a number or is malformed change |
| Next | `pdfua-snapshot v1`, unchanged version: an artifact whose text is split across nested marked content is read in drawing order (`"Page 1"`, not `"1Page"`). Only snapshots of documents with such artifacts change; a short one can show as removed and added |
| Next | `pdfua-snapshot v1`, unchanged version: a form that sets a calculation order, `NeedAppearances` or XFA prints a `form` line first in `forms`, and `javascript=yes` now covers form field scripts, so `NoActions` and `pdiffy check` report them. Only snapshots of documents with such forms or scripts change |
| Next | `pdfua-snapshot v1`, unchanged version: form fields print their default value, export values, alignment, typed text's font, size and color, and JavaScript by when it runs (`scripts=calculate:3f9c0a1b2d4e5f60`). Snapshots of documents with forms change |
| Next | `pdfua-snapshot v1`, unchanged version: a signed signature field prints its signature (`signed=yes subfilter=adbe.pkcs7.detached covers-file=yes`, whether a reason, location and signing time are given, and a certification level), not its bytes. Only snapshots of signed documents change |
| Next | `pdfua-snapshot v1`, unchanged version: an element whose marked content draws an image prints the image's hash (`images=[3f9c0a1b2d4e5f60]`), so swapping two figures' images shows on their lines. Left out with `Field.ImageContents`. Snapshots of tagged documents with images change |
| Next | `pdfua-snapshot v1`, unchanged version: an element with marked-content references into a form XObject prints `mcids-in-xobject=N`, since its text may include the page's own text for the same MCID (and the page's element the form's). Only snapshots of documents with such references change, by the new key alone |
| Next | `pdfua-snapshot v1`, unchanged version: marked content inside an element that sets its own language, replacement text, alt text or expansion is recorded on the element with the text it covers (`span-lang=[fr-FR:"bonjour"]`, `span-expansion=["Mount":"Mt."]`), and an artifact prints its kind (`type=Pagination subtype=Footer`). Snapshots with such marked content or typed artifacts change, and typed artifacts are common. A new `span-lang` key is flagged as affecting accessibility, like any language change |
| Next | `pdfua-snapshot v1`, unchanged version: letter-spaced text no longer gets a space between every letter, since a gap counts as a space only when it's wider than the line's letter spacing (read from its narrowest gaps, up to a quarter of an em). Snapshots of documents with letter-spaced text change |
| Next | `pdfua-snapshot v1`, unchanged version: text drawn at an angle is read along its own baseline, so it keeps its words and stays one layout run, which prints `angle=90`. Snapshots of documents with rotated text change |
| Next | `pdfua-snapshot v1`, unchanged version: links print where they go (`dest=p3 view=XYZ view-at=72,720`, `zoom=` when set, a named destination looked up, `file=` for another file) instead of `dest=yes`, and bookmarks print their view, web address or file (a bookmark to another file prints `file="..." dest=p1` instead of `-> p1`). Snapshots of documents with internal links or bookmarks change |
| Next | `docx-snapshot v1`, unchanged version: hidden text (`w:vanish`), double strikethrough, right-to-left runs and a run's language are read, so a run that differs from its paragraph in one of them prints a span (`span "care" hidden`), and a style whose language isn't the one on the document line prints `lang=`. An inserted Word document's styles are compared with that language too, so an inserted document in another language prints `lang=` on its styles. Only snapshots of documents with such runs or styles change |
| Next | `docx-snapshot v1`, unchanged version: right-to-left paragraphs, paragraph borders and paragraph shading are read, and a paragraph or style that sets them prints `rtl-paragraph`, `borders=` or `shading=`. Only snapshots of documents with such paragraphs or styles change |
| Next | `docx-snapshot v1`, unchanged version: a table's alt text (`caption=`, `description=`), a row's height and `split=no` (kept on one page), and a cell's vertical alignment and text direction are read. Only snapshots of documents with tables that set them change |
| Next | No snapshot changes. A line without text of its own, such as a table row or cell, is now paired by the text under it, so rows that gained different attributes are no longer mixed up, and one that kept most of its own words while its content was rewritten still pairs with its old self |
| Next | No snapshot changes. More changes are flagged as affecting accessibility: tagged content made an artifact or untagged, `marked` or `display-doc-title` turned off, the title removed, replacement text or an expansion removed, a page's tab order no longer `S`, a font's text no longer mapping to Unicode, and permissions that stop extraction for accessibility |
| 0.3.0 | `pdfua-snapshot v1`, unchanged version: when an element's text is split into tagged pieces, a space now goes between two pieces on the same page only where the page shows a gap (`"Num ber:"` becomes `"Number:"`). Snapshots with such elements change in `structure` |
| 0.3.0 | `pdfua-snapshot v1`, unchanged version: an element with marked-content references that name no page gets `mcids-without-page=N`. Only snapshots with such elements change |
| 0.3.0 | `docx-snapshot v1`, unchanged version: text boxes, equations, embedded and linked objects, ActiveX controls and absolute tabs (`w:ptab`) are read, a symbol whose code isn't hex is kept, text in right-to-left runs (`w:dir`, `w:bdo`) is read, and anything else in a paragraph PDiffy can't read prints as `unsupported`. Only snapshots of documents with those change |
| 0.3.0 | `docx-snapshot v1`, unchanged version: tables and cells get `width=`, `cell-margins=`/`margins=` and `borders=` when they set them; rows list the content controls around them and their tracked changes (`row-insert`, `row-delete`, `row-format`); a section lists a tracked change to its settings (`section-format`); a run inside paragraph properties keeps its tabs and breaks in `stray-text`. Only snapshots of documents with those change |
| 0.3.0 | `docx-snapshot v1`, unchanged version: a bookmark between paragraphs or tables prints as `bookmark "Name"` in place, content offered as `mc:AlternateContent` between them is read through its first choice, and anything else there PDiffy can't read prints as `unsupported`. Only snapshots of documents with those change |
| 0.3.0 | `docx-snapshot v1`, unchanged version: an ActiveX control's `hash=` also covers the binary part that holds its state (`activeX1.bin`), so ticking a check box or typing in a text box changes it, and a control part that isn't XML is listed as `unreadable`. Snapshots of documents with ActiveX controls change |
| 0.3.0 | `docx-snapshot v1`, unchanged version: a bookmark between table rows or cells prints as `bookmark "Name"` in place, rows and cells offered as `mc:AlternateContent` are read through the first choice, and anything else there PDiffy can't read prints as `unsupported`. Only snapshots of documents with those change |
| 0.3.0 | `docx-snapshot v1`, unchanged version: tracked changes to a table (`table-format`, `table-grid`), to the table properties a row sets for itself (`row-table-format`), to cells (`cell-insert`, `cell-delete`, `cell-merge`, `cell-format`) and to a paragraph mark's formatting (`paragraph-mark-format`) are recorded and listed by `Revisions()`. Only snapshots of documents with those change |
| 0.3.0 | `docx-snapshot v1`, unchanged version: a cell lists the content controls around it, as a row does, each marked `around` so it can't be mistaken for a control inside the cell. Only snapshots of documents with content controls around cells change |
| 0.3.0 | `docx-snapshot v1`, unchanged version: a table-column bookmark (one that sets `w:colFirst` and `w:colLast`) prints the columns it covers, counting from 0: `bookmark "Stages" columns=0-2`. Only snapshots of documents with table-column bookmarks change |
| 0.3.0 | `pdfua-snapshot v1`, unchanged version: XMP metadata is read as UTF-8, as the XMP standard requires, so a non-ASCII value such as `Café` no longer prints as `CafÃ©`. XMP that isn't well-formed XML, or has a DTD, prints `xmp=unreadable` instead of failing the read. Snapshots with non-ASCII XMP values change |
| 0.3.0 | `pdfua-snapshot v1`, unchanged version: an open action that chains others with `/Next` lists them all, in the order they run: `open-action="GoTo p1 Fit, Launch"`. Only snapshots of documents with chained open actions change |
| 0.3.0 | `pdfua-snapshot v1`, unchanged version: an element whose children are nested more than 256 deep gets `unread-children=N`, and bookmarks nested more than 256 deep, or that loop back on themselves, print `unreadable`. Such files used to overflow the stack, so no approved snapshot changes |
| 0.3.0 | `docx-snapshot v1`, unchanged version: a part with a DTD, with elements nested more than 256 deep, or larger than 256 MB is listed as `unreadable`, and an inserted document past one of those limits prints `Inserted docx unreadable`. Word doesn't write such parts, so only documents from other tools can change |
| 0.2.0 | New format `docx-snapshot v1`, for Word documents |
| 0.1.0 | New format `pdfua-snapshot v1` |

## Damaged files

A page that can't be loaded (for example, one with a damaged font) comes back marked unreadable, and its snapshot line says so, instead of the whole read failing. The same goes for an attachment whose contents can't be decoded, XMP metadata that isn't well-formed XML (`xmp=unreadable`) and a Word part that isn't (`unreadable word/styles.xml`). A file that can't be opened at all throws.

PDiffy also limits what it reads, so a file built to exhaust the stack or memory can't crash the test run:

- Structure elements and bookmarks are read 256 levels deep. An element whose children are deeper gets `unread-children=N`, and their content counts as untagged. Bookmarks nested deeper, or that loop back on themselves, print `unreadable`.
- XML with a DTD, or with elements nested more than 256 deep, is unreadable: a Word part or a PDF's XMP. An inserted Word document's nesting counts from where it's inserted.
- A Flate-compressed PDF stream that would decompress to more than 256 MB isn't decompressed: its page is unreadable, an image is hashed as stored, and XMP prints `xmp=unreadable`. A Word part larger than 256 MB is unreadable too, instead of being loaded into memory. An inserted Word document that can't be read isn't listed as unreadable; it's marked where it's inserted: `Inserted docx unreadable`.

## Development

```
dotnet build
dotnet test
```

The tests build their own small PDFs in code (`tests/PDiffy.Tests.Unit/Pdfs`): `TestPdf` writes tagged documents, and `RawPdf` writes hand-made malformed ones. No real-world PDFs are checked in. Some tests also have PDFsharp write a tagged PDF (`tests/PDiffy.Tests.Unit/Producers`) and check that PDiffy reads back what was written, using the bundled Liberation Sans font (SIL Open Font License) so they run the same on Linux.

The benchmarks build larger documents the same way and time loading, writing snapshots and comparing them: `dotnet run -c Release --project benchmarks/PDiffy.Benchmarks -- --filter '*'`. The [performance page](https://zacharydf7.github.io/pdiffy/reference/performance.html) has the latest numbers.

Pushes to main and pull requests are built and tested on Windows and Linux. Pushing a version tag such as `v0.1.0` also publishes that version of both packages to nuget.org.

## License

MIT. See [LICENSE](https://github.com/zacharydf7/pdiffy/blob/main/LICENSE).
