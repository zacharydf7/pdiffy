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

## Why not compare bytes

Byte-for-byte PDF snapshots fail for reasons that don't matter and explain nothing when they do:

- Any change to how the file is written (compression, object order, font subsetting, a library upgrade) fails every test, even when the content is identical.
- The same output differs between operating systems, so every test needs one snapshot per OS.
- A failure only says the bytes differ. Finding out what changed means opening both files in a PDF checker.

PDiffy records the content and leaves out the encoding. One snapshot covers Windows and Linux, and a failing test shows which element changed, on which page.

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
PdfUaDocument document = PdfUaDocument.Load("drug-guide.pdf");

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
public async Task DrugGuide_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    // Generate the PDF in memory.
    using Stream pdf = await ReportGenerator.CreateDrugGuideAsync();

    // Compare it with DrugGuideTests.DrugGuide_ShouldMatchApprovedSnapshot_WhenGenerated.verified.pdfua.txt,
    // named after this test file and method. A difference throws SnapshotException saying what changed.
    await PdfSnapshot.VerifyAsync(pdf);
}
```

A failure explains itself. Elements are matched one to one, so an edited paragraph is one change, not a removed line and an added one, and an element that moved in reading order is reported as moved, along with anything that changed on it. A structure element whose type changed but whose text didn't, such as a heading going from H2 to H3 or to P, is one change (`type: H2 -> H3`). A paragraph replaced by one with entirely different words is reported as removed and added, but a one-word value replaced on an otherwise unchanged line, such as a number in a table cell, is reported as changed:

```
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

When pages are inserted or removed, the shift is one change (`p2 inserted; p2-10 -> p3-11`) and each element is compared with the page it lines up with, so a new cover page doesn't report every later element as changed. The second line counts the changes in each section, so a reflow that moves 200 layout lines doesn't hide one change to the structure. Changes that can affect what a screen reader gets come first, tagged `[accessibility]` (see [Changes that affect accessibility](#changes-that-affect-accessibility)).

What happens on each run:

| Situation | PDiffy does | Test |
|---|---|---|
| No verified file yet | Writes the received file | Fails: "New PDF snapshot" |
| Snapshot differs | Writes the received file and an HTML report (`.diff.html`) | Fails with a summary of what changed and where |
| Snapshot matches | Deletes any received file or report left from an earlier run | Passes |
| `PDIFFY_ACCEPT=1` is set | Writes the snapshot as the new verified file | Passes |
| `PDIFFY_ACCEPT=1` is set on a build server (`CI` or `TF_BUILD` is set) | Ignores it | Fails as usual |

To accept a change, review the received file, then copy it over the verified one, run the tests again with `PDIFFY_ACCEPT=1`, or run `pdiffy accept` (see [Command line](#command-line)). Either way the change shows up in code review as a diff of the `.verified.pdfua.txt` file.

Snapshot files sit next to the test file by default. To keep them all in one folder, set it once, for example in a module initializer. Set it too if your build maps source paths (deterministic builds, common on CI), because the test file's path then doesn't exist on disk:

```csharp
// Every snapshot, of any kind, goes to and is read from this folder.
Snapshot.Directory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Snapshots");
```

A test whose snapshots belong somewhere else passes the folder: `await PdfSnapshot.VerifyAsync(pdf, directory: otherFolder)`.

Pass a name when one test makes several snapshots, as a theory does: `await PdfSnapshot.VerifyAsync(pdf, $"Invoice.{customer}")`.

Snapshots use LF line endings. If Git converts line endings on checkout, add `*.verified.txt text eol=lf` and `*.verified.json text eol=lf` (plus a line for any custom extension, such as `*.verified.brf`) to `.gitattributes`; PDiffy also compares a CRLF verified file as LF. Add `*.received.*` and `*.diff.html` to `.gitignore` and to your build server's artifacts, so a failed CI run keeps what it received and the report.

The report is one HTML file with nothing to download, so it opens straight from a build artifact. It lists every change, accessibility changes first (filterable by kind, or to only those that affect accessibility), links to each page, and shows each section with the verified snapshot on the left and the received one on the right, edited lines side by side with the changed words highlighted. `diff.ToHtml()` gives the same page for any `SnapshotDiff`. See a [sample report](https://zacharydf7.github.io/pdiffy/samples/DrugGuide.diff.html).

### Snapshot text and JSON

`Snapshot.VerifyAsync` and `Snapshot.VerifyJsonAsync` check other output the same way, with the same received and verified files, accept mode and report.

```csharp
[Fact]
public async Task Braille_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    string braille = await BrailleGenerator.CreateAsync();

    // Compared exactly, spaces included; only CRLF line endings are read as LF. Saved as {name}.verified.txt.
    await Snapshot.VerifyAsync(braille);
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

Text options scrub the whole text: `o => o.ScrubText(new Regex(@"Order \d+"), "Order N").ScrubDates()`. JSON scrubs strings (values or keys) that are a GUID or an ISO 8601 timestamp (`"2026-10-28T09:15:00Z"`), but leaves dates written for people, such as a letter's "November 14, 2020", and anything inside longer text, because that is content. Turn either off with `o => o.KeepGuids()` or `o => o.KeepDates()`; to scrub ids or dates inside text too, add `o => o.ScrubGuids()` or `o => o.ScrubDates()`. Pass `extension: "brf"` to name text files something other than `.txt`. A verified file that starts with a UTF-8 byte-order mark matches as if it didn't, so files approved by other tools carry over.

### Snapshot Word documents

`DocxSnapshot.VerifyAsync` does the same for DOCX files, with snapshots in `.verified.docx.txt` files. It reads the file directly, with no Word or Open XML SDK needed.

```csharp
[Fact]
public async Task Letter_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    using Stream docx = await LetterGenerator.CreateAsync();

    await DocxSnapshot.VerifyAsync(docx);
}
```

A DOCX snapshot reads like the document: one line per paragraph with its runs merged, and only what differs from its style listed.

```
# docx-snapshot v1
document
  title="Cheese list" lang=en-US compat=15 track-changes=no protection=none even-odd-headers=no created=<present> modified=<present>
sections
  s1 letter margins=72,72,72,72 header=36 footer=36 columns=1 break=nextPage headers=default
styles
  ListParagraph paragraph "List Paragraph" font="Arial" size=12 indent-left=36
  TableGrid table "Table Grid"
numbering
  list 1 definition=3
    level 0 format=upperLetter text="%1." start=1 indent=36 hanging=18
body
  P style=ListParagraph list=1:0 label="A." "Cheese has a long history"
    span "long" bold
  Table style=TableGrid widths=144,216
    Row header
      Cell
        P "Drug"
      Cell
        P "Interacts with"
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

The snapshot records the document settings, each section's page setup, the styles in use with their properties resolved through the styles they're based on, list labels as Word numbers them, paragraph layout and text formatting, tables (grid, header rows, merged cells, shading, the widths, cell margins and borders of tables and cells, and the content controls around rows and cells), images and charts with their alt text and a hash of the image, text boxes (their paragraphs nested under the shape), equations (their text and a hash of how they're built), embedded objects and ActiveX controls (their program or name and a hash of their data, or the file a linked object shows), links, bookmarks (also those between paragraphs, tables, rows and cells), symbols, fields with their results, form controls, tracked changes (to text, paragraphs and their marks, tables, rows, cells and section settings), headers and footers, footnotes, endnotes and comments, and inserted documents. Relationship ids, editing-session ids and the zip's layout are left out, so regenerating the same document gives the same snapshot, and so are styles no paragraph, run or table uses, since they can't change the document.

Anything in a paragraph, or between paragraphs, tables, rows and cells, that PDiffy doesn't read yet, such as a phonetic guide (`w:ruby`), a date or page-number element (`w:dayShort`, `w:pgNum`) or a VML shape that isn't a `v:shape` (such as `v:rect` or `v:oval`), prints as `unsupported <element> hash=...`, so a change to it still fails the test.

`DocxDocument.Load` gives the same content as properties, and `CompareTo` compares two documents. Options leave out sections, leave out one detail everywhere (see [What you can ignore](#what-you-can-ignore)) or scrub text: `o => o.Ignore(DocxSection.Styles).Ignore(DocxDetail.FieldResults).ScrubDates()`.

```csharp
DocxDocument letter = DocxDocument.Load("letter.docx");
Assert.All(letter.Images(), image => Assert.NotNull(image.AltText));
Assert.Empty(letter.Revisions());
```

### Leave out sections a test doesn't care about

Some tests only care about part of the document. Pass the sections to leave out, and they print as `(ignored)` instead of their contents. The snapshot's second line lists what was ignored, so nobody reading it mistakes a missing section for an empty one.

```csharp
[Fact]
public async Task InvoiceTemplate_ShouldKeepItsTagStructure_WhenStylingChanges()
{
    using Stream pdf = await ReportGenerator.CreateInvoiceAsync();

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
public async Task MonthlyStatement_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateStatementAsync(month: 6);

    await PdfSnapshot.VerifyAsync(pdf, configure: options => options
        // The title includes the month, so it changes every time.
        .Ignore(Field.Title)
        // Text reflows when the statement has more rows, so where things land doesn't matter here.
        // Fonts, sizes and colors are still compared.
        .Ignore(Field.Positions));
}
```

With `Field.Positions` ignored, a layout line such as `p1 text tagged at=72,720 font=Arial size=18 color=gray:0 "Statement"` becomes `p1 text tagged font=Arial size=18 color=gray:0 "Statement"`.

### Scrub values that change on every run

A generated document often prints today's date, an order number or a GUID. Scrubbers replace those inside quoted text with a stable placeholder, so the rest of the text is still compared.

```csharp
[Fact]
public async Task PharmacyReceipt_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateReceiptAsync();

    await PdfSnapshot.VerifyAsync(pdf, configure: options => options
        // "Printed 10/27/2026" becomes "Printed Date_1". The same text always gets the same number,
        // so the snapshot still shows that the print date and the fill date match.
        .ScrubDates()
        // Transaction IDs become Guid_1, Guid_2 and so on.
        .ScrubGuids()
        // Anything else that changes, with your own pattern. "$1" keeps the first group.
        .ScrubText(new Regex(@"(Rx) #\d+"), "$1 #<number>"));
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
PdfUaDocument before = PdfUaDocument.Load("statement-v1.pdf");
PdfUaDocument after = PdfUaDocument.Load("statement-v2.pdf");

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

### Check accessibility rules directly

You don't need a snapshot to use PDiffy. The document model answers questions such as "does every image have alt text?" without any PDF knowledge: `StructureType` lists every tag type by name.

```csharp
[Fact]
public async Task PatientLetter_ShouldBeReadableByScreenReaders_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreatePatientLetterAsync();
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
    Assert.Equal(["patient.name", "patient.dateOfBirth"], required);

    // The state picker is a drop-down with all 50 states.
    FormField state = document.FormFields.Single(field => field.Name == "patient.address.state");
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

The `pdiffy` tool does the same outside of tests. Install it once as a .NET tool:

```
dotnet tool install --global PDiffy.Cli
```

```
# Compare two PDFs, two DOCX files or two snapshot files: prints what changed, exits with 0 when nothing did and 1 when
# something did.
pdiffy statement-v1.pdf statement-v2.pdf

# The same, also writing the side-by-side HTML report.
pdiffy statement-v1.pdf statement-v2.pdf --html report.html

# The changes as JSON for a script: --json alone prints only the JSON, and --json with a file saves it there.
pdiffy statement-v1.pdf statement-v2.pdf --json
pdiffy statement-v1.pdf statement-v2.pdf --json changes.json

# Print a PDF's or DOCX's snapshot, or save it to a file.
pdiffy snapshot statement.pdf
pdiffy snapshot statement.pdf statement.pdfua.txt

# Accept every received snapshot (PDF, text or JSON) in a folder after reviewing them: each replaces its verified file,
# keeping its extension, and its report goes.
pdiffy accept tests/Snapshots
pdiffy accept --all    # the current folder and every folder under it

# Summarize every received snapshot, new or changed, with its changes by kind and by section. Exits with 0 when there
# are none and 1 when there are. --markdown adds the table to the end of a file, such as a CI job summary (GitHub's
# $GITHUB_STEP_SUMMARY), and --html saves a page that links each snapshot's report.
pdiffy report --all
pdiffy report --all --markdown "$GITHUB_STEP_SUMMARY" --html snapshot-summary.html
```

## Moving from Verify

Tests that snapshot PDFs, Word documents or JSON with Verify can move over one test class at a time. Most changes are one line per test:

| Verify | PDiffy |
|---|---|
| `await Verify(pdfStream, "pdf")` | `await PdfSnapshot.VerifyAsync(pdfStream)` |
| `await Verify(docxStream, "docx")`, or `VerifyXml` of the document's parts | `await DocxSnapshot.VerifyAsync(docxStream)` |
| `await VerifyJson(json)` | `await Snapshot.VerifyJsonAsync(json)` |
| `await Verify(text)` | `await Snapshot.VerifyAsync(text)` |
| `.UseFileName("Invoice")` | `name: "Invoice"` |
| `DerivePathInfo(...)` or `UseDirectory(...)` | `Snapshot.Directory = "..."` once, or `directory:` on one call |
| `.UniqueForOSPlatform()` | Nothing: a snapshot is the same text on every OS, so one file covers them all |
| `.ScrubLinesWithReplace(...)` or a custom scrubber | `ScrubText(regex, replacement)` |
| `.ScrubInlineDateTimes(...)`, `.ScrubInlineGuids()` | `ScrubDates()`, `ScrubGuids()` |
| Accepting in a diff tool, or moving `.received.` files by hand | `PDIFFY_ACCEPT=1 dotnet test`, or `pdiffy accept` |

### Seed the new snapshots from the approved files

There's no need to regenerate anything. The approved `.verified.pdf` or `.verified.bin` files are the documents your tests last approved, so the `pdiffy` tool can turn each one into a PDiffy snapshot. Name the output after the test, as PDiffy does: `{TestFile}.{TestMethod}.verified.pdfua.txt`, or `{name}.verified.pdfua.txt` when the test passes a name.

```
pdiffy snapshot InvoiceTests.Invoice_ShouldMatch.verified.pdf InvoiceTests.Invoice_ShouldMatch.verified.pdfua.txt
pdiffy snapshot LetterTests.Letter_ShouldMatch.verified.docx LetterTests.Letter_ShouldMatch.verified.docx.txt
```

A Word document saved as `.verified.bin` needs a `.docx` name first, since that's how the tool tells it from a PDF. With `UniqueForOSPlatform`, convert one OS's copy (`...Windows.verified.pdf`) and delete the others. Then delete the old files.

### What has to be approved once

- **JSON:** PDiffy writes real JSON, indented two spaces with keys in the order written, to `.verified.json` instead of Verify's unquoted style in `.verified.txt`. GUIDs and ISO 8601 timestamps scrub to `Guid_1` and `Date_1` (Verify writes `DateTimeOffset_1` or `DateTime_1`). Run the tests once with `PDIFFY_ACCEPT=1`, check the new files and delete the old ones.
- **Text:** a `.verified.txt` carries over unchanged. PDiffy reads CRLF as LF and ignores a byte-order mark.

### Repository changes

Add these lines to `.gitattributes`, so a snapshot has the same line endings on every OS:

```
*.verified.* text eol=lf
*.received.* text eol=lf
```

Add `*.received.*` and `*.diff.html` to `.gitignore`, and to the files your CI keeps after a failed run, so the report of a failure on CI can be read.

### Run both for a while, if you like

To build trust before deleting the Verify files, keep both assertions in each test for a few weeks. A failure only Verify reports is noise PDiffy leaves out on purpose (see [What's left out on purpose](#whats-left-out-on-purpose)). A failure only PDiffy reports is a real change Verify missed, or a PDiffy bug, which is worth [an issue](https://github.com/zacharydf7/pdiffy/issues).

## What a snapshot records

| Section | Contents |
|---|---|
| `document` | Whether the PDF is tagged, its language, whether the viewer shows the title, page count |
| `settings` | PDF version, page mode and layout, what happens when the file opens, whether it has JavaScript or document actions, encryption and what it permits; viewer preferences, optional-content layers and embedded files |
| `metadata` | Title, author, subject, keywords; whether dates, producer and creator are present; PDF/UA identifier; XMP title, creator, description, language and PDF/A conformance; every other Info entry (such as a company name or classification label) |
| `role-map` | Each custom tag type and the standard type it maps to |
| `pages` | Size (named when standard, such as `letter` or `a4`), rotation, tab order; crop, bleed, trim and art boxes, user unit and transparency group when set; pages that couldn't be read; page label ranges |
| `bookmarks` | The outline tree, with target pages |
| `structure` | One line per structure element, in reading order: type, mapped type, pages, language, alt text, actual text, expansion, title, table attributes (scope, headers, spans, summary), list numbering, links, how many of its marked-content references name no page (`mcids-without-page`, whose text can't be found), and the element's text |
| `artifacts` | Text marked as an artifact (headers, footers, page numbers), per page |
| `untagged` | Text that is neither tagged nor marked as an artifact (including tagged text that no structure element points to), and a count of such images and paths per page |
| `fonts` | Each font: name, type, whether it's embedded, whether it has a Unicode map, and the character codes its text uses that don't map to Unicode (that text can't be copied, searched or read aloud) |
| `annotations` | Every annotation (links, comments, form widgets and so on), page by page in the order each page lists them: type, `tagged` or `untagged`, rectangle, flags (such as `print` or `hidden`), link target, contents and accessible name |
| `forms` | Every form field: type, full name (`address.city`), page, accessible name, value, options, maximum length and flags (such as `required` or `read-only`) |
| `layout` | Every text run, image and painted vector path, page by page, each labeled `tagged`, `artifact` or `untagged`. Text runs: position, font, size, color, render mode. Images: position, size, pixel size, a hash of the pixels. Paths: bounding box, fill, stroke and line width |

## What you can ignore

Each `Section` value leaves out one section from the table above. Each `Field` value leaves out one detail wherever it appears:

| Field | Leaves out |
|---|---|
| `Language` | The document's language, each structure element's language and the XMP language |
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
| `AltText`, `ActualText` | Alternate and replacement text on structure elements |
| `Positions` | Where text, images, paths and annotations are, and the size of images, paths and annotations |
| `Colors` | Text color and path fill and stroke colors |
| `FontNames`, `FontSizes` | The font and size of each text run in the layout |
| `LineWidths` | The stroke width of each path |
| `ImageContents` | The pixel size and hash of each image |
| `LinkTargets` | Where links and other actions go |
| `AnnotationContents` | The text of annotations |
| `FormValues` | The value of each form field |

The options can also change what's printed:

| Option | Does |
|---|---|
| `ScrubDates()` | Replaces dates in quoted text with `Date_1`, `Date_2`, …: `2026-10-27`, `10/27/2026`, `October 27, 2026`, `27 Oct 2026`, each with an optional time, and PDF dates such as `D:20261027120000` |
| `ScrubGuids()` | Replaces GUIDs in quoted text with `Guid_1`, `Guid_2`, … |
| `ScrubText(regex, replacement)` | Replaces your own pattern in quoted text |
| `ShowProducerAndCreator()` | Prints the producer and creator values instead of only whether they're present |
| `OnlyPages(3, 4)` | Keeps only what is on those pages |

For Word documents, each `DocxSection` value leaves out one section, and each `DocxDetail` value leaves out one detail wherever it appears:

| DocxDetail | Leaves out |
|---|---|
| `ImageHashes` | The hash of each image, embedded object and control's data |
| `FieldResults` | Each field's `result=` and the result's text in the paragraph, also inside links, formatted text, tracked changes and content controls' values, so a PAGE or DATE field or a table of contents that shows something new doesn't fail the test. Positions after the result move back |
| `Fonts` | The font of styles and of text that differs from its style. A symbol's or a bullet's font stays, since it decides which character shows |
| `FontSizes` | The font size of styles and of text that differs from its style |
| `Colors` | Text color, highlight, cell shading and border colors |
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
| Alt text, a table header's scope, a cell's headers or a form field's or annotation's accessible name was removed or emptied, or a cell's headers point at a header that isn't there | `alt: "Logo" -> (missing)`, `scope: Column -> (missing)`, `tu: "Name" -> ""`, `headers: [TH1] -> [?h2]` |
| The document's or an element's language changed or was removed | `lang: en-US -> en-GB` |
| Something was added to the untagged content: a line, words in a line, or more images or paths. Less untagged content doesn't count, except that a text line that loses more than half its words shows as removed and added, and the added line counts | `added`, `"Page 1 {+Draft+}"` or `images: 1 -> 2` in `untagged` |
| An annotation became untagged | `... tagged ... -> ... untagged ...` in `annotations` |

The rules are fixed, so every project sees the same flags. DOCX and text snapshots have no flags. A test passes or fails on the whole snapshot as before; the flag only orders and marks the explanation. To fail only on these, compare with `CompareTo` and assert on `AffectsAccessibility`.

## Snapshot format

```
# pdfua-snapshot v1
document
  marked=yes lang=en-US display-doc-title=yes pages=3
...
structure
  Document lang=en-US
    H1 p1 "Drug Interaction Table"
    Figure p1 alt="Pharmacy logo"
    Table p2-3 summary="Drug interactions"
      TR
        TH1 p2 scope=Column "Drug"
        TH2 p2 scope=Column "Interacts with"
      TR
        TD p2 headers=[TH1] "Warfarin"
        TD p2-3 headers=[TH2] "Aspirin"
untagged
  p1 "Stray text"
  p1 images=1 paths=2
fonts
  Arial TrueType embedded=yes to-unicode=yes
layout
  p1 text tagged at=72,720 font=Arial size=18 color=gray:0 "Drug Interaction Table"
  p1 text untagged at=72,90 font=Arial size=9 color=gray:0 mode=invisible "Stray text"
  p1 image tagged at=72,600 size=120x40 pixels=600x200 hash=3f9c0a1b2d4e5f60
  p1 path artifact at=72,60 size=468x1 stroke=cmyk:0,0,0,1 width=0.5
```

- The first line is the format version. See [Format changes](#format-changes) for when it changes.
- When anything is ignored, the next lines list it: `# ignored sections: ...` and `# ignored fields: ...`. With `OnlyPages`, `# only pages: ...` follows.
- Sections always appear in the same order. An empty section prints `(none)` and an ignored one prints `(ignored)`, so something disappearing shows up as a change.
- Two spaces of indent per nesting level. Keys appear in a fixed order and are left out when they have no value.
- Text is quoted; `"` and `\` are escaped, and control characters print as `\uXXXX`.
- Table header cells that other cells refer to get stable names (`TH1`, `TH2`) instead of the PDF's internal IDs.
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

Text is normalized to Unicode NFC, with whitespace runs collapsed to one space and trimmed. A space is added between two pieces of text when the baseline changes or the gap is wider than 0.2 em. The same rule joins the pieces of an element's text, so a word a PDF splits across two tagged pieces stays one word. Pieces on different pages, and a piece that starts well to the left of where the last one ended on the same line, are always separated by a space.

## Format changes

A newer PDiffy can print some snapshots differently, when it records something new or reads something more accurately. Releases that change what a snapshot prints are minor versions (0.3.0, 0.4.0), and each one is listed below, so a failure after an upgrade can be told apart from a change to your document.

- **Some snapshots change:** a new line or key that only some documents have (one that prints only when set), or a reader fix. The format version stays the same, and the entry below says which snapshots change.
- **Nearly every snapshot changes:** a key renamed, a value printed another way, or a section added, removed or moved. The format version goes up, at most once per format per release. A failing test then says first that the verified file uses the old version, and still lists the document's changes below.

Either way, review the received files and accept them (`pdiffy accept --all`, or run the tests with `PDIFFY_ACCEPT=1`).

| Release | Change |
|---|---|
| 0.3.0 | `pdfua-snapshot v1`, unchanged version: when an element's text is split into tagged pieces, a space now goes between two pieces on the same page only where the page shows a gap (`"Num ber:"` becomes `"Number:"`). Snapshots with such elements change in `structure` |
| 0.3.0 | `pdfua-snapshot v1`, unchanged version: an element with marked-content references that name no page gets `mcids-without-page=N`. Only snapshots with such elements change |
| 0.3.0 | `docx-snapshot v1`, unchanged version: text boxes, equations, embedded and linked objects, ActiveX controls and absolute tabs (`w:ptab`) are read, a symbol whose code isn't hex is kept, text in right-to-left runs (`w:dir`, `w:bdo`) is read, and anything else in a paragraph PDiffy can't read prints as `unsupported`. Only snapshots of documents with those change |
| 0.3.0 | `docx-snapshot v1`, unchanged version: tables and cells get `width=`, `cell-margins=`/`margins=` and `borders=` when they set them; rows list the content controls around them and their tracked changes (`row-insert`, `row-delete`, `row-format`); a section lists a tracked change to its settings (`section-format`); a run inside paragraph properties keeps its tabs and breaks in `stray-text`. Only snapshots of documents with those change |
| 0.3.0 | `docx-snapshot v1`, unchanged version: a bookmark between paragraphs or tables prints as `bookmark "Name"` in place, content offered as `mc:AlternateContent` between them is read through its first choice, and anything else there PDiffy can't read prints as `unsupported`. Only snapshots of documents with those change |
| 0.3.0 | `docx-snapshot v1`, unchanged version: an ActiveX control's `hash=` also covers the binary part that holds its state (`activeX1.bin`), so ticking a check box or typing in a text box changes it, and a control part that isn't XML is listed as `unreadable`. Snapshots of documents with ActiveX controls change |
| 0.3.0 | `docx-snapshot v1`, unchanged version: a bookmark between table rows or cells prints as `bookmark "Name"` in place, rows and cells offered as `mc:AlternateContent` are read through the first choice, and anything else there PDiffy can't read prints as `unsupported`. Only snapshots of documents with those change |
| 0.3.0 | `docx-snapshot v1`, unchanged version: tracked changes to a table (`table-format`, `table-grid`), to the table properties a row sets for itself (`row-table-format`), to cells (`cell-insert`, `cell-delete`, `cell-merge`, `cell-format`) and to a paragraph mark's formatting (`paragraph-mark-format`) are recorded and listed by `Revisions()`. Only snapshots of documents with those change |
| 0.3.0 | `docx-snapshot v1`, unchanged version: a cell lists the content controls around it, as a row does, each marked `around` so it can't be mistaken for a control inside the cell. Only snapshots of documents with content controls around cells change |
| 0.3.0 | `docx-snapshot v1`, unchanged version: a table-column bookmark (one that sets `w:colFirst` and `w:colLast`) prints the columns it covers, counting from 0: `bookmark "Doses" columns=0-2`. Only snapshots of documents with table-column bookmarks change |
| 0.2.0 | New format `docx-snapshot v1`, for Word documents |
| 0.1.0 | New format `pdfua-snapshot v1` |

## Damaged files

A page that can't be loaded (for example, one with a damaged font) comes back marked unreadable, and its snapshot line says so, instead of the whole read failing. The same goes for an attachment whose contents can't be decoded. A file that can't be opened at all throws.

## Development

```
dotnet build
dotnet test
```

The tests build their own small PDFs in code (`tests/PDiffy.Tests.Unit/Pdfs`): `TestPdf` writes tagged documents, and `RawPdf` writes hand-made malformed ones. No real-world PDFs are checked in.

Pushes to main and pull requests are built and tested on Windows and Linux. Pushing a version tag such as `v0.1.0` also publishes that version of both packages to nuget.org.

## License

MIT. See [LICENSE](https://github.com/zacharydf7/pdiffy/blob/main/LICENSE).
