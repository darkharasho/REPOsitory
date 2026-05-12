# MuseumGambling

A R.E.P.O. mod that turns the cash-holding Museum Head into a slot machine. Clicking it deals 100 damage with a configurable chance to win 50,000 cash.

## Features

- Click the Museum Head to gamble: take 100 damage, roll for a 50,000 cash payout
- Configurable win chance

## Configuration

Config file: `BepInEx/config/darkharasho.MuseumGambling.cfg`

## Dependencies

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)

## Installation

Install via [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager) / r2modman, or manually place `MuseumGambling.dll` in `BepInEx/plugins/MuseumGambling/`.

## Building

```bash
GAME_DIR="/path/to/REPO" ./package.sh
```

Builds the DLL and produces a Thunderstore-ready zip. Install the zip via r2modman.
