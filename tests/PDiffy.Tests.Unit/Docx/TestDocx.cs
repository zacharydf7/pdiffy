using System.IO.Compression;
using System.Text;

namespace PDiffy.Tests.Unit.Docx;

/// <summary>
/// Writes small DOCX files for tests. Each part is given as the WordprocessingML inside its root element, with the w:
/// prefix already declared, so a test reads like the XML Word would write.
/// </summary>
internal sealed class TestDocx
{
    private const string Namespaces = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" "
        + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";

    /// <summary>What goes inside w:body.</summary>
    public string Body { get; init; } = "";

    /// <summary>What goes inside w:styles, such as w:docDefaults and w:style elements. Null leaves the part out.</summary>
    public string? Styles { get; init; }

    /// <summary>What goes inside w:numbering, such as w:abstractNum and w:num elements. Null leaves the part out.</summary>
    public string? Numbering { get; init; }

    /// <summary>What goes inside w:settings. Null leaves the part out.</summary>
    public string? Settings { get; init; }

    /// <summary>What goes inside cp:coreProperties, using the dc: and dcterms: prefixes. Null leaves the part out.</summary>
    public string? Core { get; init; }

    /// <summary>The theme's heading (major) and body (minor) fonts. Null leaves the theme out.</summary>
    public (string Major, string Minor)? ThemeFonts { get; init; }

    /// <summary>Replaces a part's whole content, for writing broken files.</summary>
    public Dictionary<string, string> RawParts { get; } = [];

    public MemoryStream Build()
    {
        var parts = new Dictionary<string, string>
        {
            ["word/document.xml"] = $"<w:document {Namespaces}><w:body>{Body}</w:body></w:document>",
        };
        var relationships = new StringBuilder();
        if (Styles is not null)
        {
            parts["word/styles.xml"] = $"<w:styles {Namespaces}>{Styles}</w:styles>";
            relationships.Append(Relationship("styles", "styles.xml"));
        }

        if (Numbering is not null)
        {
            parts["word/numbering.xml"] = $"<w:numbering {Namespaces}>{Numbering}</w:numbering>";
            relationships.Append(Relationship("numbering", "numbering.xml"));
        }

        if (Settings is not null)
        {
            parts["word/settings.xml"] = $"<w:settings {Namespaces}>{Settings}</w:settings>";
            relationships.Append(Relationship("settings", "settings.xml"));
        }

        if (ThemeFonts is var (major, minor))
        {
            parts["word/theme/theme1.xml"] = "<a:theme xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><a:themeElements>"
                + $"<a:fontScheme><a:majorFont><a:latin typeface=\"{major}\"/></a:majorFont><a:minorFont><a:latin typeface=\"{minor}\"/></a:minorFont>"
                + "</a:fontScheme></a:themeElements></a:theme>";
            relationships.Append(Relationship("theme", "theme/theme1.xml"));
        }

        var packageRelationships = new StringBuilder(Relationship("officeDocument", "word/document.xml"));
        if (Core is not null)
        {
            parts["docProps/core.xml"] = "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" "
                + "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\">" + Core + "</cp:coreProperties>";
            packageRelationships.Append("<Relationship Id=\"rCore\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/"
                + "metadata/core-properties\" Target=\"docProps/core.xml\"/>");
        }

        parts["_rels/.rels"] = Relationships(packageRelationships.ToString());
        parts["word/_rels/document.xml.rels"] = Relationships(relationships.ToString());
        parts["[Content_Types].xml"] = "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
            + "<Default Extension=\"xml\" ContentType=\"application/xml\"/></Types>";
        foreach (var (name, content) in RawParts) parts[name] = content;

        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in parts)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    public DocxDocument Load() => DocxDocument.Load(Build());

    private static string Relationship(string type, string target) =>
        $"<Relationship Id=\"r{type}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/{type}\" Target=\"{target}\"/>";

    private static string Relationships(string content) =>
        $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">{content}</Relationships>";
}
