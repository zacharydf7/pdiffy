namespace PDiffy.Tests.Unit;

public class PdfCheckSetTests
{
    // A release's set must keep the rules it shipped with, whatever later releases add.
    [Fact]
    public void Version0_3_ShouldHaveTheRulesItsReleaseShipped_WhenCalled()
    {
        var set = PdfCheckSet.Version0_3;

        Assert.Equal(
        [
            PdfCheck.DocumentLanguage, PdfCheck.DocumentTitle, PdfCheck.FigureAltText, PdfCheck.UntaggedContent,
            PdfCheck.LinkDescriptions, PdfCheck.FormFieldNames, PdfCheck.TableHeaders, PdfCheck.HeadingLevels,
            PdfCheck.FontsEmbedded, PdfCheck.FontsMapToUnicode, PdfCheck.NoActions, PdfCheck.NoEmbeddedFiles,
        ], set.Checks);
        Assert.Equal("0.3, 12 rules", set.ToString());
    }

    [Fact]
    public void Latest_ShouldHaveEveryRule_WhenCalled()
    {
        var set = PdfCheckSet.Latest;

        Assert.Equal(Enum.GetValues<PdfCheck>(), set.Checks);
        Assert.Equal("latest", set.Name);
    }

    [Fact]
    public void Only_ShouldKeepTheRulesInTheCategories_WhenGivenSome()
    {
        var set = PdfCheckSet.Latest.Only(PdfCheckCategory.Fonts, PdfCheckCategory.Language);

        Assert.Equal([PdfCheck.DocumentLanguage, PdfCheck.FontsEmbedded, PdfCheck.FontsMapToUnicode], set.Checks);
        Assert.Equal("latest", set.Name);
    }

    [Fact]
    public void Except_ShouldLeaveOutTheRules_WhenGivenSome()
    {
        var set = PdfCheckSet.Version0_3.Only(PdfCheckCategory.HouseRules).Except(PdfCheck.NoActions);

        Assert.Equal("0.3, 1 rule", set.ToString());
        Assert.Equal([PdfCheck.NoEmbeddedFiles], set.Checks);
    }
}
