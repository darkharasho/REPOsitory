# ForcedFriendship

A R.E.P.O. mod that punishes wandering off alone. If you aren't within a configurable distance of another player, you start taking damage over time — and the farther away you are, the faster the damage ticks. Stick together. Choose whether your lifeline is the nearest player or the nearest cart, and a colored tether beam shows you how safe you are at a glance.

## Configuration

Config file: `BepInEx/config/darkharasho.ForcedFriendship.cfg`. All numeric settings are whole numbers.

| Key | Section | Default | Meaning |
|-----|---------|---------|---------|
| `Enabled` | General | `true` | Master on/off switch |
| `AnchorMode` | General | `Buddy` | `Buddy` = stay near the nearest living player; `Cart` = stay near the nearest hauling cart |
| `SafeDistance` | General | `20` | Units within which your anchor keeps you safe (1–100) |
| `BandWidth` | General | `8` | Units per additional damage band beyond the safe radius (1–100) |
| `DamagePerBand` | General | `5` | HP per tick, multiplied by the band number (1–100) |
| `TickInterval` | General | `2` | Seconds between damage evaluations (1–30) |
| `Enabled` | Beams | `true` | Draw a tether beam from each player to their anchor |
| `ShowAllPlayers` | Beams | `true` | Show every player's beam; if false, only your own |
| `WarnPercent` | Beams | `25` | Outer % of `SafeDistance` where the beam turns yellow before red (0–100; 0 disables yellow) |
| `Width` | Beams | `2` | Tether thickness (1–20; 1 = thinnest, approximates the grab beam) |

The **gameplay rule** (`Enabled`, `AnchorMode`, `SafeDistance`, `BandWidth`, `DamagePerBand`,
`TickInterval`) is taken from the **host** and synced to everyone, so each client's beams match
the host-authoritative damage. **Beam display** prefs (`Beams/*`) are local to each client.

In `Buddy` mode a beam only appears once you're in the warn/danger zone — split into safe
subgroups (2-and-2) and the lines vanish. In `Cart` mode the tether to your nearest cart is
always shown (green when safe). Standing in the **extraction truck** is a safe zone: no damage,
and you count as safe — so your beam stays green in `Cart` mode and is hidden in `Buddy` mode.
The mod is active during level gameplay only (not the shop or lobby), and dead players are never damaged.

## Dependencies

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)

## Installation

Install via [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager) / r2modman, or manually place `ForcedFriendship.dll` in `BepInEx/plugins/ForcedFriendship/`.

## Building

```bash
GAME_DIR="/path/to/REPO" ./package.sh
```

Builds the DLL and produces a Thunderstore-ready zip. Install the zip via r2modman.
