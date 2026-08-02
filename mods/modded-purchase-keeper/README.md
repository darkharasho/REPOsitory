# ModdedPurchaseKeeper

Fixes REPOLib-registered (modded) shop items being **bought and charged but never appearing
in the level** — e.g. EMLGunPlus coil guns, and any other modded purchasable item.

If you've ever bought a modded weapon in the shop, watched it take your money, then loaded
into the level to find it missing — this fixes that.

## The bug

When you buy an item, the game records it in `StatsManager.itemsPurchased`, and at the start
of the next level `ItemManager.GetPurchasedItems` reads that list so the truck can respawn
what you own. But REPOLib re-registers its modded items on level transitions, and
`StatsManager.AddItem` resets `itemsPurchased[name] = 0` for each one it registers — wiping
the purchase count of a modded item you *just* bought, before it can respawn. Vanilla items
never go through REPOLib's registration path, which is why only modded items disappear.

*(Verified against EMLGunPlus coil guns on R.E.P.O. 0.4.4 / REPOLib 4.2.0.)*

## The fix

Instead of fighting REPOLib's re-registration, ModdedPurchaseKeeper keeps its own ledger of
what you actually bought (hooking `StatsManager.ItemPurchase` / `SetItemPurchase`) and
re-asserts the correct count in a prefix on `ItemManager.GetPurchasedItems` — the exact moment
the truck reads it. Whatever zeroed the value in between, it's correct when it matters. The
ledger is cleared on a genuine new-run reset (`ResetAllStats`), so purchases never leak
between runs, and it only ever *raises* a count back to what you paid for — it can't duplicate
items or interfere with consumables being used up.

No configuration. Load order is handled via a soft dependency on REPOLib.

## Scope

Covers purchasable **items** (weapons, tools, carts, etc. — anything tracked by
`itemsPurchased`). Modded single-use *upgrades* use separate dictionaries and are out of scope.

## Compatibility

- R.E.P.O. 0.4.x
- Requires [REPOLib](https://thunderstore.io/c/repo/p/Zehs/REPOLib/) and BepInEx.
- Safe alongside other shop mods (MoreShopItems, etc.) — it only reads/writes purchase counts.

## Building

```
GAME_DIR=/path/to/REPO ./package.sh
```
Produces `builds/ModdedPurchaseKeeper-<version>.zip` and deploys the DLL to a local r2modman
profile for testing.
