using ForcedFriendship;
using Xunit;

namespace ForcedFriendship.Tests;

public class DamageCalculatorTests
{
    // --- Distance ---

    [Fact]
    public void Distance_is_euclidean_3d()
    {
        var a = new PlayerState(0f, 0f, 0f, alive: true);
        var b = new PlayerState(3f, 0f, 4f, alive: true);
        Assert.Equal(5f, DamageCalculator.Distance(a, b), precision: 4);
    }

    // --- Band: safeDistance=15, bandWidth=8 ---

    [Theory]
    [InlineData(14f, 0)]   // inside safe radius
    [InlineData(15f, 0)]   // exactly at safe radius -> still safe (<=)
    [InlineData(16f, 1)]   // just past -> band 1
    [InlineData(22.9f, 1)] // still band 1
    [InlineData(23f, 2)]   // exactly one bandWidth past safe -> band 2
    [InlineData(31f, 3)]   // (31-15)/8 = 2.0 -> floor 2 + 1 = band 3
    public void Band_maps_distance_to_band(float distance, int expectedBand)
    {
        Assert.Equal(expectedBand, DamageCalculator.Band(distance, safeDistance: 15f, bandWidth: 8f));
    }
}
