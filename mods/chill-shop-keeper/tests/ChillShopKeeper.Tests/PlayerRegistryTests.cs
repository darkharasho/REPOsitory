using System.IO;
using BepInEx.Configuration;
using ChillShopKeeper;
using Xunit;

namespace ChillShopKeeper.Tests;

public class PlayerRegistryTests
{
    private static ConfigFile NewTempConfig()
    {
        var path = Path.Combine(Path.GetTempPath(), $"chill-test-{System.Guid.NewGuid():N}.cfg");
        return new ConfigFile(path, saveOnInit: true);
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
        var path = Path.Combine(Path.GetTempPath(), $"chill-test-{System.Guid.NewGuid():N}.cfg");

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
}
