# Options at a glance

Every option, in one place. The [Ignore and scrub](../guide/options.md) guide shows them in use.

## PDF profiles

[`PdfSnapshotOptions.Use(PdfProfile)`](xref:PDiffy.PdfSnapshotOptions.Use*) leaves out what a test doesn't guard, in one
call. Further `Ignore` calls leave out more, and everything a profile doesn't leave out is compared.

| PdfProfile | For tests that guard | Leaves out |
|---|---|---|
| `Strict` | Everything | Nothing, the same as no profile |
| `Content` | The text and its structure: tags, reading order, bookmarks, annotations and forms | The `Settings`, `Metadata`, `Fonts` and `Layout` sections, `PageBoxes` and `Positions` |
| `Accessibility` | What assistive technology uses: tags, alt text, language, reading order, untagged content, artifacts, annotations, forms, whether fonts map to Unicode, and permissions, which can stop a screen reader from reading the text | How content looks (`PageBoxes`, `Positions`, `Colors`, `FontNames`, `FontSizes`, `LineWidths`, `ImageContents`), settings that don't change what's read out (`PdfVersion`, `OpenAction`, `EmbeddedFileContents`) and metadata other than the title (`Author`, `Subject`, `Keywords`, `Dates`, `Producer`, `Creator`, `CustomInfo`) |
| `Layout` | Everything on the page, including where it's drawn and how it looks | The `Settings` and `Metadata` sections |

## PDF sections

[`PdfSnapshotOptions.Ignore(params PdfSection[])`](xref:PDiffy.PdfSnapshotOptions.Ignore*) leaves whole sections out. Each prints
`(ignored)` instead of its contents.

| Section | Contains |
|---|---|
| `Document` | Whether the PDF is tagged, its language, whether the viewer shows the title, page count |
| `Settings` | PDF version, page mode and layout, open action, JavaScript, encryption and permissions, viewer preferences, layers, embedded files |
| `Metadata` | Title, author, subject, keywords, whether dates, producer and creator are present, PDF/UA and PDF/A identifiers, other Info entries |
| `RoleMap` | Each custom tag type and the standard type it maps to |
| `Pages` | Size, rotation, tab order, page boxes, unreadable pages, page labels |
| `Bookmarks` | The outline tree, with target pages |
| `Structure` | The tag tree in reading order, with text, alt text and table attributes |
| `Artifacts` | Text marked as an artifact (headers, footers, page numbers) |
| `Untagged` | Content that is neither tagged nor an artifact |
| `Fonts` | Each font, whether it's embedded, and codes that don't map to Unicode |
| `Annotations` | Links, comments, widgets and other annotations |
| `Forms` | Every form field |
| `Layout` | Every text run, image and path, with position and style |

## PDF details

[`PdfSnapshotOptions.Ignore(params PdfDetail[])`](xref:PDiffy.PdfSnapshotOptions.Ignore*) leaves one detail out wherever it
appears; the rest of each line is still compared.

| Detail | Leaves out |
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

## PDF options that change what's printed

| Option | Does |
|---|---|
| [`ScrubDates()`](xref:PDiffy.PdfSnapshotOptions.ScrubDates) | Replaces dates in quoted text with `Date_1`, `Date_2`, ...: `2026-10-27`, `10/27/2026`, `October 27, 2026`, `27 Oct 2026`, each with an optional time, and PDF dates such as `D:20261027120000` |
| [`ScrubGuids()`](xref:PDiffy.PdfSnapshotOptions.ScrubGuids) | Replaces GUIDs in quoted text with `Guid_1`, `Guid_2`, ... |
| [`ScrubText(regex, replacement)`](xref:PDiffy.PdfSnapshotOptions.ScrubText*) | Replaces your own pattern in quoted text. The replacement can refer to groups, such as `$1` |
| [`ShowProducerAndCreator()`](xref:PDiffy.PdfSnapshotOptions.ShowProducerAndCreator) | Prints the producer and creator values instead of only whether they're present |
| [`OnlyPages(3, 4)`](xref:PDiffy.PdfSnapshotOptions.OnlyPages*) | Keeps only what is on those pages |
| [`Tolerate(PdfTolerance.Positions, 1)`](xref:PDiffy.PdfSnapshotOptions.Tolerate*) | Lets positions differ by up to 1 pt without failing the test; also takes `FontSizes` and `LineWidths`. The header lists it as `# tolerances: positions 1pt` |

## Word profiles

[`DocxSnapshotOptions.Use(DocxProfile)`](xref:PDiffy.DocxSnapshotOptions.Use*):

| DocxProfile | For tests that guard | Leaves out |
|---|---|---|
| `Strict` | Everything | Nothing, the same as no profile |
| `Content` | The text and its structure: paragraphs, tables, lists, headers, footers, notes and inline formatting such as bold | The `Sections` and `Styles` sections, and `Fonts`, `FontSizes`, `Colors`, `Spacing` and `Indents` |

The snapshot's header lists the sections and fields a profile leaves out, not the profile's name. If a later release
changes what a profile leaves out, the header changes too, so the test fails and says so instead of quietly checking
less.

## Word sections

[`DocxSnapshotOptions.Ignore(params DocxSection[])`](xref:PDiffy.DocxSnapshotOptions.Ignore*):

| DocxSection | Contains |
|---|---|
| `Document` | Title, subject, language, compatibility mode, tracked changes, protection and header settings |
| `Sections` | Each section's page size, margins, columns, page numbering and which headers and footers it has |
| `Styles` | The styles the document uses, with their properties resolved |
| `Numbering` | The lists the document uses and how each level is numbered |
| `Body` | Paragraphs, tables and inserted documents, with everything inside them |
| `Headers`, `Footers` | Each section's headers and footers |
| `Notes` | Footnotes, endnotes and comments |

## Word details

[`DocxSnapshotOptions.Ignore(params DocxDetail[])`](xref:PDiffy.DocxSnapshotOptions.Ignore*):

| DocxDetail | Leaves out |
|---|---|
| `ImageHashes` | The hash of each image, embedded object and control's data |
| `FieldResults` | Each field's `result=` and the result's text in the paragraph, also inside links, formatted text, tracked changes and content controls' values. Positions after the result move back |
| `Fonts` | The font of styles and of text that differs from its style. A symbol's or a bullet's font stays, since it decides which character shows |
| `FontSizes` | The font size of styles and of text that differs from its style |
| `Colors` | Text color, highlight, cell and paragraph shading, and border colors |
| `Spacing` | Space before and after paragraphs, and line spacing |
| `Indents` | Left, right, first-line and hanging indents of paragraphs, styles and list levels |
| `RevisionAuthors` | Who made each tracked change |
| `CommentAuthors` | Who wrote each comment |

`ScrubDates()`, `ScrubGuids()` and `ScrubText(regex, replacement)` work as for PDFs, and also apply to link addresses.

## Text and JSON options

[`TextSnapshotOptions`](xref:PDiffy.TextSnapshotOptions) scrubs the whole text:

| Option | Does |
|---|---|
| `ScrubText(regex, replacement)` | Replaces your pattern anywhere |
| `ScrubDates()` | Replaces dates anywhere |
| `ScrubGuids()` | Replaces GUIDs anywhere |
| `KeepDates()` | Keeps timestamp strings in JSON, which are scrubbed by default |
| `KeepGuids()` | Keeps GUID strings in JSON, which are scrubbed by default |

## Defaults for the whole suite

| Property | Applies to | Combines with a call's `configure` |
|---|---|---|
| [`Snapshot.DefaultPdfOptions`](xref:PDiffy.Snapshot.DefaultPdfOptions) | `Snapshot.VerifyPdfAsync` and `VerifyPdf` | Runs first; the call adds to it and can't bring back what it leaves out |
| [`Snapshot.DefaultDocxOptions`](xref:PDiffy.Snapshot.DefaultDocxOptions) | `Snapshot.VerifyDocxAsync` and `VerifyDocx` | Runs first; the call adds to it and can't bring back what it leaves out |
| [`Snapshot.DefaultTextOptions`](xref:PDiffy.Snapshot.DefaultTextOptions) | `Snapshot.VerifyTextAsync`, `VerifyJsonAsync`, `VerifyText` and `VerifyJson` | Runs first (after JSON's own scrubbing); a call's `KeepDates()` or `KeepGuids()` undoes a default scrub |

Profiles and ignores add up: a default `Use(PdfProfile.Accessibility)` and a call's `Ignore(PdfSection.Fonts)` leave
out both, and a call's `Use(PdfProfile.Strict)` leaves out nothing more, so it doesn't undo the default.
`ToSnapshot` and `CompareTo` don't use the defaults.

## Environment variables

| Variable | Does |
|---|---|
| `PDIFFY_ACCEPT=1` | Accepts every received snapshot as the new verified one. Ignored when `CI` or `TF_BUILD` is set |
| `PDIFFY_OPEN_REPORT=1` | Opens the HTML report of each snapshot that differs from its verified file in the default browser. Ignored when `CI` or `TF_BUILD` is set |
| `PDIFFY_RUN_LOG=<folder>` | Logs the verified file each snapshot call uses, one log per test process, for [`pdiffy unused`](command-line.md#unused). Use a full path, since each test process resolves a relative one from its own folder |
