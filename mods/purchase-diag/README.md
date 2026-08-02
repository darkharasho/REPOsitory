# PurchaseDiag

Throwaway diagnostic mod. It logs the shop-purchase → next-level respawn chain so we
can see exactly which link drops a purchased item (used to debug EMLGunPlus coil guns
being buyable in the shop but never appearing in the level).

All log lines are prefixed `[PDIAG]`. It changes no game behavior — patches are
log-only postfixes/prefixes.

## What it logs

- `ItemPurchase('name')` — a purchase was recorded; shows resulting `itemsPurchased`
  count and whether the item is in `itemDictionary`.
- `GetPurchasedItems` — dumps every `itemsPurchased` entry >0 and the resulting
  `purchasedItems` truck list (name + volume size).
- `TruckPopulateItemVolumes` — host-only; whether this client is master, how many
  purchased items / truck ItemVolumes exist, and the available volume sizes.
- `SpawnItem 'name'` — per item: its volume, the matched truck slot, and its
  `prefab.ResourcePath` + `IsValid` (the value handed to Photon).
- `InstantiateRoomObject('path')` — whether the network spawn returned an object or
  NULL (silent spawn failure).

## Use

```
./package.sh              # builds + deploys to the "0.4.0" r2modman profile
R2_PROFILE=MyProfile ./package.sh
```

Then: launch, buy a coil gun (and a vanilla gun for comparison), enter a level, and
grep the log:

```
grep '\[PDIAG\]' "$HOME/.config/r2modmanPlus-local/REPO/profiles/0.4.0/BepInEx/LogOutput.log"
```

The first line where the coil gun disappears (present in `GetPurchasedItems` but not
`SpawnItem`, or `InstantiateRoomObject -> NULL`, etc.) is the broken link.
