using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace PDiffy.Docx.Reading;

/// <summary>
/// The parts of a DOCX file, which is a zip of XML files linked by relationship files (word/_rels/document.xml.rels
/// and so on). A part that can't be read as XML is noted in <see cref="Unreadable"/> and treated as missing.
/// </summary>
internal sealed class DocxPackage : IDisposable
{
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";

    private readonly ZipArchive _zip;

    public DocxPackage(Stream stream)
    {
        _zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        MainPart = Related("", "/officeDocument") ?? "word/document.xml";
    }

    public string MainPart { get; }

    public List<string> Unreadable { get; } = [];

    public XDocument? Xml(string? part)
    {
        if (part is null || _zip.GetEntry(part) is not { } entry) return null;
        try
        {
            using var stream = entry.Open();
            return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            Unreadable.Add(part);
            return null;
        }
    }

    /// <summary>
    /// The part related to <paramref name="part"/> whose relationship type ends with the given name, such as
    /// "/styles", as a part name like "word/styles.xml".
    /// </summary>
    public string? Related(string part, string typeEnding)
    {
        var folder = part.Contains('/') ? part[..part.LastIndexOf('/')] : "";
        var name = part[(part.LastIndexOf('/') + 1)..];
        var relationships = Xml(folder.Length == 0 ? $"_rels/{name}.rels" : $"{folder}/_rels/{name}.rels")?.Root;
        var target = relationships?.Elements(Relationships + "Relationship")
            .Where(rel => ((string?)rel.Attribute("Type"))?.EndsWith(typeEnding, StringComparison.Ordinal) == true)
            .Select(rel => (string?)rel.Attribute("Target")).FirstOrDefault();
        return target is null ? null : Resolve(folder, target);
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
