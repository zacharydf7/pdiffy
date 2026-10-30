# Word snapshot format

A Word snapshot is UTF-8 text with LF line endings, saved as `{name}.verified.docx.txt`. It reads like the document:
one line per paragraph with its runs merged, and only what differs from its style listed.

```text
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

## Sections

Sections always appear in this order. Each can be left out with [`DocxSection`](xref:PDiffy.DocxSection).

| Section | Contents |
|---|---|
| `document` | Title, subject, language, compatibility mode, whether changes are tracked, protection, even and odd headers, whether created and modified dates are present; parts that couldn't be read |
| `sections` | One line per section (`s1`, `s2`, ...): page size, margins, header and footer distances, columns, section break, page numbering, title page, which headers and footers it has; a tracked change to its settings |
| `styles` | The styles the document uses, with their properties resolved through the styles they're based on. A style's language prints only when it isn't the language on the document line. An inserted Word document's styles are compared with that language too, so one in another language prints `lang=` on its styles |
| `numbering` | The lists the document uses, and each level's format, text, start and indents |
| `body` | Paragraphs, tables, content controls and inserted documents, in reading order |
| `headers`, `footers` | Each section's headers and footers, by kind (`default`, `first`, `even`) |
| `notes` | Footnotes, endnotes and comments, with their content |

## Body lines

| Line | Means |
|---|---|
| `P ... "text"` | A paragraph: its style, list item and label, layout that differs from its style (such as `align=`, `indent-left=`, `rtl-paragraph`, `borders=` or `shading=`), then its text |
| `span "text" ...` | Part of the paragraph whose formatting differs from the paragraph's, such as `bold`, `font=`, `hidden` (text Word doesn't show or print), `double-strike`, `rtl` (right to left) or `lang=fr-FR` |
| `Table ...` | A table: style, grid widths, width, cell margins, borders, and its alt text (`caption=`, `description=`) |
| `Row` / `Row header` | A table row; a header row repeats on each page. Its height (`height=20`, `height=20-exact` or `height=auto`) and `split=no` when it's kept on one page |
| `Cell ...` | A cell: span, merge, shading, width, margins, borders, vertical alignment (`valign=`), text direction (`text-direction=`) |
| `Control <kind> ...` | A content control, with its tag (`name=`) and title. Under a cell, `around` marks one around the cell |
| `Inserted <kind>` | An inserted document (`w:altChunk`); a DOCX is read in place, other formats by hash |
| `bookmark "Name"` | A bookmark between blocks, rows or cells; `columns=0-2` for a table-column bookmark |
| `unsupported <element> hash=...` | Something PDiffy doesn't read yet, kept by hash so a change to it still shows |
| `ends-section s1` | The paragraph that ends a section |

## Lines inside a paragraph

Each ends with `at=N`, the position in the paragraph's text where it sits.

| Line | Means |
|---|---|
| `link "text" -> target` | A hyperlink and where it goes |
| `field "CODE" result="..."` | A field, such as `PAGE`, and the result Word last computed |
| `image`, `chart`, `shape`, `object`, `control`, ... | A picture, other graphic, embedded object or ActiveX control: `inline` or `floating`, size, alt text, type, and a hash of its data |
| `math "text" hash=...` | An equation |
| `symbol font=... code=...` | A character from a symbol font |
| `break page` / `break column` | A page or column break |
| `form <kind> ...` | A form control: name, title, value, choices |
| `note footnote 1`, `comment 0` | Where a note or comment is attached |
| `bookmark "Name"` | A bookmark |
| `insert`, `delete`, `move-to`, `move-from`, `format` "text" | A tracked change to text, with its author |
| `paragraph-insert`, `paragraph-format`, ... | A tracked change to the paragraph itself |
| `stray-text "text"` | Text in a place Word may drop or move it |
| `unsupported <element> hash=...` | Something PDiffy doesn't read yet |

Tables, rows and cells list their own tracked changes (`table-format`, `row-insert`, `cell-delete` and so on) under
their line. See [`DocxRevisionKind`](xref:PDiffy.DocxRevisionKind) for every kind.

## Rules

- When anything is ignored, the second line lists it: `# ignored sections: ...` and `# ignored fields: ...`.
- An empty section prints `(none)` and an ignored one prints `(ignored)`.
- Lengths are in points. Colors are hex.
- Relationship ids, editing-session ids, the zip's layout and styles nothing uses are left out, so regenerating the
  same document gives the same snapshot.

## Damaged files

A part that can't be read is listed on the `document` section's `unreadable` line, such as `unreadable word/styles.xml`,
and read as if it were missing. A part is unreadable when it isn't well-formed XML, has a DTD, nests elements more than
256 deep (an inserted document's nesting counts from where it's inserted) or is larger than 256 MB. An inserted document
that can't be read is marked where it's inserted instead: `Inserted docx unreadable`. A file whose body can't be read
throws.
