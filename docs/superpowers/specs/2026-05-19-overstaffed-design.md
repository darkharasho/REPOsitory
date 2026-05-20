# Overstaffed — Design Spec

**Date:** 2026-05-19
**Author:** darkharasho
**Status:** Approved, pending implementation plan

## Problem

In R.E.P.O. 0.4.0, the existing `Spindles-MorePlayersImproved` mod no longer functions. Its Harmony patches target `NetworkConnect.TryJoiningRoom` (now a private instance method with a changed signature) and `SteamManager.HostLobby` (now takes a `bool _open` parameter). Both patches silently fail to apply, so the player cap stays at the vanilla 6.

The user wants a working replacement that raises the max player count above 6 on 0.4.0.

## Goals

- Allow hosting and joining R.E.P.O. lobbies with more than 6 players (up to 20).
- Be configurable via BepInEx config.
- Cover private and public/random matchmaking flows.

## Non-Goals

- No UI/HUD changes.
- No "[Modded]" lobby-name prefix (the original mod's public-lobby tag).
- No changes to room-name, password, or region flow.
- No mod-version compatibility shim for the old `Spindles-MorePlayersImproved` config.

## Background: what 0.4.0 changed

The 0.4.0 game code now has first-class fields for player capacity:

- `GameManager.maxPlayersDefault = 6` (const)
- `GameManager.maxPlayers = 6` (mutable instance field; used everywhere lobby/room sizing happens)
- `GameManager.maxPlayersPhoton = 20` (mutable instance field; upper clamp)
- `GameManager.SetMaxPlayers(int _target)` — clamps `_target` to `[1, maxPlayersPhoton]` and assigns to `maxPlayers`; also propagates to `PhotonNetwork.CurrentRoom.MaxPlayers` and `SteamManager.currentLobby.MaxMembers`.

Critically, the consumers of these fields are now centralized:

- `SteamManager.HostLobby(bool _open)` calls `SteamMatchmaking.CreateLobbyAsync(GameManager.instance.maxPlayers)`.
- `NetworkConnect.OnConnectedToMaster` builds `RoomOptions { MaxPlayers = GameManager.instance.maxPlayers, ... }` for the public-matchmaking create branch.
- `NetworkConnect.TryJoiningRoom` (private) builds `RoomOptions { MaxPlayers = GameManager.instance.maxPlayers, ... }` for the private join/create path.

This means we do not need to rewrite any room-creation logic. Raising `GameManager.maxPlayers` early in the lifecycle is sufficient for almost every code path.

The one exception is the public/random matchmaking *join* branch in `NetworkConnect.OnConnectedToMaster`, which passes a hardcoded `(byte)6` as `expectedMaxPlayers` to `PhotonNetwork.JoinRandomRoom` / `JoinRandomOrCreateRoom` when `GameManager.matchmakingMaxPlayers` is true. That value needs its own patch to respect the configured cap.

## Design

### Mod identity

- Folder: `mods/overstaffed/`
- BepInEx GUID: `darkharasho.Overstaffed`
- Plugin name: `Overstaffed`
- Initial version: `0.1.0`
- Project: `Overstaffed.csproj` (mirroring the `mods/mini-eepo` template layout)

### Configuration

File: `BepInEx/config/darkharasho.Overstaffed.cfg`

```
[General]

## The maximum number of players allowed in a server.
## Hard-capped at 20 (R.E.P.O.'s internal Photon ceiling and the
## practical Photon stability limit per upstream guidance).
# Setting type: Int32
# Default value: 10
# Acceptable value range: From 1 to 20
MaxPlayers = 10
```

Implementation note: bind with `AcceptableValueRange<int>(1, 20)` so r2modman/Gale renders a clean slider and rejects out-of-range input.

### Harmony patches

Three patches, applied in `Plugin.Awake` via `harmony.PatchAll()`.

#### Patch 1 — `GameManager.Awake` postfix (primary)

After the game's own `Awake` initializes `GameManager.instance`:

1. Read `Plugin.ConfigMaxPlayers.Value` into `target`.
2. If `target > GameManager.maxPlayersPhoton`, set `maxPlayersPhoton = target` (raise the ceiling first so step 3 isn't clamped).
3. Set `GameManager.maxPlayers = target`.
4. Log: `"[Overstaffed] maxPlayers {old} -> {target}, maxPlayersPhoton {old} -> {new}"`.

This is the load-bearing patch. Every consumer of `GameManager.instance.maxPlayers` (Steam lobby create, Photon room create for both public and private flows, Photon room join, member-leave reconciliation) will now observe the configured value.

#### Patch 2 — `GameManager.SetMaxPlayers` prefix (safety net)

The game calls `SetMaxPlayers` in several reconciliation paths (e.g. `OnLobbyMemberLeft` when `GameManager.maxPlayers < _lobby.MaxMembers`). The method clamps `_target` to `[1, maxPlayersPhoton]`, so if the game's internal logic ever passes a target above the vanilla ceiling, it would be silently trimmed.

Prefix behavior:

1. If `_target > GameManager.maxPlayersPhoton`, set `maxPlayersPhoton = _target` *before* the original method runs.
2. Let the original method proceed normally.

This patch is defensive — it ensures that if the game itself ever asks for a higher cap, our raised ceiling allows it through.

#### Patch 3 — `NetworkConnect.OnConnectedToMaster` prefix (public matchmaking)

In the public/random matchmaking branch (`GameManager.connectRandom == true` and no specific server name), the game computes:

```csharp
byte expectedMaxPlayers = (byte)(GameManager.instance.matchmakingMaxPlayers ? 6u : 0u);
```

This `6u` is hardcoded and is passed to `PhotonNetwork.JoinRandomRoom` / `JoinRandomOrCreateRoom` as the `expectedMaxPlayers` filter, which restricts matchmaking to rooms of exactly that size.

We cannot easily replace just that byte without a transpiler. Two viable approaches:

- **(A) Transpiler:** Replace the `ldc.i4.6` immediate with a call to a helper returning `(byte)GameManager.instance.maxPlayers`.
- **(B) Full prefix replacement:** Reimplement `OnConnectedToMaster` in a prefix returning `false`, using our configured value for `expectedMaxPlayers` and forwarding all other behavior verbatim.

**Decision: (A) Transpiler.** Cleaner, less code to maintain, doesn't fight future game updates to `OnConnectedToMaster`'s control flow. The transpiler finds the first `ldc.i4.6` followed by `conv.u1` (or `ldc.i4.6` used as a byte literal in this method) inside `OnConnectedToMaster` and substitutes a call to `Overstaffed.Patches.NetworkConnectPatches.GetExpectedMaxPlayers()`.

If the transpiler's anchor is brittle (e.g. game updates change the IL pattern), we fall back to (B) in a later version. The patch will log on apply/skip so failure is observable.

### Patch ordering and safety

- All three patches are independent; no ordering dependency between them.
- Each patch is wrapped in `try/catch` at the *registration* level (`harmony.CreateClassProcessor(typeof(X)).Patch()`) so a failure in one patch doesn't prevent the others from applying. The plugin logs a warning when a patch fails to apply but keeps loading.
- Patch 3 (transpiler) logs success/failure of the IL substitution explicitly.

### Logging

Single `ManualLogSource` named `Overstaffed`, used everywhere.

- `LogInfo` on plugin load with resolved config value.
- `LogInfo` from Patch 1 with before/after values.
- `LogInfo` from Patch 3 indicating whether the IL substitution succeeded.
- `LogWarning` from any patch that failed to register.

### File layout

Mirrors `mods/mini-eepo`:

```
mods/overstaffed/
├── CHANGELOG.md
├── CLAUDE.md
├── CONTRIBUTING.md
├── manifest.json
├── nuget.config
├── Overstaffed.csproj
├── package.sh
├── README.md
├── docs/
│   └── superpowers/  (links back to this spec)
├── libs/             (BepInEx + game DLL references)
└── src/
    ├── Plugin.cs
    └── Patches/
        ├── GameManagerPatches.cs        # Patch 1 + Patch 2
        └── NetworkConnectPatches.cs     # Patch 3
```

### Manifest

```json
{
  "name": "Overstaffed",
  "version_number": "0.1.0",
  "website_url": "https://github.com/darkharasho/REPOsitory",
  "description": "Raises R.E.P.O.'s max player count above 6. Configurable up to 20.",
  "dependencies": ["BepInEx-BepInExPack-5.4.2100"]
}
```

(Exact BepInEx dependency string copied verbatim from `mods/mini-eepo/manifest.json` at implementation time.)

## Testing

Manual integration testing — no automated tests (consistent with other mods in this repo and the package.sh-builds-zip-only workflow noted in user memory).

1. `mods/overstaffed/package.sh` → produces `builds/Overstaffed-0.1.0.zip`.
2. Install zip into an r2modman 0.4.0 profile.
3. Launch R.E.P.O. → verify `LogOutput.log` contains the Overstaffed banner and the "maxPlayers 6 -> 10" line.
4. Host a private/friends-only lobby → invite 7+ Steam friends → confirm all can join.
5. Verify the player roster UI populates correctly for >6 players.
6. (If feasible) test public/random matchmaking with another modded host to confirm Patch 3 lets matchmaking find a >6-player room.

## Risks

- **Photon stability above ~10 players:** The original mod's README warned about this; we inherit the same risk. Mitigation: `AcceptableValueRange` caps the config at 20, matching the game's own `maxPlayersPhoton`.
- **Future game updates:** If 0.4.x renames `GameManager.maxPlayers` or restructures `OnConnectedToMaster`, the mod breaks. This is unavoidable for any BepInEx mod and is the same failure mode that killed the predecessor. The defensive patch registration (each patch in its own try/catch) limits blast radius.
- **Vanilla-host compatibility:** A non-modded host's lobby still caps at 6; our mod only changes behavior when *we* host or join modded hosts. This matches the predecessor's behavior and is expected.

## Open questions

None. Design approved by user.
