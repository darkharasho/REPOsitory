# ModdedPurchaseKeeper Changelog

## 0.1.1

- Fixed used power-ups (and spent consumables) being duplicated in the truck on every
  scene load. The game consumes purchases by writing `itemsPurchased` directly
  (`ItemUpgrade.PlayerUpgrade`, `StatsManager.ItemRemove`) — never through
  `SetItemPurchase` — so the ledger kept the bought count and the pre-truck re-assert
  resurrected items the player had already used (buy 1 health upgrade, get a free one
  every level). The ledger now mirrors those direct consumption writes; the sync only
  fires when the intercepted call itself changed the count, so REPOLib's registration
  wipe (the thing this mod exists to undo) can never be mistaken for a consumption.

## 0.1.0

- Initial release. Preserves modded (REPOLib-registered) item purchase counts across
  REPOLib's per-level `RunStartStats` re-registration, so modded shop items you buy actually
  respawn in the level instead of being silently dropped. Confirmed against EMLGunPlus coil
  guns in REPO 0.4.4 / REPOLib 4.2.0.
