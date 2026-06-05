# CondensedPlayerList

A R.E.P.O. mod that condenses the lobby and ESC-menu player list into tighter single-column rows, so large player groups fit on screen without the list running off the edge.

Inspired by the condensed view of [MorePlayerList](https://thunderstore.io/c/repo/p/yazirushi/MorePlayerList/).

## Features

- Tighter single-column row spacing in the lobby and ESC/pause menu
- Keeps the list on screen for full lobbies
- Purely client-side and visual — no config needed

## Dependencies

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)

## Installation

Install via [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager) / r2modman, or manually place `CondensedPlayerList.dll` in `BepInEx/plugins/CondensedPlayerList/`.

## Building

```bash
GAME_DIR="/path/to/REPO" ./package.sh
```

Builds the DLL and produces a Thunderstore-ready zip. Install the zip via r2modman.
