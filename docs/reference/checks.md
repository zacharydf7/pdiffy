# Check rules

What each rule [`Check`](xref:PDiffy.PdfUaDocument.Check*) applies finds, why it matters to someone using assistive
technology, and how to fix it. A finding's [`HelpUrl`](xref:PDiffy.PdfCheckFinding.HelpUrl) links to its rule's entry
here. Each rule names the clause of PDF/UA-1 (ISO 14289-1) it comes from; that says where the rule comes from, not that
a document passing it conforms to the standard. For conformance, use [veraPDF](https://verapdf.org/) or PAC.

Most fixes belong in whatever generates the document: a template, a report library's settings, or the export options
of the application it comes from. The [recipes](../guide/recipes.md) cover common generators.

## DocumentLanguage

**Finds:** a document with no language. **Category:** `Language`. **Severity:** `Error`. **From:** ISO 14289-1 7.2.

**Why it matters:** a screen reader picks its voice and pronunciation from the document's language. Without one, it
guesses, and English text read with a French voice, or the other way round, is hard to follow.

**How to fix it:** set the document language, such as `en-US`, in the generator. In the PDF it's the catalog's `/Lang`
entry. Text in another language inside the document sets its own language on its element or marked content.

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

## UntaggedContent

**Finds:** text, images and paths that are neither tagged nor marked as artifacts, page by page. **Category:**
`Content`. **Severity:** `Error`. **From:** ISO 14289-1 7.1.

**Why it matters:** assistive technology reads the tag tree. Content outside it may be skipped, or read out of order,
so a reader can miss a heading, a warning or a price.

**How to fix it:** turn on tagged output in the generator, and check that custom drawing code and pasted-in content are
tagged or marked as artifacts too. Content that repeats or only decorates, such as running headers and footers, page
numbers, rules and backgrounds, is marked as an artifact, so a screen reader doesn't announce it on every page.

## LinkDescriptions

**Finds:** a link with no description: no contents, and no alt text or text on the element that tags it.
**Category:** `Annotations`. **Severity:** `Error`. **From:** ISO 14289-1 7.18.5.

**Why it matters:** a screen reader announces a link by its description. A link on an icon or image with none reads as
"link", with no hint of where it goes.

**How to fix it:** give each link annotation a `/Contents` entry that says where it goes, such as "Trail conditions
page", as PDF/UA-1 asks. Visible link text in its `Link` element, or alt text on that element, also passes this check,
but not that clause of the standard.

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

## HeadingLevels

**Finds:** a heading that skips a level, or one that comes before any `H1`. **Category:** `Headings`. **Severity:**
`Error`. **From:** ISO 14289-1 7.4.2.

**Why it matters:** screen reader users move through a document by its headings, and the levels tell them how sections
nest. A skipped level, such as an `H3` straight after an `H1`, hides where they are.

**How to fix it:** start with `H1` and go down one level at a time. In the generator, map styles to heading levels by
their place in the outline, not by how they look.

## FontsEmbedded

**Finds:** a font that isn't embedded. **Category:** `Fonts`. **Severity:** `Error`. **From:** ISO 14289-1 7.21.4.1.

**Why it matters:** without the font program, a viewer draws the text with a substitute, which can change its width,
its spacing and which characters it can show, so the document may not read as written.

**How to fix it:** embed every font, at least the characters the document uses. Most generators have a setting for it;
on Linux build servers, also install the fonts the document asks for.

## FontsMapToUnicode

**Finds:** a font whose text uses character codes that don't map to Unicode. **Category:** `Fonts`. **Severity:**
`Error`. **From:** ISO 14289-1 7.21.7.

**Why it matters:** a screen reader, search and copy and paste all need to know which character each glyph is. Without
the map, the text reads as nothing or as gibberish, though it looks right on screen.

**How to fix it:** use fonts that come with a Unicode map, and generator settings that write a `ToUnicode` CMap for each
font. A font that maps some codes but not others usually means a symbol or ligature the generator draws specially.

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
