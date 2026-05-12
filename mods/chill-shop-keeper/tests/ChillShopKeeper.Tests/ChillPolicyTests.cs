using ChillShopKeeper;
using Xunit;

namespace ChillShopKeeper.Tests;

public class ChillPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true,  true)]
    [InlineData(true,  false, true)]
    [InlineData(true,  true,  true)]
    public void ShouldSkip_truth_table(bool disableGlobally, bool playerExempt, bool expected)
    {
        Assert.Equal(expected, ChillPolicy.ShouldSkip(disableGlobally, playerExempt));
    }
}
