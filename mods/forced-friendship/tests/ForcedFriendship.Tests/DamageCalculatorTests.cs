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

    private static readonly Vec3[] NoCarts = System.Array.Empty<Vec3>();

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

    // --- Classify: safeDistance=15, warnPercent=0.25 -> warnStart=11.25 ---

    [Theory]
    [InlineData(0f, BeamZone.Safe)]
    [InlineData(11.24f, BeamZone.Safe)]
    [InlineData(11.25f, BeamZone.Warn)]   // exactly at warn edge
    [InlineData(15f, BeamZone.Warn)]      // exactly at safe radius -> still warn (not damaged yet)
    [InlineData(15.01f, BeamZone.Danger)] // past safe radius -> taking damage
    public void Classify_maps_distance_to_zone(float distance, BeamZone expected)
    {
        Assert.Equal(expected, DamageCalculator.Classify(distance, safeDistance: 15f, warnPercent: 0.25f));
    }

    [Fact]
    public void Classify_warnPercent_zero_has_no_yellow_zone()
    {
        Assert.Equal(BeamZone.Safe, DamageCalculator.Classify(14.9f, 15f, 0f));
        Assert.Equal(BeamZone.Danger, DamageCalculator.Classify(15.1f, 15f, 0f));
    }

    // --- ResolveAnchors ---

    [Fact]
    public void ResolveAnchors_buddy_picks_nearest_living_other()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(100f, 0f, 0f, alive: true),
            new PlayerState(31f, 0f, 0f, alive: true), // nearest to player 0
        };
        var anchors = DamageCalculator.ResolveAnchors(players, AnchorMode.Buddy, NoCarts);
        Assert.True(anchors[0].HasAnchor);
        Assert.Equal(31f, anchors[0].Distance, precision: 4);
        Assert.Equal(31f, anchors[0].X, precision: 4); // anchored on the nearest other's position
    }

    [Fact]
    public void ResolveAnchors_buddy_lone_living_player_has_no_anchor()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(5f, 0f, 0f, alive: false),
        };
        var anchors = DamageCalculator.ResolveAnchors(players, AnchorMode.Buddy, NoCarts);
        Assert.False(anchors[0].HasAnchor);
    }

    [Fact]
    public void ResolveAnchors_dead_self_has_no_anchor()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: false),
            new PlayerState(3f, 0f, 0f, alive: true),
        };
        var anchors = DamageCalculator.ResolveAnchors(players, AnchorMode.Buddy, NoCarts);
        Assert.False(anchors[0].HasAnchor);
    }

    [Fact]
    public void ResolveAnchors_cart_measures_every_living_player_to_cart()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(3f, 0f, 0f, alive: true),
        };
        var anchors = DamageCalculator.ResolveAnchors(players, AnchorMode.Cart, new[] { new Vec3(0f, 0f, 4f) });
        Assert.Equal(4f, anchors[0].Distance, precision: 4);
        Assert.Equal(5f, anchors[1].Distance, precision: 4); // (3,0,0)->(0,0,4) = 5
        Assert.Equal(4f, anchors[0].Z, precision: 4);        // anchored on the cart
    }

    [Fact]
    public void ResolveAnchors_cart_falls_back_to_buddy_when_no_cart()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(31f, 0f, 0f, alive: true),
        };
        var anchors = DamageCalculator.ResolveAnchors(players, AnchorMode.Cart, NoCarts);
        Assert.Equal(31f, anchors[0].Distance, precision: 4); // nearest other, not the (ignored) cart args
    }

    [Fact]
    public void ResolveAnchors_ignores_height_when_includeHeight_false()
    {
        // Two players directly above each other 30 apart vertically, same X/Z.
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(0f, 30f, 0f, alive: true),
        };
        var withHeight = DamageCalculator.ResolveAnchors(players, AnchorMode.Buddy, NoCarts, includeHeight: true);
        Assert.Equal(30f, withHeight[0].Distance, precision: 4);   // full vertical separation

        var flat = DamageCalculator.ResolveAnchors(players, AnchorMode.Buddy, NoCarts, includeHeight: false);
        Assert.Equal(0f, flat[0].Distance, precision: 4);          // same column -> distance 0
    }

    [Fact]
    public void ResolveAnchors_cart_uses_nearest_of_multiple_carts()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(50f, 0f, 0f, alive: true),
        };
        var carts = new[] { new Vec3(5f, 0f, 0f), new Vec3(48f, 0f, 0f) };
        var anchors = DamageCalculator.ResolveAnchors(players, AnchorMode.Cart, carts);
        Assert.Equal(5f, anchors[0].Distance, precision: 4);   // p0 -> first cart
        Assert.Equal(5f, anchors[0].X, precision: 4);
        Assert.Equal(2f, anchors[1].Distance, precision: 4);   // p1 -> second cart (nearest)
        Assert.Equal(48f, anchors[1].X, precision: 4);
    }

    // --- EvaluateDamage ---

    [Fact]
    public void EvaluateDamage_applies_band_formula_per_anchor()
    {
        var anchors = new[]
        {
            new AnchorResult(0f, 0f, 0f, distance: 31f, hasAnchor: true), // band 3 -> 15
            new AnchorResult(0f, 0f, 0f, distance: 0f, hasAnchor: false), // no anchor -> 0
        };
        var result = DamageCalculator.EvaluateDamage(anchors, Settings());
        Assert.Equal(new[] { 15, 0 }, result);
    }

    [Fact]
    public void EvaluateDamage_disabled_returns_all_zero()
    {
        var anchors = new[] { new AnchorResult(0f, 0f, 0f, 100f, true) };
        var result = DamageCalculator.EvaluateDamage(anchors, Settings(enabled: false));
        Assert.Equal(new[] { 0 }, result);
    }

    // --- Truck safe zone ---

    [Fact]
    public void ResolveAnchors_marks_in_truck_player_safe()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true, inTruck: true),
            new PlayerState(100f, 0f, 0f, alive: true),
        };
        var anchors = DamageCalculator.ResolveAnchors(players, AnchorMode.Buddy, NoCarts);
        Assert.True(anchors[0].Safe);      // in truck -> safe
        Assert.True(anchors[0].HasAnchor); // still anchored (beam can draw if the mode wants it)
        Assert.False(anchors[1].Safe);
    }

    [Fact]
    public void ResolveAnchors_buddy_ignores_in_truck_players_as_anchors()
    {
        // Explorer far out; their only other living buddy is parked in the truck.
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),                  // explorer
            new PlayerState(100f, 0f, 0f, alive: true, inTruck: true), // buddy in the truck
        };
        var anchors = DamageCalculator.ResolveAnchors(players, AnchorMode.Buddy, NoCarts);
        Assert.False(anchors[0].HasAnchor); // no eligible buddy -> no anchor -> no damage
    }

    [Fact]
    public void EvaluateDamage_in_truck_player_takes_no_damage_even_when_far()
    {
        var anchors = new[]
        {
            new AnchorResult(0f, 0f, 0f, distance: 100f, hasAnchor: true, safe: true),  // far but in truck
            new AnchorResult(0f, 0f, 0f, distance: 100f, hasAnchor: true, safe: false), // far -> bleeds
        };
        var result = DamageCalculator.EvaluateDamage(anchors, Settings());
        Assert.Equal(0, result[0]);
        Assert.True(result[1] > 0);
    }

    [Fact]
    public void ZoneForAnchor_safe_anchor_is_always_green()
    {
        var safeFar = new AnchorResult(0f, 0f, 0f, distance: 100f, hasAnchor: true, safe: true);
        Assert.Equal(BeamZone.Safe, DamageCalculator.ZoneForAnchor(safeFar, safeDistance: 15f, warnPercent: 0.25f));
    }

    [Fact]
    public void ZoneForAnchor_non_safe_anchor_uses_distance()
    {
        var danger = new AnchorResult(0f, 0f, 0f, distance: 100f, hasAnchor: true, safe: false);
        Assert.Equal(BeamZone.Danger, DamageCalculator.ZoneForAnchor(danger, safeDistance: 15f, warnPercent: 0.25f));
    }

    // --- Beam visibility: AlwaysShow toggles hide-when-safe ---

    [Theory]
    [InlineData(BeamZone.Safe, true, true)]    // always show -> safe beam drawn
    [InlineData(BeamZone.Safe, false, false)]  // hide-when-safe -> safe beam hidden
    [InlineData(BeamZone.Warn, false, true)]   // hide-when-safe still shows warn
    [InlineData(BeamZone.Danger, false, true)] // ...and danger
    public void ShouldDrawBeam_respects_alwaysShow(BeamZone zone, bool alwaysShow, bool expected)
    {
        Assert.Equal(expected, DamageCalculator.ShouldDrawBeam(zone, hasAnchor: true, alwaysShow));
    }

    [Fact]
    public void ShouldDrawBeam_no_anchor_never_draws()
    {
        Assert.False(DamageCalculator.ShouldDrawBeam(BeamZone.Danger, hasAnchor: false, alwaysShow: true));
    }
}
