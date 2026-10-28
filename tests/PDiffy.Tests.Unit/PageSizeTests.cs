using PDiffy;

namespace PDiffy.Tests.Unit;

public class PageSizeTests
{
    [Theory]
    [InlineData(612, 792, "letter")]
    [InlineData(612.9, 791.2, "letter")]
    [InlineData(792, 612, "letter-landscape")]
    [InlineData(612, 1008, "legal")]
    [InlineData(595.28, 841.89, "a4")]
    [InlineData(595, 842, "a4")]
    public void Describe_ShouldReturnNamedClass_WhenWithinOnePointOfKnownSize(double width, double height, string expected)
    {
        Assert.Equal(expected, PageSize.Describe(width, height));
    }

    [Theory]
    [InlineData(614, 792, "614x792")]
    [InlineData(300.5, 400.4, "301x400")]
    public void Describe_ShouldReturnRoundedDimensions_WhenNoKnownSizeMatches(double width, double height, string expected)
    {
        Assert.Equal(expected, PageSize.Describe(width, height));
    }
}
