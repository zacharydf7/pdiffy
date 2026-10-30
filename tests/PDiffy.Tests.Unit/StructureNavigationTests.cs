using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

public class StructureNavigationTests
{
    [Fact]
    public void Load_ShouldGiveTheTextOfTheElementAndItsChildrenInOrder_WhenAParagraphHoldsALink()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Read the").Tagged(1, "trail guide", "Link").Tagged(2, "before you go");
        pdf.Structure(new TestElement("P", 0, new TestElement("Link", 1), 2));

        var paragraph = Assert.Single(pdf.Load().Structure);

        Assert.Equal("Read the before you go", paragraph.Text);
        Assert.Equal("Read the trail guide before you go", paragraph.AllText);
    }

    [Theory]
    [InlineData("Link", "(Read the ) Tj", "(trail guide) Tj", "(.) Tj", "Read the trail guide.")]
    [InlineData("Span", "(Ridge) Tj", "(line) Tj", "( is open) Tj", "Ridgeline is open")]
    public void Load_ShouldJoinTheTextByPosition_WhenAChildsTextContinuesTheParents(string childType, string before,
        string inside, string after, string expected)
    {
        var pdf = new TestPdf();
        pdf.AddPage().Raw($"BT /F1 12 Tf 72 700 Td /P << /MCID 0 >> BDC {before} EMC "
            + $"/{childType} << /MCID 1 >> BDC {inside} EMC /P << /MCID 2 >> BDC {after} EMC ET");
        pdf.Structure(new TestElement("P", 0, new TestElement(childType, 1), 2));

        var paragraph = Assert.Single(pdf.Load().Structure);

        Assert.Equal(expected, paragraph.AllText);
    }

    [Fact]
    public void Elements_ShouldSuggestATypeTheDocumentUses_WhenTheTypeDiffersOnlyInCase()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Ridge", "Trail");
        pdf.Structure(new TestElement("Trail", 0));
        var document = pdf.Load();

        var error = Assert.Throws<ArgumentException>(() => document.Elements("trail"));

        Assert.Contains("Did you mean 'Trail'?", error.Message);
    }

    [Fact]
    public void Load_ShouldGiveACellItsParagraphsText_WhenTheCellHasNoTextOfItsOwn()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Ridgeline");
        pdf.Structure(new TestElement("Table", new TestElement("TR", new TestElement("TD", new TestElement("P", 0)))));

        var cell = Assert.Single(pdf.Load().Elements(StructureType.TableDataCell));

        Assert.Equal((null, "Ridgeline"), (cell.Text, cell.AllText));
    }

    [Fact]
    public void Load_ShouldGiveEachElementThePathACheckFindingShows_WhenSiblingsShareAType()
    {
        var pdf = new TestPdf();
        pdf.AddPage().TaggedImage(0).TaggedImage(1);
        pdf.Structure(new TestElement("Document", new TestElement("Figure", 0) { Entries = "/Alt (Map)" },
            new TestElement("Figure", 1)));
        var document = pdf.Load();

        var finding = Assert.Single(document.Check(PdfCheck.FigureAltText));

        Assert.Equal(["structure > Document", "structure > Document > Figure[1]", "structure > Document > Figure[2]"],
            document.AllElements().Select(element => element.Path));
        Assert.Equal(finding.Path, document.Elements(StructureType.Figure).Last().Path);
    }

    [Fact]
    public void Elements_ShouldGiveTheDescendantsOfAType_WhenAskedOnAnElement()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Trail").Tagged(1, "Ridgeline").Tagged(2, "Notes");
        pdf.Structure(new TestElement("Document",
            new TestElement("Table",
                new TestElement("TR", new TestElement("TD", 0)), new TestElement("TR", new TestElement("TD", 1))),
            new TestElement("TD", 2)));
        var table = Assert.Single(pdf.Load().Elements(StructureType.Table));

        var cells = table.Elements(StructureType.TableDataCell).Select(cell => cell.Text);

        Assert.Equal(["Trail", "Ridgeline"], cells);
        Assert.Equal(4, table.Descendants().Count());
    }

    [Fact]
    public void Headings_ShouldListEachHeadingWithItsLevel_WhenSomeAreRoleMapped()
    {
        var pdf = new TestPdf().RoleMap("Title", "H1");
        pdf.AddPage().Tagged(0, "Trail Guide").Tagged(1, "Trails").Tagged(2, "Notes").Tagged(3, "Ridgeline");
        pdf.Structure(
            new TestElement("Title", 0), new TestElement("H2", 1), new TestElement("H", 2), new TestElement("P", 3));

        var headings = pdf.Load().Headings();

        Assert.Equal([(1, "Trail Guide"), (2, "Trails"), ((int?)null, "Notes")],
            headings.Select(heading => (heading.Level, heading.Element.Text)));
    }

    [Theory]
    [InlineData("figure", "Did you mean 'Figure'?")]
    [InlineData("Chrat", "See StructureType for the standard types.")]
    public void Elements_ShouldThrowSayingWhatWasMeant_WhenTheTypeIsNotKnown(string type, string hint)
    {
        var document = OneParagraph(new TestPdf().RoleMap("Chart", "Figure"));

        var error = Assert.Throws<ArgumentException>(() => document.Elements(type));

        Assert.Contains(hint, error.Message);
    }

    [Theory]
    [InlineData("Figure")]
    [InlineData("Chart")]
    [InlineData("Note")]
    [InlineData("Aside")]
    public void Elements_ShouldGiveNothing_WhenAKnownTypeIsNotUsed(string type)
    {
        var document = OneParagraph(new TestPdf().RoleMap("Chart", "Figure"));

        var elements = document.Elements(type);

        Assert.Empty(elements);
    }

    [Fact]
    public void Elements_ShouldFindACustomType_WhenItIsUsedButNotRoleMapped()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Ridge", "Trail");
        pdf.Structure(new TestElement("Trail", 0));

        var trails = pdf.Load().Elements("Trail");

        Assert.Equal("Ridge", Assert.Single(trails).Text);
    }

    private static PdfUaDocument OneParagraph(TestPdf pdf)
    {
        pdf.AddPage().Tagged(0, "Trailhead");
        pdf.Structure(new TestElement("P", 0));
        return pdf.Load();
    }
}
