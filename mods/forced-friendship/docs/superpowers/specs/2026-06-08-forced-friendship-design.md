# ForcedFriendship — Design Spec

**Date:** 2026-06-08
**Mod:** ForcedFriendship (`darkharasho.ForcedFriendship`)
**Target:** BepInEx 5 plugin for R.E.P.O., netstandard2.1, Harmony-based
**Scaffold:** `mods/forced-friendship/`

## Concept

A player takes damage over time whenever they are not within a configurable
distance of another player. The farther past the safe radius they are, the
larger each damage tick — damage ramps in discrete bands. Stick together or
bleed out.

## Mechanic — banded damage-over-time

The host runs a tick loop every `TickInterval` seconds. On each tick, for every
**living** player `P`:

1. Find the nearest **living other** player; let `d` be that distance. If no
   such player exists (solo play, or `P` is the last one alive), skip `P` — the
   mechanic cannot punish an impossible task.
2. If `d <= SafeDistance`, `P` is safe — no damage.
3. Otherwise compute the band and apply damage:
   - `band = floor((d - SafeDistance) / BandWidth) + 1`
   - `damage = band * DamagePerBand`
   - Apply via `P.playerHealth.Hurt(damage, savingGrace: false)`.

"Nearest player" semantics: a player is safe if **any** other living player is
within `SafeDistance`; otherwise the damage band is determined by distance to
the **closest** living player. Buddying up with anyone is enough.

Damage is applied host-side. R.E.P.O.'s `PlayerHealth.Hurt` is host-driven and
RPCs the damage to the owning client, so each affected player gets the standard
in-game damage feedback (vignette) for free. No custom UI in v0.1.

`savingGrace: false` so the mechanic *can* eventually down a stray player; the
small, ramped ticks give time to react before that happens.

## Configuration

Config file: `BepInEx/config/darkharasho.ForcedFriendship.cfg`

| Key | Default | Range | Meaning |
|-----|---------|-------|---------|
| `Enabled` | `true` | — | Master on/off switch |
| `SafeDistance` | `15` | `> 0` | Units within which the nearest living player keeps you safe |
| `BandWidth` | `8` | `> 0` | Units per additional damage band beyond the safe radius |
| `DamagePerBand` | `5` | `>= 1` | HP per tick, multiplied by the band number |
| `TickInterval` | `2.0` | `> 0` | Seconds between damage evaluations |

Defaults are first-guess values; they will be tuned in playtest since R.E.P.O.
world-unit scale is a feel decision. Example: at `SafeDistance=15`,
`BandWidth=8`, `DamagePerBand=5`, `TickInterval=2`:
- `d = 14` → safe.
- `d = 20` → band 1 → 5 HP every 2s (2.5 dps).
- `d = 31` → band 3 → 15 HP every 2s (7.5 dps).

## Multiplayer — host-authoritative

Reuse the established monorepo `SettingsSyncer` pattern (as in mini-eepo /
upgrade-limiter):

- The host owns all five settings and pushes them to Photon room properties on
  room join and on host migration, with `_lastPushed*` dedup to avoid redundant
  broadcasts.
- Clients pull host settings into `Active*` mirror fields and ignore their own
  local config while in a room. On leaving the room, clients reset to local
  config.
- Only the host computes distances and applies damage. Clients never run the
  damage tick loop.
- Solo play (not in a Photon room) uses local config directly via the same
  `Active*` mirrors.

## Activity gating

The tick loop only runs when **all** of:
- `Enabled` (active value) is true, **and**
- an `IsInGameplay()`-style check passes — level gameplay only, not the
  shop, truck, lobby, menus, or loading screens (where players naturally
  cluster anyway), **and**
- the local client is the Photon host (or not in a room → solo).

Dead / spectating players are excluded on **both** sides: they are not valid
safe anchors for other players, and they are never themselves damaged.

## Architecture & components

- **`DamageCalculator`** (pure, no Unity/Photon deps) — the tested core. Input:
  a snapshot of players (id, position, alive flag) plus the effective config.
  Output: a per-player damage amount for this tick. All band math, nearest-of-
  many selection, dead-player exclusion, solo/last-alive handling, and the
  disabled short-circuit live here.
- **MonoBehaviour tick driver** — owns the `TickInterval` accumulator, gathers
  the live player snapshot from `GameDirector.instance.PlayerList`, calls
  `DamageCalculator`, and applies results via `playerHealth.Hurt`. Thin glue.
- **`SettingsSyncer`** — Photon room-property push/pull, `_lastPushed` dedup,
  `Active*` mirrors, host/room state transitions. Mirrors the existing mods.
- **`Plugin`** — config binding, `Active*` field declarations, Harmony bootstrap,
  wiring `SettingChanged` handlers to the syncer.

This keeps all game/network coupling in thin shells around a fully unit-tested
calculator.

## Testing (TDD)

Unit tests target `DamageCalculator` only (no Unity runtime needed):

- Player inside `SafeDistance` of nearest → 0 damage.
- Player exactly at `SafeDistance` → 0 damage (boundary: `<=` is safe).
- Player just past `SafeDistance` → band 1.
- Multi-band boundaries: distances landing exactly on band edges produce the
  expected band number (verify the `floor(... ) + 1` math).
- Nearest-of-many: with several other players, the closest one determines safety
  and band.
- Dead anchor excluded: a nearby *dead* player does not make a player safe.
- Dead target excluded: a dead player receives 0 damage.
- Solo / last alive: a player with no living other player receives 0 damage.
- Disabled: `Enabled = false` → all players receive 0 damage.

The MonoBehaviour driver and `SettingsSyncer` stay thin enough to verify by
inspection and in-game playtest; the risky logic is all in the tested core.

## Out of scope (v0.1)

- Custom warning UI / proximity meters (rely on the built-in damage feedback).
- Per-player or role-based distance rules.
- Configurable damage curves beyond the banded model.
