# REPOsitory

Monorepo for my [R.E.P.O.](https://store.steampowered.com/app/3241660/REPO/) mods.

## Mods

| Mod | Description |
| --- | --- |
| [MiniEepo](mods/mini-eepo) | Shrinks all players, items, and valuables to 40% size. Held guns sit right and pushing a cart no longer shoves tiny players. |
| [UpgradeLimiter](mods/upgrade-limiter) | Caps how many of each player upgrade can be stacked. Configurable per upgrade. |
| [ChillShopKeeper](mods/chill-shop-keeper) | Stops the ShopKeeper from punishing ruckus. Global kill-switch + per-player exemptions. |
| [MuseumGambling](mods/museum-gambling) | Turns the Museum Head into a slot machine — configurable chance to pay out a money bag instead of dealing damage. |
| [UnchainedIndestructibles](mods/unchained-indestructibles) | Removes the hard cap on Indestructible Drones so you can stock and carry as many as you want. |
| [Overstaffed](mods/overstaffed) | Raises R.E.P.O.'s max player count above 6. Configurable up to 20. |
| [CondensedPlayerList](mods/condensed-player-list) | Condenses the lobby and ESC-menu player list into tighter single-column rows so large lobbies fit on screen. |
| [FaerieFlight](mods/faerie-flight) | Everyone sprouts wings and floats — a permanent Zero Gravity Staff effect for all players during levels. |
| [ForcedFriendship](mods/forced-friendship) | Take damage over time when you stray too far from other players; the farther away, the faster it ticks. |
| [ModdedPurchaseKeeper](mods/modded-purchase-keeper) | Fixes modded (REPOLib) shop items being bought but not appearing in the level. |
| [InfiniteEnergy](mods/infinite-energy) | Infinite battery for weapons and items — guns, melee durability, drones and tools never run dry. Host-only. |
| [FullHeal](mods/full-heal) | Restores health at the start of each level and on revival. A drop-in FullHealth replacement (same config) that no longer freezes the game. |

## Layout

```
mods/
  mini-eepo/           # MiniEepo BepInEx plugin
  upgrade-limiter/     # UpgradeLimiter BepInEx plugin
  chill-shop-keeper/   # ChillShopKeeper BepInEx plugin
  museum-gambling/     # MuseumGambling BepInEx plugin
  unchained-indestructibles/  # UnchainedIndestructibles BepInEx plugin
  overstaffed/         # Overstaffed BepInEx plugin
  condensed-player-list/  # CondensedPlayerList BepInEx plugin
  faerie-flight/       # FaerieFlight BepInEx plugin
  forced-friendship/   # ForcedFriendship BepInEx plugin
  modded-purchase-keeper/  # ModdedPurchaseKeeper BepInEx plugin
  infinite-energy/     # InfiniteEnergy BepInEx plugin
  full-heal/           # FullHeal BepInEx plugin
  purchase-diag/       # PurchaseDiag diagnostic plugin (not published)
.github/workflows/     # Per-mod build workflows (path-filtered)
```

Each mod folder is self-contained: `.csproj`, `manifest.json`, `package.sh`, source, and docs.

## Publishing

Bump `version_number` in the mod's `manifest.json`, commit, then tag `<mod-dir>-v<version>` and push the tag:

```
git tag upgrade-limiter-v0.4.4 && git push origin upgrade-limiter-v0.4.4
```

`.github/workflows/publish.yml` builds that mod on the self-hosted runner and publishes it to Thunderstore with `scripts/thunderstore-publish.sh`, using the `THUNDERSTORE_TOKEN` repo secret. It keeps the categories the package already has on Thunderstore and skips versions that are already published. A brand-new mod's first upload needs categories: put them comma-separated in `mods/<mod-dir>/thunderstore-categories` (e.g. `ai-generated,serverside,items`) and the workflow passes them on.
