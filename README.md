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

## Layout

```
mods/
  mini-eepo/           # MiniEepo BepInEx plugin
  upgrade-limiter/     # UpgradeLimiter BepInEx plugin
  chill-shop-keeper/   # ChillShopKeeper BepInEx plugin
  museum-gambling/     # MuseumGambling BepInEx plugin
  unchained-indestructibles/  # UnchainedIndestructibles BepInEx plugin
  overstaffed/         # Overstaffed BepInEx plugin
.github/workflows/     # Per-mod build workflows (path-filtered)
```

Each mod folder is self-contained: `.csproj`, `manifest.json`, `package.sh`, source, and docs.
