# Changelog

## 0.1.0
- Initial release.
- `DisableGlobally` toggle disables all ShopKeeper ruckus punishment.
- Per-player `Player_<SteamID>` exemptions, lazily created on first observation, persist across sessions.
- REPOConfig-compatible (plain BepInEx `ConfigEntry<bool>` entries).
