using System.Globalization;
using System.Security.Cryptography;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

internal sealed class DocumentSettingsReader(PdfObjects objects, DestinationReader destinations)
{
    // PDF numbers permission bits from 1; these are the ones with a meaning.
    private static readonly (int Bit, DocumentPermissions Permission)[] PermissionBits =
    [
        (3, DocumentPermissions.Print), (4, DocumentPermissions.Modify), (5, DocumentPermissions.Copy),
        (6, DocumentPermissions.Annotate), (9, DocumentPermissions.FillForms), (10, DocumentPermissions.ExtractForAccessibility),
        (11, DocumentPermissions.Assemble), (12, DocumentPermissions.PrintHighQuality),
    ];

    public static DocumentSettings Read(PdfDocument document, PdfObjects objects, DictionaryToken catalog,
        DestinationReader destinations)
    {
        var reader = new DocumentSettingsReader(objects, destinations);
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
                || objects.Chain(openAction).Any(objects.RunsScript),
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

    // Read like a link's destination, so both agree, and printed in the PDF's order (p1 XYZ 72 720 2).
    private string? DescribeDestination(IToken? destination)
    {
        if (destinations.Read(destination) is not { } target) return null;
        if (target.Name is { } name) return $"named {name}";

        var positions = target.Positions.Select(position => position?.ToString(CultureInfo.InvariantCulture) ?? "-");
        var parts = new[] { $"p{target.Page?.ToString(CultureInfo.InvariantCulture) ?? "?"}", target.View }
            .Concat(positions).Append(target.Zoom?.ToString(CultureInfo.InvariantCulture));
        return string.Join(" ", parts.OfType<string>());
    }

    // The open action and those it chains through /Next, in the order they run, each described on its own.
    private List<string> DescribeOpenActions(IToken? openAction)
    {
        var chained = objects.Chain(openAction).Skip(1).Select(action => DescribeAction(action));
        return chained.Prepend(DescribeAction(openAction)).OfType<string>().ToList();
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
            var file = new EmbeddedFile(name, objects.FileName(specification), objects.Text(specification, "Desc"),
                objects.Name(specification, "AFRelationship"), contents?.Length, hash);
            files.Add(file with { Unreadable = stream is not null && contents is null });
        }

        return files;
    }
}
