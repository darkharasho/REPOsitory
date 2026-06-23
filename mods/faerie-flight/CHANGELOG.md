# Changelog

## 0.2.0
- Rewrote multiplayer float spawning to mirror the game's own Zero Gravity staff: the host now broadcasts the float roster (via Photon RaiseEvent) and every client spawns the effect bound by network ID, instead of each client independently spawning/destroying single-player effects keyed on the laggy networked tumble flag. Fixes clients getting stuck unable to move with no collision.

## 0.1.2
- Clean republish so every player lands on a byte-identical build (avoids r2modman serving a stale same-version cache). No behavior change from 0.1.1.

## 0.1.1
- Reset prefab + per-player caches on scene load (fixes cross-level "can't move").

## 0.1.0
- Initial release: permanent Zero Gravity Staff float for all players during levels.
