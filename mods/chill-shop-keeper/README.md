# ChillShopKeeper

A R.E.P.O. mod that prevents the in-shop ShopKeeper from punishing players for "ruckus" (gunfire, explosions, melee, attacking the shopkeeper, etc.). Configurable per-player or globally.

## Features

- `DisableGlobally` — kill-switch. When true, the ShopKeeper ignores ruckus from everyone, regardless of the in-shop toggle.
- Per-player exemptions — for each player ever observed in a lobby, a toggle keyed by their display name is added to the `[Immunity]` section of the config. Set to `true` to exempt that player from punishment. Entries are created automatically on first observation and persist across sessions.

`DisableGlobally` overrides per-player toggles.

## Configuration

Config file: `BepInEx/config/darkharasho.ChillShopKeeper.cfg`

Example after a couple of sessions:

```
[General]
DisableGlobally = false

[Immunity]
Alice = false
Bob = true
```

| Section | Key | Default | Description |
|---------|-----|---------|-------------|
| `[General]` | `DisableGlobally` | `false` | ShopKeeper ignores ruckus from everyone. |
| `[Immunity]` | `<DisplayName>` | `false` | When true, that player is exempt. Auto-added on first observation. If two players share a display name, they share one toggle. |

If two players in your lobby share a display name, they share one toggle. If a player renames, the next time they're observed a new entry is created with the new name; the old entry remains in the cfg but is no longer used.

Compatible with [REPOConfig](https://thunderstore.io/c/repo/p/nickklmao/REPOConfig/) — toggles can be flipped in-game.

## Multiplayer

The ShopKeeper's ruckus logic runs only on the host (master client), so only the host's `ChillShopKeeper` settings matter. Clients running the mod can toggle their own copies, but those settings have no effect when they are not the host.

## Dependencies

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)

## Installation

Install via [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager) / r2modman, or manually place `ChillShopKeeper.dll` in `BepInEx/plugins/ChillShopKeeper/`.

## Building

```bash
GAME_DIR="/path/to/REPO" ./package.sh
```

Builds the DLL and produces a Thunderstore-ready zip.
