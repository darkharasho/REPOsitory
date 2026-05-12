using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using ChillShopKeeper;
using Xunit;

namespace ChillShopKeeper.Tests;

public class PlayerRegistryTests : IDisposable
{
    private readonly List<string> _tempPaths = new();

    private string NewTempPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"chill-test-{Guid.NewGuid():N}.cfg");
        _tempPaths.Add(path);
        return path;
    }

    private ConfigFile NewTempConfig() => new ConfigFile(NewTempPath(), saveOnInit: true);

    public void Dispose()
    {
        foreach (var p in _tempPaths)
        {
            try { File.Delete(p); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void Observe_creates_entry_defaulting_to_false()
    {
        var cfg = NewTempConfig();
        var reg = new PlayerRegistry(cfg);

        reg.Observe("76561198000000001", "Alice");

        Assert.False(reg.IsExempt("76561198000000001"));
    }

    [Fact]
    public void Observe_is_idempotent_and_preserves_user_value()
    {
        var cfg = NewTempConfig();
        var reg = new PlayerRegistry(cfg);

        reg.Observe("76561198000000001", "Alice");
        reg.SetExempt("76561198000000001", true);
        reg.Observe("76561198000000001", "Alice");

        Assert.True(reg.IsExempt("76561198000000001"));
    }

    [Fact]
    public void IsExempt_returns_false_for_unknown_steamID()
    {
        var cfg = NewTempConfig();
        var reg = new PlayerRegistry(cfg);

        Assert.False(reg.IsExempt("76561198999999999"));
    }

    [Fact]
    public void Values_persist_across_ConfigFile_reload()
    {
        var path = NewTempPath();

        {
            var cfg = new ConfigFile(path, saveOnInit: true);
            var reg = new PlayerRegistry(cfg);
            reg.Observe("76561198000000001", "Alice");
            reg.SetExempt("76561198000000001", true);
            cfg.Save();
        }

        {
            var cfg = new ConfigFile(path, saveOnInit: true);
            var reg = new PlayerRegistry(cfg);
            reg.Observe("76561198000000001", "Alice");
            Assert.True(reg.IsExempt("76561198000000001"));
        }
    }

    [Fact]
    public void Observe_with_colliding_display_names_shares_one_entry()
    {
        var cfg = NewTempConfig();
        var reg = new PlayerRegistry(cfg);

        reg.Observe("76561198000000001", "Alice");
        reg.Observe("76561198000000002", "Alice");  // same display name, different steam id
        reg.SetExempt("76561198000000001", true);

        // BepInEx ConfigFile.Bind returns the existing entry for the same key.
        // Both steamIDs end up pointing at the same ConfigEntry, so toggling one toggles both.
        Assert.True(reg.IsExempt("76561198000000001"));
        Assert.True(reg.IsExempt("76561198000000002"));
    }

    [Fact]
    public void Observe_falls_back_to_steamID_key_for_blank_display_name()
    {
        var cfg = NewTempConfig();
        var reg = new PlayerRegistry(cfg);

        reg.Observe("76561198000000003", "   ");  // whitespace-only sanitizes to empty
        reg.SetExempt("76561198000000003", true);

        Assert.True(reg.IsExempt("76561198000000003"));
    }
}
