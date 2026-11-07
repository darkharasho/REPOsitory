# InfiniteEnergy

A R.E.P.O. mod that gives weapons and items infinite battery/energy — guns, drones, and other battery-powered items never run dry.

Uses the game's own built-in infinite-battery switch, so it covers every battery-powered item (guns, melee weapon durability, drones, tools), including the revolver's chambers. Battery bars stay full and no drain/charge sounds play when firing.

## Configuration

Config file: `BepInEx/config/darkharasho.InfiniteEnergy.cfg`

| Key | Default | Description |
|-----|---------|-------------|
| `Enabled` | `true` | Keep every item battery full. Can be toggled mid-run. |

## Multiplayer

Batteries are host-authoritative: the host needs the mod installed and enabled. Clients don't need it.

## Dependencies

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)

## Installation

Install via [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager) / r2modman, or manually place `InfiniteEnergy.dll` in `BepInEx/plugins/InfiniteEnergy/`.

## Building

```bash
GAME_DIR="/path/to/REPO" ./package.sh
```

Builds the DLL and produces a Thunderstore-ready zip.
