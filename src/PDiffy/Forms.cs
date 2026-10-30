namespace PDiffy;

/// <summary>How a viewer treats an annotation: whether it is shown, printed, editable and so on.</summary>
[Flags]
public enum PdfAnnotationFlags
{
    /// <summary>No flags set: the viewer shows the annotation on screen but leaves it off printouts.</summary>
    None = 0,

    /// <summary>
    /// A viewer that doesn't know the annotation's type hides it instead of drawing its stored appearance.
    /// </summary>
    Invisible = 1 << 0,

    /// <summary>The viewer doesn't show or print the annotation, and the user can't interact with it.</summary>
    Hidden = 1 << 1,

    /// <summary>The viewer prints the annotation with the page; without this flag it's left off printouts.</summary>
    Print = 1 << 2,

    /// <summary>The annotation keeps its size when the user zooms the page.</summary>
    NoZoom = 1 << 3,

    /// <summary>The annotation stays upright when the user rotates the page.</summary>
    NoRotate = 1 << 4,

    /// <summary>
    /// The viewer doesn't show the annotation on screen or let the user use it, but may still print it.
    /// </summary>
    NoView = 1 << 5,

    /// <summary>
    /// The user can't interact with the annotation, though it still shows and prints. A form field's widget uses
    /// <see cref="PdfFormFieldFlags.ReadOnly"/> instead.
    /// </summary>
    ReadOnly = 1 << 6,

    /// <summary>The user can't move, resize or delete the annotation, though its contents can still change.</summary>
    Locked = 1 << 7,

    /// <summary>Reverses <see cref="NoView"/> for events such as the pointer passing over the annotation.</summary>
    ToggleNoView = 1 << 8,

    /// <summary>The user can't change the annotation's contents, but can still move, resize or delete it.</summary>
    LockedContents = 1 << 9,
}

/// <summary>What kind of control a form field is.</summary>
public enum PdfFormFieldType
{
    /// <summary>A box the user types text into.</summary>
    Text,

    /// <summary>A check box, on or off.</summary>
    Checkbox,

    /// <summary>A set of radio buttons, of which at most one is on.</summary>
    RadioButton,

    /// <summary>A button that runs an action when pressed and holds no value.</summary>
    PushButton,

    /// <summary>
    /// A drop-down list of choices, which takes typed text too when <see cref="PdfFormFieldFlags.Editable"/>.
    /// </summary>
    ComboBox,

    /// <summary>A scrolling list of choices.</summary>
    ListBox,

    /// <summary>A field that holds a digital signature.</summary>
    Signature,

    /// <summary>A field whose type is missing or isn't one PDF defines.</summary>
    Unknown,
}

/// <summary>Options set on a form field. Which ones apply depends on the <see cref="PdfFormFieldType"/>.</summary>
[Flags]
public enum PdfFormFieldFlags
{
    /// <summary>No options set.</summary>
    None = 0,

    /// <summary>The user can't change the field's value.</summary>
    ReadOnly = 1 << 0,

    /// <summary>The field must have a value before the form can be submitted.</summary>
    Required = 1 << 1,

    /// <summary>The field's value isn't sent when the form is submitted.</summary>
    NoExport = 1 << 2,

    /// <summary>A text field that wraps its text onto more than one line.</summary>
    Multiline = 1 << 3,

    /// <summary>
    /// A text field that shows what's typed as dots or asterisks and doesn't save its value in the file.
    /// </summary>
    Password = 1 << 4,

    /// <summary>A text field that holds a file's path; submitting the form sends that file's contents.</summary>
    FileSelect = 1 << 5,

    /// <summary>The viewer doesn't spell-check text typed into a text field or editable combo box.</summary>
    DoNotSpellCheck = 1 << 6,

    /// <summary>A text field that doesn't scroll, so it takes no more text than fits in it.</summary>
    DoNotScroll = 1 << 7,

    /// <summary>
    /// A text field split into <see cref="PdfFormField.MaxLength"/> equal boxes, one character in each.
    /// </summary>
    Comb = 1 << 8,

    /// <summary>A text field whose value is formatted rich text rather than plain text.</summary>
    RichText = 1 << 9,

    /// <summary>Radio buttons that always keep one on: clicking the one that's on doesn't turn it off.</summary>
    NoToggleToOff = 1 << 10,

    /// <summary>Radio buttons with the same export value turn on and off together.</summary>
    RadiosInUnison = 1 << 11,

    /// <summary>A combo box that also lets the user type a value that isn't in its list.</summary>
    Editable = 1 << 12,

    /// <summary>
    /// The choices should be kept in alphabetical order. It tells the tool that edits the form; a viewer shows them in
    /// the order the file lists them.
    /// </summary>
    Sort = 1 << 13,

    /// <summary>A list box that lets the user pick more than one choice.</summary>
    MultiSelect = 1 << 14,

    /// <summary>
    /// A combo box or list box that saves a choice as soon as it's picked, not when the field loses focus.
    /// </summary>
    CommitOnSelectionChange = 1 << 15,
}

/// <summary>
/// A form field. <see cref="Name"/> is the full name, parent names joined with dots (address.city).
/// <see cref="AccessibleName"/> is what a screen reader announces for it.
/// </summary>
public sealed record PdfFormField
{
    internal PdfFormField()
    {
    }

    /// <summary>The field's full name, or <c>(unnamed)</c> when neither it nor its parents have one.</summary>
    public required string Name { get; init; }

    /// <summary>What kind of control the field is.</summary>
    public PdfFormFieldType Type { get; init; }

    /// <summary>The page number, from 1, of the field's first widget; null when its page can't be found.</summary>
    public int? Page { get; init; }

    /// <summary>The field's tooltip text (/TU), which a screen reader announces; null when it has none.</summary>
    public string? AccessibleName { get; init; }

    /// <summary>
    /// The field's value as text, or its parent's when it sets none; null when there's none. A check box or radio
    /// button holds the name of the state it's in (<c>Off</c> or an export value), and a list with several choices
    /// picked joins them with ", ".
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// The text shown for each choice of a combo box or list box, in order; empty when there are none.
    /// </summary>
    public IReadOnlyList<string> Options
    {
        get;
        init => field = ValueList<string>.Of(value);
    } = ValueList<string>.Empty;

    /// <summary>The most characters a text field takes, or its parent's limit; null when there's no limit.</summary>
    public int? MaxLength { get; init; }

    /// <summary>The options set on the field. Only those that apply to its <see cref="Type"/> are read.</summary>
    public PdfFormFieldFlags Flags { get; init; }

    /// <summary>The value a reset puts back.</summary>
    public string? DefaultValue { get; init; }

    /// <summary>The values a check box or radio button sends when it's on, one per widget, such as <c>Yes</c>.</summary>
    public IReadOnlyList<string> ExportValues
    {
        get;
        init => field = ValueList<string>.Of(value);
    } = ValueList<string>.Empty;

    /// <summary>How text in the field lines up; left unless the field, a widget or the form says otherwise.</summary>
    public PdfFormFieldAlignment Alignment { get; init; }

    /// <summary>The font of text typed into the field, as the PDF names it (<c>Helvetica</c>).</summary>
    public string? TextFont { get; init; }

    /// <summary>The size of text typed into the field, in points; 0 means the viewer fits the text to the field.</summary>
    public double? TextSize { get; init; }

    /// <summary>The color of text typed into the field, written like a layout color (<c>gray:0</c>, <c>#ff0000</c>).</summary>
    public string? TextColor { get; init; }

    /// <summary>
    /// The field's JavaScript: its own scripts, then each widget's, each in <see cref="PdfFormScriptTrigger"/> order,
    /// with scripts an action chains after it under the same trigger.
    /// </summary>
    public IReadOnlyList<PdfFormScript> Scripts
    {
        get;
        init => field = ValueList<PdfFormScript>.Of(value);
    } = ValueList<PdfFormScript>.Empty;

    /// <summary>The signature in a signed signature field; null for other fields and unsigned ones.</summary>
    public PdfSignature? Signature { get; init; }
}

/// <summary>Settings that apply to a whole form rather than to one field.</summary>
public sealed record PdfFormSettings
{
    internal PdfFormSettings()
    {
    }

    /// <summary>The fields whose calculate scripts run when a value changes, by full name, in the order they run.</summary>
    public IReadOnlyList<string> CalculationOrder
    {
        get;
        init => field = ValueList<string>.Of(value);
    } = ValueList<string>.Empty;

    /// <summary>Whether the viewer must draw each field itself instead of using the appearance stored in the file.</summary>
    public bool NeedsAppearances { get; init; }

    /// <summary>The XFA form the PDF also carries, which viewers that support XFA show instead; null when there's none.</summary>
    public PdfXfaForm? Xfa { get; init; }
}

/// <summary>What kind of XFA form a PDF carries.</summary>
public enum PdfXfaForm
{
    /// <summary>A fixed layout, with the same pages as the PDF.</summary>
    Static,

    /// <summary>A layout the viewer builds from the XFA itself, so the PDF's own pages may be only a placeholder.</summary>
    Dynamic,
}

/// <summary>How text in a field lines up.</summary>
public enum PdfFormFieldAlignment
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
public sealed record PdfFormScript
{
    internal PdfFormScript()
    {
    }

    /// <summary>The event that runs the script, such as a value changing or the field getting focus.</summary>
    public required PdfFormScriptTrigger Trigger { get; init; }

    /// <summary>The first 16 hex digits of a SHA-256 of the script's text, such as "9c1e5a07b3d24f68".</summary>
    public required string? Hash { get; init; }
}

/// <summary>When a form field's script runs.</summary>
public enum PdfFormScriptTrigger
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
public sealed record PdfSignature
{
    internal PdfSignature()
    {
    }

    /// <summary>The signature's format, such as <c>adbe.pkcs7.detached</c> or <c>ETSI.CAdES.detached</c>.</summary>
    public string? SubFilter { get; init; }

    /// <summary>
    /// Whether the signed bytes run to the end of the file. An update saved after signing leaves the end unsigned. Null
    /// when the signature's byte range can't be read.
    /// </summary>
    public bool? CoversWholeFile { get; init; }

    /// <summary>Whether the signature gives a reason for signing.</summary>
    public bool HasReason { get; init; }

    /// <summary>Whether the signature says where it was signed.</summary>
    public bool HasLocation { get; init; }

    /// <summary>Whether the signature records when it was signed. The time itself is left out.</summary>
    public bool HasSigningTime { get; init; }

    /// <summary>What a certifying signature still allows to change; null for a signature that doesn't certify.</summary>
    public PdfSignatureCertification? Certification { get; init; }
}

/// <summary>What a certifying signature allows to change after signing without invalidating it.</summary>
public enum PdfSignatureCertification
{
    /// <summary>No changes at all.</summary>
    NoChanges = 1,

    /// <summary>Filling in form fields and adding signatures, the default for a certifying signature.</summary>
    FormFilling = 2,

    /// <summary>Filling in form fields and adding signatures, and adding, editing or deleting annotations.</summary>
    FormFillingAndAnnotations = 3,
}
