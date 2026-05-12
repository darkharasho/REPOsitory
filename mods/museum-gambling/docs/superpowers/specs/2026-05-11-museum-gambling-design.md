# MuseumGambling — Design

**Date:** 2026-05-11
**Status:** Approved for planning
**Scaffold:** `mods/museum-gambling/`

## One-liner

Gamble at the Museum Head — when a player triggers its suck-in, the host rolls; on a (configurable) win the damage is suppressed and a 50,000-value money bag spawns instead.

## Behavior

When a player triggers the Museum Head's suck-in interaction:

1. **Host-only roll.** Only the host (or singleplayer) decides outcomes. Clients defer to vanilla.
2. **Roll** a uniform integer `1..100`.
3. **Loss** (`roll > WinChancePercent`, ~95% by default): vanilla runs unchanged. The suck-in plays and the head deals its normal damage to the clicking player.
4. **Win** (`roll <= WinChancePercent`, ~5% by default): the suck-in still plays normally. At the damage step, the host suppresses the damage and spawns a money-bag valuable with value `PayoutValue` (default 50,000) at the head's position.

The mod does not modify the head's geometry, value display, animation, suck-in motion, or any client-side state. It is purely a host-side decision point that swaps the damage outcome for a valuable spawn.

## Architecture

Three files under `mods/museum-gambling/src/`:

### `Plugin.cs`

BepInEx entrypoint. Mirrors the shape of `mods/chill-shop-keeper/src/Plugin.cs`:

- Binds `BepInEx.Configuration` entries and exposes them as `internal static ConfigEntry<T>` fields:
  - `ConfigEntry<bool> Enabled`
  - `ConfigEntry<int> WinChancePercent`
  - `ConfigEntry<int> PayoutValue`
- Runs `new Harmony("darkharasho.MuseumGambling").PatchAll()`.
- Logs version on `Awake`.

### `Patches/MuseumHeadPatches.cs`

Harmony **prefix** on the `MuseumPropMoneyHead` method that applies damage to the clicking player at the end of the suck-in. (Exact method name resolved during implementation — see Research notes.)

Prefix logic:

1. If `!PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom` → return `true` (clients defer to host).
2. If `!Plugin.Enabled.Value` → return `true` (kill-switch).
3. Compute `roll = UnityEngine.Random.Range(1, 101)`.
4. Call `Outcome.ShouldWin(roll, Plugin.WinChancePercent.Value)`:
   - **false (loss):** return `true` — vanilla damage runs.
   - **true (win):** call `Payout.Spawn(head.transform.position, Plugin.PayoutValue.Value)` then return `false` — vanilla damage is skipped.
5. On exception → log, return `true` (fail open to vanilla).

### `Outcome.cs`

Pure helper, no Unity dependencies, fully unit-testable:

```csharp
internal static class Outcome
{
    internal static bool ShouldWin(int roll, int winChancePercent)
        => winChancePercent > 0 && roll <= winChancePercent;
}
```

Edge cases the function handles:
- `winChancePercent <= 0` → always loss (never wins, even on roll 1).
- `winChancePercent >= 100` → always win (roll is 1..100, all satisfy `roll <= 100`).

### `Payout.cs`

`Payout.Spawn(Vector3 position, int value)` instantiates the money-bag valuable prefab at `position` and sets its dollar value to `value`. Host-side only; the spawn must replicate to clients via the game's existing valuable-spawn pathway (likely `ValuableDirector` or equivalent — see Research notes).

## Multiplayer authority

Host-authoritative, matching the `chill-shop-keeper` precedent:

- The host's mod runs the prefix; the host's RNG is the only RNG that matters.
- The host's config values are the only ones that matter — no Photon room-property sync needed because there is no shared mutable state beyond "host won this click", which is already expressed by the spawn replicating to clients.
- Clients with the mod installed but not hosting: their prefix early-returns on the `IsMasterClient` check, so client-local config has no effect during multiplayer. In singleplayer (no Photon room, `InRoom == false`), the local player is effectively the host and the mod runs normally.
- Clients without the mod installed: see vanilla behavior, plus see the money bag appear when the host wins (because the spawn replicates through the game's normal valuable network path).

## Configuration

File: `BepInEx/config/darkharasho.MuseumGambling.cfg`

```ini
[General]
Enabled = true
WinChancePercent = 5
PayoutValue = 50000
```

| Key | Type | Default | Range | Notes |
|---|---|---|---|---|
| `Enabled` | bool | `true` | — | Global kill-switch. When false, vanilla is untouched. |
| `WinChancePercent` | int | `5` | `0..100` | 0 = never win. 100 = always win. Clamped at bind time via `AcceptableValueRange`. |
| `PayoutValue` | int | `50000` | `0..1_000_000` | Value stamped on the spawned money bag. Clamped at bind time. |

Config is read live (`ConfigEntry<T>.Value` on each roll) — no level reload required for changes to take effect.

## Testing

### Unit tests (host-free)

`Outcome.ShouldWin` is a pure function. Cases:

- `ShouldWin(1, 0)` → false
- `ShouldWin(100, 0)` → false
- `ShouldWin(1, 100)` → true
- `ShouldWin(100, 100)` → true
- `ShouldWin(5, 5)` → true
- `ShouldWin(6, 5)` → false
- `ShouldWin(50, -1)` → false (defensive)
- `ShouldWin(50, 101)` → true (defensive)

### Manual integration (hosted lobby)

1. **Always-win:** set `WinChancePercent = 100`, click head once. Expect: suck-in plays, no damage, one 50k money bag appears at head position.
2. **Never-win:** set `WinChancePercent = 0`, click head once. Expect: vanilla suck-in + 100 damage, no bag.
3. **Default 5%:** click head ~40 times (at full health between, or just observe). Expect: roughly 2 wins (95% CI is wide; this is a smoke check, not a statistical test).
4. **Kill-switch:** `Enabled = false`, click head. Expect: vanilla, no rolls, no bags.
5. **Custom payout:** `PayoutValue = 1`, win once. Expect: money bag worth $1 spawns.
6. **Client without host running mod:** host runs vanilla, client has mod. Expect: no wins ever (client prefix early-returns).
7. **Host runs mod, client doesn't:** host wins → client sees the money bag appear (replicates via game's valuable network path).

## Research notes (resolved during planning/implementation)

1. **Damage method to patch.** The exact `MuseumPropMoneyHead` method that runs at the damage moment of the suck-in. Decompile `Assembly-CSharp.dll` for `MuseumPropMoneyHead` and identify the call site that invokes `PlayerAvatar.PlayerHurt` (or equivalent). The patch must run *after* the suck-in animation has played but *before* damage is applied — likely the very call to the damage method, prefixed.
2. **Money-bag spawn API.** The R.E.P.O. valuable-spawn pathway used by host code. Candidates: `ValuableDirector`, `ValuableObject`, `LevelGenerator` spawn helpers. Need to confirm:
   - The prefab/asset reference for the money-bag valuable.
   - The method that registers the spawn with Photon so clients see it.
   - The field/property that sets the per-instance dollar value (`dollarValue`, `valueCurrent`, etc.).
3. **`PlayerAvatar.steamID`** field-ref pattern is already proven in `mods/chill-shop-keeper/src/Patches/ShopKeeperPatches.cs:10` if logging needs to identify the clicking player.

These are implementation tasks, not design decisions — they do not affect the architecture above.

## Out of scope

- Any modification to the Museum Head's appearance, value display, suck-in animation, or grab behavior.
- Per-player cooldowns or rate limiting (damage is the natural cost).
- Per-player tracked credit / wallet (R.E.P.O. has no per-player wallet; money goes to team haul via the valuable).
- Visual feedback distinct from vanilla suck-in + the spawned money bag (no extra particles, sounds, UI).
- Photon room-property config sync (host-authoritative by enforcement; host config is the only one consulted).
