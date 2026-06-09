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

    // --- Evaluate ---
    // Settings used below: enabled, safeDistance=15, bandWidth=8, damagePerBand=5

    private static DamageSettings Settings(bool enabled = true) =>
        new DamageSettings(enabled, safeDistance: 15f, bandWidth: 8f, damagePerBand: 5);

    [Fact]
    public void Evaluate_player_within_safe_radius_takes_no_damage()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(10f, 0f, 0f, alive: true), // 10 units away -> safe
        };
        var result = DamageCalculator.Evaluate(players, Settings());
        Assert.Equal(new[] { 0, 0 }, result);
    }

    [Fact]
    public void Evaluate_uses_nearest_living_other_player()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(100f, 0f, 0f, alive: true), // far
            new PlayerState(31f, 0f, 0f, alive: true),  // nearest -> band 3 -> 15 dmg
        };
        var result = DamageCalculator.Evaluate(players, Settings());
        Assert.Equal(15, result[0]);
    }

    [Fact]
    public void Evaluate_ignores_dead_player_as_a_safe_anchor()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(5f, 0f, 0f, alive: false), // dead, nearby -> does NOT make safe
            new PlayerState(31f, 0f, 0f, alive: true), // nearest living -> band 3 -> 15 dmg
        };
        var result = DamageCalculator.Evaluate(players, Settings());
        Assert.Equal(15, result[0]);
    }

    [Fact]
    public void Evaluate_never_damages_a_dead_player()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: false),  // dead -> 0 regardless of distance
            new PlayerState(100f, 0f, 0f, alive: true),
        };
        var result = DamageCalculator.Evaluate(players, Settings());
        Assert.Equal(0, result[0]);
    }

    [Fact]
    public void Evaluate_lone_living_player_takes_no_damage()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),    // only living player
            new PlayerState(100f, 0f, 0f, alive: false), // everyone else dead
        };
        var result = DamageCalculator.Evaluate(players, Settings());
        Assert.Equal(0, result[0]);
    }

    [Fact]
    public void Evaluate_single_player_list_takes_no_damage()
    {
        var players = new[] { new PlayerState(0f, 0f, 0f, alive: true) };
        var result = DamageCalculator.Evaluate(players, Settings());
        Assert.Equal(new[] { 0 }, result);
    }

    [Fact]
    public void Evaluate_disabled_returns_all_zero()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(100f, 0f, 0f, alive: true), // would be heavily damaged if enabled
        };
        var result = DamageCalculator.Evaluate(players, Settings(enabled: false));
        Assert.Equal(new[] { 0, 0 }, result);
    }

    [Fact]
    public void Evaluate_returns_one_entry_per_player_with_symmetric_damage()
    {
        // Two living players 31 apart: each is the other's nearest -> both band 3 -> 15.
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(31f, 0f, 0f, alive: true),
        };
        var result = DamageCalculator.Evaluate(players, Settings());
        Assert.Equal(new[] { 15, 15 }, result);
    }
}
