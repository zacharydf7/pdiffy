using System.Xml;
using System.Xml.Linq;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

internal static class MetadataReader
{
    private static readonly XNamespace PdfUaId = "http://www.aiim.org/pdfua/ns/id/";
    private static readonly XNamespace PdfAId = "http://www.aiim.org/pdfa/ns/id/";
    private static readonly XNamespace DublinCore = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

    // Info entries that Metadata already has a property for; every other entry goes into OtherInfo.
    private static readonly string[] StandardInfoKeys =
        ["Title", "Author", "Subject", "Keywords", "Creator", "Producer", "CreationDate", "ModDate"];

    public static PdfDocumentInfo ReadDocumentInfo(PdfObjects objects, DictionaryToken catalog, int pageCount)
    {
        var markInfo = objects.Dictionary(catalog, "MarkInfo");
        var viewerPreferences = objects.Dictionary(catalog, "ViewerPreferences");
        var marked = markInfo is not null && objects.Boolean(markInfo, "Marked");
        var suspects = markInfo is not null && objects.Boolean(markInfo, "Suspects");
        var displayDocTitle = viewerPreferences is not null && objects.Boolean(viewerPreferences, "DisplayDocTitle");
        return new PdfDocumentInfo
        {
            Marked = marked, Suspects = suspects, Language = objects.Text(catalog, "Lang"),
            DisplayDocTitle = displayDocTitle, PageCount = pageCount,
            HasStructureTree = objects.Dictionary(catalog, "StructTreeRoot") is not null,
        };
    }

    public static PdfMetadata ReadMetadata(PdfDocument document, PdfObjects objects)
    {
        var info = document.Information;
        var otherInfo = objects.Settings(info.DocumentInformationDictionary, StandardInfoKeys);

        var xmp = document.TryGetXmpMetadata(out var found) ? found : null;
        var xml = xmp is null ? null : ReadXmp(xmp);
        var pdfUaPart = XmpValue(xml, PdfUaId + "part");
        var pdfAPart = XmpValue(xml, PdfAId + "part");
        var xmpTitle = xml?.Descendants(DublinCore + "title").Descendants().LastOrDefault()?.Value;
        return new PdfMetadata
        {
            Title = info.Title, Author = info.Author, Subject = info.Subject, Keywords = info.Keywords,
            HasCreatedDate = info.CreationDate is not null, HasModifiedDate = info.ModifiedDate is not null,
            Producer = info.Producer, Creator = info.Creator, PdfUaPart = pdfUaPart, XmpTitle = xmpTitle,
            OtherInfo = otherInfo, XmpCreator = XmpValue(xml, DublinCore + "creator"),
            XmpDescription = XmpValue(xml, DublinCore + "description"),
            XmpLanguage = XmpValue(xml, DublinCore + "language"),
            PdfAConformance = pdfAPart is null ? null : pdfAPart + XmpValue(xml, PdfAId + "conformance"),
            XmpUnreadable = xmp is not null && xml is null, HasXmp = xmp is not null, PdfUaIdPrefix = OtherPrefix(xml),
        };
    }

    // The prefix the pdfuaid properties are written with, as elements or as attributes of rdf:Description, when it
    // isn't the one PDF/UA names. LINQ to XML keeps the declarations, not the prefix each name was written with, so
    // this is the prefix in scope.
    private static string? OtherPrefix(XDocument? xml)
    {
        if (xml is null) return null;

        var elements = xml.Descendants().Where(element => element.Name.Namespace == PdfUaId);
        var owners = xml.Descendants().Where(element => element.Attributes().Any(a => a.Name.Namespace == PdfUaId));
        return elements.Concat(owners).Select(element => element.GetPrefixOfNamespace(PdfUaId) ?? "")
            .FirstOrDefault(prefix => prefix != "pdfuaid");
    }

    private static XDocument? ReadXmp(XmpMetadata xmp)
    {
        try
        {
            return UntrustedXml.Load(new MemoryStream(xmp.GetXmlBytes().ToArray()), preserveWhitespace: false);
        }
        catch (Exception problem) when (problem is XmlException or CappedFilterProvider.StreamTooLargeException)
        {
            return null;
        }
    }

    // An XMP property can be an element, a list of rdf:li items (joined with "; "), or an attribute of rdf:Description.
    private static string? XmpValue(XDocument? xml, XName name)
    {
        if (xml is null)
        {
            return null;
        }

        if (xml.Descendants(name).FirstOrDefault() is { } element)
        {
            var items = element.Descendants(Rdf + "li").Select(item => item.Value).ToList();
            return items.Count > 0 ? string.Join("; ", items) : element.Value;
        }

        return xml.Descendants().Attributes(name).FirstOrDefault()?.Value;
    }
}
