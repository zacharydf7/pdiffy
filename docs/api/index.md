# API reference

Everything public is in the `PDiffy` namespace. This page groups the types by what you'd use them for; the sidebar lists
them all.

## Snapshot tests

| Type | Use it to |
|---|---|
| <xref:PDiffy.Snapshot> | Compare a PDF, a Word document, text or JSON with its approved snapshot, and set the snapshot folder for every kind |
| <xref:PDiffy.SnapshotException> | The failure a test throws, with what changed |

## Options

| Type | Use it to |
|---|---|
| <xref:PDiffy.PdfSnapshotOptions> | Ignore, scrub or limit a PDF snapshot |
| <xref:PDiffy.PdfSection>, <xref:PDiffy.PdfDetail> | Name the PDF sections and details to leave out |
| <xref:PDiffy.PdfProfile>, <xref:PDiffy.PdfTolerance> | Pick what a test guards, and the numbers it lets differ a little |
| <xref:PDiffy.DocxSnapshotOptions> | Ignore or scrub a Word snapshot |
| <xref:PDiffy.DocxSection>, <xref:PDiffy.DocxDetail> | Name the Word sections and details to leave out |
| <xref:PDiffy.TextSnapshotOptions> | Scrub a text or JSON snapshot |

## Comparing

| Type | Use it to |
|---|---|
| <xref:PDiffy.SnapshotDiff> | Hold the differences between two snapshots, and print them as a summary, HTML or JSON |
| <xref:PDiffy.SnapshotChange> | Describe one difference: kind, section, path, page, detail, accessibility flag |
| <xref:PDiffy.ChangeKind> | Name the kind of a difference |

## Checks

| Type | Use it to |
|---|---|
| <xref:PDiffy.PdfCheck> | Name a rule <xref:PDiffy.PdfUaDocument.Check*> applies |
| <xref:PDiffy.PdfCheckFinding> | Describe what a rule found: where, how much it matters and the clause it comes from |
| <xref:PDiffy.PdfCheckSeverity>, <xref:PDiffy.PdfCheckCategory> | Tell errors from warnings, and group rules by family |
| <xref:PDiffy.PdfCheckSet> | Apply every rule, or only the rules a release shipped, narrowed by family or rule |

## The PDF model

<xref:PDiffy.PdfUaDocument> is everything PDiffy reads from a PDF, in the order a snapshot prints it.

| Type | Holds |
|---|---|
| <xref:PDiffy.PdfDocumentInfo>, <xref:PDiffy.PdfMetadata>, <xref:PDiffy.PdfSettings> | Document-wide facts, metadata and settings |
| <xref:PDiffy.PdfPage>, <xref:PDiffy.PdfPageLabelRange>, <xref:PDiffy.PdfBookmark> | Pages, page labels and the outline |
| <xref:PDiffy.PdfStructureElement>, <xref:PDiffy.PdfRoleMapping> | The tag tree and custom tag types |
| <xref:PDiffy.PdfPageText> | Artifact and untagged text, per page |
| <xref:PDiffy.PdfFont> | Fonts and the codes that don't map to Unicode |
| <xref:PDiffy.PdfAnnotation>, <xref:PDiffy.PdfFormField> | Annotations and form fields |
| <xref:PDiffy.PdfTextRun>, <xref:PDiffy.PdfImage>, <xref:PDiffy.PdfVectorPath> | What's drawn on each page |

Constants and flags that list every option, so you never need to know PDF names:

| Type | Lists |
|---|---|
| <xref:PDiffy.StructureType> | Every standard tag type, such as `Figure` or `H1` |
| <xref:PDiffy.PdfContentRole> | Tagged, artifact or untagged |
| <xref:PDiffy.PdfTextRenderMode> | How text is drawn, such as `Invisible` |
| <xref:PDiffy.PdfAnnotationFlags> | Annotation flags, such as `Hidden` |
| <xref:PDiffy.PdfFormFieldType>, <xref:PDiffy.PdfFormFieldFlags> | Form field types and flags |
| <xref:PDiffy.PdfPermissions> | What an encrypted document allows |
| <xref:PDiffy.PdfPageModes>, <xref:PDiffy.PdfPageLayouts> | How the file opens |
| <xref:PDiffy.PdfAnnotationTypes>, <xref:PDiffy.PdfActionTypes>, <xref:PDiffy.PdfDestinationViews> | Annotation types, link actions and the views a destination shows |
| <xref:PDiffy.PdfTabOrders> | A page's tab order |
| <xref:PDiffy.PdfTableScopes>, <xref:PDiffy.PdfListNumberings> | Table header scopes and list numbering |
| <xref:PDiffy.PdfPageLabelStyles> | Page label numbering, such as roman numerals |
| <xref:PDiffy.PdfArtifactTypes>, <xref:PDiffy.PdfArtifactSubtypes> | Artifact kinds, such as `Pagination` and `Header` |
| <xref:PDiffy.PdfFontTypes> | Font types, such as `TrueType` |
| <xref:PDiffy.PdfFileRelationships> | How an attached file relates to the document |

## The Word model

<xref:PDiffy.DocxDocument> is everything PDiffy reads from a DOCX file.

| Type | Holds |
|---|---|
| <xref:PDiffy.DocxInfo>, <xref:PDiffy.PageSetup> | Document settings and each section's page setup |
| <xref:PDiffy.DocxStyle>, <xref:PDiffy.DocxList> | Styles in use and lists |
| <xref:PDiffy.DocxBlock> | A paragraph, table, content control or inserted document: <xref:PDiffy.DocxParagraph>, <xref:PDiffy.DocxTable>, <xref:PDiffy.DocxControl>, <xref:PDiffy.DocxInserted> |
| <xref:PDiffy.DocxInline> | Something inside a paragraph, such as a <xref:PDiffy.DocxLink>, <xref:PDiffy.DocxField>, <xref:PDiffy.DocxImage> or <xref:PDiffy.DocxRevision> |
| <xref:PDiffy.DocxHeaderFooter>, <xref:PDiffy.DocxNote> | Headers, footers, notes and comments |
| <xref:PDiffy.DocxBorder>, <xref:PDiffy.DocxMargin>, <xref:PDiffy.DocxTabStop>, <xref:PDiffy.DocxLineSpacing>, <xref:PDiffy.DocxWidth>, <xref:PDiffy.DocxRowHeight> | Borders, cell margins, tab stops, line spacing, widths and row heights |

| Type | Lists |
|---|---|
| <xref:PDiffy.DocxRevisionKind> | Every kind of tracked change |
| <xref:PDiffy.DocxFormKind> | Content control and legacy form field kinds |
| <xref:PDiffy.DocxNoteKind> | Footnotes, endnotes and comments |
| <xref:PDiffy.DocxAlignments> | Paragraph alignment, where `both` means justified |
| <xref:PDiffy.DocxStyleTypes> | Style types |
| <xref:PDiffy.DocxSectionBreaks>, <xref:PDiffy.DocxHeaderFooterKinds>, <xref:PDiffy.DocxBreakKinds> | How sections start, header and footer kinds, and page and column breaks |
| <xref:PDiffy.DocxGraphicKinds> | Pictures, shapes, charts and other drawings |
| <xref:PDiffy.DocxUnderlines>, <xref:PDiffy.DocxHighlights>, <xref:PDiffy.DocxRunVerticalAligns> | Underlines, highlight colors, superscript and subscript |
| <xref:PDiffy.DocxCellMerges>, <xref:PDiffy.DocxCellVerticalAligns>, <xref:PDiffy.DocxTextDirections> | Merged cells, where a cell's text sits and which way it runs |
| <xref:PDiffy.DocxProtections> | What editing a protected document allows |
| <xref:PDiffy.DocxWidthUnit>, <xref:PDiffy.DocxSizeRules> | Width units, and how line spacing and row heights are measured |
