using PDiffy.Tests.Unit.Docx;
using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

public class ModelEqualityTests
{
    [Fact]
    public void Equals_ShouldBeTrue_WhenTheSamePdfIsLoadedTwice()
    {
        var pdf = new TestPdf { Title = "Trail Guide" }.Bookmark("Start", 1);
        pdf.AddPage().Tagged(0, "Trail Guide", "H1").Tagged(1, "Start at the lodge").Tagged(2, "Distance", "TD")
            .InLayer("Draft", page => page.UntaggedPath());
        var cell = new TestElement("TD", 2) { Entries = "/A << /O /Table /Headers [(h1)] >>" };
        pdf.Structure(new TestElement("H1", 0), new TestElement("P", new TestElement("Span", 1)),
            new TestElement("Table", new TestElement("TR", cell)));
        var bytes = pdf.Build();

        var (first, second) = (PdfUaDocument.Load(bytes), PdfUaDocument.Load(bytes));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_ShouldBeTrue_WhenTheSameDocxIsLoadedTwice()
    {
        var docx = new TestDocx
        {
            Body = "<w:p><w:r><w:t>Trail Guide</w:t></w:r></w:p>"
                + "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>Start</w:t></w:r></w:p></w:tc></w:tr></w:tbl>"
                + "<w:sectPr><w:headerReference w:type=\"default\" r:id=\"top\"/></w:sectPr>",
        }.WithHeader("top", "<w:p><w:r><w:t>Trail notes</w:t></w:r></w:p>");

        var (first, second) = (docx.Load(), docx.Load());

        Assert.Equal(first, second);
    }

    [Fact]
    public void Equals_ShouldCompareListsByTheirItems_WhenGivenDifferentKindsOfList()
    {
        var child = new PdfBookmark { Title = "Map", Page = 2, Children = [] };
        var fromList = new PdfBookmark { Title = "Start", Page = 1, Children = new List<PdfBookmark> { child } };
        var fromArray = new PdfBookmark { Title = "Start", Page = 1, Children = [child] };
        var otherChild = fromArray with { Children = [child with { Page = 3 }] };

        Assert.Equal(fromList, fromArray);
        Assert.NotEqual(fromArray, otherChild);
    }

    [Fact]
    public void ToString_ShouldListTheItems_WhenARecordHoldsAList()
    {
        var map = new PdfBookmark { Title = "Map", Page = 2, Children = [] };
        var bookmark = new PdfBookmark { Title = "Start", Page = 1, Children = [map] };

        Assert.Contains("Children = [PdfBookmark { Title = Map, Page = 2, Children = [], ", bookmark.ToString());
    }
}
