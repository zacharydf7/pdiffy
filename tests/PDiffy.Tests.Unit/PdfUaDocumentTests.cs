using PDiffy;

namespace PDiffy.Tests.Unit;

public class PdfUaDocumentTests
{
    [Fact]
    public void Elements_ShouldReturnMatchesInReadingOrder_WhenTypeOrMappedTypeMatches()
    {
        var document = Documents.Empty() with
        {
            Structure =
            [
                new PdfStructureElement
                {
                    Type = "Document",
                    Children =
                    [
                        new PdfStructureElement { Type = "Sect", Children = [new PdfStructureElement { Type = "Figure", AltText = "first" }] },
                        new PdfStructureElement { Type = "Chart", MappedType = "Figure", AltText = "second" },
                        new PdfStructureElement { Type = "Figure", AltText = "third" },
                    ],
                },
            ],
        };

        var alts = document.Elements(StructureType.Figure).Select(f => f.AltText);

        Assert.Equal(["first", "second", "third"], alts);
    }
}
