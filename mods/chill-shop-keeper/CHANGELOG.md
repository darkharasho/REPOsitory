# Changelog

## 0.3.0
- New opt-in `FixCartCannonDetection` toggle (default off) that fixes a vanilla R.E.P.O. bug where Cart Cannon bullets aren't attributed to the shooter, causing the ShopKeeper to ignore that damage. Enable it if you want the ShopKeeper to punish Cart Cannon users.
- New opt-in `HostOnlyOnOffSwitch` toggle (default off) that blocks non-host players from pressing the ShopKeeper's on/off switch. Vanilla already silently drops non-host presses on the host; this just prevents the wasted local press for clarity.

## 0.2.1
- Updated Thunderstore description to reflect per-player exemptions.

## 0.2.0
- Per-player exemption keys now use the player's display name (e.g. `Alice = true`) under a `[Immunity]` section, instead of `Player_<SteamID> = true` under `[Players]`. Breaking change for any cfg from 0.1.0 — old entries are ignored; players will get fresh entries with their display name on next observation.

## 0.1.0
- Initial release.
- `DisableGlobally` toggle disables all ShopKeeper ruckus punishment.
- Per-player `Player_<SteamID>` exemptions, lazily created on first observation, persist across sessions.
- REPOConfig-compatible (plain BepInEx `ConfigEntry<bool>` entries).
