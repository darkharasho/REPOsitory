# Changelog

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
