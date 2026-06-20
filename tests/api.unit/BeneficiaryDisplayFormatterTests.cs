using MidiKaval.Api.Infrastructure.Cases;

namespace MidiKaval.Api.UnitTests;

public class BeneficiaryDisplayFormatterTests
{
    [Theory]
    [InlineData("Ravi Kumar", "R. K.")]
    [InlineData("Priya", "P.")]
    [InlineData("  a   b  c ", "A. B.")]
    [InlineData("", "—")]
    [InlineData("   ", "—")]
    public void ToInitials_FormatsExpected(string input, string expected)
    {
        Assert.Equal(expected, BeneficiaryDisplayFormatter.ToInitials(input));
    }
}
