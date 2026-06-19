# ForcedFriendship 0.2.0 — Cart Anchor Mode + Tether Beams

Date: 2026-06-18

## Summary

Two additions to the ForcedFriendship mod:

1. **Anchor mode** — a setting to switch the point of distance measurement between
   the existing **Buddy** rule (nearest living player) and a new **Cart** rule (the
   main hauling cart).
2. **Tether beams** — a client-side visual beam from each player to their anchor,
   colored green / yellow / red to communicate safety at a glance.

The authoritative damage model (host-only, banded DoT) is unchanged. Beams are
purely cosmetic and computed locally on every client.

## Background

Current behavior (0.1.0): on the Photon host, every `TickInterval` seconds each
living player is damaged based on the band of the distance to its **nearest living
other player** (`DamageCalculator.Evaluate`). `BandWidth` units past `SafeDistance`
is one more band; HP lost = `band * DamagePerBand`.

REPO's cart is `PhysGrabCart` (`MonoBehaviour`). A level can contain a small cart
and the main cart; `isSmallCart` distinguishes them. There is no static singleton
accessor, so the main cart is found via `Object.FindObjectsOfType<PhysGrabCart>()`
filtered to `!isSmallCart`.

## Decisions (resolved)

- **Beam visibility:** show **everyone's** beams (all living players), not just own.
- **Cart fallback:** when Cart mode is selected but no main cart exists in the level
  yet, **fall back to the Buddy rule** for that tick.
- **Yellow warning zone:** the last **25%** of the safe radius before damage begins,
  expressed as a configurable percentage (`WarnPercent`, default `0.25`).

## Configuration

New entries in `Plugin.cs`:

| Section | Key | Type | Default | Notes |
|---|---|---|---|---|
| General | `AnchorMode` | enum `Buddy` \| `Cart` | `Buddy` | Point of distance measurement. |
| Beams | `Enabled` | bool | `true` | Master toggle for beam rendering. |
| Beams | `ShowAllPlayers` | bool | `true` | If false, render only the local player's beam. |
| Beams | `WarnPercent` | float (0–1) | `0.25` | Fraction of `SafeDistance` that is the yellow zone. Range `[0,1)`. |

Existing entries (`Enabled`, `SafeDistance`, `BandWidth`, `DamagePerBand`,
`TickInterval`) are unchanged.

## Architecture

Three units, each independently understandable and (where pure) testable.

### 1. `DamageCalculator` (pure, unchanged math + shared classification)

- Banded damage math (`Distance`, `Band`, `Evaluate`) is unchanged.
- Add a small pure helper so beams and damage share one source of truth for the
  green/yellow/red decision:

  ```
  enum BeamZone { Safe, Warn, Danger }   // green, yellow, red

  static BeamZone Classify(float distance, float safeDistance, float warnPercent)
  ```

  - `distance > safeDistance`            → `Danger` (taking damage; band ≥ 1)
  - `distance >= safeDistance*(1-warnPercent)` (and ≤ safeDistance) → `Warn`
  - otherwise                            → `Safe`

  `warnPercent` is clamped to `[0,1)`; `warnPercent <= 0` means no yellow zone.

- This keeps the unit Unity-free. The driver and beam renderer both consume it.

### 2. Anchor resolution

The "which anchor does player *i* measure against, and how far is it" logic differs
by mode:

- **Buddy:** nearest living *other* player (existing `Evaluate` behavior).
- **Cart:** the main cart's position, for every player. If no main cart is present,
  use the Buddy rule for that tick.

Implementation: the host-side driver resolves, per tick, an anchor **position** per
player and the corresponding **distance**. Buddy mode reuses the existing
nearest-other computation. Cart mode finds the main cart once per tick and measures
every living player to it; if the cart is null, it falls through to the Buddy
computation. The resulting per-player distances feed the unchanged band → damage
formula.

To avoid duplicating the nearest-other logic between damage and beams, expose a
small reusable function that, given the player list + mode + cart position, returns
each living player's `(anchorPosition, distance)`. Both `ForcedFriendshipDriver`
(host damage) and `BeamRenderer` (all clients) call it.

### 3. `ForcedFriendshipDriver` (host only) — unchanged loop, mode branch

- Same gate (master client, in gameplay, tick accumulator).
- Before evaluating, resolve the main cart (Cart mode only) and compute per-player
  anchor distances via the shared function.
- Feed distances into the band/damage formula and apply via
  `playerHealth.HurtOther(dmg, Vector3.zero, savingGrace: false)` as today.

### 4. `BeamRenderer` (every client) — new `MonoBehaviour`

- Added alongside the driver in `Plugin.Awake` (runs on all clients, not just host).
- Each frame (cheap; throttle position refresh is optional), when
  `Beams.Enabled` and `IsInGameplay()`:
  - Build the same per-player `(anchor, distance)` map via the shared function,
    using the same config values.
  - For each player to render (all living players if `ShowAllPlayers`, else only the
    local player): ensure a pooled `LineRenderer` exists, set its two endpoints
    (player chest height → anchor), and color it from
    `DamageCalculator.Classify(...)`:
    - `Safe` → green, `Warn` → yellow, `Danger` → red.
  - Players with no valid anchor (e.g., Buddy mode, no other living player) get no
    beam (LineRenderer disabled).
- LineRenderers are pooled/keyed by player and cleaned up when players leave or when
  beams are disabled / out of gameplay.
- Uses a simple unlit colored material (`Shader.Find("Sprites/Default")` or
  `"Hidden/Internal-Colored"`), small width (~0.05u), so it reads as a thin tether.

## Data flow

```
config ─┐
        ├─> shared anchor-resolution fn ──> per-player (anchor pos, distance)
players ┘            │                               │
                     ├──(host, per tick)──> Band → damage → HurtOther
                     └──(all clients, per frame)──> Classify → LineRenderer color
```

## Error handling / edge cases

- No main cart in Cart mode → Buddy fallback (per tick / per frame).
- Dead or disabled players: not damaged, not used as buddy anchors, no beam.
- Single living player in Buddy mode: no anchor → no damage, no beam.
- `SafeDistance` very small / `WarnPercent` 0 → no yellow zone (green jumps to red).
- Beams must never affect gameplay state or run host-authoritative logic.

## Testing

- **Unit (pure, no Unity):** extend the existing calculator tests —
  - `Classify` boundaries: exactly at `safeDistance` (Warn), just past (Danger),
    at the `1-warnPercent` edge (Warn), inside it (Safe), `warnPercent<=0` (no Warn).
  - Cart-mode distance selection: every player measures to the cart position; null
    cart falls back to nearest-other.
- **Manual in-game:** Buddy mode unchanged; Cart mode bleeds when away from cart;
  beams appear for all players, colors transition green→yellow→red as you walk out,
  toggles respect `Beams.Enabled` / `ShowAllPlayers`.

## Out of scope

- Beam styling beyond a thin colored line (no gradients, labels, particles).
- Per-player or per-mode separate distances/bands.
- Networking beam state (each client computes its own; cosmetic only).

## Versioning

- `manifest.json` + `PluginInfo` version → `0.2.0`.
- CHANGELOG entry; README config table updated with the new keys.
