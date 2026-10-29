using System.Xml;
using System.Xml.Linq;

namespace PDiffy.Reading;

/// <summary>Loads XML from a file being read, such as a DOCX part or a PDF's XMP, which may not be trustworthy.</summary>
internal static class UntrustedXml
{
    /// <param name="preserveWhitespace">Keep the whitespace between elements, which Word's text needs and XMP doesn't.</param>
    /// <param name="maxDepth">How deep elements may nest, which is less than <see cref="ReadLimits.MaxDepth"/> for XML read inside other XML.</param>
    /// <exception cref="XmlException">The XML is malformed, has a DTD, or nests deeper than <paramref name="maxDepth"/>.</exception>
    public static XDocument Load(Stream stream, bool preserveWhitespace, int maxDepth = ReadLimits.MaxDepth)
    {
        // Neither Word nor XMP writers use a DTD, and refusing one rules out entity expansion ("billion laughs").
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = !preserveWhitespace,
        };
        using var reader = XmlReader.Create(stream, settings);
        var document = XDocument.Load(reader);
        if (document.Descendants().Any(element => element.Ancestors().Skip(maxDepth - 1).Any()))
        {
            throw new XmlException($"The XML nests elements more than {maxDepth} deep.");
        }

        return document;
    }
}
