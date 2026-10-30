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
