using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>Reads every field in the AcroForm field tree.</summary>
internal sealed class FormReader(PdfObjects objects, PageTree pages, long fileLength, DictionaryToken form)
{
    // The keys of a field's own /AA and of its widget's /AA, with /A (a widget's click action) as Activate.
    private static readonly (string Key, FormScriptTrigger Trigger)[] FieldTriggers =
    [
        ("K", FormScriptTrigger.Keystroke), ("F", FormScriptTrigger.Format), ("V", FormScriptTrigger.Validate),
        ("C", FormScriptTrigger.Calculate),
    ];

    private static readonly (string Key, FormScriptTrigger Trigger)[] WidgetTriggers =
    [
        ("E", FormScriptTrigger.MouseEnter), ("X", FormScriptTrigger.MouseExit), ("D", FormScriptTrigger.MouseDown),
        ("U", FormScriptTrigger.MouseUp), ("Fo", FormScriptTrigger.Focus), ("Bl", FormScriptTrigger.Blur),
        ("PO", FormScriptTrigger.PageOpen), ("PC", FormScriptTrigger.PageClose), ("PV", FormScriptTrigger.PageVisible),
        ("PI", FormScriptTrigger.PageInvisible),
    ];

    // PDF numbers field flag bits from 1. Bits 16, 17 and 18 pick the button or choice type instead.
    private static readonly (int Bit, FormFieldFlags Flag)[] CommonFlags =
        [(1, FormFieldFlags.ReadOnly), (2, FormFieldFlags.Required), (3, FormFieldFlags.NoExport)];

    private static readonly (int Bit, FormFieldFlags Flag)[] TextFlags =
    [
        (13, FormFieldFlags.Multiline), (14, FormFieldFlags.Password), (21, FormFieldFlags.FileSelect),
        (23, FormFieldFlags.DoNotSpellCheck), (24, FormFieldFlags.DoNotScroll), (25, FormFieldFlags.Comb), (26, FormFieldFlags.RichText),
    ];

    private static readonly (int Bit, FormFieldFlags Flag)[] ButtonFlags =
        [(15, FormFieldFlags.NoToggleToOff), (26, FormFieldFlags.RadiosInUnison)];

    private static readonly (int Bit, FormFieldFlags Flag)[] ChoiceFlags =
    [
        (19, FormFieldFlags.Editable), (20, FormFieldFlags.Sort), (22, FormFieldFlags.MultiSelect),
        (23, FormFieldFlags.DoNotSpellCheck), (27, FormFieldFlags.CommitOnSelectionChange),
    ];

    private readonly List<FormField> _fields = [];
    private readonly HashSet<IndirectReference> _visited = [];
    private readonly Dictionary<IndirectReference, int> _widgetPages = [];
    private readonly Dictionary<IndirectReference, string> _names = [];

    /// <param name="fileLength">The file's length in bytes, to tell whether a signature covers all of it.</param>
    public static (IReadOnlyList<FormField> Fields, FormSettings? Settings) Read(PdfObjects objects, DictionaryToken catalog,
        PageTree pages, long fileLength)
    {
        if (objects.Dictionary(catalog, "AcroForm") is not { } form)
        {
            return ([], null);
        }

        var reader = new FormReader(objects, pages, fileLength, form);
        reader.MapWidgetPages();
        var pending = new Stack<(IToken Field, Inherited Parent)>();
        var formDefaults = new Inherited(null, null, null, null, null, null, objects.Text(form, "DA"), objects.Integer(form, "Q"));
        foreach (var field in objects.Items(form.Data.GetValueOrDefault("Fields")).Reverse())
        {
            pending.Push((field, formDefaults));
        }

        while (pending.TryPop(out var next)) reader.AddField(next.Field, next.Parent, pending);
        return (reader._fields, reader.ReadSettings(catalog));
    }

    // A form that also carries XFA is dynamic when the catalog asks the viewer to build its pages from the XFA.
    private FormSettings ReadSettings(DictionaryToken catalog) => new()
    {
        CalculationOrder = objects.Items(form.Data.GetValueOrDefault("CO"))
            .Select(field => field is IndirectReferenceToken reference ? _names.GetValueOrDefault(reference.Data) : null)
            .Select(name => name ?? "(unknown)")
            .ToList(),
        NeedsAppearances = objects.Boolean(form, "NeedAppearances"),
        Xfa = !form.Data.ContainsKey("XFA") ? null
            : objects.Boolean(catalog, "NeedsRendering") ? XfaFormKind.Dynamic : XfaFormKind.Static,
    };

    // A widget's page is the page whose /Annots lists it; its own /P entry is optional.
    private void MapWidgetPages()
    {
        for (var i = 0; i < pages.Pages.Count; i++)
        {
            foreach (var item in objects.Items(pages.Pages[i].Dictionary.Data.GetValueOrDefault("Annots")))
            {
                if (item is IndirectReferenceToken reference)
                {
                    _widgetPages.TryAdd(reference.Data, i + 1);
                }
            }
        }
    }

    // Type, flags, value, default value, maximum length, text appearance and alignment pass down to child fields that
    // don't set their own; the form sets the text appearance and alignment for fields that don't. Child fields go on the
    // stack instead of being read in turn, so a tree nested thousands deep can't overflow it. A node reached a second
    // time (a malformed loop) is skipped.
    private void AddField(IToken token, Inherited parent, Stack<(IToken Field, Inherited Parent)> pending)
    {
        if (token is IndirectReferenceToken visit && !_visited.Add(visit.Data))
        {
            return;
        }

        if (objects.Resolve(token) is not DictionaryToken field)
        {
            return;
        }

        var partialName = objects.Text(field, "T");
        var name = parent.Name is null ? partialName : partialName is null ? parent.Name : $"{parent.Name}.{partialName}";
        if (token is IndirectReferenceToken named && name is not null) _names[named.Data] = name;
        var current = new Inherited(name, objects.Name(field, "FT") ?? parent.Type, objects.Integer(field, "Ff") ?? parent.Flags,
            objects.Get(field, "V") ?? parent.Value, objects.Get(field, "DV") ?? parent.DefaultValue,
            objects.Integer(field, "MaxLen") ?? parent.MaxLength, objects.Text(field, "DA") ?? parent.Appearance,
            objects.Integer(field, "Q") ?? parent.Alignment);

        // Kids with their own /T are child fields; kids without one are this field's widgets on the page. A kid already
        // read (a loop back up the tree) doesn't count, so the looping field is still recorded once.
        var kids = objects.Items(field.Data.GetValueOrDefault("Kids"));
        var childFields = kids
            .Where(kid => !IsVisited(kid) && objects.Resolve(kid) is DictionaryToken child && child.Data.ContainsKey("T"))
            .ToList();
        if (childFields.Count > 0)
        {
            // Pushed last to first, so they're read first to last.
            for (var i = childFields.Count - 1; i >= 0; i--)
            {
                pending.Push((childFields[i], current));
            }

            return;
        }

        var flags = current.Flags ?? 0;
        var widgets = field.Data.ContainsKey("Subtype") ? [field] : kids.Select(objects.Resolve).OfType<DictionaryToken>().ToList();
        var type = TypeOf(current.Type, flags);
        // A viewer looks at the widget before the field, so a widget's own appearance and alignment win.
        var (font, size, color) = ReadAppearance(widgets.Select(widget => objects.Text(widget, "DA")).FirstOrDefault(da => da is not null)
            ?? current.Appearance);
        var alignment = widgets.Select(widget => objects.Integer(widget, "Q")).FirstOrDefault(q => q is not null) ?? current.Alignment;
        _fields.Add(new FormField
        {
            Name = name ?? "(unnamed)",
            Type = type,
            Page = PageOf(field.Data.ContainsKey("Subtype") ? token : kids.FirstOrDefault()),
            AccessibleName = objects.Text(field, "TU"),
            Value = FormatValue(objects.Resolve(current.Value)),
            Options = ReadOptions(field),
            MaxLength = current.MaxLength,
            Flags = FlagsOf(current.Type, flags),
            DefaultValue = FormatValue(objects.Resolve(current.DefaultValue)),
            ExportValues = type is FormFieldType.Checkbox or FormFieldType.RadioButton ? ExportValuesOf(widgets) : [],
            Alignment = alignment is 1 or 2 ? (FormFieldAlignment)alignment : FormFieldAlignment.Left,
            TextFont = font,
            TextSize = size,
            TextColor = color,
            Scripts = ReadScripts(field, widgets),
            Signature = current.Type == "Sig" ? ReadSignature(objects.Resolve(current.Value)) : null,
        });
    }

    // A check box or radio button widget's "on" appearance is named after the value it sends; /Off is its other state.
    private List<string> ExportValuesOf(IEnumerable<DictionaryToken> widgets) =>
        widgets.Select(widget => objects.Dictionary(widget, "AP") is { } appearances ? objects.Dictionary(appearances, "N") : null)
            .OfType<DictionaryToken>()
            .SelectMany(states => states.Data.Keys.Where(state => state != "Off").Order(StringComparer.Ordinal))
            .ToList();

    // A default appearance is content-stream operators such as "/Helv 12 Tf 0 g": the font resource and size, then the
    // color. The resource name is looked up in the form's fonts, since it's only a local name.
    private (string? Font, double? Size, string? Color) ReadAppearance(string? appearance)
    {
        var words = appearance?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) ?? [];
        string? font = null;
        double? size = null;
        string? color = null;
        for (var i = 0; i < words.Length; i++)
        {
            if (words[i] == "Tf" && i >= 2 && words[i - 2].StartsWith('/'))
            {
                font = FontNamed(words[i - 2][1..]);
                size = Number(words[i - 1]);
            }
            else if (words[i] is "g" or "rg" or "k")
            {
                color = ColorOf(words[i], words[..i]) ?? color;
            }
        }

        return (font, size, color);
    }

    private string FontNamed(string resource)
    {
        var fonts = objects.Dictionary(form, "DR") is { } resources ? objects.Dictionary(resources, "Font") : null;
        var font = fonts is null ? null : objects.Dictionary(fonts, resource);
        var name = font is null ? null : objects.Name(font, "BaseFont");
        return name is null ? resource : FontReader.WithoutSubsetPrefix(name);
    }

    private static string? ColorOf(string operation, string[] before)
    {
        var count = operation switch { "g" => 1, "rg" => 3, _ => 4 };
        if (before.Length < count) return null;
        var values = before[^count..].Select(Number).ToList();
        if (values.Any(value => value is null)) return null;
        var v = values.Select(value => value!.Value).ToList();
        IColor color = count switch
        {
            1 => new GrayColor(v[0]),
            3 => new RGBColor(v[0], v[1], v[2]),
            _ => new CMYKColor(v[0], v[1], v[2], v[3]),
        };
        return ColorText.Format(color);
    }

    private static double? Number(string word) =>
        double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;

    private List<FormScript> ReadScripts(DictionaryToken field, IReadOnlyList<DictionaryToken> widgets)
    {
        var scripts = new List<FormScript>();
        var fieldActions = objects.Dictionary(field, "AA");
        foreach (var (key, trigger) in FieldTriggers)
        {
            AddScript(scripts, trigger, fieldActions?.Data.GetValueOrDefault(key));
        }

        foreach (var widget in widgets)
        {
            AddScript(scripts, FormScriptTrigger.Activate, widget.Data.GetValueOrDefault("A"));
            var widgetActions = objects.Dictionary(widget, "AA");
            foreach (var (key, trigger) in WidgetTriggers)
            {
                AddScript(scripts, trigger, widgetActions?.Data.GetValueOrDefault(key));
            }
        }

        return scripts;
    }

    private void AddScript(List<FormScript> scripts, FormScriptTrigger trigger, IToken? action)
    {
        foreach (var chained in objects.Chain(action).Where(chained => objects.Name(chained, "S") == "JavaScript"))
        {
            var script = ScriptText(objects.Get(chained, "JS"));
            var hash = script is null ? null : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(script)))[..16];
            scripts.Add(new FormScript(trigger, hash));
        }
    }

    // A script is a text string or a stream of text, which is UTF-16 when it starts with a byte order mark and
    // otherwise PDFDocEncoding, close enough to Latin-1 to hash. Null when the stream is damaged.
    private string? ScriptText(IToken? script)
    {
        if (script is not StreamToken stream) return PdfObjects.TextOf(script) ?? "";
        var bytes = objects.Contents(stream);
        if (bytes is null) return null;
        if (bytes is [0xFE, 0xFF, ..]) return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes is [0xEF, 0xBB, 0xBF, ..]) return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        return Encoding.Latin1.GetString(bytes);
    }

    private SignatureInfo? ReadSignature(IToken? value)
    {
        if (value is not DictionaryToken signature)
        {
            return null;
        }

        return new SignatureInfo
        {
            SubFilter = objects.Name(signature, "SubFilter"),
            CoversWholeFile = CoversWholeFile(signature),
            HasReason = objects.Text(signature, "Reason") is not null,
            HasLocation = objects.Text(signature, "Location") is not null,
            HasSigningTime = objects.Text(signature, "M") is not null,
            Certification = CertificationOf(signature),
        };
    }

    // The byte range is [start, length, start, length]: the file before the signature's contents and the file after it.
    private bool? CoversWholeFile(DictionaryToken signature)
    {
        var range = objects.Items(signature.Data.GetValueOrDefault("ByteRange")).Select(objects.Resolve).ToList();
        if (range.Count != 4 || range.Any(item => item is not NumericToken))
        {
            return null;
        }

        var numbers = range.Cast<NumericToken>().Select(number => number.Long).ToList();
        return numbers[0] == 0 && numbers[2] + numbers[3] == fileLength;
    }

    // A certifying signature names the DocMDP transform; its /P defaults to 2, form filling.
    private SignatureCertification? CertificationOf(DictionaryToken signature)
    {
        foreach (var item in objects.Items(signature.Data.GetValueOrDefault("Reference")))
        {
            if (objects.Resolve(item) is DictionaryToken reference && objects.Name(reference, "TransformMethod") == "DocMDP")
            {
                var level = objects.Dictionary(reference, "TransformParams") is { } parameters ? objects.Integer(parameters, "P") : null;
                return level is 1 or 3 ? (SignatureCertification)level : SignatureCertification.FormFilling;
            }
        }

        return null;
    }

    private bool IsVisited(IToken kid) => kid is IndirectReferenceToken reference && _visited.Contains(reference.Data);

    private int? PageOf(IToken? widget)
    {
        if (widget is IndirectReferenceToken reference && _widgetPages.TryGetValue(reference.Data, out var page))
        {
            return page;
        }

        return objects.Resolve(widget) is DictionaryToken dictionary ? pages.NumberOf(dictionary.Data.GetValueOrDefault("P")) : null;
    }

    // An option is either the text shown or an [export value, text shown] pair.
    private IReadOnlyList<string> ReadOptions(DictionaryToken field) =>
        objects.Items(field.Data.GetValueOrDefault("Opt"))
            .Select(objects.Resolve)
            .Select(option => option is ArrayToken pair && pair.Data.Count > 1 ? objects.Resolve(pair.Data[1]) : option)
            .Select(option => PdfObjects.TextOf(option) ?? "")
            .ToList();

    // Nested arrays are followed only so deep, so a value that contains itself can't loop forever.
    private string? FormatValue(IToken? value, int depth = 0) => value switch
    {
        null => null,
        NameToken name => name.Data,
        NumericToken number => number.Data.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ArrayToken array when depth < PdfObjects.MaxArrayDepth =>
            string.Join(", ", array.Data.Select(item => FormatValue(objects.Resolve(item), depth + 1))),
        ArrayToken => "...",
        _ => PdfObjects.TextOf(value),
    };

    private static FormFieldType TypeOf(string? type, int flags) => type switch
    {
        "Tx" => FormFieldType.Text,
        "Btn" when HasBit(flags, 17) => FormFieldType.PushButton,
        "Btn" when HasBit(flags, 16) => FormFieldType.RadioButton,
        "Btn" => FormFieldType.Checkbox,
        "Ch" when HasBit(flags, 18) => FormFieldType.ComboBox,
        "Ch" => FormFieldType.ListBox,
        "Sig" => FormFieldType.Signature,
        _ => FormFieldType.Unknown,
    };

    private static FormFieldFlags FlagsOf(string? type, int flags)
    {
        var typeFlags = type switch { "Tx" => TextFlags, "Btn" => ButtonFlags, "Ch" => ChoiceFlags, _ => [] };
        var result = FormFieldFlags.None;
        foreach (var (bit, flag) in CommonFlags.Concat(typeFlags))
        {
            if (HasBit(flags, bit))
            {
                result |= flag;
            }
        }

        return result;
    }

    private static bool HasBit(int flags, int bit) => (flags & (1 << (bit - 1))) != 0;

    private sealed record Inherited(string? Name, string? Type, int? Flags, IToken? Value, IToken? DefaultValue, int? MaxLength,
        string? Appearance, int? Alignment);
}
