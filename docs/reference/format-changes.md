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

### Checks

- `PdfCheckSet.Latest` has seven new rules, `RoleMap`, `XmpMetadata`, `TaggedPdf`, `PdfVersion`, `ListStructure`,
  `TocStructure` and `HeadingTypes`, so a snapshot that records checks with it prints `# checks: latest, 19 rules` and
  what they find ([#62](https://github.com/zacharydf7/pdiffy/issues/62)-[#65](https://github.com/zacharydf7/pdiffy/issues/65)). A release's own set, such as `PdfCheckSet.Version0_3`,
  doesn't change.

### Document setup

- The `document` line prints `suspects=yes` when MarkInfo says the tags may be unreliable, and a structure element with
  no parent entry gets `parent=missing` ([#63](https://github.com/zacharydf7/pdiffy/issues/63)). Only snapshots of
  such documents change.
- A file whose header says PDF 2.0 prints `version=2.0`. PDiffy read the version through PdfPig, which reads a
  header it doesn't know as 1.4, so such snapshots printed `version=1.4` and change.

### Accessibility flags

- A new problem in the `checks` section from a rule that isn't a house rule is flagged as affecting
  accessibility ([#56](https://github.com/zacharydf7/pdiffy/issues/56)). No snapshot changes.

### PDF (`pdfua-snapshot v1`, unchanged)

- A test can opt into a `checks` section with `Check(PdfCheckSet)`: what the checks find, one line per
  finding with its rule, page and path, after a `# checks: ...` header line that names the rule set
  ([#55](https://github.com/zacharydf7/pdiffy/issues/55)). Snapshots without the option don't change.
- An element whose marked content is in a form XObject, such as a stamp the page draws, reads its text from the form
  alone ([#89](https://github.com/zacharydf7/pdiffy/issues/89)). A form numbers its MCIDs apart from the page, and
  before, when the two used the same MCID, both the page's element and the form's printed the text of both. Only
  snapshots of documents whose forms reuse the page's MCIDs change, and text a form tags that no element points to now
  shows in `untagged`.

## 0.4.0

### PDF (`pdfua-snapshot v1`, unchanged)

- A PDF that refers to an object it doesn't have, has a bookmark without a title, or has an image that can't be
  decoded now loads: the reference reads as nothing, as the PDF standard says, the bookmarks print `unreadable`, and
  the image is hashed as stored. Such files used to throw, so no approved snapshot changes.
- Text, images and paths drawn inside optional-content marked content (`/OC ... BDC`), a layer a viewer can hide or
  leave out of print, list the layers they're in on their layout lines (`layers=["Draft"]`), outermost first, and text
  runs end where a layer starts or ends. Wrapping a watermark or draft text in a layer used to give the same snapshot.
  Only snapshots of documents with such content change. A run that a layer now splits shows as changed to its first
  part, with the rest added. An image or form that puts itself in a layer with its own `/OC` entry isn't read yet.
- Untagged text prints one `untagged` line per line of text instead of one per page, so a line added to an untagged
  page shows as that line alone. Snapshots of documents with untagged text on more than one line of a page change: the
  old page line usually shows as removed and each line as added (or, when the first line holds most of the page's
  words, as changed to that line), and the added lines are flagged as affecting accessibility like any new untagged
  text. `PdfCheck.UntaggedContent` and `pdiffy check` report untagged text once per page, now including tagged text no
  structure element points to, which used to be a finding per marked-content ID.
- `javascript=` in `settings` now also says `yes` when a link or other annotation, a page's open or close actions, or a
  bookmark runs JavaScript, including JavaScript chained after another action with `/Next`. A Rendition action (which
  plays media) with a script of its own (`/JS`) now counts as JavaScript too, wherever it runs, and a form field lists
  it in `scripts=`. Only snapshots of documents with such scripts change. `PdfCheck.NoActions` and `pdiffy check` now
  report them.
- The open action reads its destination the way links do: a named destination is looked up and prints its page
  (`open-action="GoTo p2 Fit"`, not `"GoTo named intro"`), positions are whole points with `-` for one the viewer
  keeps, an XYZ view's zoom prints only when it sets one (`p1 XYZ 72 720`, not `p1 XYZ 72 720 0`), and a page given as
  a number counts from 0, as it does for links (`p1 Fit`, not `p? Fit`). A destination that isn't one, such as a bare
  number, is left out instead of printing as `named 5`. Only snapshots of documents whose open action is named, has
  fractional positions, keeps a position or the zoom, gives its page as a number or is malformed change.
- An artifact whose text is split across nested marked content is read in the order it's drawn (`"Page 1"`, not
  `"1Page"`). Only snapshots of documents with such artifacts change, and their artifact text was wrong. A short artifact,
  such as a page number, can show as removed and added, since the old text shares no word with the new.
- A form that sets a calculation order, asks the viewer to draw its fields (`NeedAppearances`) or carries XFA prints a
  `form` line first in `forms` (`form calculation-order=["trip.tax","trip.total"] need-appearances=yes xfa=dynamic`),
  and `javascript=` in `settings` now also says `yes` when a form field runs JavaScript, which it used to miss. Only
  snapshots of documents with such forms or scripts change. `PdfCheck.NoActions` and `pdiffy check` now report a
  document whose form fields run JavaScript.
- Form fields print how they behave: the default value (`default="0"`), the value each check box or radio button widget
  sends (`exports=["Yes"]`), alignment when it isn't left (`align=center`), the font, size and color of typed text
  (`text-font=Helvetica text-size=auto text-color=gray:0`, set on a widget, the field or the whole form) and their
  JavaScript by when it runs, as a hash (`scripts=calculate:3f9c0a1b2d4e5f60`), including scripts an action chains
  after it. Snapshots of documents with forms change, and most forms
  set a text appearance.
- A signed signature field prints its signature, which used to be left out, so a signed and an unsigned copy gave the
  same snapshot: `signature "approval" p1 signed=yes subfilter=adbe.pkcs7.detached covers-file=yes reason=<present>
  certification=form-filling`. `covers-file=no` means an update was saved after signing. Only snapshots of signed
  documents change.
- An element whose marked content draws an image prints the image's hash (`Figure p1 alt="Trail map"
  images=[3f9c0a1b2d4e5f60]`), the same hash as the image's layout line. If two figures' images are swapped, the alt
  text no longer matches its picture, and the figures' lines now show it. The key is left out with
  `PdfDetail.ImageContents`, so the `Accessibility` profile doesn't print it. Snapshots of tagged documents with images
  change.
- Links print where they go instead of `dest=yes`: the page, the view it opens at and the view's positions
  (`dest=p3 view=XYZ view-at=72,720`, and `zoom=` when an XYZ view sets one). A named destination is looked up; one the
  PDF doesn't define, or one in another file, prints `dest-name="..."`. GoTo actions print their destination too, and GoToR and Launch actions the file they open
  (`file="maps.pdf"`). Bookmarks print their view (`"Trails" -> p2 view=Fit`), web address (`uri="..."`) or file; a
  bookmark to another file prints `file="atlas.pdf" dest=p1` instead of `-> p1`, since that page isn't in this document.
  Snapshots of documents with internal links or bookmarks change.
- Text drawn at an angle is read along its own baseline, so rotated text such as a column header or a chart label
  keeps its words (`"Hello world"`, not `"H e llo w o rld"`) and stays one layout run, which prints `angle=90`.
  Snapshots of documents with rotated text change.
- Letter-spaced text (`Tc`) no longer gets a space between every letter: a gap counts as a space only when it's wider
  than the line's letter spacing by 0.2 em (`"Hello world"`, not `"H e l l o w o r l d"`). Letter spacing is read from
  the narrowest gaps on each line, up to a quarter of an em, from lines with at least three gaps, so a letter-spaced
  word of three letters or fewer still prints a space between its letters. Snapshots of documents with letter-spaced
  text change.
- Marked content inside a structure element that sets its own language, replacement text, alt text or expansion is
  recorded on the element with the text it covers, unless that's all of the element's text (`span-lang=[fr-FR:"bonjour"]`, `span-expansion=["Mount":"Mt."]`,
  `span-actual=`, `span-alt=`), so a span that grows or moves to other words shows too, and an
  artifact prints its kind when it gives one (`p1 type=Pagination subtype=Footer "Page 1"`). Snapshots of documents
  with such marked content or typed artifacts change; typed artifacts are common, so many `artifacts` sections change.
  A new `span-lang` key is flagged as affecting accessibility, like any language change, so it shows as
  `[accessibility]` when an older approved snapshot meets the new one.
- An element with marked-content references into a form XObject (an MCR with `/Stm`) prints `mcids-in-xobject=N`. Its
  text is read as before, but a form's MCIDs can't yet be told from the page's own, so when the page uses the same
  MCID, the element's text includes the page's too, and the page's own element includes the form's (without the key);
  the key marks that it may. Only snapshots of documents with such references change, by the new key alone.

### Word (`docx-snapshot v1`, unchanged)

- Hidden text (`w:vanish`), double strikethrough, right-to-left runs and a run's language are read. A run that differs
  from its paragraph in one of these prints a span (`span "care" hidden`), and a style whose language isn't the one on
  the document line prints `lang=`. An inserted Word document's styles are compared with that language too, so an
  inserted document in another language prints `lang=` on its styles. Only snapshots of documents with such runs or
  styles change.
- Right-to-left paragraphs (`w:bidi`), paragraph borders (`w:pBdr`) and paragraph shading are read: a paragraph or
  style that sets them prints `rtl-paragraph`, `borders=top:single:0.5` and `shading=FFFF00`. A border set on a
  paragraph replaces only that side of its style's, and `nil` or `none` removes it. Only snapshots of documents with
  such paragraphs or styles change.
- A table's alt text (`caption=`, `description=`), a row's height and `split=no` (kept on one page), and a cell's vertical alignment
  and text direction are read. Only snapshots of documents with tables that set them change.

### Diffs

No snapshot changes. A line without text of its own, such as a table row or cell, is now paired by the text under it,
so rows that gained different attributes are no longer mixed up, and one that kept most of its own words while its
content was rewritten, such as a table that gained a page, still pairs with its old self. Tests that assert on the exact
list of changes can see different pairings.

### Accessibility flags

No snapshot changes, but more changes are flagged as
[affecting accessibility](accessibility-changes.md): a language set on marked content inside an element changed, or
the replacement text, alt text or expansion set on it removed;
tagged content made an artifact or untagged, `marked` or
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
