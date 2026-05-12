using System.Collections.Concurrent;
using BepInEx.Configuration;

namespace ChillShopKeeper;

public sealed class PlayerRegistry
{
    private const string Section = "Immunity";
    private readonly ConfigFile _config;
    private readonly ConcurrentDictionary<string, ConfigEntry<bool>> _entries = new();

    public PlayerRegistry(ConfigFile config)
    {
        _config = config;
    }

    public void Observe(string steamID, string displayName)
    {
        if (string.IsNullOrEmpty(steamID)) return;

        // ConcurrentDictionary.GetOrAdd may invoke the factory more than once under contention;
        // BepInEx ConfigFile.Bind is idempotent on key, so a discarded double-Bind is harmless.
        _entries.GetOrAdd(steamID, sid =>
            _config.Bind(
                Section,
                ConfigKeyFor(sid, displayName),
                false,
                $"Steam ID {sid}. When true, this player is exempt from ShopKeeper punishment."));
    }

    public bool IsExempt(string steamID)
    {
        if (string.IsNullOrEmpty(steamID)) return false;
        return _entries.TryGetValue(steamID, out var entry) && entry.Value;
    }

    // Convenience setter for tests and external callers; not used by the patches internally.
    public void SetExempt(string steamID, bool value)
    {
        if (_entries.TryGetValue(steamID, out var entry))
            entry.Value = value;
    }

    private static string ConfigKeyFor(string steamID, string displayName)
    {
        var key = Sanitize(displayName);
        return string.IsNullOrEmpty(key) ? $"Player_{steamID}" : key;
    }

    private static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c == '=' || c == '\n' || c == '\r' || c == '[' || c == ']')
                chars[i] = '_';
        }
        return new string(chars).Trim();
    }
}
