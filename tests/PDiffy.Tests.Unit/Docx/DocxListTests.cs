namespace PDiffy.Tests.Unit.Docx;

public class DocxListTests
{
    // Definition 1 numbers "A." then "A.1" one level down; definition 2 is a bullet. Lists 1 and 2 share definition 1,
    // list 3 is the bullet, list 4 restarts definition 1 at "C", list 5 starts its second level at 5, list 6 replaces
    // its first level with roman numerals, and list 7's definition only links to the "Outline" list style (definition 8).
    private const string Numbering = """
        <w:abstractNum w:abstractNumId="1">
          <w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="upperLetter"/><w:lvlText w:val="%1."/><w:pPr><w:ind w:left="720" w:hanging="360"/></w:pPr></w:lvl>
          <w:lvl w:ilvl="1"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%1.%2"/></w:lvl>
        </w:abstractNum>
        <w:abstractNum w:abstractNumId="2">
          <w:lvl w:ilvl="0"><w:numFmt w:val="bullet"/><w:lvlText w:val="&#xF0B7;"/><w:rPr><w:rFonts w:ascii="Symbol"/></w:rPr></w:lvl>
        </w:abstractNum>
        <w:num w:numId="1"><w:abstractNumId w:val="1"/></w:num>
        <w:num w:numId="2"><w:abstractNumId w:val="1"/></w:num>
        <w:num w:numId="3"><w:abstractNumId w:val="2"/></w:num>
        <w:num w:numId="4"><w:abstractNumId w:val="1"/><w:lvlOverride w:ilvl="0"><w:startOverride w:val="3"/></w:lvlOverride></w:num>
        <w:num w:numId="5"><w:abstractNumId w:val="1"/><w:lvlOverride w:ilvl="1"><w:startOverride w:val="5"/></w:lvlOverride></w:num>
        <w:num w:numId="6"><w:abstractNumId w:val="1"/><w:lvlOverride w:ilvl="0">
          <w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="lowerRoman"/><w:lvlText w:val="(%1)"/></w:lvl></w:lvlOverride></w:num>
        <w:abstractNum w:abstractNumId="7"><w:numStyleLink w:val="Outline"/></w:abstractNum>
        <w:abstractNum w:abstractNumId="8"><w:styleLink w:val="Outline"/>
          <w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%1)"/></w:lvl></w:abstractNum>
        <w:num w:numId="7"><w:abstractNumId w:val="7"/></w:num>
        """;

    private const string Steps = """
        <w:style w:type="paragraph" w:styleId="Step"><w:pPr><w:numPr><w:ilvl w:val="1"/><w:numId w:val="1"/></w:numPr></w:pPr></w:style>
        <w:style w:type="paragraph" w:styleId="SubStep"><w:basedOn w:val="Step"/></w:style>
        """;

    [Fact]
    public void Load_ShouldCountLabelsLevelByLevel_WhenItemsGoDownAndBackUp()
    {
        var labels = Labels(Item(1, 0), Item(1, 1), Item(1, 1), Item(1, 0), Item(1, 1));

        Assert.Equal(["A.", "A.1", "A.2", "B.", "B.1"], labels);
    }

    [Fact]
    public void Load_ShouldKeepCounting_WhenTwoListsShareADefinition()
    {
        Assert.Equal(["A.", "B."], Labels(Item(1, 0), Item(2, 0)));
    }

    [Fact]
    public void Load_ShouldRestartTheCount_WhenAListOverridesWhereALevelStarts()
    {
        Assert.Equal(["A.", "C.", "D."], Labels(Item(1, 0), Item(4, 0), Item(4, 0)));
    }

    [Fact]
    public void ToSnapshot_ShouldShowTheListsUsedAndEachItemsLabel_WhenParagraphsAreListItems()
    {
        var snapshot = Docx(Item(3, 0) + Item(1, 0)).Load().ToSnapshot();

        Assert.Contains("""
            numbering
              list 1 definition=1
                level 0 format=upperLetter text="%1." start=1 indent=36 hanging=18
              list 3 definition=2
                level 0 format=bullet text="\uf0b7" start=1 font="Symbol"
            body
              P list=3:0 label="\uf0b7" "Item"
              P list=1:0 label="A." "Item"
            """.ReplaceLineEndings("\n"), snapshot);
    }

    [Fact]
    public void Load_ShouldPutAParagraphInItsStylesList_WhenAStyleItIsBasedOnSetsNumbering()
    {
        var docx = new TestDocx
        {
            Styles = Steps,
            Numbering = Numbering,
            Body = """<w:p><w:pPr><w:pStyle w:val="SubStep"/></w:pPr></w:p>""",
        };

        Assert.Equal(new DocxListItem(1, 1, "A.1"), Assert.Single(docx.Load().Paragraphs()).List);
    }

    [Fact]
    public void Load_ShouldLeaveTheParagraphOutOfItsStylesList_WhenItSetsListZero()
    {
        var docx = new TestDocx
        {
            Styles = Steps,
            Numbering = Numbering,
            Body = """<w:p><w:pPr><w:pStyle w:val="SubStep"/><w:numPr><w:numId w:val="0"/></w:numPr></w:pPr></w:p>""",
        };

        Assert.Null(Assert.Single(docx.Load().Paragraphs()).List);
    }

    [Fact]
    public void Load_ShouldRestartANestedLevelAtItsOverride_WhenItsParentMovesOn()
    {
        Assert.Equal(["A.", "A.5", "B.", "B.5"], Labels(Item(5, 0), Item(5, 1), Item(5, 0), Item(5, 1)));
    }

    [Fact]
    public void Load_ShouldKeepCountingTheSharedList_WhenARestartedListCountsOnItsOwn()
    {
        Assert.Equal(["A.", "C.", "B."], Labels(Item(1, 0), Item(4, 0), Item(1, 0)));
    }

    [Fact]
    public void Load_ShouldUseTheListsOwnLevel_WhenItReplacesOneOfItsDefinitionsLevels()
    {
        Assert.Equal(["(i)", "(ii)"], Labels(Item(6, 0), Item(6, 0)));
    }

    [Fact]
    public void Load_ShouldNumberFromTheStylesDefinition_WhenADefinitionOnlyLinksToAListStyle()
    {
        Assert.Equal(["1)", "2)"], Labels(Item(7, 0), Item(7, 0)));
    }

    [Fact]
    public void ToSnapshot_ShouldMarkTheLevelMissingAndGiveNoLabel_WhenTheDefinitionHasNoSuchLevel()
    {
        var snapshot = Docx(Item(3, 4)).Load().ToSnapshot();

        Assert.Contains("  list 3 definition=2\n    level 4 (missing)\n", snapshot);
        Assert.Contains("  P list=3:4 \"Item\"\n", snapshot);
    }

    [Theory]
    [InlineData("lowerRoman", 4, "iv")]
    [InlineData("upperRoman", 1994, "MCMXCIV")]
    [InlineData("lowerLetter", 27, "aa")]
    [InlineData("decimalZero", 7, "07")]
    [InlineData("none", 3, "")]
    public void Load_ShouldWriteTheNumberInTheLevelsFormat_WhenItIsNotPlainDigits(string format, int start, string expected)
    {
        var docx = new TestDocx
        {
            Numbering = $"""
                <w:abstractNum w:abstractNumId="9"><w:lvl w:ilvl="0"><w:start w:val="{start}"/><w:numFmt w:val="{format}"/><w:lvlText w:val="%1"/></w:lvl></w:abstractNum>
                <w:num w:numId="9"><w:abstractNumId w:val="9"/></w:num>
                """,
            Body = Item(9, 0),
        };

        Assert.Equal(expected, Assert.Single(docx.Load().Paragraphs()).List?.Label);
    }

    [Fact]
    public void Load_ShouldKeepTheItemWithoutALabel_WhenItsListIsNotDefined()
    {
        var paragraph = Assert.Single(Docx(Item(70, 0)).Load().Paragraphs());

        Assert.Equal(new DocxListItem(70, 0, null), paragraph.List);
    }

    [Fact]
    public void Load_ShouldNumberAParagraphBeforeTheItemsInItsTextBox_WhenBothAreInOneList()
    {
        var textBox = $"""<w:pict xmlns:v="urn:schemas-microsoft-com:vml"><v:shape><v:textbox><w:txbxContent>{Item(1, 0)}</w:txbxContent></v:textbox></v:shape></w:pict>""";
        var withTextBox = Item(1, 0).Replace("<w:t>Item</w:t>", "<w:t>Item</w:t>" + textBox);

        Assert.Equal(["A.", "B.", "C.", "D."], Labels(Item(1, 0), withTextBox, Item(1, 0)));
    }

    private static string Item(int list, int level) =>
        $"""<w:p><w:pPr><w:numPr><w:ilvl w:val="{level}"/><w:numId w:val="{list}"/></w:numPr></w:pPr><w:r><w:t>Item</w:t></w:r></w:p>""";

    private static TestDocx Docx(string body) => new() { Numbering = Numbering, Body = body };

    private static List<string?> Labels(params string[] items) =>
        Docx(string.Concat(items)).Load().Paragraphs().Select(paragraph => paragraph.List?.Label).ToList();
}
