# Changelog

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
