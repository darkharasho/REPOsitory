using System.Collections.Concurrent;
using BepInEx.Configuration;

namespace ChillShopKeeper;

public sealed class PlayerRegistry
{
    private const string Section = "Players";
    private readonly ConfigFile _config;
    private readonly ConcurrentDictionary<string, ConfigEntry<bool>> _entries = new();

    public PlayerRegistry(ConfigFile config)
    {
        _config = config;
    }

    public void Observe(string steamID, string displayName)
    {
        if (string.IsNullOrEmpty(steamID)) return;

        _entries.GetOrAdd(steamID, sid =>
            _config.Bind(
                Section,
                $"Player_{sid}",
                false,
                $"Exempt {displayName} from ShopKeeper punishment"));
    }

    public bool IsExempt(string steamID)
    {
        if (string.IsNullOrEmpty(steamID)) return false;
        return _entries.TryGetValue(steamID, out var entry) && entry.Value;
    }

    // Test-only convenience. Safe to call in prod too; not used by the patches.
    public void SetExempt(string steamID, bool value)
    {
        if (_entries.TryGetValue(steamID, out var entry))
            entry.Value = value;
    }
}
