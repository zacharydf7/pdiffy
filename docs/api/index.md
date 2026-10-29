# API reference

Everything public is in the `PDiffy` namespace. This page groups the types by what you'd use them for; the sidebar lists
them all.

## Snapshot tests

| Type | Use it to |
|---|---|
| <xref:PDiffy.PdfSnapshot> | Compare a PDF with its approved snapshot |
| <xref:PDiffy.DocxSnapshot> | Compare a Word document with its approved snapshot |
| <xref:PDiffy.Snapshot> | Compare text or JSON, and set the snapshot folder for every kind |
| <xref:PDiffy.SnapshotException> | The failure a test throws, with what changed |

## Options

| Type | Use it to |
|---|---|
| <xref:PDiffy.SnapshotOptions> | Ignore, scrub or limit a PDF snapshot |
| <xref:PDiffy.Section>, <xref:PDiffy.Field> | Name the PDF sections and details to leave out |
| <xref:PDiffy.DocxSnapshotOptions> | Ignore or scrub a Word snapshot |
| <xref:PDiffy.DocxSection>, <xref:PDiffy.DocxDetail> | Name the Word sections and details to leave out |
| <xref:PDiffy.TextSnapshotOptions> | Scrub a text or JSON snapshot |

## Comparing

| Type | Use it to |
|---|---|
| <xref:PDiffy.SnapshotDiff> | Hold the differences between two snapshots, and print them as a summary, HTML or JSON |
| <xref:PDiffy.SnapshotChange> | Describe one difference: kind, section, path, page, detail |
| <xref:PDiffy.ChangeKind> | Name the kind of a difference |

## The PDF model

<xref:PDiffy.PdfUaDocument> is everything PDiffy reads from a PDF, in the order a snapshot prints it.

| Type | Holds |
|---|---|
| <xref:PDiffy.DocumentInfo>, <xref:PDiffy.Metadata>, <xref:PDiffy.DocumentSettings> | Document-wide facts, metadata and settings |
| <xref:PDiffy.PageInfo>, <xref:PDiffy.PageLabelRange>, <xref:PDiffy.Bookmark> | Pages, page labels and the outline |
| <xref:PDiffy.StructureElement>, <xref:PDiffy.RoleMapping> | The tag tree and custom tag types |
| <xref:PDiffy.PageText> | Artifact and untagged text, per page |
| <xref:PDiffy.FontInfo> | Fonts and the codes that don't map to Unicode |
| <xref:PDiffy.Annotation>, <xref:PDiffy.FormField> | Annotations and form fields |
| <xref:PDiffy.TextRun>, <xref:PDiffy.ImageInfo>, <xref:PDiffy.PathInfo> | What's drawn on each page |

Constants and flags that list every option, so you never need to know PDF names:

| Type | Lists |
|---|---|
| <xref:PDiffy.StructureType> | Every standard tag type, such as `Figure` or `H1` |
| <xref:PDiffy.ContentRole> | Tagged, artifact or untagged |
| <xref:PDiffy.TextRenderMode> | How text is drawn, such as `Invisible` |
| <xref:PDiffy.AnnotationFlags> | Annotation flags, such as `Hidden` |
| <xref:PDiffy.FormFieldType>, <xref:PDiffy.FormFieldFlags> | Form field types and flags |
| <xref:PDiffy.DocumentPermissions> | What an encrypted document allows |
| <xref:PDiffy.PageModes>, <xref:PDiffy.PageLayouts> | How the file opens |

## The Word model

<xref:PDiffy.DocxDocument> is everything PDiffy reads from a DOCX file.

| Type | Holds |
|---|---|
| <xref:PDiffy.DocxInfo>, <xref:PDiffy.PageSetup> | Document settings and each section's page setup |
| <xref:PDiffy.DocxStyle>, <xref:PDiffy.DocxList> | Styles in use and lists |
| <xref:PDiffy.DocxBlock> | A paragraph, table, content control or inserted document: <xref:PDiffy.DocxParagraph>, <xref:PDiffy.DocxTable>, <xref:PDiffy.DocxControl>, <xref:PDiffy.DocxInserted> |
| <xref:PDiffy.DocxInline> | Something inside a paragraph, such as a <xref:PDiffy.DocxLink>, <xref:PDiffy.DocxField>, <xref:PDiffy.DocxImage> or <xref:PDiffy.DocxRevision> |
| <xref:PDiffy.DocxHeaderFooter>, <xref:PDiffy.DocxNote> | Headers, footers, notes and comments |
