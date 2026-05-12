# ChillShopKeeper

A R.E.P.O. mod that controls the shop guardian bot — the entity that punishes players for causing havoc in the shop. Configure it to exempt the host from punishment, or disable the bot's punishment globally for everyone.

## Features

- `ExemptHost` — when true, the guardian bot ignores the host but still punishes other players
- `DisableGlobally` — when true, the guardian bot punishes nobody, regardless of whether it's enabled in-shop

## Configuration

Config file: `BepInEx/config/darkharasho.ChillShopKeeper.cfg`

| Key | Default | Description |
|-----|---------|-------------|
| `ExemptHost` | `false` | When true, the shop guardian bot will not punish the host. |
| `DisableGlobally` | `false` | When true, the shop guardian bot will not punish anyone, no matter the in-shop toggle state. |

## Dependencies

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)

## Installation

Install via [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager) / r2modman, or manually place `ChillShopKeeper.dll` in `BepInEx/plugins/ChillShopKeeper/`.

## Building

```bash
GAME_DIR="/path/to/REPO" ./package.sh
```

Builds the DLL and produces a Thunderstore-ready zip. Install the zip via r2modman.
