using System.Globalization;
using System.Security.Cryptography;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

internal sealed class DocumentSettingsReader(PdfObjects objects, DestinationReader destinations)
{
    // PDF numbers permission bits from 1; these are the ones with a meaning.
    private static readonly (int Bit, PdfPermissions Permission)[] PermissionBits =
    [
        (3, PdfPermissions.Print), (4, PdfPermissions.Modify), (5, PdfPermissions.Copy),
        (6, PdfPermissions.Annotate), (9, PdfPermissions.FillForms), (10, PdfPermissions.ExtractForAccessibility),
        (11, PdfPermissions.Assemble), (12, PdfPermissions.PrintHighQuality),
    ];

    public static PdfSettings Read(PdfDocument document, PdfObjects objects, DictionaryToken catalog,
        DestinationReader destinations, PageTree pages)
    {
        var reader = new DocumentSettingsReader(objects, destinations);
        var names = objects.Dictionary(catalog, "Names");
        var openAction = catalog.Data.GetValueOrDefault("OpenAction");
        var openActions = reader.DescribeOpenActions(openAction);
        return new PdfSettings
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
            LayerConfigurations = reader.ReadLayerConfigurations(catalog),
            EmbeddedFiles =
            [
                .. names is null ? [] : reader.ReadEmbeddedFiles(names.Data.GetValueOrDefault("EmbeddedFiles")),
                .. reader.ReadAttachedFiles(pages),
            ],
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

    private PdfPermissions ReadPermissions(PdfDocument document)
    {
        var encryption = objects.Resolve(document.Structure.Trailer.EncryptionToken) as DictionaryToken;
        var flags = encryption is null ? 0 : objects.Integer(encryption, "P") ?? 0;
        var permissions = PdfPermissions.None;
        foreach (var (bit, permission) in PermissionBits)
        {
            if ((flags & (1 << (bit - 1))) != 0)
            {
                permissions |= permission;
            }
        }

        return permissions;
    }

    // The default configuration (/D), then the others (/Configs).
    private List<PdfLayerConfiguration> ReadLayerConfigurations(DictionaryToken catalog)
    {
        if (objects.Dictionary(catalog, "OCProperties") is not { } properties) return [];

        var configurations = objects.Items(properties.Data.GetValueOrDefault("Configs"))
            .Prepend(properties.Data.GetValueOrDefault("D"));
        return configurations.Select(objects.Resolve).OfType<DictionaryToken>().Select(configuration =>
            new PdfLayerConfiguration
            {
                Name = objects.Text(configuration, "Name"),
                ChangesLayersByItself = configuration.Data.ContainsKey("AS"),
            }).ToList();
    }

    // A layer is visible by default unless the default configuration turns it off, or turns everything off and not it back on.
    private IReadOnlyList<PdfLayer> ReadLayers(DictionaryToken catalog)
    {
        if (objects.Dictionary(catalog, "OCProperties") is not { } properties)
        {
            return [];
        }

        var defaults = objects.Dictionary(properties, "D");
        var allOff = defaults is not null && objects.Name(defaults, "BaseState") == "OFF";
        var off = References(defaults, "OFF");
        var on = References(defaults, "ON");
        var layers = new List<PdfLayer>();
        foreach (var item in objects.Items(properties.Data.GetValueOrDefault("OCGs")))
        {
            if (objects.Resolve(item) is not DictionaryToken group)
            {
                continue;
            }

            var turnedOn = item is IndirectReferenceToken onReference && on.Contains(onReference.Data);
            var turnedOff = item is IndirectReferenceToken offReference && off.Contains(offReference.Data);
            var visible = allOff ? turnedOn : !turnedOff;
            layers.Add(new PdfLayer { Name = objects.Text(group, "Name") ?? "(unnamed)", VisibleByDefault = visible });
        }

        return layers;
    }

    private HashSet<IndirectReference> References(DictionaryToken? owner, string key)
    {
        var items = owner is null ? [] : objects.Items(owner.Data.GetValueOrDefault(key));
        return items.OfType<IndirectReferenceToken>().Select(reference => reference.Data).ToHashSet();
    }

    // Embedded files are a name tree of file specifications.
    private List<PdfEmbeddedFile> ReadEmbeddedFiles(IToken? tree) =>
        objects.TreeEntries(tree, "Names")
            .Select(entry => ReadEmbeddedFile(PdfObjects.TextOf(objects.Resolve(entry.Key)) ?? "", entry.Value, null))
            .ToList();

    // A FileAttachment annotation, such as a viewer's "attach file" comment, holds a file specification (/FS) too. It
    // has no key in a list, so its file name names it. One that only names a file outside (a string, or no /EF) embeds
    // nothing, so it isn't listed.
    private List<PdfEmbeddedFile> ReadAttachedFiles(PageTree pages)
    {
        var files = new List<PdfEmbeddedFile>();
        for (var i = 0; i < pages.Pages.Count; i++)
        {
            foreach (var item in objects.Items(pages.Pages[i].Dictionary.Data.GetValueOrDefault("Annots")))
            {
                if (objects.Resolve(item) is not DictionaryToken annotation) continue;
                if (objects.Name(annotation, "Subtype") != PdfAnnotationTypes.FileAttachment) continue;

                var specification = annotation.Data.GetValueOrDefault("FS");
                if (objects.Resolve(specification) is not DictionaryToken dictionary) continue;
                if (!dictionary.Data.ContainsKey("EF")) continue;

                files.Add(ReadEmbeddedFile(objects.FileName(dictionary) ?? "(unnamed)", specification, i + 1));
            }
        }

        return files;
    }

    // A file specification names the file, and its contents are in /EF.
    private PdfEmbeddedFile ReadEmbeddedFile(string name, IToken? value, int? page)
    {
        if (objects.Resolve(value) is not DictionaryToken specification)
        {
            return new PdfEmbeddedFile
            {
                Name = name, Page = page, FileName = null, Description = null, Relationship = null, Size = null,
                Hash = null,
            };
        }

        var streams = objects.Dictionary(specification, "EF");
        var stream = streams is null ? null : (objects.Get(streams, "UF") ?? objects.Get(streams, "F")) as StreamToken;
        var contents = stream is null ? null : objects.Contents(stream);
        var hash = contents is null ? null : Convert.ToHexStringLower(SHA256.HashData(contents))[..16];
        return new PdfEmbeddedFile
        {
            Name = name, Page = page, FileName = objects.FileName(specification),
            Description = objects.Text(specification, "Desc"),
            Relationship = objects.Name(specification, "AFRelationship"), Size = contents?.Length, Hash = hash,
            NamedBothWays = specification.Data.ContainsKey("F") && specification.Data.ContainsKey("UF"),
            Unreadable = stream is not null && contents is null,
        };
    }
}
