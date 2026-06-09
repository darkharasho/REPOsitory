# ForcedFriendship

A R.E.P.O. mod that punishes wandering off alone. If you aren't within a configurable distance of another player, you start taking damage over time — and the farther away you are, the faster the damage ticks. Stick together.

## Configuration

Config file: `BepInEx/config/darkharasho.ForcedFriendship.cfg`

| Key | Default | Meaning |
|-----|---------|---------|
| `Enabled` | `true` | Master on/off switch |
| `SafeDistance` | `15` | Units within which the nearest living player keeps you safe |
| `BandWidth` | `8` | Units per additional damage band beyond the safe radius |
| `DamagePerBand` | `5` | HP per tick, multiplied by the band number |
| `TickInterval` | `2.0` | Seconds between damage evaluations |

Only the host's settings apply in multiplayer — the host computes distances and
applies damage to everyone. The mod is active during level gameplay only (not the
shop, truck, or lobby), and dead players are never damaged.

## Dependencies

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)

## Installation

Install via [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager) / r2modman, or manually place `ForcedFriendship.dll` in `BepInEx/plugins/ForcedFriendship/`.

## Building

```bash
GAME_DIR="/path/to/REPO" ./package.sh
```

Builds the DLL and produces a Thunderstore-ready zip. Install the zip via r2modman.
