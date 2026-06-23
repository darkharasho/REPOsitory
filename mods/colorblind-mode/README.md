# ColorblindMode

A R.E.P.O. mod that applies color-blind correction, adjusting the game's colors for colorblind accessibility.

The mod applies a full-screen daltonization filter that corrects colors based on the selected colorblindness type, with adjustable intensity. Choose from Deuteranopia, Protanopia, or Tritanopia correction, or turn it off to restore the game's default colors.

## Features

- Per-type colorblind correction (Deuteranopia, Protanopia, Tritanopia) with adjustable intensity
- Applies globally to the whole screen via Post Processing (no per-object recoloring needed)
- Client-side; compatible with [REPOConfig](https://thunderstore.io/c/repo/p/nickklmao/REPOConfig/) — select correction type and intensity in-game via the config menu

## Configuration

Config file: `BepInEx/config/darkharasho.ColorblindMode.cfg`

| Key | Default | Description |
|-----|---------|-------------|
| `Type` | `Off` | Colorblindness type to correct for: `Off`, `Deuteranopia`, `Protanopia`, `Tritanopia`. `Off` restores the game's default colors. |
| `Intensity` | `1.0` | Strength of the correction (`0.0`–`1.0`). `0` = no change, `1` = full correction. |

## Dependencies

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)

## Installation

Install via [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager) / r2modman, or manually place `ColorblindMode.dll` in `BepInEx/plugins/ColorblindMode/`.

## Building

```bash
GAME_DIR="/path/to/REPO" ./package.sh
```

Builds the DLL and produces a Thunderstore-ready zip. Install the zip via r2modman.
