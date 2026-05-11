# REPOsitory

Monorepo for my [R.E.P.O.](https://store.steampowered.com/app/3241660/REPO/) mods.

## Mods

| Mod | Description |
| --- | --- |
| [MiniEepo](mods/mini-eepo) | Shrinks all players, items, and valuables to 40% size. |
| [UpgradeLimiter](mods/upgrade-limiter) | Caps how many of each player upgrade can be stacked. Configurable per upgrade. |

## Layout

```
mods/
  mini-eepo/         # MiniEepo BepInEx plugin
  upgrade-limiter/   # UpgradeLimiter BepInEx plugin
.github/workflows/   # Per-mod build workflows (path-filtered)
```

Each mod folder is self-contained: `.csproj`, `manifest.json`, `package.sh`, source, and docs.
