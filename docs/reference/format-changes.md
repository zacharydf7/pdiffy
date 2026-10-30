# Format changes

A newer PDiffy can print some snapshots differently, when it records something new or reads something more accurately.
Releases that change what a snapshot prints are minor versions (0.3.0, 0.4.0), and each one is listed below, so a
failure after an upgrade can be told apart from a change to your document.

- **Some snapshots change** when there's a new line or key that only some documents have, or a reader fix. The format
  version stays the same, and the entry below says which snapshots change.
- **Nearly every snapshot changes** when a key is renamed, a value is printed another way, or a section is added,
  removed or moved. The format version goes up, at most once per format per release. A failing test says first that
  the verified file uses the old version, and still lists the document's changes.

Either way, review the received files and accept them: `pdiffy accept --all`, or run the tests with `PDIFFY_ACCEPT=1`.

## Next release

### Accessibility flags

No snapshot changes, but more changes are flagged as
[affecting accessibility](accessibility-changes.md): tagged content made an artifact or untagged, `marked` or
`display-doc-title` turned off, the title removed, replacement text or an expansion removed, a page's tab order no
longer `S`, a font's text no longer mapping to Unicode, and permissions that stop text extraction for accessibility.
Tests that assert on `AffectsAccessibility` can fail where they passed.

## 0.3.0

### PDF (`pdfua-snapshot v1`, unchanged)

- When an element's text is split into tagged pieces, a space now goes between two pieces on the same page only where
  the page shows a gap (`"Num ber:"` becomes `"Number:"`). Snapshots with such elements change in `structure`.
- An element with marked-content references that name no page gets `mcids-without-page=N`. Only snapshots with such
  elements change.
- XMP metadata is read as UTF-8, as the XMP standard requires, so a non-ASCII value such as `Café` no longer prints as
  `CafÃ©`. XMP that isn't well-formed XML, or has a DTD, prints `xmp=unreadable` instead of failing the read. Snapshots
  with non-ASCII XMP values change.
- An open action that chains others with `/Next` lists them all, in the order they run:
  `open-action="GoTo p1 Fit, Launch"`. Only snapshots of documents with chained open actions change.
- An element whose children are nested more than 256 deep gets `unread-children=N`, and bookmarks nested more than 256
  deep, or that loop back on themselves, print `unreadable`. Such files used to overflow the stack, so no approved
  snapshot changes.

### Word (`docx-snapshot v1`, unchanged)

Only snapshots of documents that use these change:

- Text boxes, equations, embedded and linked objects, ActiveX controls and absolute tabs (`w:ptab`) are read; a symbol
  whose code isn't hex is kept; text in right-to-left runs (`w:dir`, `w:bdo`) is read; anything else in a paragraph
  PDiffy can't read prints as `unsupported`.
- Tables and cells get `width=`, `cell-margins=`/`margins=` and `borders=` when they set them. Rows list the content
  controls around them and their tracked changes (`row-insert`, `row-delete`, `row-format`). A section lists a tracked
  change to its settings (`section-format`). A run inside paragraph properties keeps its tabs and breaks in
  `stray-text`.
- A bookmark between paragraphs, tables, rows or cells prints as `bookmark "Name"` in place. Content offered as
  `mc:AlternateContent` there is read through its first choice, and anything else PDiffy can't read prints as
  `unsupported`.
- An ActiveX control's `hash=` also covers the binary part that holds its state (`activeX1.bin`), so ticking a check box
  or typing in a text box changes it. A control part that isn't XML is listed as `unreadable`.
- Tracked changes to a table (`table-format`, `table-grid`), to the table properties a row sets for itself
  (`row-table-format`), to cells (`cell-insert`, `cell-delete`, `cell-merge`, `cell-format`) and to a paragraph mark's
  formatting (`paragraph-mark-format`) are recorded and listed by `Revisions()`.
- A cell lists the content controls around it, each marked `around`.
- A table-column bookmark prints the columns it covers, counting from 0: `bookmark "Stages" columns=0-2`.
- A part with a DTD, with elements nested more than 256 deep, or larger than 256 MB is listed as `unreadable`, and an
  inserted document past one of those limits prints `Inserted docx unreadable`. Word doesn't write such parts.

## 0.2.0

New format `docx-snapshot v1`, for Word documents.

## 0.1.0

New format `pdfua-snapshot v1`.
