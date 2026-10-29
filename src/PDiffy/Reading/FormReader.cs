using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>Reads every field in the AcroForm field tree.</summary>
internal sealed class FormReader(PdfObjects objects, PageTree pages)
{
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

    public static IReadOnlyList<FormField> Read(PdfObjects objects, DictionaryToken catalog, PageTree pages)
    {
        if (objects.Dictionary(catalog, "AcroForm") is not { } form)
        {
            return [];
        }

        var reader = new FormReader(objects, pages);
        reader.MapWidgetPages();
        var pending = new Stack<(IToken Field, Inherited Parent)>();
        foreach (var field in objects.Items(form.Data.GetValueOrDefault("Fields")).Reverse())
        {
            pending.Push((field, new Inherited(null, null, null, null, null)));
        }

        while (pending.TryPop(out var next)) reader.AddField(next.Field, next.Parent, pending);
        return reader._fields;
    }

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

    // Type, flags, value and maximum length pass down to child fields that don't set their own. Child fields go on the
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
        var current = new Inherited(name, objects.Name(field, "FT") ?? parent.Type, objects.Integer(field, "Ff") ?? parent.Flags,
            objects.Get(field, "V") ?? parent.Value, objects.Integer(field, "MaxLen") ?? parent.MaxLength);

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
        _fields.Add(new FormField
        {
            Name = name ?? "(unnamed)",
            Type = TypeOf(current.Type, flags),
            Page = PageOf(field.Data.ContainsKey("Subtype") ? token : kids.FirstOrDefault()),
            AccessibleName = objects.Text(field, "TU"),
            Value = FormatValue(objects.Resolve(current.Value)),
            Options = ReadOptions(field),
            MaxLength = current.MaxLength,
            Flags = FlagsOf(current.Type, flags),
        });
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

    private sealed record Inherited(string? Name, string? Type, int? Flags, IToken? Value, int? MaxLength);
}
