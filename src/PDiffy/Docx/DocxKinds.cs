namespace PDiffy;

/// <summary>What a tracked change (<see cref="DocxRevision"/>) changed.</summary>
public enum DocxRevisionKind
{
    /// <summary>Text was inserted.</summary>
    Insert,

    /// <summary>Text was deleted.</summary>
    Delete,

    /// <summary>Text was moved here.</summary>
    MoveTo,

    /// <summary>Text was moved away from here.</summary>
    MoveFrom,

    /// <summary>A run's formatting changed.</summary>
    Format,

    /// <summary>The paragraph was inserted.</summary>
    ParagraphInsert,

    /// <summary>The paragraph was deleted.</summary>
    ParagraphDelete,

    /// <summary>The paragraph was moved here.</summary>
    ParagraphMoveTo,

    /// <summary>The paragraph was moved away from here.</summary>
    ParagraphMoveFrom,

    /// <summary>The paragraph's formatting changed.</summary>
    ParagraphFormat,

    /// <summary>The formatting of the paragraph mark, the pilcrow at its end, changed.</summary>
    ParagraphMarkFormat,

    /// <summary>A table's formatting changed.</summary>
    TableFormat,

    /// <summary>A table's column widths changed.</summary>
    TableGrid,

    /// <summary>A row was inserted.</summary>
    RowInsert,

    /// <summary>A row was deleted.</summary>
    RowDelete,

    /// <summary>A row's formatting changed.</summary>
    RowFormat,

    /// <summary>The table formatting a row overrides changed.</summary>
    RowTableFormat,

    /// <summary>A cell was inserted.</summary>
    CellInsert,

    /// <summary>A cell was deleted.</summary>
    CellDelete,

    /// <summary>A cell was merged with the one above or below it.</summary>
    CellMerge,

    /// <summary>A cell's formatting changed.</summary>
    CellFormat,

    /// <summary>A section's page settings changed.</summary>
    SectionFormat,
}

/// <summary>
/// The kind of a form control: a content control (<see cref="DocxForm"/>, <see cref="DocxControl"/>) or a legacy form
/// field.
/// </summary>
public enum DocxFormKind
{
    /// <summary>Plain text.</summary>
    Text,

    /// <summary>Formatted text, the kind a content control is when it sets no other.</summary>
    RichText,

    /// <summary>A drop-down list.</summary>
    Dropdown,

    /// <summary>A combo box, a drop-down list that also takes typed text.</summary>
    Combobox,

    /// <summary>A date picker.</summary>
    Date,

    /// <summary>A check box.</summary>
    Checkbox,

    /// <summary>A picture.</summary>
    Picture,

    /// <summary>A building-block gallery, such as a table of contents.</summary>
    Gallery,

    /// <summary>A group of content that can't be edited apart.</summary>
    Group,

    /// <summary>A citation.</summary>
    Citation,

    /// <summary>A bibliography.</summary>
    Bibliography,

    /// <summary>An equation.</summary>
    Equation,

    /// <summary>A section the reader can repeat.</summary>
    RepeatingSection,

    /// <summary>One copy of a repeating section.</summary>
    RepeatingSectionItem,

    /// <summary>A legacy text form field.</summary>
    LegacyText,

    /// <summary>A legacy check box form field.</summary>
    LegacyCheckbox,

    /// <summary>A legacy drop-down form field.</summary>
    LegacyDropdown,
}

/// <summary>The kind of a <see cref="DocxNote"/> or <see cref="DocxNoteReference"/>.</summary>
public enum DocxNoteKind
{
    /// <summary>A footnote.</summary>
    Footnote,

    /// <summary>An endnote.</summary>
    Endnote,

    /// <summary>A comment; only a note, never a reference, has this kind.</summary>
    Comment,
}
