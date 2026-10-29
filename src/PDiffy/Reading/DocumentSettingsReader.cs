using System.Globalization;
using System.Security.Cryptography;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

internal sealed class DocumentSettingsReader(PdfObjects objects, PageTree pages)
{
    // PDF numbers permission bits from 1; these are the ones with a meaning.
    private static readonly (int Bit, DocumentPermissions Permission)[] PermissionBits =
    [
        (3, DocumentPermissions.Print), (4, DocumentPermissions.Modify), (5, DocumentPermissions.Copy),
        (6, DocumentPermissions.Annotate), (9, DocumentPermissions.FillForms), (10, DocumentPermissions.ExtractForAccessibility),
        (11, DocumentPermissions.Assemble), (12, DocumentPermissions.PrintHighQuality),
    ];

    public static DocumentSettings Read(PdfDocument document, PdfObjects objects, DictionaryToken catalog, PageTree pages)
    {
        var reader = new DocumentSettingsReader(objects, pages);
        var names = objects.Dictionary(catalog, "Names");
        var openAction = catalog.Data.GetValueOrDefault("OpenAction");
        var openActions = reader.DescribeOpenActions(openAction);
        return new DocumentSettings
        {
            PdfVersion = document.Version.ToString("0.0", CultureInfo.InvariantCulture),
            CatalogVersion = objects.Name(catalog, "Version"),
            PageMode = objects.Name(catalog, "PageMode"),
            PageLayout = objects.Name(catalog, "PageLayout"),
            OpenAction = openActions.Count == 0 ? null : string.Join(", ", openActions),
            OpenActions = openActions,
            // The catalog's /AA actions (run on close, save and print) are always JavaScript.
            HasJavaScript = names?.Data.ContainsKey("JavaScript") == true || catalog.Data.ContainsKey("AA")
                || reader.Chain(openAction).Any(action => objects.Name(action, "S") == "JavaScript"),
            HasAdditionalActions = catalog.Data.ContainsKey("AA"),
            IsEncrypted = document.IsEncrypted,
            Permissions = document.IsEncrypted ? reader.ReadPermissions(document) : null,
            ViewerPreferences = objects.Settings(objects.Dictionary(catalog, "ViewerPreferences"), "DisplayDocTitle"),
            Layers = reader.ReadLayers(catalog),
            EmbeddedFiles = names is null ? [] : reader.ReadEmbeddedFiles(names.Data.GetValueOrDefault("EmbeddedFiles")),
        };
    }

    // An open action is a destination or an action dictionary with a type (/S) and its own details.
    private string? DescribeAction(IToken? action)
    {
        if (objects.Resolve(action) is not DictionaryToken dictionary)
        {
            return DescribeDestination(action);
        }

        var type = objects.Name(dictionary, "S");
        return type switch
        {
            "GoTo" => $"GoTo {DescribeDestination(dictionary.Data.GetValueOrDefault("D"))}".TrimEnd(),
            "URI" => $"URI {objects.Text(dictionary, "URI")}",
            _ => type,
        };
    }

    // A destination is [page view-type arguments...], such as [page /XYZ left top zoom], or the name of one.
    private string? DescribeDestination(IToken? destination)
    {
        if (objects.Resolve(destination) is not ArrayToken array || array.Data.Count == 0)
        {
            return objects.Describe(destination) is { } name ? $"named {name}" : null;
        }

        var page = pages.NumberOf(array.Data[0])?.ToString() ?? "?";
        var view = array.Data.Skip(1).Select(item => objects.Describe(item) ?? "null");
        return string.Join(" ", view.Prepend($"p{page}"));
    }

    // The open action and those it chains through /Next, in the order they run, each described on its own.
    private List<string> DescribeOpenActions(IToken? openAction)
    {
        var chained = Chain(openAction).Skip(1).Select(action => DescribeAction(action));
        return chained.Prepend(DescribeAction(openAction)).OfType<string>().ToList();
    }

    // An action and those it chains through /Next, in the order they run. An action reached a second time (a
    // malformed loop) is skipped.
    private List<DictionaryToken> Chain(IToken? first)
    {
        var chain = new List<DictionaryToken>();
        var pending = new Stack<IToken>();
        var visited = new HashSet<IndirectReference>();
        if (first is not null)
        {
            pending.Push(first);
        }

        while (pending.Count > 0)
        {
            var token = pending.Pop();
            if (token is IndirectReferenceToken reference && !visited.Add(reference.Data))
            {
                continue;
            }

            if (objects.Resolve(token) is not DictionaryToken action)
            {
                continue;
            }

            chain.Add(action);

            // Pushed last to first, so they're taken first to last.
            foreach (var next in objects.Items(action.Data.GetValueOrDefault("Next")).Reverse())
            {
                pending.Push(next);
            }
        }

        return chain;
    }

    private DocumentPermissions ReadPermissions(PdfDocument document)
    {
        var encryption = objects.Resolve(document.Structure.Trailer.EncryptionToken) as DictionaryToken;
        var flags = encryption is null ? 0 : objects.Integer(encryption, "P") ?? 0;
        var permissions = DocumentPermissions.None;
        foreach (var (bit, permission) in PermissionBits)
        {
            if ((flags & (1 << (bit - 1))) != 0)
            {
                permissions |= permission;
            }
        }

        return permissions;
    }

    // A layer is visible by default unless the default configuration turns it off, or turns everything off and not it back on.
    private IReadOnlyList<Layer> ReadLayers(DictionaryToken catalog)
    {
        if (objects.Dictionary(catalog, "OCProperties") is not { } properties)
        {
            return [];
        }

        var defaults = objects.Dictionary(properties, "D");
        var allOff = defaults is not null && objects.Name(defaults, "BaseState") == "OFF";
        var off = References(defaults, "OFF");
        var on = References(defaults, "ON");
        var layers = new List<Layer>();
        foreach (var item in objects.Items(properties.Data.GetValueOrDefault("OCGs")))
        {
            if (objects.Resolve(item) is not DictionaryToken group)
            {
                continue;
            }

            var turnedOn = item is IndirectReferenceToken onReference && on.Contains(onReference.Data);
            var turnedOff = item is IndirectReferenceToken offReference && off.Contains(offReference.Data);
            var visible = allOff ? turnedOn : !turnedOff;
            layers.Add(new Layer(objects.Text(group, "Name") ?? "(unnamed)", visible));
        }

        return layers;
    }

    private HashSet<IndirectReference> References(DictionaryToken? owner, string key)
    {
        var items = owner is null ? [] : objects.Items(owner.Data.GetValueOrDefault(key));
        return items.OfType<IndirectReferenceToken>().Select(reference => reference.Data).ToHashSet();
    }

    // Embedded files are a name tree of file specifications; the file's contents are in /EF.
    private IReadOnlyList<EmbeddedFile> ReadEmbeddedFiles(IToken? tree)
    {
        var files = new List<EmbeddedFile>();
        foreach (var (key, value) in objects.TreeEntries(tree, "Names"))
        {
            var name = PdfObjects.TextOf(objects.Resolve(key)) ?? "";
            if (objects.Resolve(value) is not DictionaryToken specification)
            {
                files.Add(new EmbeddedFile(name, null, null, null, null, null));
                continue;
            }

            var streams = objects.Dictionary(specification, "EF");
            var stream = streams is null ? null : (objects.Get(streams, "UF") ?? objects.Get(streams, "F")) as StreamToken;
            var contents = stream is null ? null : objects.Contents(stream);
            var hash = contents is null ? null : Convert.ToHexStringLower(SHA256.HashData(contents))[..16];
            var file = new EmbeddedFile(name, objects.Text(specification, "UF") ?? objects.Text(specification, "F"),
                objects.Text(specification, "Desc"), objects.Name(specification, "AFRelationship"), contents?.Length, hash);
            files.Add(file with { Unreadable = stream is not null && contents is null });
        }

        return files;
    }
}
