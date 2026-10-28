namespace PDiffy;

/// <summary>How a viewer treats an annotation: whether it is shown, printed, editable and so on.</summary>
[Flags]
public enum AnnotationFlags
{
    None = 0,
    Invisible = 1 << 0,
    Hidden = 1 << 1,
    Print = 1 << 2,
    NoZoom = 1 << 3,
    NoRotate = 1 << 4,
    NoView = 1 << 5,
    ReadOnly = 1 << 6,
    Locked = 1 << 7,
    ToggleNoView = 1 << 8,
    LockedContents = 1 << 9,
}

public enum FormFieldType
{
    Text,
    Checkbox,
    RadioButton,
    PushButton,
    ComboBox,
    ListBox,
    Signature,
    Unknown,
}

/// <summary>Options set on a form field. Which ones apply depends on the <see cref="FormFieldType"/>.</summary>
[Flags]
public enum FormFieldFlags
{
    None = 0,
    ReadOnly = 1 << 0,
    Required = 1 << 1,
    NoExport = 1 << 2,
    Multiline = 1 << 3,
    Password = 1 << 4,
    FileSelect = 1 << 5,
    DoNotSpellCheck = 1 << 6,
    DoNotScroll = 1 << 7,
    Comb = 1 << 8,
    RichText = 1 << 9,
    NoToggleToOff = 1 << 10,
    RadiosInUnison = 1 << 11,
    Editable = 1 << 12,
    Sort = 1 << 13,
    MultiSelect = 1 << 14,
    CommitOnSelectionChange = 1 << 15,
}

/// <summary>
/// A form field. <see cref="Name"/> is the full name, parent names joined with dots (address.city).
/// <see cref="AccessibleName"/> is what a screen reader announces for it.
/// </summary>
public sealed record FormField
{
    public required string Name { get; init; }
    public FormFieldType Type { get; init; }
    public int? Page { get; init; }
    public string? AccessibleName { get; init; }
    public string? Value { get; init; }
    public IReadOnlyList<string> Options { get; init; } = [];
    public int? MaxLength { get; init; }
    public FormFieldFlags Flags { get; init; }
}
