# ForcedFriendship

A R.E.P.O. mod that punishes wandering off alone. If you aren't within a configurable distance of another player, you start taking damage over time — and the farther away you are, the faster the damage ticks. Stick together.

## Configuration

Config file: `BepInEx/config/darkharasho.ForcedFriendship.cfg`

## Dependencies

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)

## Installation

Install via [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager) / r2modman, or manually place `ForcedFriendship.dll` in `BepInEx/plugins/ForcedFriendship/`.

## Building

```bash
GAME_DIR="/path/to/REPO" ./package.sh
```

Builds the DLL and produces a Thunderstore-ready zip. Install the zip via r2modman.
