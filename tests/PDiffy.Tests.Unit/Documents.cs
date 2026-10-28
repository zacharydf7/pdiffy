using PDiffy;

namespace PDiffy.Tests.Unit;

internal static class Documents
{
    public static PdfUaDocument Empty() => new()
    {
        Document = new DocumentInfo(Marked: true, Lang: "en-US", DisplayDocTitle: true, PageCount: 1),
        Metadata = new Metadata("Title", null, null, null, true, true, "Producer 1.0", null, "1", "Title"),
    };
}
