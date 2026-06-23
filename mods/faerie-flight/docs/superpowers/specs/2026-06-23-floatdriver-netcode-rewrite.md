# FloatDriver netcode rewrite — design

Date: 2026-06-23
Status: approved (implementation)

## Problem

On non-host clients, floating players intermittently end up **stuck: cannot move and have no
collision** (clip through geometry). Reloading the level clears it. Four prior commits
(`af9f248`, `175cf06`, `ac2b4df`, `4c8db12`) each patched a symptom of this same class of bug
(retry-until-tumbling, no-overlap, timer top-up, per-scene cache reset) without removing it.

## Root cause (confirmed by decompiling the game)

The mod reuses the game's real Zero-Gravity effect (`SemiAffectZeroGravity` /
`SemiAffect`). That effect is **designed to be spawned by the game's master-coordinated,
networked path** — `SemiAreaOfEffect`:

1. The **master** raycasts to choose affected objects and collects their `photonViewID`s.
2. The master sends **one** `photonView.RPC("CreateAffectsRPC", RpcTarget.All, …)`.
3. `CreateAffectsRPC` is `[PunRPC]`, gated by `SemiFunc.MasterOnlyRPC(_info)`. On **every**
   client it then `Object.Instantiate`s the effect and binds it via `SemiAffect.Setup(photonViewID, affectTime)`.

Each effect's internal `Start`/`Update`/`FixedUpdate` is full of `IsMasterClientOrSingleplayer()`
and `isLocal`/`localCamera` gates that assume the **same logical effect exists on every machine
at once**: the master drives tumble + drift (player input `InputDirectionRaw` is networked via
`OnPhotonSerializeView`), each client applies its local zero-gravity physics override.

`FloatDriver` instead spawns effects the **single-player, uncoordinated** way:
`Object.Instantiate` + `SetupSingleplayer`, run **independently in every client's `Update`**, and
actively **destroys/respawns** effects keyed on the player's `isTumbling` flag. On a client
`isTumbling` is a **networked value that lags**, so the lifecycle thrashes — destroying the
effect (which removes the zero-gravity drift override) while the master still has the player
tumbling. Net result: a tumbling player with no drift control = "can't move, no collision",
until a scene reload resets state.

Note: `SetupSingleplayer` vs `Setup(photonViewID)` is **not** the bug — for a player target they
produce identical bindings. The bug is the **lifecycle**: uncoordinated per-client spawn/destroy
vs the game's master-broadcast, fire-and-refresh model.

## Fix — mirror the game's model

Replace the per-frame, per-client spawn/destroy loop with a master-coordinated refresh:

- **Master** (host, or singleplayer): on a timer (`RefreshInterval ≈ 4s`) and immediately when
  floating turns on, gather every alive player's `photonView.ViewID`.
  - Multiplayer: broadcast the id array via `PhotonNetwork.RaiseEvent(FloatEventCode, ids,
    Receivers.All, Reliable)` — `Receivers.All` includes the master, so there is one receive path.
  - Singleplayer / not in a room: handle the ids locally (no RaiseEvent).
- **All clients** (`IOnEventCallback.OnEvent`): for each id, if there is **no live effect** for
  that id, `Object.Instantiate(prefab)` + `Setup(id, AffectTime)`. Track `_active[viewID]`.
  Spawning only-when-absent prevents effect overlap (the "launch" bug).
- **Maintenance** (`Update`, all machines): drop expired/null effects from `_active` (so the next
  refresh respawns them); top the timer of live effects to `timerTotal` so they never expire
  between refreshes (no fall gap). No destroy-on-not-tumbling.
- Network-not-ready races are handled by the game itself: `Setup(id)` destroys the effect if
  `PhotonView.Find(id)` is null; the next 4s refresh retries. This replaces the manual
  `_nextTry` retry logic.

Keep unchanged: prefab resolution via the item DB, per-scene cache reset (`OnSceneLoaded`),
the flashlight patch, and the `Enabled` config gate.

## Networking details

- Event code: a fixed `byte` in the user range (1–199), e.g. `199`. Payload: `int[]` of view ids.
- Register `PhotonNetwork.AddCallbackTarget(this)` in `OnEnable`, remove in `OnDisable`.
- Types: `Photon.Pun` (`PhotonNetwork`), `Photon.Realtime` (`RaiseEventOptions`,
  `ReceiverGroup`), `ExitGames.Client.Photon` (`EventData`, `SendOptions`).
- Reliability: `SendOptions.SendReliable`. Refresh cadence makes a dropped event self-heal anyway.

## Testing

Cannot be reproduced or verified single-machine — it is a host/client desync. Verification is a
live 2+ player session: host enables floating, a **client** confirms it can move/drift and has
collision across a level transition. Per-player diagnostic logging is re-added (host-side
`master/local/alive/tumbling/effect` per player) so the user can confirm from the client log.

## Out of scope

No change to the float feel/constants, the flashlight behavior, or the config surface.
