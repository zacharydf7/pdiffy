using System.Xml.Linq;
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

    public static DocumentInfo ReadDocumentInfo(PdfObjects objects, DictionaryToken catalog, int pageCount)
    {
        var markInfo = objects.Dictionary(catalog, "MarkInfo");
        var viewerPreferences = objects.Dictionary(catalog, "ViewerPreferences");
        var marked = markInfo is not null && objects.Boolean(markInfo, "Marked");
        var displayDocTitle = viewerPreferences is not null && objects.Boolean(viewerPreferences, "DisplayDocTitle");
        return new DocumentInfo(marked, objects.Text(catalog, "Lang"), displayDocTitle, pageCount);
    }

    public static Metadata ReadMetadata(PdfDocument document, PdfObjects objects)
    {
        var info = document.Information;
        var otherInfo = objects.Settings(info.DocumentInformationDictionary, StandardInfoKeys);

        XDocument? xml = document.TryGetXmpMetadata(out var xmp) ? xmp.GetXDocument() : null;
        var pdfUaPart = XmpValue(xml, PdfUaId + "part");
        var pdfAPart = XmpValue(xml, PdfAId + "part");
        var xmpTitle = xml?.Descendants(DublinCore + "title").Descendants().LastOrDefault()?.Value;
        return new Metadata(
            info.Title, info.Author, info.Subject, info.Keywords, info.CreationDate is not null, info.ModifiedDate is not null,
            info.Producer, info.Creator, pdfUaPart, xmpTitle)
        {
            OtherInfo = otherInfo,
            XmpCreator = XmpValue(xml, DublinCore + "creator"),
            XmpDescription = XmpValue(xml, DublinCore + "description"),
            XmpLanguage = XmpValue(xml, DublinCore + "language"),
            PdfAConformance = pdfAPart is null ? null : pdfAPart + XmpValue(xml, PdfAId + "conformance"),
        };
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
