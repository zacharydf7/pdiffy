# Check rules

What each rule [`Check`](xref:PDiffy.PdfUaDocument.Check*) applies finds, why it matters to someone using assistive
technology, and how to fix it. A finding's [`HelpUrl`](xref:PDiffy.PdfCheckFinding.HelpUrl) links to its rule's entry
here. Each rule names the clause it comes from, of PDF/UA-1 (ISO 14289-1) or, for a warning PDF/UA doesn't check, of
WCAG 2.2; that says where the rule comes from, not that a document passing it conforms to the standard. For
conformance, use [veraPDF](https://verapdf.org/) or PAC.

Most fixes belong in whatever generates the document: a template, a report library's settings, or the export options
of the application it comes from. The [recipes](../guide/recipes.md) cover common generators.

## DocumentLanguage

**Finds:** a document with no language. **Category:** `Language`. **Severity:** `Error`. **From:** ISO 14289-1 7.2.

**Why it matters:** a screen reader picks its voice and pronunciation from the document's language. Without one, it
guesses, and English text read with a French voice, or the other way round, is hard to follow.

**How to fix it:** set the document language, such as `en-US`, in the generator. In the PDF it's the catalog's `/Lang`
entry. Text in another language inside the document sets its own language on its element or marked content.

## LanguageTags

**Finds:** a language on the document, a structure element or marked content that isn't a well-formed language tag
(BCP 47), such as `english`, `en_US`, `en-` or a single space. An empty language means an unknown one, so it isn't
reported here; for the document, [`DocumentLanguage`](#documentlanguage) reports it. **Category:** `Language`. **Severity:** `Error`.
**From:** ISO 14289-1 7.2.

**Why it matters:** a screen reader looks the tag up to choose its voice and pronunciation. One it can't parse is
treated as no language at all, so the text is read with whatever voice it falls back to.

**How to fix it:** use a language code with a hyphen before the region, such as `en`, `en-US`, `fr-CA` or `zh-Hant-TW`.
Generators usually take it from a culture or locale setting; an underscore (`en_US`) is a sign the value came from a
platform locale name rather than a language tag.

## TextLanguage

**Finds:** in a document with no language of its own, text no language reaches: an element's text, alt text,
replacement text or expansion when neither it, an element around it, nor the marked content covering the text sets a
language; marked content's own alt text, replacement text or expansion; annotation contents and form field names
outside such an element; bookmark titles; and an XMP title or description for the default language (`x-default`).
A document that sets no language anywhere is left to [`DocumentLanguage`](#documentlanguage), since one document
language fixes it all. **Category:** `Language`. **Severity:** `Error`. **From:** ISO 14289-1 7.2.

**Why it matters:** a screen reader reads each piece of text in its language. Text that has none is read with
whatever voice the reader falls back to, so a French passage can come out in an English accent.

**How to fix it:** set the document language. Where a passage is in another language, also set it on that passage's
element, such as `/Lang (fr-FR)` on its `P`, and its contents inherit it.

## DocumentTitle

**Finds:** a document with no title, or one whose viewer shows the file name instead. **Category:** `Document`.
**Severity:** `Error`. **From:** ISO 14289-1 7.1.

**Why it matters:** the title is the first thing a screen reader announces and what a window or tab shows. A file name
such as `report-final-2.pdf` says little about what the document is.

**How to fix it:** give the document a title in its metadata (`dc:title`, and the Info dictionary's `/Title`), and tell
the viewer to show it: `/DisplayDocTitle true` in the catalog's `/ViewerPreferences`.

## FigureAltText

**Finds:** a figure with no alt text or replacement text. **Category:** `AltText`. **Severity:** `Error`. **From:**
ISO 14289-1 7.3.

**Why it matters:** a screen reader can't describe a picture. Without alt text it says nothing, or only "graphic", and
the information the figure carries is lost.

**How to fix it:** give each meaningful image alt text (`/Alt` on its `Figure` element) that says what it shows or
means, such as "Trail map of the ridge loop, 4.2 miles". An image that's only decoration should be marked as an artifact
instead, so it isn't announced at all.

## FormulaAltText

**Finds:** a formula with no alt text or replacement text. **Category:** `AltText`. **Severity:** `Error`. **From:**
ISO 14289-1 7.7.

**Why it matters:** a formula drawn as glyphs or an image reads as a jumble of symbols, or as nothing. Alt text such as
"x equals negative b plus or minus the square root of b squared minus 4 a c, all over 2 a" is how a screen reader user
gets the math.

**How to fix it:** give each `Formula` element alt text (`/Alt`) that reads the formula aloud, or replacement text
(`/ActualText`) when the formula is plain text drawn specially. Equation editors often have a setting to export it.

## MediaClips

**Finds:** a media clip, such as a video a Screen annotation plays on click or when its page opens, with no content type
(`/CT`) or no alternate text (`/Alt`). For a selector rendition, the first rendition is read. **Category:** `AltText`. **Severity:** `Error`. **From:** ISO 14289-1 7.18.6.2.

**Why it matters:** a viewer needs the content type to know whether it can play the clip, and alternate text is what a
screen reader reads in its place, so a reader who can't play or see it still knows what it shows.

**How to fix it:** give the media clip data dictionary a `/CT` such as `(video/mp4)`, and `/Alt` with a language and a
description for each language: `[(en-US) (A walk along the ridge trail)]`.

## MeaningfulAltText

**Finds:** alt text that's a file name, such as `IMG_2041.jpg` or `trail-map final.png`, or a placeholder word such as
`image`, `picture`, `graphic`, `icon` or `untitled`, with or without a number. It looks at alt text on elements and in
marked content. **Category:** `AltText`. **Severity:** `Warning`. **From:** WCAG 2.2 1.1.1, which asks that alt text
serve the same purpose as the image; PDF/UA only asks that it's there.

**Why it matters:** this alt text passes [`FigureAltText`](#figurealttext), but a screen reader user hears "image 3"
or a file name and learns nothing about what the picture shows.

**How to fix it:** write what the image shows or means, such as "Trail map of the ridge loop, 4.2 miles". Most
generators fill alt text from the file name or a default when none was given, so set it where the image is added.

## LinkText

**Finds:** a link whose text is only "click", "click here", "here", "this", "this link", "link", "more", "read more",
"learn more" or "more info", with or without trailing punctuation such as "Read more >". It looks at the link element's
alt text, replacement text and text, including its child elements', and at the link's contents, and finds the link only
when none of them says more. **Category:** `Annotations`.
**Severity:** `Warning`. **From:** WCAG 2.2 2.4.4, which asks that a link's purpose be clear from its text and context;
PDF/UA only asks that a link has a description.

**Why it matters:** this link passes [`LinkDescriptions`](#linkdescriptions), but screen reader users often list a
page's links to move around it, and a list of "click here" links says nothing about where each goes.

**How to fix it:** make the link's text say where it goes, such as "Download the ridge loop trail map", or give the
link contents that do.

## UntaggedContent

**Finds:** text, images and paths that are neither tagged nor marked as artifacts, page by page. **Category:**
`Content`. **Severity:** `Error`. **From:** ISO 14289-1 7.1.

**Why it matters:** assistive technology reads the tag tree. Content outside it may be skipped, or read out of order,
so a reader can miss a heading, a warning or a price.

**How to fix it:** turn on tagged output in the generator, and check that custom drawing code and pasted-in content are
tagged or marked as artifacts too. Content that repeats or only decorates, such as running headers and footers, page
numbers, rules and backgrounds, is marked as an artifact, so a screen reader doesn't announce it on every page.

## ArtifactNesting

**Finds:** an artifact (`/Artifact` marked content) inside tagged content, and an artifact that holds tagged content
(marked content with an MCID), counted page by page. **Category:** `Content`. **Severity:** `Error`. **From:** ISO
14289-1 7.1.

**Why it matters:** content is either read (tagged) or skipped (an artifact), and a screen reader can't do both. Nested
the other way, a page number inside a paragraph may be read in the middle of a sentence, or real text inside a
decoration may be skipped.

**How to fix it:** close the tagged content before drawing the artifact, and the other way round. The usual cause is a
generator that draws a header, footer or watermark while it's in the middle of writing a tagged paragraph. Text that
form XObjects draw outside the structure is reported by [`UntaggedContent`](#untaggedcontent).

## LinkDescriptions

**Finds:** a link with no description: no contents, and no alt text or text on the element that tags it.
**Category:** `Annotations`. **Severity:** `Error`. **From:** ISO 14289-1 7.18.5.

**Why it matters:** a screen reader announces a link by its description. A link on an icon or image with none reads as
"link", with no hint of where it goes.

**How to fix it:** give each link annotation a `/Contents` entry that says where it goes, such as "Trail conditions
page", as PDF/UA-1 asks. Visible link text in its `Link` element, or alt text on that element, also passes this check,
but not that clause of the standard.

## AnnotationTags

**Finds:** a link annotation not tagged with a `Link` element, a form field's widget not tagged with `Form`, any other
annotation not tagged with `Annot`, and any of them not tagged at all, unless it's hidden or a pop-up; a `Form` that
sets no field role (PrintField `Role`) and holds anything but one widget; a `TrapNet` annotation; and a tagged
`PrinterMark`. **Category:** `Annotations`. **Severity:** `Error`.
**From:** ISO 14289-1 7.18, with each finding naming its clause: 7.18.1, 7.18.2, 7.18.4, 7.18.5 or 7.18.8.

**Why it matters:** a screen reader announces an annotation by the element that tags it: "link", "check box", "comment".
Tagged with the wrong element, a link reads as a paragraph and a form field may not be offered for filling in. Trap
networks and printer marks are for print production, and a reader shouldn't come across them at all.

**How to fix it:** tag each link annotation with its `Link` element, each widget with its own `Form`, and comments,
stamps and other annotations with `Annot`. A `Form` for a group of widgets, such as radio buttons, sets its PrintField
`Role`; a `Form` without one holds just its widget, with the field's label in an element of its own. Leave printer
marks out of the structure tree, and remove trap networks from the file.

## AnnotationDescriptions

**Finds:** an annotation other than a link or a form field, such as a comment or a stamp, with no contents and no alt
text on the element that tags it. Pop-ups, hidden annotations, printer marks and trap networks have nothing to read,
so they're left out. **Category:** `Annotations`. **Severity:** `Error`. **From:** ISO 14289-1 7.18.1.

**Why it matters:** a screen reader announces a comment or stamp by its description. Without one, a reader hears
"annotation" and nothing else, while a sighted reader sees the note or the "Approved" stamp.

**How to fix it:** give each annotation a `/Contents` entry that says what it holds or means, or alt text on the
`Annot` element that tags it. Links are covered by [`LinkDescriptions`](#linkdescriptions), and form fields by
[`FormFieldNames`](#formfieldnames).

## TabOrder

**Finds:** a page that has annotations and whose tab order (`/Tabs`) isn't `S`, the order of the structure tree.
**Category:** `Annotations`. **Severity:** `Error`. **From:** ISO 14289-1 7.18.3.

**Why it matters:** someone using the keyboard moves between links and form fields with Tab. With the structure order,
they meet them in the same order as the text around them; in row or column order, they jump around the page.

**How to fix it:** set `/Tabs /S` on every page with links, form fields or other annotations. Most generators have a
setting for it, often called tab order or "use document structure".

## FormFieldNames

**Finds:** a form field with no accessible name (tooltip). **Category:** `Forms`. **Severity:** `Error`. **From:**
ISO 14289-1 7.18.1.

**Why it matters:** a screen reader announces a field by its accessible name. Without one, a reader hears "edit text"
and has to guess what to type.

**How to fix it:** give each field a tooltip (`/TU`) that says what it's for, such as "Emergency contact phone".

## TableHeaders

**Finds:** a header with no scope that no cell names, and a cell that names a header that doesn't exist.
**Category:** `Tables`. **Severity:** `Error`. **From:** ISO 14289-1 7.5.

**Why it matters:** a screen reader reads a cell together with its headers, such as "Distance, Ridge loop: 4.2 miles".
A header it can't tie to its cells leaves each value without its meaning.

**How to fix it:** give each `TH` a scope (`Row`, `Column` or `Both`), or give its cells a `/Headers` list that names
the header's ID. Make sure every ID a cell names belongs to a header in the table.

## TableStructure

**Finds:** a row (`TR`) outside a `Table`, `THead`, `TBody` or `TFoot`; a row group (`THead`, `TBody`, `TFoot`) outside a
`Table`; a cell (`TH`, `TD`) outside a `TR`; a table, row or row group holding something it can't; a second `THead`,
`TFoot` or `Caption` in one table; a `THead` or `TFoot` in a table with no `TBody`; and a caption that isn't the
table's first or last child. **Category:** `Tables`. **Severity:** `Error`. **From:** ISO 14289-1 7.2.

**Why it matters:** a screen reader moves through a table by row and column, and tells the reader where they are, such
as "row 3, column 2". It can only count rows and columns that sit where a table keeps them, so a cell outside a row,
or text loose in a table, breaks the count and the headers it reads with each cell.

**How to fix it:** keep the standard nesting: `Table`, then optional `THead`, `TBody` and `TFoot` (at most one head and
one foot, with a body whenever there's either), then `TR`, then `TH` and `TD`. Text goes inside a cell, and the caption
goes first or last. Most generators get this right for tables built with their table feature; the usual cause is a
layout made of positioned text or nested tables used for spacing.

## TableGrid

**Finds:** a cell that overlaps one spanning down from an earlier row, a cell that spans past the table's last row, and
a row that covers a different number of columns from the first row. Cells are laid out as a viewer lays them out: row
by row, each in the first column no cell above still covers, counting `RowSpan` and `ColSpan`. **Category:** `Tables`.
**Severity:** `Error`. **From:** ISO 14289-1 7.2.

**Why it matters:** a screen reader reads a cell with the headers of its row and column, and says how many rows and
columns the table has. When the cells don't line up, it pairs values with the wrong headers, or reports a column that
isn't there.

**How to fix it:** check each `RowSpan` and `ColSpan` against the table as it's drawn: a cell that spans two rows takes
a column in the row below, so that row has one cell fewer. A generator that merges cells usually sets the spans; one
that draws merged cells as separate boxes needs its merging turned on, or the extra empty cells tagged.

## HeadingLevels

**Finds:** a heading that skips a level, or one that comes before any `H1`. **Category:** `Headings`. **Severity:**
`Error`. **From:** ISO 14289-1 7.4.2.

**Why it matters:** screen reader users move through a document by its headings, and the levels tell them how sections
nest. A skipped level, such as an `H3` straight after an `H1`, hides where they are.

**How to fix it:** start with `H1` and go down one level at a time. In the generator, map styles to heading levels by
their place in the outline, not by how they look.

## HeadingTypes

**Finds:** a document that uses both unnumbered headings (`H`) and numbered ones (`H1` to `H6`), and an element, or the
structure tree root, that holds more than one `H`. The document's first heading says which kind it uses, so each
heading of the other kind is reported. **Category:** `Headings`. **Severity:** `Error`. **From:** ISO 14289-1 7.4.4.

**Why it matters:** numbered headings give their level outright. An `H` gets its level from how deep its section is
nested, so it only works when each section (`Sect`) has one, and a document that mixes the two leaves a screen reader
with two ways to work out the outline that disagree.

**How to fix it:** pick one kind. Numbered headings are simpler and what most generators write. With `H`, give each
heading its own `Sect` and nest the sections the way the outline goes.

## NoteIds

**Finds:** a note (`Note`, a footnote or endnote) with no ID, and a note with the same ID as an earlier one.
**Category:** `Structure`. **Severity:** `Error`. **From:** ISO 14289-1 7.9.

**Why it matters:** a reference in the text points to its note by the note's ID, so a screen reader can jump to the
note and back. Without an ID, or with one two notes share, the reference leads nowhere or to the wrong note.

**How to fix it:** give each `Note` element an `/ID` that no other note uses, such as `fn1`, `fn2`. Generators that
tag footnotes usually number them; check that the IDs survive when documents are merged or sections are reused.

## FontsEmbedded

**Finds:** a font that isn't embedded. **Category:** `Fonts`. **Severity:** `Error`. **From:** ISO 14289-1 7.21.4.1.

**Why it matters:** without the font program, a viewer draws the text with a substitute, which can change its width,
its spacing and which characters it can show, so the document may not read as written.

**How to fix it:** embed every font, at least the characters the document uses. Most generators have a setting for it;
on Linux build servers, also install the fonts the document asks for.

## FontsMapToUnicode

**Finds:** a font whose text uses character codes that don't map to Unicode, or that its ToUnicode map sends to
U+0000, U+FEFF or U+FFFE, which give no text. **Category:** `Fonts`. **Severity:** `Error`. **From:** ISO 14289-1
7.21.7.

**Why it matters:** a screen reader, search and copy and paste all need to know which character each glyph is. Without
the map, the text reads as nothing or as gibberish, though it looks right on screen.

**How to fix it:** use fonts that come with a Unicode map, and generator settings that write a `ToUnicode` CMap for each
font. A font that maps some codes but not others usually means a symbol or ligature the generator draws specially.

## NoMissingGlyphs

**Finds:** a font whose text shows its .notdef glyph, the empty box a font draws for a character it lacks. It finds the
codes a simple font's encoding names `.notdef`, and in a CID font with an Identity encoding, CID 0 or a code its
`CIDToGIDMap` sends to glyph 0. A glyph missing from the font file itself isn't found. **Category:** `Fonts`.
**Severity:** `Error`. **From:** ISO 14289-1 7.21.8.

**Why it matters:** the text a screen reader reads may be there, but sighted readers see empty boxes in its place, and
the two no longer agree.

**How to fix it:** use a font that has every character the text needs, or embed the whole font instead of a subset
that leaves some out. Boxes often appear when text is pasted in from another font or uses a symbol the font lacks.

## FontDictionaries

**Finds:** a Type 0 or TrueType font whose dictionaries break a PDF/UA font rule, one finding for each kind of problem:

- its CIDSystemInfo names a different character collection from its CMap's (7.21.3.1);
- it's an embedded TrueType-based CID font with no `CIDToGIDMap` (7.21.3.2);
- its CMap is neither built into every reader nor embedded, an embedded CMap gives two writing modes, or it uses a
  CMap that isn't built in (7.21.3.3);
- it's non-symbolic TrueType with an encoding other than `MacRomanEncoding` or `WinAnsiEncoding`, or symbolic
  TrueType with an `Encoding` entry (7.21.6).

**Category:** `Fonts`. **Severity:** `Error`. **From:** ISO 14289-1 7.21; each finding names its own clause.

**Why it matters:** these entries tell a reader which glyph and which character each code stands for. When they're
missing or disagree, readers can pick different glyphs or characters, so the text can look right in one viewer and
read wrong in another.

**How to fix it:** these come from the generator, not the document's content. Update the generator or its PDF/UA
settings, or embed fonts as TrueType with WinAnsi encoding where it offers the choice.

## LayerConfigurations

**Finds:** an optional-content configuration (a set of layers a viewer can switch to) with no name, and one that turns
layers on or off by itself, such as by zoom or for printing (`/AS`). **Category:** `Document`. **Severity:** `Error`.
**From:** ISO 14289-1 7.10.

**Why it matters:** a viewer lists configurations by name, so one without a name can't be chosen or announced. Layers
that appear and disappear on their own change what a reader gets without them doing anything.

**How to fix it:** give each configuration in `/OCProperties` a `/Name`, and leave out `/AS`.

## EmbeddedFileNames

**Finds:** a file in the document's list of embedded files (`/EmbeddedFiles`) whose file specification doesn't give
its name both as `/F` and as `/UF` (Unicode). Files attached through a comment (a FileAttachment annotation) aren't
read yet.
**Category:** `Document`. **Severity:** `Error`. **From:** ISO 14289-1 7.11.

**Why it matters:** a viewer and a screen reader show and announce the attachment by its name, and `/UF` is the one
that holds any character; without both, a name can be lost or garbled.

**How to fix it:** write both entries in each file specification, with the same name.

## NoDynamicXfa

**Finds:** a dynamic XFA form, one the viewer builds its pages from as the file opens (`/NeedsRendering`).
**Category:** `Forms`. **Severity:** `Error`. **From:** ISO 14289-1 7.15.

**Why it matters:** the pages of a dynamic XFA form don't exist until a viewer that supports XFA builds them, so most
viewers and screen readers see nothing, or only a "please wait" page.

**How to fix it:** export the form as a static PDF form (AcroForm), with tagged fields.

## AccessibilityPermission

**Finds:** an encrypted document whose permissions don't let assistive technology extract its text (bit 10 of `/P`).
**Category:** `Document`. **Severity:** `Error`. **From:** ISO 14289-1 7.16.

**Why it matters:** a screen reader gets the text the same way copying does. With that permission off, it gets
nothing, however well the document is tagged.

**How to fix it:** keep "content accessibility" (extraction for accessibility) allowed in the generator's security
settings. It can stay on even when copying is off.

## NoReferenceXObjects

**Finds:** a reference XObject, a form that shows a page of another file (`/Ref`), in the pages' resources or the
forms in them, drawn or not. Annotation appearances aren't read yet. **Category:** `Content`.
**Severity:** `Error`. **From:** ISO 14289-1 7.20.

**Why it matters:** the page it shows lives in another file, outside this document's tags, so a reader can't reach it,
and most viewers only show the form's own fallback drawing.

**How to fix it:** import the page's content into the document instead of referring to it.

## NoActions

**Finds:** JavaScript, actions run on events such as closing or printing, and an open action that does more than go
to a page. **Category:** `HouseRules`. **Severity:** `Warning`. **From:** a house rule, not a standard.

**Why it matters:** scripts and automatic actions can change a document as it's read, run differently in each viewer
or not at all, and surprise someone who can't see what changed. A generated document rarely needs them.

**How to fix it:** turn off scripting and document actions in the generator, and keep the open action to going to a
page and view, if anything.

## NoEmbeddedFiles

**Finds:** each embedded file. **Category:** `HouseRules`. **Severity:** `Warning`. **From:** a house rule, not a
standard.

**Why it matters:** an attachment is easy to miss and may not be accessible itself, so information that lives only in
it can be lost to some readers.

**How to fix it:** put the information in the document itself, or leave the attachment out if it isn't needed. A
document that needs an attachment, such as the source data, can leave this rule out of the set it checks.

## RoleMap

**Finds:** a custom structure type that doesn't map to a standard one, a mapping that loops back on itself, and a
standard type that's remapped. **Category:** `Structure`. **Severity:** `Error`. **From:** ISO 14289-1 7.1.

**Why it matters:** assistive technology only knows the standard types, such as `P`, `H1` and `Table`. A custom type,
such as `Callout`, means nothing to it unless the role map says which standard type it stands for, so an element of an
unmapped type may be read as plain text or skipped. A loop never reaches a standard type, and remapping a standard
type, such as `P` to `Span`, changes what every element of that type is.

**How to fix it:** map each custom type to the standard type closest in meaning, such as `Callout` to `Div` or
`BodyText` to `P`, in the structure tree root's `/RoleMap`. A chain of custom types is fine as long as it ends at a
standard one. Leave the standard types out of the role map. Most generators build it from their style names, so the
fix is usually a style mapping setting. The finding's path names the type, with `loop` or `standard` after it for the
other two problems: `role-map > Callout`, `role-map > Callout > loop`, `role-map > P > standard`.

## XmpMetadata

**Finds:** a document with no XMP metadata, XMP that can't be read, XMP that doesn't say the document is PDF/UA-1
(`pdfuaid:part` 1) or says so with another prefix than `pdfuaid`, and XMP with no title (`dc:title`).
**Category:** `Document`. **Severity:** `Error`. **From:** ISO 14289-1 5, and 7.1 for a missing XMP stream or title.

**Why it matters:** the PDF/UA identifier is how a viewer, a library catalog or a checker knows the document claims
to be accessible, and the XMP title is the one PDF/UA asks for. [`DocumentTitle`](#documenttitle) also accepts the
Info dictionary's title, which viewers show, so a document can pass it and still fail here.

**How to fix it:** turn on PDF/UA output in the generator, which writes the XMP stream with
`<pdfuaid:part>1</pdfuaid:part>` and a `dc:title`. XML lets any prefix stand for a namespace, but PDF/UA names
`pdfuaid`, and some checkers look for it.

## TaggedPdf

**Finds:** a document that isn't marked as tagged (MarkInfo `Marked`), one that says its tags may be unreliable
(MarkInfo `Suspects`), one with no structure tree, and structure elements with no parent entry (`P`).
**Category:** `Structure`. **Severity:** `Error`. **From:** ISO 14289-1 7.1, and 6.2 for a document that isn't
marked.

**Why it matters:** assistive technology only uses the tags of a document marked as tagged, and treats suspect tags
as guesses. Without a structure tree there's nothing to read but the raw text, and a tool that walks up the tree
from an element without a parent can't tell where it belongs.

**How to fix it:** turn on tagged output in the generator. `Suspects` is set by tools that add tags after the fact;
check the tags, then clear it. An element without a parent entry is a generator bug: each `StructElem` needs a `/P`
pointing to the element or root that holds it.

## PdfVersion

**Finds:** a file header past PDF 1.7, such as `%PDF-2.0`, and a catalog `/Version` past it, which upgrades the file.
**Category:** `Document`. **Severity:** `Error`. **From:** ISO 14289-1 6.1.

**Why it matters:** PDF/UA-1 is defined on PDF 1.7. A PDF 2.0 file follows PDF/UA-2, whose rules differ, so checks
written for PDF/UA-1 may not apply to it.

**How to fix it:** write PDF 1.7 for PDF/UA-1, or, for a PDF 2.0 file, check it against PDF/UA-2 and leave this rule
out of the set.

## ListStructure

**Finds:** a list item (`LI`) outside a list (`L`), a list body (`LBody`) outside a list item, a list holding something
other than lists, list items and a caption, a list item holding something other than a label (`Lbl`) and a body, and a
list caption that isn't the list's first child. **Category:** `Lists`. **Severity:** `Error`. **From:** ISO 14289-1 7.2.

**Why it matters:** a screen reader announces a list and how many items it has, such as "list, 4 items", and reads
each item's label and body together. Items outside a list, or text loose in it, are read as plain text, without the
count or where each item starts.

**How to fix it:** put each item in an `L`, and each item's text in an `LBody`, with its bullet or number in an `Lbl`.
A nested list goes in the `LBody` of the item it belongs to, or straight in the outer `L`. A list's caption goes first.

## TocStructure

**Finds:** a table of contents item (`TOCI`) outside a table of contents (`TOC`), a table of contents holding
something other than tables of contents, items and a caption, and a caption that isn't its first child.
**Category:** `Lists`. **Severity:** `Error`. **From:** ISO 14289-1 7.2.

**Why it matters:** a screen reader reads a table of contents as a list of entries that lead into the document. Entries
outside it, or other content mixed in, read as plain text, so the list and how many entries it has are lost.

**How to fix it:** put each entry in a `TOCI` inside the `TOC`, a nested part in a `TOC` of its own inside the outer one,
and the caption first. Most generators build this from their table of contents field; check that field's tagging
setting.
