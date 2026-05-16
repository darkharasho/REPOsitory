# UnchainedIndestructibles — Design Spec

**Date:** 2026-05-15
**Status:** Approved
**Mod:** `mods/unchained-indestructibles/`
**Plugin GUID:** `darkharasho.UnchainedIndestructibles`

## Goal

Remove R.E.P.O.'s vanilla cap of 1 on the Indestructible Drone, so the player can:

1. Stock multiple Indestructible Drones in the shop per refresh.
2. Carry multiple Indestructible Drones into a run from purchased inventory.

The mod is drone-only. Indestructible Orb and other capped items are explicitly out of scope.

## Background

The decompile shows the cap is enforced in two host-side code paths, gated by per-item ScriptableObject fields:

| Field on `Item` (default `1`) | Enforced at | Effect |
|---|---|---|
| `maxAmountInShop` | `ShopManager.GetAllItemsFromStatsManager` (Assembly-CSharp.dll, decompile line 233-238) | Caps how many of an item the shop populates per refresh, including a skip-entirely branch when `purchased >= maxAmountInShop`. |
| `maxAmount` | `ItemManager.GetPurchasedItems` (decompile line 153, `Mathf.Clamp(value, 0, item.maxAmount)`) | Caps how many of an item are added to the run inventory from `StatsManager.itemsPurchased`. |

The Indestructible Drone `Item` ScriptableObject lives in `StatsManager.instance.itemDictionary` and is identifiable by `itemType == SemiFunc.itemType.drone_indestructible`. `ItemDroneIndestructible` (the per-instance behavior) has no global cap of its own — running multiple drones simultaneously is supported by the game.

## Architecture

Single BepInEx 5 plugin, netstandard2.1, Harmony-based. Three logical pieces:

1. **Config** — two `ConfigEntry<int>` values under section `[IndestructibleDrone]`, both clamped via `AcceptableValueRange<int>(1, 99)`.
2. **Drone item resolver** — lazy cached lookup of the drone's `Item` from `StatsManager.instance.itemDictionary` by `itemType == drone_indestructible`.
3. **Two Harmony patches** — scoped prefix/finalizer pairs that temporarily raise the drone's cap fields during the enforcing method's execution, then restore the original value.

### Why scoped prefix/finalizer instead of postfix

Both enforcement methods read the cap field inline mid-method, with branches that depend on the value (e.g. `ShopManager` skips the item entirely when `purchased >= maxAmountInShop`). A postfix would have to re-implement that branching to "top up" the result. A scoped prefix that bumps `drone.maxAmountInShop` and a finalizer that restores it is one pair of two-line patches with no logic duplication.

The mutation is bounded to a single method call — the `Item` ScriptableObject is back to its original value before any other code observes it. This satisfies the "no shared-state leak" requirement we agreed on for Approach B.

## Config

File: `BepInEx/config/darkharasho.UnchainedIndestructibles.cfg`

```ini
[IndestructibleDrone]
MaxAmount = 10           # 1–99, run inventory cap
MaxAmountInShop = 3      # 1–99, shop spawn cap per refresh
```

`ConfigEntry` value changes take effect on the next patched-method call — no restart, no level reload.

## Patches

### Patch 1: `ShopManager.GetAllItemsFromStatsManager` (shop populate)

- **Prefix** (priority normal): resolve the drone `Item`. If found, store its current `maxAmountInShop` and overwrite with `Config.MaxAmountInShop`.
- **Finalizer**: restore the original `maxAmountInShop` on the drone `Item`. Runs even if the original method throws.

Drone resolution may fail (drone missing from `itemDictionary`, `StatsManager.instance == null`). In that case the prefix no-ops and the finalizer has nothing to restore.

### Patch 2: `ItemManager.GetPurchasedItems` (run inventory build)

- **Prefix**: resolve drone `Item`. If found, store its current `maxAmount` and overwrite with `Config.MaxAmount`.
- **Finalizer**: restore the original `maxAmount`.

Same null-safety as Patch 1.

### Drone item resolver

Static helper. Caches the resolved `Item` reference. Invalidated when `StatsManager.instance` reference changes (compared by reference each call — `StatsManager` lives as a singleton, so this should never change during a session, but guarding against it is cheap).

```csharp
internal static class DroneItem
{
    private static Item? _cached;
    private static StatsManager? _cachedStats;

    internal static Item? Resolve()
    {
        var stats = StatsManager.instance;
        if (stats == null) return null;
        if (!ReferenceEquals(stats, _cachedStats))
        {
            _cached = null;
            _cachedStats = stats;
        }
        if (_cached != null) return _cached;
        foreach (var item in stats.itemDictionary.Values)
        {
            if (item.itemType == SemiFunc.itemType.drone_indestructible)
            {
                _cached = item;
                break;
            }
        }
        if (_cached == null)
        {
            Plugin.Log.LogWarning(
                "Indestructible Drone item not found in StatsManager.itemDictionary — mod will be inert.");
        }
        return _cached;
    }
}
```

## Multiplayer

Both patched methods are host-only in vanilla:

- `ShopManager.GetAllItemsFromStatsManager` early-returns on `SemiFunc.IsNotMasterClient()` (decompile line 203).
- `ItemManager.GetPurchasedItems` builds the host's run inventory from the host's `StatsManager`.

Therefore: **host's config wins; client-side config is ignored when joining someone else's lobby.** No Photon room-property sync (`SettingsSyncer`-style push/pull) is needed. README and CLAUDE.md note this explicitly.

Clients still need the mod installed only if they want to host their own runs with the cap lifted.

## Edge cases

| Case | Behavior |
|---|---|
| Drone item missing from `itemDictionary` (e.g. modded game removes it) | Resolver returns null, prefix/finalizer no-op, mod is silently inert (with a one-time log warning). |
| Config value <1 or >99 | BepInEx `AcceptableValueRange<int>(1, 99)` clamps at parse time. |
| `StatsManager.instance == null` at patch time | Resolver returns null, patches no-op. Vanilla method runs unmodified. |
| Original method throws | Harmony finalizer still runs, restoring the cap field. |
| User disables mod mid-session (unload DLL) | Patches stop applying on next call; `Item` ScriptableObject is in its original state (we restore after every call). |
| Player has purchased more drones than vanilla would allow before mod install | Run inventory respects `Config.MaxAmount`, so they get up to that many. Excess purchased counts stay in `StatsManager.itemsPurchased` but are clamped on use. |

## File layout

```
mods/unchained-indestructibles/
  src/
    Plugin.cs               # BepInPlugin entry, config wiring, Harmony.PatchAll
    Config.cs               # static accessor for MaxAmount / MaxAmountInShop
    DroneItem.cs            # resolver (above)
    Patches/
      ShopManagerPatches.cs       # GetAllItemsFromStatsManager prefix + finalizer
      ItemManagerPatches.cs       # GetPurchasedItems prefix + finalizer
```

## Testing strategy

This repo has no in-game test harness (verified across `mods/mini-eepo`, `mods/upgrade-limiter`, `mods/chill-shop-keeper`). The mod-author workflow is:

1. **Build** — `./package.sh` produces the DLL and Thunderstore zip.
2. **Install** — load the zip via r2modman into the local R.E.P.O. profile.
3. **Verify in BepInEx log** — confirm `UnchainedIndestructibles vX.Y.Z loaded.` line appears with no patch-failure stack traces.
4. **In-game smoke test** —
    - Open a shop, refresh until the Indestructible Drone appears; confirm it can appear in counts >1 when `MaxAmountInShop > 1`.
    - Purchase >1 drone, start a run, confirm multiple drones spawn in inventory up to `MaxAmount`.
5. **Multiplayer smoke test** — host with mod, join with vanilla client: confirm both see multiple drones (host-authoritative behavior).

Automated unit tests are out of scope — the patches are too tightly coupled to live Unity/Photon singletons to be meaningfully unit-testable without harness infrastructure that doesn't exist here.

## Out of scope

- Indestructible Orb (`orb_indestructible`) — exists in code but not currently obtainable in-game per user.
- Upgrade-stand entries (`UpgradeStand.cs:1624`) — the Indestructible Drone is not sold at upgrade stands.
- Other capped items — adding more uncapped items will require config-schema rework (we chose drone-named keys, Option A).
- In-game config UI (REPOConfig integration) — stock BepInEx `ConfigEntry` is sufficient.
