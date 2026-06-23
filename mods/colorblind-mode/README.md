# ColorblindMode

A R.E.P.O. mod that adds an on/off colorblind mode, adjusting the game's colors for colorblind accessibility.

When enabled, the mod applies a full-screen color-correction filter so on-screen colors are easier to distinguish for colorblind players. Toggle it off to return to the game's default colors.

## Features

- Single on/off toggle for colorblind correction
- Applies globally to the whole screen (no per-object recoloring needed)
- Compatible with [REPOConfig](https://thunderstore.io/c/repo/p/nickklmao/REPOConfig/) — flip the toggle in-game via the config menu

## Configuration

Config file: `BepInEx/config/darkharasho.ColorblindMode.cfg`

| Key | Default | Description |
|-----|---------|-------------|
| `Enabled` | `false` | When true, the colorblind color correction is applied. When false, the game uses its default colors. |

## Dependencies

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)

## Installation

Install via [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager) / r2modman, or manually place `ColorblindMode.dll` in `BepInEx/plugins/ColorblindMode/`.

## Building

```bash
GAME_DIR="/path/to/REPO" ./package.sh
```

Builds the DLL and produces a Thunderstore-ready zip. Install the zip via r2modman.
