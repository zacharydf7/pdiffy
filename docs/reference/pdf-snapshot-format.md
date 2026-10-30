# PDF snapshot format

A PDF snapshot is UTF-8 text with LF line endings and no byte-order mark, saved as `{name}.verified.pdfua.txt`.

```text
# pdfua-snapshot v1
document
  marked=yes lang=en-US display-doc-title=yes pages=3
...
structure
  Document lang=en-US
    H1 p1 "Trail Table"
    Figure p1 alt="Park logo"
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

## Sections

Sections always appear in this order. Each can be left out with [`Section`](xref:PDiffy.Section).

| Section | Contents |
|---|---|
| `document` | Whether the PDF is tagged, its language, whether the viewer shows the title, page count |
| `settings` | PDF version, page mode and layout, what happens when the file opens, whether it has JavaScript or document actions, encryption and what it permits; viewer preferences, optional-content layers and embedded files |
| `metadata` | Title, author, subject, keywords; whether dates, producer and creator are present; PDF/UA identifier; XMP title, creator, description, language and PDF/A conformance, or `xmp=unreadable`; every other Info entry (such as a company name or classification label) |
| `role-map` | Each custom tag type and the standard type it maps to |
| `pages` | Size (named when standard, such as `letter` or `a4`), rotation, tab order; crop, bleed, trim and art boxes, user unit and transparency group when set; pages that couldn't be read; page label ranges |
| `bookmarks` | The outline tree, with each bookmark's target: a page and the view it opens at, a web address or another file. Or `unreadable` when it nests more than 256 deep or loops back on itself |
| `structure` | One line per structure element, in reading order: type, mapped type, pages, language, alt text, actual text, expansion, title, table attributes (scope, headers, spans, summary), list numbering, links, how many of its marked-content references name no page (`mcids-without-page`, whose text can't be found), how many child elements are nested more than 256 deep and left out (`unread-children`), and the element's text |
| `artifacts` | Text marked as an artifact (headers, footers, page numbers), per page |
| `untagged` | Text that is neither tagged nor marked as an artifact (including tagged text that no structure element points to), and a count of such images and paths per page |
| `fonts` | Each font: name, type, whether it's embedded, whether it has a Unicode map, and the character codes its text uses that don't map to Unicode (that text can't be copied, searched or read aloud) |
| `annotations` | Every annotation (links, comments, form widgets and so on), page by page in the order each page lists them: type, `tagged` or `untagged`, rectangle, flags (such as `print` or `hidden`), where a link goes (a web address, or a page and the view it opens at, or another file), contents and accessible name |
| `forms` | Every form field: type, full name (`address.city`), page, accessible name, value, options, maximum length and flags (such as `required` or `read-only`) |
| `layout` | Every text run, image and painted vector path, page by page, each labeled `tagged`, `artifact` or `untagged`. Text runs: position, font, size, color, render mode, angle. Images: position, size, pixel size, a hash of the pixels. Paths: bounding box, fill, stroke and line width |

## Rules

### Header

- The first line is the format version. See [Format changes](format-changes.md) for when it changes.
- When anything is ignored, the next lines list it: `# ignored sections: ...` and `# ignored fields: ...`. With
  `Tolerate`, `# tolerances: ...` follows, such as `# tolerances: positions 1pt`, and with `OnlyPages`,
  `# only pages: ...`. Comparing two snapshots applies the tolerances their header lists.

### Lines and keys

- An empty section prints `(none)` and an ignored one prints `(ignored)`, so something disappearing shows up as a
  change.
- Two spaces of indent per nesting level. Keys appear in a fixed order and are left out when they have no value.
- Text is quoted; `"` and `\` are escaped, and control characters print as `\uXXXX`.
- Pages print as `p1`, or `p2-3` for an element that spans pages.
- Table header cells that other cells refer to get stable names (`TH1`, `TH2`) instead of the PDF's internal IDs.

### Layout

- Positions and sizes are whole points from the page's bottom-left corner. Font sizes and line widths are rounded to
  0.1 pt.
- Colors name their color space: `gray:0.5`, `#ff0000` (RGB), `cmyk:0,0,0,1`, or `pattern:shading` / `pattern:tiling`
  for gradients and tiled fills.
- `mode=` appears only for text that isn't drawn the usual way (`invisible`, `stroke`, `fill-stroke`, ...).
- `angle=` appears only for text that doesn't run across the page: the angle in degrees, counterclockwise, such as
  `angle=90` for text that reads upward. (A page's `rotate=` is the PDF's own setting, which turns clockwise.)
- `fill=`, `stroke=` and `width=` appear only for what a path actually paints.
- An image's hash is taken from its decoded pixels where possible, so recompressing an image doesn't change it.
- Annotation rectangles use the same convention: the bottom-left corner and size, in whole points.

### Links and bookmarks

- A link that goes to a page prints `dest=p3` (`p?` when its page reference isn't a page), the view it opens the page
  at (`view=XYZ`, `Fit`, `FitH` and so on) and the view's positions in whole points (`view-at=72,720`, with `-` for a
  position the viewer keeps as it is), and an XYZ view's zoom when it sets one (`zoom=2`). The positions belong to
  `Field.Positions`, so they can be tolerated or ignored.
- A named destination is looked up, so it prints like any other. One the PDF doesn't define, or one in another file,
  prints `dest-name="..."`.
- A link to another file prints `file="maps.pdf"`, with the page it opens there for `action=GoToR`.
- A bookmark prints `-> p2` and the view it opens at, or `uri="..."`, or `file="..."` with `dest=` for the page it opens
  in that file. A bookmark's zoom isn't read.

### Fonts

- `unmapped=[0041-0043,0050]` lists hex character codes, with runs shortened to first-last.
- A code counts as mapped when the font's ToUnicode map gives it text; for a simple font, also when its encoding gives
  it a standard glyph name; and for a CID font, when it uses a predefined CMap other than Identity-H or Identity-V over
  one of Adobe's character collections (Japanese, Chinese or Korean).
- Text inside form XObjects counts too, under each font it's drawn with.

## Text

Text is normalized to Unicode NFC, with whitespace runs collapsed to one space and trimmed. A space is added between two
pieces of text when the baseline changes or the gap is wider than 0.2 em. The same rule joins the pieces of an element's
text, so a word a PDF splits across two tagged pieces stays one word. Pieces on different pages, and a piece that starts
well to the left of where the last one ended on the same line, are always separated by a space.

## What's left out on purpose

These change without changing what anyone reading the document gets:

- Object numbers, compression, object order, stream lengths and cross-reference layout.
- MCIDs and structure element IDs; references are resolved to names.
- Font subset prefixes: `ABCDEF+Arial` is recorded as `Arial`.
- Dates, document IDs and the producer string: only whether they're present.
  [`ShowProducerAndCreator()`](xref:PDiffy.SnapshotOptions.ShowProducerAndCreator) prints the producer and creator.

## Damaged files

A page that can't be loaded (for example, one with a damaged font) comes back marked unreadable, and its snapshot line
says so, instead of the whole read failing. The same goes for an attachment whose contents can't be decoded, and for XMP
metadata that isn't well-formed XML or has a DTD (`xmp=unreadable`). A file that can't be opened at all throws.

PDiffy also reads structure elements and bookmarks only 256 levels deep, so a file built to overflow the stack can't
crash the test run. An element whose children are deeper gets `unread-children=N`, and their content counts as untagged.
Bookmarks nested deeper, or that loop back on themselves, print `unreadable`. A Flate-compressed stream that would
decompress to more than 256 MB isn't decompressed: a page whose content it is comes back unreadable, an image is hashed
as stored, and XMP prints `xmp=unreadable`. See [Limitations](limitations.md) for what PDiffy can't guard against.
