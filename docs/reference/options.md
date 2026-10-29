# Options at a glance

Every option, in one place. The [Ignore and scrub](../guide/options.md) guide shows them in use.

## PDF sections

[`SnapshotOptions.Ignore(params Section[])`](xref:PDiffy.SnapshotOptions.Ignore*) leaves whole sections out. Each prints
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

## PDF fields

[`SnapshotOptions.Ignore(params Field[])`](xref:PDiffy.SnapshotOptions.Ignore*) leaves one detail out wherever it
appears; the rest of each line is still compared.

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

## PDF options that change what's printed

| Option | Does |
|---|---|
| [`ScrubDates()`](xref:PDiffy.SnapshotOptions.ScrubDates) | Replaces dates in quoted text with `Date_1`, `Date_2`, ...: `2026-10-27`, `10/27/2026`, `October 27, 2026`, `27 Oct 2026`, each with an optional time, and PDF dates such as `D:20261027120000` |
| [`ScrubGuids()`](xref:PDiffy.SnapshotOptions.ScrubGuids) | Replaces GUIDs in quoted text with `Guid_1`, `Guid_2`, ... |
| [`ScrubText(regex, replacement)`](xref:PDiffy.SnapshotOptions.ScrubText*) | Replaces your own pattern in quoted text. The replacement can refer to groups, such as `$1` |
| [`ShowProducerAndCreator()`](xref:PDiffy.SnapshotOptions.ShowProducerAndCreator) | Prints the producer and creator values instead of only whether they're present |
| [`OnlyPages(3, 4)`](xref:PDiffy.SnapshotOptions.OnlyPages*) | Keeps only what is on those pages |

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
| `Colors` | Text color, highlight, cell shading and border colors |
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

## Environment variables

| Variable | Does |
|---|---|
| `PDIFFY_ACCEPT=1` | Accepts every received snapshot as the new verified one. Ignored when `CI` or `TF_BUILD` is set |
