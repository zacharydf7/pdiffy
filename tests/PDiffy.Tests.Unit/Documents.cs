using PDiffy;

namespace PDiffy.Tests.Unit;

internal static class Documents
{
    public static PdfUaDocument Empty() => new()
    {
        Document = new PdfDocumentInfo(Marked: true, Language: "en-US", DisplayDocTitle: true, PageCount: 1),
        Metadata = new PdfMetadata("Title", null, null, null, true, true, "Producer 1.0", null, "1", "Title"),
    };
}
