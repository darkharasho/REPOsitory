# ForcedFriendship

A R.E.P.O. mod that punishes wandering off alone. If you aren't within a configurable distance of another player, you start taking damage over time — and the farther away you are, the faster the damage ticks. Stick together. Choose whether your lifeline is the nearest player or the cart, and a colored beam shows you how safe you are at a glance.

## Configuration

Config file: `BepInEx/config/darkharasho.ForcedFriendship.cfg`

| Key | Section | Default | Meaning |
|-----|---------|---------|---------|
| `Enabled` | General | `true` | Master on/off switch |
| `AnchorMode` | General | `Buddy` | `Buddy` = stay near the nearest living player; `Cart` = stay near the main hauling cart |
| `SafeDistance` | General | `15` | Units within which your anchor keeps you safe |
| `BandWidth` | General | `8` | Units per additional damage band beyond the safe radius |
| `DamagePerBand` | General | `5` | HP per tick, multiplied by the band number |
| `TickInterval` | General | `2.0` | Seconds between damage evaluations |
| `Enabled` | Beams | `true` | Draw a tether beam from each player to their anchor |
| `ShowAllPlayers` | Beams | `true` | Show every player's beam; if false, only your own |
| `WarnPercent` | Beams | `0.25` | Outer fraction of `SafeDistance` where the beam turns yellow before red (0 disables yellow) |

Only the host's settings apply in multiplayer — the host computes distances and
applies damage to everyone. Beam display is local to each client, driven by that
client's `Beams/*` settings. The mod is active during level gameplay only (not the
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
