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
                new StructureElement
                {
                    Type = "Document",
                    Children =
                    [
                        new StructureElement { Type = "Sect", Children = [new StructureElement { Type = "Figure", Alt = "first" }] },
                        new StructureElement { Type = "Chart", MappedType = "Figure", Alt = "second" },
                        new StructureElement { Type = "Figure", Alt = "third" },
                    ],
                },
            ],
        };

        var alts = document.Elements(StructureType.Figure).Select(f => f.Alt);

        Assert.Equal(["first", "second", "third"], alts);
    }
}
