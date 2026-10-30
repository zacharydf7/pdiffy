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

    /// <summary>The value a reset puts back.</summary>
    public string? DefaultValue { get; init; }

    /// <summary>The values a check box or radio button sends when it's on, one per widget, such as <c>Yes</c>.</summary>
    public IReadOnlyList<string> ExportValues { get; init; } = [];

    /// <summary>How text in the field lines up; left unless the field, a widget or the form says otherwise.</summary>
    public FormFieldAlignment Alignment { get; init; }

    /// <summary>The font of text typed into the field, as the PDF names it (<c>Helvetica</c>).</summary>
    public string? TextFont { get; init; }

    /// <summary>The size of text typed into the field, in points; 0 means the viewer fits the text to the field.</summary>
    public double? TextSize { get; init; }

    /// <summary>The color of text typed into the field, written like a layout color (<c>gray:0</c>, <c>#ff0000</c>).</summary>
    public string? TextColor { get; init; }

    /// <summary>
    /// The field's JavaScript: its own scripts, then each widget's, each in <see cref="FormScriptTrigger"/> order, with
    /// scripts an action chains after it under the same trigger.
    /// </summary>
    public IReadOnlyList<FormScript> Scripts { get; init; } = [];

    /// <summary>The signature in a signed signature field; null for other fields and unsigned ones.</summary>
    public SignatureInfo? Signature { get; init; }
}

/// <summary>Settings that apply to a whole form rather than to one field.</summary>
public sealed record FormSettings
{
    /// <summary>The fields whose calculate scripts run when a value changes, by full name, in the order they run.</summary>
    public IReadOnlyList<string> CalculationOrder { get; init; } = [];

    /// <summary>Whether the viewer must draw each field itself instead of using the appearance stored in the file.</summary>
    public bool NeedsAppearances { get; init; }

    /// <summary>The XFA form the PDF also carries, which viewers that support XFA show instead; null when there's none.</summary>
    public XfaFormKind? Xfa { get; init; }
}

/// <summary>What kind of XFA form a PDF carries.</summary>
public enum XfaFormKind
{
    /// <summary>A fixed layout, with the same pages as the PDF.</summary>
    Static,

    /// <summary>A layout the viewer builds from the XFA itself, so the PDF's own pages may be only a placeholder.</summary>
    Dynamic,
}

/// <summary>How text in a field lines up.</summary>
public enum FormFieldAlignment
{
    /// <summary>Lined up on the left, the default.</summary>
    Left,

    /// <summary>Centered.</summary>
    Center,

    /// <summary>Lined up on the right.</summary>
    Right,
}

/// <summary>
/// A script on a form field: when it runs, and a hash of its JavaScript, so any edit to it shows. The hash is null when
/// the script's stream is damaged and can't be read.
/// </summary>
public sealed record FormScript(FormScriptTrigger Trigger, string? Hash);

/// <summary>When a form field's script runs.</summary>
public enum FormScriptTrigger
{
    /// <summary>As the user types or picks a value.</summary>
    Keystroke,

    /// <summary>Before the value is shown, to format it.</summary>
    Format,

    /// <summary>When the value changes, to accept or reject it.</summary>
    Validate,

    /// <summary>When another field changes, to work out this field's value.</summary>
    Calculate,

    /// <summary>When the field is clicked or activated, such as a button press.</summary>
    Activate,

    /// <summary>When the pointer moves onto the field.</summary>
    MouseEnter,

    /// <summary>When the pointer leaves the field.</summary>
    MouseExit,

    /// <summary>When a mouse button is pressed on the field.</summary>
    MouseDown,

    /// <summary>When a mouse button is released on the field.</summary>
    MouseUp,

    /// <summary>When the field gets the keyboard focus.</summary>
    Focus,

    /// <summary>When the field loses the keyboard focus.</summary>
    Blur,

    /// <summary>When the field's page opens.</summary>
    PageOpen,

    /// <summary>When the field's page closes.</summary>
    PageClose,

    /// <summary>When the field's page comes into view.</summary>
    PageVisible,

    /// <summary>When the field's page goes out of view.</summary>
    PageInvisible,
}

/// <summary>
/// What a signature field's signature records, without checking it. The signature's bytes and signing time are left out,
/// since they change every time a file is signed.
/// </summary>
public sealed record SignatureInfo
{
    /// <summary>The signature's format, such as <c>adbe.pkcs7.detached</c> or <c>ETSI.CAdES.detached</c>.</summary>
    public string? SubFilter { get; init; }

    /// <summary>
    /// Whether the signed bytes run to the end of the file. An update saved after signing leaves the end unsigned. Null
    /// when the signature's byte range can't be read.
    /// </summary>
    public bool? CoversWholeFile { get; init; }

    public bool HasReason { get; init; }
    public bool HasLocation { get; init; }
    public bool HasSigningTime { get; init; }

    /// <summary>What a certifying signature still allows to change; null for a signature that doesn't certify.</summary>
    public SignatureCertification? Certification { get; init; }
}

/// <summary>What a certifying signature allows to change after signing without invalidating it.</summary>
public enum SignatureCertification
{
    NoChanges = 1,
    FormFilling = 2,
    FormFillingAndAnnotations = 3,
}
