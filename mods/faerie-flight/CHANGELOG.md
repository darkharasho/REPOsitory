# Changelog

## 0.2.5
- **THE lobby-freeze fix.** Live bisection (solo repro, control mod, and two single-mechanism builds) pinned the freeze on registering our Photon event callback at plugin boot: the first `PhotonNetwork` touch force-runs PUN's static init before REPO's own Photon setup (the game ships a modified PUN and configures region/version/AppId itself at lobby time), leaving lobby creation spinning a half-configured connection at 100% CPU. The callback is now registered lazily — only while floating is actually active in a loaded multiplayer level (by which point the game initialized Photon long ago) — and dropped again on every transition. A client registering a frame late just catches the next 4s roster broadcast.
- The 0.2.3/0.2.4 hardening (bounded scans, loading gate, settle delay) stays: those windows were real, but the boot-time Photon touch was the trigger actually freezing lobbies.
- (0.2.4 was never published; its changes ship here.)

## 0.2.4
- **Fixes the black-screen hang 0.2.3 could still hit** (freeze on creating a lobby, leaving to menu, or entering a level). 0.2.3 throttled prefab-scan *retries* but still fired the first scan on the very frame a scene loaded — mid-transition, while REPOLib re-registers items and level bundles are in flight, that synchronous bundle access can block the main thread forever. All float work (scans, spawns, roster handling) now waits until the game's own level-load animation has completed, and the first scan additionally waits a 1.5s settle after every scene load. Verified against the live hang: the log's last line was the scene reset, with the scan's completion line never printed.

## 0.2.3
- **Fixes the freeze shipped in 0.2.2** (and latent in all 0.2.x): on any machine where the Zero Gravity staff prefab can't be resolved from the item database (spawn/shop mods pruning items, or a custom level's bundles mid-load), the prefab scan re-walked the whole item dictionary every frame — and every `PrefabRef.Prefab` access is a synchronous bundle load — grinding the game to an apparent freeze. Failed scans now retry at most every 2s. This is what froze the Aug 5 group sessions (pinned on this mod by A/B testing). **All 0.2.x users should update.**
- The staff prefab is now resolved by its exact item key first (one dictionary lookup); the fuzzy scan only runs as a fallback when the staff is renamed or replaced.
- Strength upgrades now work while floating: the game clamps a tumbling grabber to base strength, so permanent float silently disabled every Strength upgrade. Restored via the object-level grab-strength override (applied after the game's clamp), softened toward a square-root curve because floating grabbers take the grab's recoil (`RestoreGrabUpgrades` / `StrengthSoftening` config). Grabbing enemies keeps vanilla behavior.

## 0.2.2
- Hardened the networked spawn path against the level-start race: a roster entry is skipped until the player's PlayerTumble (and its PhysGrabObject) is linked — previously `SemiAffect.Setup` threw inside Photon's event dispatch and leaked a half-initialized particle effect on each 4s retry. The event handler is now exception-guarded, and stale rosters are ignored when the mod is disabled locally or the run isn't in a floatable level.
- Note: the Jun 23 multiplayer test of 0.2.1 froze on the loading screen because of Overstaffed ≤0.1.0's broken NetworkConnect transpiler (fixed in Overstaffed 0.2.0), not because of this mod. 0.2.1's netcode was sound; run this alongside Overstaffed ≥0.2.0.

## 0.2.1
- Fixed clients getting stuck on the loading screen in multiplayer. 0.2.0's float-roster broadcast used Photon event code 199, which REPO reserves for its "you were kicked by the server" event — so every 4s broadcast kicked all clients (OutroStart + leave room). Moved to an unreserved code (117) and added a guard that disables the networked float if it's ever pointed at a REPO-reserved code (123/124/199).

## 0.2.0
- Rewrote multiplayer float spawning to mirror the game's own Zero Gravity staff: the host now broadcasts the float roster (via Photon RaiseEvent) and every client spawns the effect bound by network ID, instead of each client independently spawning/destroying single-player effects keyed on the laggy networked tumble flag. Fixes clients getting stuck unable to move with no collision.

## 0.1.2
- Clean republish so every player lands on a byte-identical build (avoids r2modman serving a stale same-version cache). No behavior change from 0.1.1.

## 0.1.1
- Reset prefab + per-player caches on scene load (fixes cross-level "can't move").

## 0.1.0
- Initial release: permanent Zero Gravity Staff float for all players during levels.
