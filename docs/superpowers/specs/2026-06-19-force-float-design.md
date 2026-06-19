# ForceFloat — Design

**Date:** 2026-06-19
**Status:** Approved for planning

## Summary

A client-side BepInEx mod for R.E.P.O. that keeps every player permanently under
the game's Zero Gravity Staff effect for the duration of each level. Players
ragdoll-float (tumble), have their gravity cancelled, and can steer through the
air — exactly like being hit by `ItemStaffZeroGravity`'s area effect, but always
on instead of from a projectile.

## Goals

- Every player in the lobby floats continuously while a level is active.
- Faithful to the staff effect: tumble ragdoll + anti-gravity + camera-directed
  drift + wing visuals.
- Always on — no activation input.
- Normal play in non-level phases (truck, shop, lobby, menus, splash, tutorial,
  arena).

## Non-Goals

- No toggle hotkey (always on by choice).
- No "gentle hover" / preserved-walking mode — authentic effect only.
- Not floating during the arena/endgame, truck, or shop (see Activation Gate).

## Game APIs Used (verified against current Assembly-CSharp)

All public, no Harmony patches required:

- `PlayerController.instance.AntiGravity(float timer)` — cancels gravity for the
  local player for `timer` seconds; re-applied each frame.
- `PlayerAvatar.tumble` (type `PlayerTumble`):
  - `TumbleRequest(bool isTumbling, bool playerInput)` — RPC auto-routes to the
    master client; guarded by `!MenuLevel()` and a state-change check internally.
  - `TumbleOverrideTime(float time)` — keeps tumble from auto-ending.
- `PlayerAvatar.UpgradeTumbleWingsVisualsActive(bool active = true)` — wing visuals.
- `PlayerAvatar.InputDirectionRaw`, `PlayerAvatar.localCamera.GetOverrideTransform()`,
  `PlayerAvatar.isLocal`, `PlayerAvatar.isTumbling` — for drift + per-player checks.
- `GameDirector.instance.PlayerList` (`List<PlayerAvatar>`) — enumerate all players.
- `SemiFunc.RunIsLevel()` — true only on regular levels (false for shop, lobby,
  menus, splash, tutorial, and arena); `SemiFunc.MenuLevel()`,
  `SemiFunc.IsMasterClientOrSingleplayer()`.
- `PlayerAvatar.instance` — the local avatar; `PlayerController.instance` — local
  controller.

These mirror what `SemiAffectZeroGravity` already does internally, so behavior
matches the staff.

## Architecture

Single plugin assembly, one driver component. No patches.

### `Plugin` (BepInEx `BaseUnityPlugin`)

- Reads config (see Config).
- On `Awake`, creates a persistent `FloatDriver` `GameObject`
  (`DontDestroyOnLoad`).

### `FloatDriver` (MonoBehaviour)

The whole behavior lives here.

**`Update()` — runs every frame:**

1. **Activation gate.** If `RunManager`/`GameDirector`/`PlayerAvatar.instance`
   aren't ready, or `!SemiFunc.RunIsLevel()`, or `SemiFunc.MenuLevel()`:
   - If we were previously active, release: `localAvatar.tumble.TumbleRequest(false, false)`
     and turn wings off. Then return (idle).
2. **Local player float** (this client's own avatar — same path the game uses for
   `isLocalPlayer`):
   - `PlayerController.instance.AntiGravity(0.1f)`.
   - If not already tumbling: `tumble.TumbleRequest(true, false)`.
   - `tumble.TumbleOverrideTime(0.5f)` to sustain it.
   - If `Wings` config on and visuals not active: `UpgradeTumbleWingsVisualsActive()`.
3. **Everyone else** (master client only — `SemiFunc.IsMasterClientOrSingleplayer()`):
   - For each `pa` in `GameDirector.instance.PlayerList` that is not the local
     avatar and `!pa.isTumbling`: `pa.tumble.TumbleRequest(true, false)` +
     `TumbleOverrideTime(0.5f)`. This tumbles even players who lack the mod (they
     ragdoll-float via physics; only modded clients additionally get AntiGravity
     + steering for their own avatar).

**`FixedUpdate()` — drift steering for the local player (only when active and
`EnableDrift`):**

- Mirror `SemiAffectZeroGravity.FixedUpdate`: read
  `localCamera.GetOverrideTransform()`, build
  `forward * InputDirectionRaw.z + right * InputDirectionRaw.x`, normalize if
  >1, and `rb.AddForce(dir * driftForce * rb.mass, ForceMode.Force)` with
  `driftForce = 8`. Also apply the gentle follow-rotation torque so the body
  faces movement, as the game does.
  - Needs the local avatar's `Rigidbody` (via its `PhysGrabObject`/`rb`).

### Why master-client tumbles others

`TumbleRequest` already RPCs to the master client. Having every modded client
drive only its own avatar covers all modded players. The master-client loop over
`PlayerList` is the fallback so unmodded players also float, satisfying
"everyone in the lobby."

## Config (BepInEx `Config`)

| Key | Type | Default | Effect |
|-----|------|---------|--------|
| `EnableDrift` | bool | `true` | Camera-directed steering. Off = pure ragdoll drift. |
| `Wings` | bool | `true` | Show the tumble wing visuals. |

No keybind — always on.

## Activation Gate Details

Floating is active **only** when `SemiFunc.RunIsLevel()` is true. By the game's
own definition that excludes: level-shop, lobby, lobby-menu, main menu, splash
screen, tutorial, and arena. Truck/shop/lobby therefore behave normally, and the
endgame arena is unaffected. When the gate flips from active→inactive, the driver
explicitly releases the local tumble and wings so players land normally.

## Error Handling / Safety

- All per-frame access null-guards `RunManager.instance`, `GameDirector.instance`,
  `PlayerAvatar.instance`, `PlayerController.instance`, and `localCamera` —
  these are null during load/scene transitions.
- No exceptions thrown into Unity's loop: wrap the per-frame body so a transient
  null can't spam the BepInEx log.
- Tumble release on gate-exit prevents players being stuck floating in the truck.

## Testing

- Manual in-game (primary): single-player and a 2-client lobby — confirm float
  starts on level entry, stops in truck/shop, and unmodded clients still float
  (master-client path).
- Unit-testable seams (mirror forced-friendship's `tests/`): pure helpers like
  the activation-gate decision and the drift-vector computation extracted as
  static functions taking inputs (camera transform, input vector) → output force,
  so they can be tested without Unity.

## Project Structure

Scaffold with the `/new` template (matches `mods/mini-eepo`, `mods/forced-friendship`):

```
mods/force-float/
  src/Plugin.cs          # BaseUnityPlugin + config + spawns driver
  src/FloatDriver.cs     # MonoBehaviour: gate + tumble + antigravity + drift
  ForceFloat.csproj
  manifest.json
  package.sh
  icon.png
  README.md  CHANGELOG.md
```

Build/package via the project's `build` skill → `builds/ForceFloat-<version>.zip`,
installed through r2modman.
