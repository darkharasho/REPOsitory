# Changelog

## 0.2.0
- Per-player exemption keys now use the player's display name (e.g. `Alice = true`) under a `[Immunity]` section, instead of `Player_<SteamID> = true` under `[Players]`. Breaking change for any cfg from 0.1.0 — old entries are ignored; players will get fresh entries with their display name on next observation.

## 0.1.0
- Initial release.
- `DisableGlobally` toggle disables all ShopKeeper ruckus punishment.
- Per-player `Player_<SteamID>` exemptions, lazily created on first observation, persist across sessions.
- REPOConfig-compatible (plain BepInEx `ConfigEntry<bool>` entries).
