# MuseumGambling — Design

**Date:** 2026-05-11
**Status:** Approved for planning. Revised 2026-05-11 (v2) after Phase 0 research — see "Revision history" at the bottom.
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

Harmony **postfix** on `MuseumPropMoneyHead.StateSetRPC(byte _newState, PhotonMessageInfo _info)`. This RPC runs on all clients when the state machine transitions. Logic:

1. If `(State)_newState != State.Closed` → return (only the Closed transition is the gamble moment).
2. If `!Plugin.Enabled.Value` → return (kill-switch).
3. If not master client (`!SemiFunc.IsMasterClient()` or equivalent) → return (only master decides outcomes).
4. Compute `roll = UnityEngine.Random.Range(1, 101)`.
5. Call `Outcome.ShouldWin(roll, Plugin.WinChancePercent.Value)`.
6. Call `WinBroadcast.Send(__instance.photonView.ViewID, win)` — broadcasts the result to every client (including master).
7. If win → call `Payout.Spawn(__instance.transform.position, Plugin.PayoutValue.Value)`.
8. Wrap in try/catch — on exception, log and let vanilla continue.

### `Patches/HurtColliderPatches.cs`

Harmony **prefix** on `HurtCollider.PlayerHurt(PlayerAvatar _player)`. Runs on each client for their own player (vanilla `photonView.IsMine` guard remains intact). Logic:

1. Resolve the parent `MuseumPropMoneyHead`: `var head = __instance.GetComponentInParent<MuseumPropMoneyHead>()`.
2. If `head == null` → return `true` (this `HurtCollider` belongs to something else — leave it alone).
3. Look up `WinBroadcast.ConsumePendingResult(head.photonView.ViewID)`:
   - Returns `true` if there's a pending win for this view ID (and clears the entry).
   - Returns `false` otherwise (no entry, or entry was a loss).
4. If win → return `false` (suppress damage).
5. Else → return `true` (vanilla damage runs).
6. Wrap in try/catch — fail open.

### `WinBroadcast.cs`

Encapsulates the master→all-clients win-result broadcast using `PhotonNetwork.RaiseEvent`. Surface:

- `internal const byte EventCode = 199;` — chosen from Photon's reserved user range (0..199). Documented in code with a comment so future mod authors don't collide.
- `internal static void Register()` — called from `Plugin.Awake`. Subscribes to `PhotonNetwork.NetworkingClient.EventReceived`.
- `internal static void Send(int viewId, bool win)` — master only. Calls `PhotonNetwork.RaiseEvent(EventCode, payload, new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable)`. Payload is `object[] { viewId, win }`. The broadcast also lands on master's own subscriber via `ReceiverGroup.All`, so master stores the result the same way clients do — keeps the consumption path symmetric.
- `internal static bool ConsumePendingResult(int viewId)` — checks `_pending[viewId]`, removes the entry, returns its value (false if absent).
- Internal `Dictionary<int, bool> _pending` — keyed by `MuseumPropMoneyHead` view ID. Entries are written by the event handler and removed by `ConsumePendingResult` (one-shot). Stale entries (event broadcast but `PlayerHurt` never fires) are acceptable — the next `StateSetRPC→Closed` overwrites them. The dictionary is plain (not thread-safe) because all reads/writes happen on Unity's main thread.

Singleplayer (no Photon room): `PhotonNetwork.RaiseEvent` is a no-op when not in a room. To still suppress damage in singleplayer, `Send` also calls the local handler directly with the same payload before invoking `RaiseEvent`. This keeps singleplayer working through the same code path.

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

Host-authoritative roll + custom Photon event broadcast:

- Only master client rolls. The roll fires once per `StateSetRPC(Closed)` transition on master.
- Master broadcasts the result (per-Museum-Head-instance, identified by `PhotonView.ViewID`) to all clients via `PhotonNetwork.RaiseEvent(EventCode=199, ...)`.
- Each client (including master via `ReceiverGroup.All`) records the result in `WinBroadcast._pending`. The clicking player's local `HurtCollider.PlayerHurt` prefix consumes that entry and decides whether to suppress damage.
- Master also spawns the money-bag valuable. That spawn replicates to clients via the game's normal valuable network path (resolved in research — see `Payout.cs`).
- Clients with the mod installed but not master: their `StateSetRPC` postfix early-returns (master-only check), but their `HurtCollider.PlayerHurt` prefix and event handler are still live, so they correctly suppress damage when they receive a "win" event from master.
- Clients without the mod installed: see vanilla behavior. Their `PlayerHurt` is not patched so they take damage even if master rolled a win for them. **This is a known degraded-experience case** — for clean multiplayer the mod must be installed on every client. Acceptable per the chill-shop-keeper precedent (mods are not silently retro-fitted onto unmodded clients).
- Singleplayer (`!PhotonNetwork.InRoom`): master-client checks return true; `WinBroadcast.Send` calls its local handler directly (and the `RaiseEvent` call is harmlessly no-op'd). Everything else flows the same way.

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
6. **Client without master running mod:** master runs vanilla, client has mod. Expect: no wins ever (master never broadcasts a win event; client's `PlayerHurt` prefix never finds a pending entry → vanilla damage).
7. **Master runs mod, client doesn't:** master wins → master spawns money bag (replicates via valuable network path), but the clicking client (no mod) takes damage anyway. Known degraded-experience case.
8. **Both run mod, non-master is the clicker:** master rolls a win on `StateSetRPC(Closed)` → broadcasts via RaiseEvent → clicking client's `PlayerHurt` prefix consumes the entry → suppresses damage. Money bag spawns. ✅

## Research notes (resolved during Phase 0)

See `mods/museum-gambling/docs/superpowers/RESEARCH.md` for the resolved patch targets and remaining unknowns. Key resolved items:

1. **Patch targets** (v2):
   - `MuseumPropMoneyHead.StateSetRPC(byte _newState, PhotonMessageInfo _info)` — postfix, master-only roll + broadcast on Closed transition.
   - `HurtCollider.PlayerHurt(PlayerAvatar _player)` — prefix, each client suppresses its own damage when a pending win exists for the parent `MuseumPropMoneyHead`'s view ID.
2. **Money-bag spawn API.** Still unresolved. Task 0.3 of the plan identifies the valuable class, spawn entry point, and per-instance value setter.
3. **`PlayerAvatar.steamID`** field-ref pattern from `mods/chill-shop-keeper/src/Patches/ShopKeeperPatches.cs:10` is available if needed (not currently required by v2 — the prefix has the `PlayerAvatar` directly via `__args[0]`).

## Out of scope

- Any modification to the Museum Head's appearance, value display, suck-in animation, or grab behavior.
- Per-player cooldowns or rate limiting (damage is the natural cost).
- Per-player tracked credit / wallet (R.E.P.O. has no per-player wallet; money goes to team haul via the valuable).
- Visual feedback distinct from vanilla suck-in + the spawned money bag (no extra particles, sounds, UI).
- Photon room-property config sync (host-authoritative by enforcement; host config is the only one consulted).
- Backwards-compat with unmodded clients (test case 7 is a known degraded case, accepted).

## Revision history

**v1 (initial brainstorming):** Single Harmony prefix on a `MuseumPropMoneyHead` damage method; host-only roll suppresses damage and spawns money bag in one seam.

**v2 (after Phase 0 research, 2026-05-11):** Phase 0 found that damage is applied by a child `HurtCollider`'s `PlayerHurt` method, which runs on each client for their own player (not host-only), and `hurtCollider.SetActive(true)` is called by every client in their local `StateClosed` block (not master-gated). The single-seam design doesn't work for multiplayer. Replaced with:

- Postfix on `MuseumPropMoneyHead.StateSetRPC` (master-only roll on `Closed` transition, broadcasts via `PhotonNetwork.RaiseEvent`).
- Prefix on `HurtCollider.PlayerHurt` (consumes broadcast result and suppresses damage on win).
- New `WinBroadcast.cs` for the event encode/decode + per-view pending map.

User intent unchanged. Config surface unchanged.
