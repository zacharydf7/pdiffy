using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using PDiffy.Reading;

namespace PDiffy.Docx.Reading;

/// <summary>
/// The parts of a DOCX file, which is a zip of XML files linked by relationship files (word/_rels/document.xml.rels
/// and so on). A part that can't be read as XML, or is larger than <see cref="MaxPartSize"/>, is noted in
/// <see cref="Unreadable"/> and treated as missing.
/// </summary>
internal sealed class DocxPackage : IDisposable
{
    /// <summary>A part is loaded into memory whole, so a larger one, such as a zip bomb's, isn't read.</summary>
    public const long MaxPartSize = 256L * 1024 * 1024;

    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";

    private readonly ZipArchive _zip;
    private readonly Dictionary<string, List<XElement>> _relationships = [];

    public DocxPackage(Stream stream, int maxDepth)
    {
        _zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        MaxDepth = maxDepth;
        MainPart = Related("", "/officeDocument") ?? "word/document.xml";
    }

    public string MainPart { get; }

    /// <summary>How deep a part's elements may nest; a part nested deeper is unreadable.</summary>
    public int MaxDepth { get; }

    public List<string> Unreadable { get; } = [];

    /// <summary>
    /// The first 16 hex digits of the SHA-256 of the parts' bytes one after another, such as an image's, or null when
    /// none of them exists.
    /// </summary>
    public string? Hash(params string?[] parts)
    {
        var entries = parts.Select(part => part is null ? null : _zip.GetEntry(part)).OfType<ZipArchiveEntry>()
            .ToList();
        if (entries.Count == 0) return null;

        // Read in pieces, so a large embedded file isn't held in memory to hash it.
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        foreach (var entry in entries)
        {
            using var stream = entry.Open();
            int read;
            while ((read = stream.Read(buffer)) > 0) hash.AppendData(buffer, 0, read);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset())[..16];
    }

    /// <summary>A part's bytes in a stream of their own, such as a DOCX inserted into this one, or null when there's no such part.</summary>
    /// <exception cref="InvalidDataException">The part is larger than <see cref="MaxPartSize"/>.</exception>
    public MemoryStream? Open(string? part)
    {
        if (part is null || _zip.GetEntry(part) is not { } entry) return null;
        if (entry.Length > MaxPartSize) throw new InvalidDataException($"{part} is larger than {MaxPartSize} bytes.");

        var copy = new MemoryStream();
        using (var stream = entry.Open()) stream.CopyTo(copy);
        copy.Position = 0;
        return copy;
    }

    public XDocument? Xml(string? part)
    {
        if (part is null || _zip.GetEntry(part) is not { } entry) return null;

        // The zip reader stops at the size the entry declares, so a part can't expand past this check.
        var document = entry.Length <= MaxPartSize ? TryLoad(entry) : null;
        if (document is null) Unreadable.Add(part);
        return document;
    }

    private XDocument? TryLoad(ZipArchiveEntry entry)
    {
        try
        {
            using var stream = entry.Open();
            return UntrustedXml.Load(stream, preserveWhitespace: true, MaxDepth);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// The part related to <paramref name="part"/> whose relationship type ends with the given name, such as
    /// "/styles", as a part name like "word/styles.xml".
    /// </summary>
    public string? Related(string part, string typeEnding) => Target(part,
        RelationshipsOf(part).FirstOrDefault(rel => ((string?)rel.Attribute("Type"))?.EndsWith(typeEnding, StringComparison.Ordinal) == true));

    /// <summary>
    /// Where a relationship of a part points, by its id (r:id, r:embed): a part name for something in the file, or the
    /// address as written for something outside it, such as a link's URL.
    /// </summary>
    public string? Target(string part, string id) =>
        Target(part, RelationshipsOf(part).FirstOrDefault(rel => (string?)rel.Attribute("Id") == id));

    private string? Target(string part, XElement? relationship)
    {
        if ((string?)relationship?.Attribute("Target") is not { } target) return null;
        if ((string?)relationship.Attribute("TargetMode") == "External") return target;
        return Resolve(part.Contains('/') ? part[..part.LastIndexOf('/')] : "", target);
    }

    // Read once per part: a document with many images or links looks each one up.
    private List<XElement> RelationshipsOf(string part)
    {
        if (_relationships.TryGetValue(part, out var cached)) return cached;
        var folder = part.Contains('/') ? part[..part.LastIndexOf('/')] : "";
        var name = part[(part.LastIndexOf('/') + 1)..];
        var relationships = Xml(folder.Length == 0 ? $"_rels/{name}.rels" : $"{folder}/_rels/{name}.rels")?.Root;
        return _relationships[part] = relationships?.Elements(Relationships + "Relationship").ToList() ?? [];
    }

    public void Dispose() => _zip.Dispose();

    // Targets are relative to the part's folder, or absolute from the package root when they start with a slash.
    private static string Resolve(string folder, string target)
    {
        var segments = new List<string>();
        var path = target.StartsWith('/') ? target[1..] : folder.Length == 0 ? target : $"{folder}/{target}";
        foreach (var segment in path.Split('/'))
        {
            if (segment == "..") { if (segments.Count > 0) segments.RemoveAt(segments.Count - 1); }
            else if (segment is not ("." or "")) segments.Add(segment);
        }

        return string.Join('/', segments);
    }
}
