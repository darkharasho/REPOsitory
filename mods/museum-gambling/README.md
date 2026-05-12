# MuseumGambling

A R.E.P.O. mod that turns the Museum Head into a slot machine. Each time it sucks a player in, the host rolls — by default 95% of clicks deliver the vanilla 100 damage, but 5% of clicks suppress the damage and spawn a 50,000-value money bag at the head's feet instead.

## Features

- Host-authoritative roll on every Museum Head suck-in (one roll per `Closed` state transition).
- Configurable percent chance of winning (whole number, 0–100).
- Configurable payout value, stamped on a vanilla "surplus valuable small" money bag.
- Damage suppression replicates to the clicking player's machine via a custom Photon event so multiplayer rolls stay consistent.
- Kill-switch toggle to disable the mod without uninstalling.

## Configuration

Config file: `BepInEx/config/darkharasho.MuseumGambling.cfg`

```
[General]
Enabled = true
WinChancePercent = 5
PayoutValue = 50000
```

| Key | Default | Range | Description |
|-----|---------|-------|-------------|
| `Enabled` | `true` | — | When false, the Museum Head behaves vanilla (no rolling, no payouts). |
| `WinChancePercent` | `5` | `0–100` | Whole-number percent chance a click pays out instead of dealing damage. |
| `PayoutValue` | `50000` | `0–1,000,000` | Dollar value stamped on the spawned money bag. |

Config is read live — values take effect on the next click without needing a level reload. Compatible with [REPOConfig](https://thunderstore.io/c/repo/p/nickklmao/REPOConfig/) — knobs can be tweaked in-game.

## Multiplayer

Only the host rolls. The result is broadcast to every client via `PhotonNetwork.RaiseEvent`, so the player being sucked in correctly suppresses their local damage when the host's roll is a win. Clients running the mod with the host *not* running it will never see a win. Clients *without* the mod when the host has it will see the money bag spawn but still take damage — install the mod on every client for clean play.

## Dependencies

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)

## Installation

Install via [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager) / r2modman, or manually place `MuseumGambling.dll` in `BepInEx/plugins/MuseumGambling/`.

## Building

```bash
GAME_DIR="/path/to/REPO" ./package.sh
```

Builds the DLL and produces a Thunderstore-ready zip. Install the zip via r2modman.
