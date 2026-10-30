using PDiffy;

namespace PDiffy.Tests.Unit;

internal static class Documents
{
    public static PdfUaDocument Empty() => new()
    {
        Document = new PdfDocumentInfo { Marked = true, Language = "en-US", DisplayDocTitle = true, PageCount = 1 },
        Metadata = new PdfMetadata
        {
            Title = "Title", Author = null, Subject = null, Keywords = null, HasCreatedDate = true,
            HasModifiedDate = true, Producer = "Producer 1.0", Creator = null, PdfUaPart = "1", XmpTitle = "Title",
        },
    };
}
