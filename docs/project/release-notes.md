# Release notes

What each release added and fixed, newest first. A release that changes what a snapshot prints also lists exactly
which snapshots change in [Format changes](../reference/format-changes.md), so a failure after an upgrade can be told
apart from a change to your document. After upgrading, review the received files and accept them with
`pdiffy accept --all`.

## Next release (not yet tagged)

Everything below is on `main`.

### Breaking API changes

PDiffy is new, so its API can still change before 1.0. Snapshots don't change with these; only code that uses the
library does.

- PDF types carry a `Pdf` prefix, as Word types carry `Docx`, so they no longer clash with PdfPig's
  ([#91](https://github.com/zacharydf7/pdiffy/issues/91)):

  | Before | Now |
  |---|---|
  | `SnapshotOptions`, `Section`, `Field`, `SnapshotProfile` | `PdfSnapshotOptions`, `PdfSection`, `PdfDetail`, `PdfProfile` |
  | `Annotation`, `AnnotationFlags`, `Bookmark`, `Destination` | `PdfAnnotation`, `PdfAnnotationFlags`, `PdfBookmark`, `PdfDestination` |
  | `DocumentInfo`, `DocumentSettings`, `DocumentPermissions`, `Metadata` | `PdfDocumentInfo`, `PdfSettings`, `PdfPermissions`, `PdfMetadata` |
  | `EmbeddedFile`, `Layer`, `Setting`, `RoleMapping`, `CheckFinding` | `PdfEmbeddedFile`, `PdfLayer`, `PdfSetting`, `PdfRoleMapping`, `PdfCheckFinding` |
  | `FontInfo`, `ImageInfo`, `PathInfo`, `TextRun`, `TextRenderMode`, `ContentRole` | `PdfFont`, `PdfImage`, `PdfVectorPath`, `PdfTextRun`, `PdfTextRenderMode`, `PdfContentRole` |
  | `PageInfo`, `PageBox`, `PageGroup`, `PageLabelRange`, `PageText`, `PageLayouts`, `PageModes` | `PdfPage`, `PdfBox`, `PdfPageGroup`, `PdfPageLabelRange`, `PdfPageText`, `PdfPageLayouts`, `PdfPageModes` |
  | `StructureElement`, `Heading`, `MarkedSpan` | `PdfStructureElement`, `PdfHeading`, `PdfMarkedSpan` |
  | `FormField`, `FormFieldType`, `FormFieldFlags`, `FormFieldAlignment`, `FormScript`, `FormScriptTrigger`, `FormSettings`, `XfaFormKind` | The same with `Pdf` in front, and `PdfXfaForm` |
  | `SignatureInfo`, `SignatureCertification` | `PdfSignature`, `PdfSignatureCertification` |

- `Lang` and `Alt` are `Language` and `AltText`, as in the Word model, and `HasCreationDate` and `HasModDate` are
  `HasCreatedDate` and `HasModifiedDate`.
- `Tolerate` takes a `PdfTolerance` (`Positions`, `FontSizes` or `LineWidths`), so only a number it can tolerate
  compiles.
- `PdfAnnotation.HasDestination` is gone: it covered only a link's own destination entry, not a GoTo action's.
  `Destination` says where any link goes.
- Model types (`PdfStructureElement`, `PdfTextRun`, `DocxParagraph` and the rest) compare by content: two loads of
  the same document are equal, since their lists compare item by item, and a failing `Assert.Equal` prints each list's
  items. Their properties are `init` properties rather than constructor parameters, and the constructors are internal,
  so a later release can add a property without breaking code that reads them
  ([#92](https://github.com/zacharydf7/pdiffy/issues/92)).
- `SnapshotChange.Section` is gone. `SectionName` holds the section as the snapshot prints it, the same string as
  the JSON's `section`, and `PdfSection` or `DocxSection` gives it as an enum for its kind of snapshot. Word changes
  now say which section they're in. `AffectsAccessibility` is a `bool?`: null for Word and text snapshots, which
  PDiffy doesn't assess, so assert on `AffectsAccessibility == true`. The JSON's `affectsAccessibility` is `null`
  for them too, where it was `false`, so the JSON from `ToJson`, `pdiffy <before> <after> --json` and
  `pdiffy compare --json` is now `version` 2
  ([#94](https://github.com/zacharydf7/pdiffy/issues/94)).
- Word kinds PDiffy decides are enums, so a typo no longer compiles: `DocxRevision.Kind` is a `DocxRevisionKind`,
  `DocxForm.Kind` and `DocxControl.Kind` a `DocxFormKind`, and `DocxNote.Kind` and `DocxNoteReference.Kind` a
  `DocxNoteKind`. Snapshots print them as before ([#93](https://github.com/zacharydf7/pdiffy/issues/93)).
- Values PDiffy copies from the file stay strings, and new classes list their standard values, such as
  `PdfAnnotationTypes.Link`, `PdfTableScopes.Column` and `DocxAlignments.Justified` (`"both"`); the
  [API reference](../api/index.md) lists them all.
- Word values that packed several parts into one string are small records: borders are `DocxBorder` (side, style,
  width and color), cell margins `DocxMargin`, tab stops `DocxTabStop`, line spacing `DocxLineSpacing`, table and
  cell widths `DocxWidth` and row heights `DocxRowHeight`, and a table bookmark's `Columns` is now `FirstColumn` and
  `LastColumn`. Snapshots print them as before, except a percentage width written in an unusual way: `50.0%` now
  prints as `50%`, and one that isn't a number is left out.
- Every snapshot test starts from `Snapshot`: `VerifyPdfAsync`, `VerifyDocxAsync`, `VerifyTextAsync` and
  `VerifyJsonAsync`, their synchronous versions (`VerifyPdf` and so on), and `DefaultPdfOptions`,
  `DefaultDocxOptions` and `DefaultTextOptions`, next to the settings that apply to every kind, such as `Directory`.
  `PdfSnapshot`, `DocxSnapshot`, `Snapshot.VerifyAsync`, `Snapshot.Verify` and `Snapshot.DefaultOptions` still work
  in this release, marked obsolete ([#95](https://github.com/zacharydf7/pdiffy/issues/95)).
- `SnapshotDiff.Compare(string, string)` is now `CompareSnapshots`, so two file paths passed by mistake no longer
  compile. `SnapshotDiff.Compare(before, after)` compares two PDFs or two Word documents, with the earlier one
  first, and `SnapshotDiff.CompareFiles(beforePath, afterPath)` compares two files the way `pdiffy before after`
  does. `CompareTo` names its parameter `after`.

## 0.4.0 (2026-10-30)

The snapshot format stays `pdfua-snapshot v1` and `docx-snapshot v1`, but many PDF snapshots gain keys or
lines; see [Format changes](../reference/format-changes.md#040) for each one.

### Snapshots record more

- Links and bookmarks print where they go: page, view, positions and zoom, a named destination looked up, or the file
  they open ([#83](https://github.com/zacharydf7/pdiffy/issues/83)). The open action reads its destination the same
  way ([#144](https://github.com/zacharydf7/pdiffy/issues/144)).
- Rotated and vertical text is read along its own baseline instead of as scattered letters
  ([#85](https://github.com/zacharydf7/pdiffy/issues/85)), and letter-spaced text no longer gets a space between every
  letter ([#86](https://github.com/zacharydf7/pdiffy/issues/86)).
- Marked content inside an element records its own language, replacement text, alt text and expansion with the words
  it covers, and artifacts print their type ([#87](https://github.com/zacharydf7/pdiffy/issues/87),
  [#147](https://github.com/zacharydf7/pdiffy/issues/147)).
- Signatures, form field behavior (defaults, export values, alignment, text style, scripts) and form-wide settings
  ([#88](https://github.com/zacharydf7/pdiffy/issues/88)).
- Each figure prints the hash of the image it draws, so swapped images show
  ([#90](https://github.com/zacharydf7/pdiffy/issues/90)).
- `javascript=` also counts scripts in links and other annotations, page actions, bookmarks and Rendition actions
  ([#149](https://github.com/zacharydf7/pdiffy/issues/149), [#153](https://github.com/zacharydf7/pdiffy/issues/153)).
- Content drawn in an optional-content layer lists the layer (`layers=["Draft"]`)
  ([#118](https://github.com/zacharydf7/pdiffy/issues/118), in part).
- Untagged text prints one line per text line, so an added line shows alone
  ([#117](https://github.com/zacharydf7/pdiffy/issues/117)).
- Word documents record hidden text, double strikethrough, right-to-left text, run languages, paragraph borders and
  shading, and table alt text, row heights and cell alignment
  ([#84](https://github.com/zacharydf7/pdiffy/issues/84)).
- Elements with marked content inside a form XObject are marked `mcids-in-xobject=N`
  ([#89](https://github.com/zacharydf7/pdiffy/issues/89), first step).

### Read more accurately

- An artifact split across nested marked content is read in drawing order
  ([#152](https://github.com/zacharydf7/pdiffy/issues/152)).
- A PDF that refers to an object it doesn't have, has a bookmark without a title, or has an image that can't be
  decoded now loads instead of throwing ([#155](https://github.com/zacharydf7/pdiffy/issues/155)).
- A PDF too damaged to open, or encrypted with a password, throws `InvalidDataException`, as input that isn't a PDF
  does, instead of an exception from the PDF library underneath
  ([#156](https://github.com/zacharydf7/pdiffy/issues/156)). A file or other stream that fails part way through throws
  its own exception, since every stream that isn't already in memory is read whole before parsing.

### Accessibility flags

- More changes are flagged as affecting accessibility: tagged content made an artifact, a lost title, replacement
  text or expansion removed, a page's tab order changed, a font losing its Unicode map, permissions that stop text
  extraction, and more ([#82](https://github.com/zacharydf7/pdiffy/issues/82)).

### Tests and the API

- `Load` and the snapshot methods take streams at any position and byte arrays, and say plainly when the input isn't a
  PDF or a Word document ([#96](https://github.com/zacharydf7/pdiffy/issues/96)).
- Two calls that would share one snapshot file fail with a message, and `variant:` names a second snapshot in one test
  ([#97](https://github.com/zacharydf7/pdiffy/issues/97)).
- `PdfSnapshot.DefaultOptions`, `DocxSnapshot.DefaultOptions` and `Snapshot.DefaultOptions` set options once for a
  suite ([#98](https://github.com/zacharydf7/pdiffy/issues/98)).
- `Snapshot.ProjectFolder("Snapshots")` finds the test project's folder from any output folder
  ([#99](https://github.com/zacharydf7/pdiffy/issues/99)).
- Structure elements have `AllText`, `Path`, `Descendants()`, `Elements(type)` and `TextRuns`, and documents have
  `Headings()` ([#100](https://github.com/zacharydf7/pdiffy/issues/100),
  [#148](https://github.com/zacharydf7/pdiffy/issues/148)).
- A missing `await` fails the test: `VerifyAsync` throws at the call, and synchronous `Verify` methods were added
  ([#102](https://github.com/zacharydf7/pdiffy/issues/102)).
- Every public member has a doc comment ([#101](https://github.com/zacharydf7/pdiffy/issues/101)).

### Failure messages and tools

- A failure prints the `dotnet pdiffy accept` command for its file
  ([#104](https://github.com/zacharydf7/pdiffy/issues/104)), says when the change looks like a missing font
  ([#105](https://github.com/zacharydf7/pdiffy/issues/105)), and a new snapshot shows its size by section
  ([#106](https://github.com/zacharydf7/pdiffy/issues/106)).
- A reflow that shifts many lines is described as one change
  ([#108](https://github.com/zacharydf7/pdiffy/issues/108)), and a move names the old and new parent
  ([#151](https://github.com/zacharydf7/pdiffy/issues/151)).
- `pdiffy --version` and `--help` ([#103](https://github.com/zacharydf7/pdiffy/issues/103)).
- `pdiffy unused`, with `PDIFFY_RUN_LOG`, finds snapshots no test uses any more
  ([#114](https://github.com/zacharydf7/pdiffy/issues/114)).
- `PDIFFY_OPEN_REPORT=1` opens a failing snapshot's report in the browser, never on CI
  ([#122](https://github.com/zacharydf7/pdiffy/issues/122)).

### Docs

- [Catch generator upgrades](../guide/generator-upgrades.md) ([#116](https://github.com/zacharydf7/pdiffy/issues/116))
  and [Recipes for generators](../guide/recipes.md) ([#123](https://github.com/zacharydf7/pdiffy/issues/123)).

## 0.3.2 (2026-10-29)

- A table cell that only gained attributes is reported as an attribute change, not moved or removed and added
  ([#47](https://github.com/zacharydf7/pdiffy/issues/47)).
- Each removed line pairs with the most alike line nearby, not the first similar one
  ([#49](https://github.com/zacharydf7/pdiffy/issues/49)).
- Every docs sample uses one neutral trail guide theme.

## 0.3.1 (2026-10-29)

- Tests against tagged PDFs written by PDFsharp, asserting what must hold whatever wrote them
  ([#40](https://github.com/zacharydf7/pdiffy/issues/40)).
- README and docs fixes.

## 0.3.0 (2026-10-29)

- Profiles that leave out, in one call, what a test doesn't guard: `Content`, `Accessibility` and `Layout`
  ([#30](https://github.com/zacharydf7/pdiffy/issues/30)), and tolerances for positions, font sizes and line widths
  ([#31](https://github.com/zacharydf7/pdiffy/issues/31)).
- Built-in checks, `document.Check()` and `pdiffy check`
  ([#32](https://github.com/zacharydf7/pdiffy/issues/32)).
- `pdiffy compare` for two folders ([#33](https://github.com/zacharydf7/pdiffy/issues/33)), `pdiffy report`
  ([#7](https://github.com/zacharydf7/pdiffy/issues/7)), `pdiffy coverage`
  ([#38](https://github.com/zacharydf7/pdiffy/issues/38)) and JSON output for diffs
  ([#8](https://github.com/zacharydf7/pdiffy/issues/8)).
- The HTML report draws a map of each page with a layout change
  ([#36](https://github.com/zacharydf7/pdiffy/issues/36)).
- Diffs pair lines by their text, report an inserted page once, report a changed element type as one change, and list
  accessibility changes first ([#4](https://github.com/zacharydf7/pdiffy/issues/4),
  [#29](https://github.com/zacharydf7/pdiffy/issues/29), [#23](https://github.com/zacharydf7/pdiffy/issues/23),
  [#5](https://github.com/zacharydf7/pdiffy/issues/5)).
- Limits for untrusted input: no DTDs, a 256 MB cap on DOCX parts and decompressed PDF streams, and depth limits
  ([#34](https://github.com/zacharydf7/pdiffy/issues/34), [#43](https://github.com/zacharydf7/pdiffy/issues/43)).
- Many more DOCX elements recorded: text boxes, equations, objects, table widths and borders, tracked changes to
  tables, content controls and bookmarks around rows and cells
  ([#1](https://github.com/zacharydf7/pdiffy/issues/1), [#11](https://github.com/zacharydf7/pdiffy/issues/11),
  [#22](https://github.com/zacharydf7/pdiffy/issues/22)).
- A version policy for snapshot formats, and the [Format changes](../reference/format-changes.md) page
  ([#6](https://github.com/zacharydf7/pdiffy/issues/6)).
- The documentation site, a sample report and the [Limitations](../reference/limitations.md) and
  [Performance](../reference/performance.md) pages ([#35](https://github.com/zacharydf7/pdiffy/issues/35),
  [#37](https://github.com/zacharydf7/pdiffy/issues/37)).

## 0.2.0 (2026-10-28)

- Word documents: `DocxDocument`, `DocxSnapshot.VerifyAsync` and the `docx-snapshot v1` format, covering settings,
  sections, styles, paragraphs, lists, tables, images, links, headers, footers, fields, notes, comments, tracked
  changes and form controls.
- Text and JSON snapshots: `Snapshot.VerifyAsync` and `Snapshot.VerifyJsonAsync`, with scrubbing.

## 0.1.0 (2026-10-27)

- The first release: `PdfUaDocument`, PDF snapshots in the `pdfua-snapshot v1` format, `PdfSnapshot.VerifyAsync`,
  `SnapshotDiff` with its HTML report, and the `pdiffy` command-line tool.
