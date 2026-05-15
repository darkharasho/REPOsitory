# ChillShopKeeper: Host-Only ShopKeeper On/Off Switch

**Mod:** `mods/chill-shop-keeper`
**Target version:** `0.3.0`
**Date:** 2026-05-14

## Problem

The ShopKeeper in R.E.P.O. has a physical on/off switch on its body (`ShopKeeper.OnOffSwitch`) that any player can press. In multiplayer the network side is already host-authoritative — the underlying `WakeUpRPC` / `SleepRPC` are gated by `SemiFunc.MasterOnlyRPC`, so non-host presses are silently ignored on the host. But the local press still runs `ShopKeeper.WakeUpOrSleepLogic()` on the client (consuming the 0.5s `onOffCooldown`, sending an RPC the host will drop). Hosts who want to manage shop quiet/loud state themselves currently have no way to keep teammates from spamming the switch.

## Goal

Add an opt-in BepInEx config toggle that blocks non-host players from initiating a press of the ShopKeeper on/off switch.

## Non-goals

- In-game UI/HUD indicator when a press is blocked.
- Per-player allowlist for who can press the switch (host vs. not is the only axis).
- Network-side enforcement beyond what vanilla already provides (vanilla already drops the RPC).
- Changing the existing `DisableGlobally` / per-player Immunity behavior.

## Design

### Config

One new entry in `Plugin.cs`, in the existing `[General]` section:

```csharp
HostOnlyOnOffSwitch = Config.Bind(
    "General",
    "HostOnlyOnOffSwitch",
    false,
    "When true, only the host (Photon master client) can press the ShopKeeper's on/off switch. Non-host players' presses are blocked locally before any animation, cooldown, or RPC. Default false preserves vanilla button feel.");
```

Default: `false` (opt-in; preserves vanilla feel for clients).

### Harmony patch

New file: `mods/chill-shop-keeper/src/Patches/OnOffSwitchHostOnlyPatches.cs`.

Prefix patch on `ShopKeeper.WakeUpOrSleepLogic()`:

```
1. If !Plugin.HostOnlyOnOffSwitch.Value → return true (run vanilla).
2. If !SemiFunc.IsMultiplayer()           → return true (singleplayer = you are host).
3. If !PhotonNetwork.IsMasterClient       → return false (skip vanilla; no cooldown, no RPC).
4. Otherwise                              → return true (host runs vanilla).
```

Wrap the body in try/catch and `return true` on exception (fail-open, consistent with `ShopKeeper_AddRuckusScore_Prefix`).

### Why prefix `WakeUpOrSleepLogic` and not the RPCs

- It's the single funnel for both wake and sleep paths.
- Skipping it on non-hosts prevents the local `onOffCooldown = 0.5f` write and the RPC send — clean no-op.
- The RPCs themselves are already host-authoritative via `MasterOnlyRPC`; patching them adds nothing.

### Interaction with existing features

- Independent of `DisableGlobally` and per-player Immunity. Those control whether ruckus *scores* the ShopKeeper. This toggle controls whether non-hosts can press the on/off switch. They compose freely.
- No change to `FixCartCannonDetection` or any other patch.

### Files changed

- `mods/chill-shop-keeper/src/Plugin.cs` — add `ConfigEntry<bool> HostOnlyOnOffSwitch` binding.
- `mods/chill-shop-keeper/src/Patches/OnOffSwitchHostOnlyPatches.cs` — new Harmony prefix.
- `mods/chill-shop-keeper/manifest.json` — bump `version_number` to `0.3.0`.
- `mods/chill-shop-keeper/CHANGELOG.md` — `## 0.3.0` entry.

### Versioning

`0.3.0` — minor bump for a new user-facing feature flag.

## Testing

This is a BepInEx mod that compiles to a DLL; verification is manual via r2modman:

1. **Compiles cleanly** — `package.sh` produces `builds/ChillShopKeeper-0.3.0.zip`.
2. **Config appears** — after first launch with the new build, `BepInEx/config/<plugin>.cfg` has `HostOnlyOnOffSwitch = false` under `[General]`.
3. **Default (false) — vanilla behavior:** host and clients can press the switch (clients' presses are inert on host per vanilla, but the local press triggers normally).
4. **Enabled (true), host:** host can press and toggle the ShopKeeper as normal.
5. **Enabled (true), non-host client:** pressing the switch produces no local press feedback (no cooldown consumed, no RPC sent). Host's switch state remains unchanged.
6. **Singleplayer + enabled (true):** local player (acting as host) can still press normally.

No automated tests — the project has none and adding a Unity test rig is out of scope.

## Risks

- **Mistaken Photon master check.** If `PhotonNetwork.IsMasterClient` returns true for a non-host in some edge state (e.g., during host migration), a non-host could briefly act. Acceptable: matches how the rest of the codebase identifies host.
- **Harmony target signature.** `WakeUpOrSleepLogic` is `public void` with no args — stable target. If a future game patch renames it, the prefix will silently no-op (Harmony logs a warning but doesn't crash) and vanilla behavior resumes; the try/catch fail-open further protects.
