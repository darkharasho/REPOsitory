# UnchainedIndestructibles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the UnchainedIndestructibles BepInEx mod so the Indestructible Drone can spawn in the shop and be carried into a run beyond the vanilla cap of 1.

**Architecture:** Two scoped Harmony prefix+finalizer pairs. Each prefix temporarily raises a cap field (`Item.maxAmountInShop` for shop populate, `Item.maxAmount` for run inventory build) on the Indestructible Drone's `Item` ScriptableObject; the finalizer restores it. A static resolver caches the drone `Item` by `emojiIcon == SemiFunc.emojiIcon.drone_indestructible` from `StatsManager.instance.itemDictionary` (`SemiFunc.itemType` is only category-level; `Item.emojiIcon` distinguishes drone variants). One config section drives both caps.

**Tech Stack:** C# / netstandard2.1 / BepInEx 5.4.21 / HarmonyX (`HarmonyLib`) / Unity 2022 (R.E.P.O.). No automated tests (Unity singletons + Photon make unit tests impractical in this monorepo); verification is build + plugin-loaded log line per the spec's testing-strategy section.

**Spec:** `mods/unchained-indestructibles/docs/superpowers/specs/2026-05-15-unchained-indestructibles-design.md`

---

## File Structure

All paths are relative to `mods/unchained-indestructibles/`.

| File | Responsibility |
|---|---|
| `src/Plugin.cs` | `[BepInPlugin]` entry. Binds config, runs `Harmony.PatchAll`, emits loaded log line. |
| `src/Config.cs` | Static container exposing `MaxAmount` and `MaxAmountInShop` (`ConfigEntry<int>`). Bound in `Plugin.Awake`. |
| `src/DroneItem.cs` | Static resolver. Lazy lookup of the Indestructible Drone `Item` by `itemType`. Cached per `StatsManager.instance` reference. Null-safe. |
| `src/Patches/ShopManagerPatches.cs` | `[HarmonyPatch(typeof(ShopManager), "GetAllItemsFromStatsManager")]` — prefix bumps drone `maxAmountInShop`, finalizer restores. |
| `src/Patches/ItemManagerPatches.cs` | `[HarmonyPatch(typeof(ItemManager), "GetPurchasedItems")]` — prefix bumps drone `maxAmount`, finalizer restores. |

The `Plugin.cs` scaffold from `new` already exists with a no-op `Awake`; Task 1 replaces it.

---

## Task 1: Config wiring

**Files:**
- Create: `mods/unchained-indestructibles/src/Config.cs`
- Modify: `mods/unchained-indestructibles/src/Plugin.cs`

- [ ] **Step 1: Create `src/Config.cs`**

```csharp
using BepInEx.Configuration;

namespace UnchainedIndestructibles
{
    internal static class Config
    {
        internal static ConfigEntry<int> MaxAmount = null!;
        internal static ConfigEntry<int> MaxAmountInShop = null!;

        internal static void Bind(ConfigFile config)
        {
            var range = new AcceptableValueRange<int>(1, 99);

            MaxAmount = config.Bind(
                "IndestructibleDrone", "MaxAmount", 10,
                new ConfigDescription(
                    "Maximum number of Indestructible Drones the host can carry into a run from purchased inventory. " +
                    "Vanilla is 1.",
                    range));

            MaxAmountInShop = config.Bind(
                "IndestructibleDrone", "MaxAmountInShop", 3,
                new ConfigDescription(
                    "Maximum number of Indestructible Drones the host's shop can spawn per refresh. Vanilla is 1.",
                    range));
        }
    }
}
```

- [ ] **Step 2: Replace `src/Plugin.cs`**

```csharp
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace UnchainedIndestructibles
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;

        private void Awake()
        {
            Log = Logger;

            Config.Bind(base.Config);

            var harmony = new Harmony("darkharasho.UnchainedIndestructibles");
            harmony.PatchAll();

            Log.LogInfo($"UnchainedIndestructibles v{PluginInfo.PLUGIN_VERSION} loaded.");
        }
    }
}
```

Note: `base.Config` is the BepInEx `ConfigFile` exposed by `BaseUnityPlugin`. The local `Config` static class shadows it; we explicitly call `base.Config` to avoid the name collision. (If the collision feels fragile, an alternative is renaming the static class — but matching `Config.cs`/`namespace.Config` is the convention used in upgrade-limiter's surrounding code, so we keep it.)

- [ ] **Step 3: Build to verify config compiles standalone**

Run:
```bash
cd mods/unchained-indestructibles
GAME_DIR="/var/mnt/data/SteamLibrary/steamapps/common/REPO/" dotnet build UnchainedIndestructibles.csproj --configuration Release
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. Output DLL at `bin/Release/netstandard2.1/UnchainedIndestructibles.dll`.

- [ ] **Step 4: Commit**

```bash
git add mods/unchained-indestructibles/src/Config.cs mods/unchained-indestructibles/src/Plugin.cs
git commit -m "UnchainedIndestructibles: add config + wire Plugin.Awake"
```

---

## Task 2: Drone item resolver

**Files:**
- Create: `mods/unchained-indestructibles/src/DroneItem.cs`

- [ ] **Step 1: Create `src/DroneItem.cs`**

```csharp
namespace UnchainedIndestructibles
{
    internal static class DroneItem
    {
        private static Item? _cached;
        private static StatsManager? _cachedStats;
        private static bool _missingWarned;

        // Returns the Indestructible Drone Item from StatsManager.itemDictionary,
        // or null if StatsManager isn't initialized yet or the item is missing
        // (e.g. some other mod removed it). Caches by StatsManager.instance ref;
        // re-resolves automatically if the singleton is rebuilt (it shouldn't,
        // but we guard cheaply).
        internal static Item? Resolve()
        {
            var stats = StatsManager.instance;
            if (stats == null) return null;

            if (!ReferenceEquals(stats, _cachedStats))
            {
                _cached = null;
                _cachedStats = stats;
                _missingWarned = false;
            }
            if (_cached != null) return _cached;

            foreach (var item in stats.itemDictionary.Values)
            {
                if (item != null && item.emojiIcon == SemiFunc.emojiIcon.drone_indestructible)
                {
                    _cached = item;
                    break;
                }
            }

            if (_cached == null && !_missingWarned)
            {
                Plugin.Log.LogWarning(
                    "Indestructible Drone item not found in StatsManager.itemDictionary — mod is inert.");
                _missingWarned = true;
            }
            return _cached;
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
cd mods/unchained-indestructibles
GAME_DIR="/var/mnt/data/SteamLibrary/steamapps/common/REPO/" dotnet build UnchainedIndestructibles.csproj --configuration Release
```

Expected: success. If `Item` or `SemiFunc.emojiIcon.drone_indestructible` aren't resolvable, the build fails — fix typos before continuing.

- [ ] **Step 3: Commit**

```bash
git add mods/unchained-indestructibles/src/DroneItem.cs
git commit -m "UnchainedIndestructibles: add DroneItem resolver"
```

---

## Task 3: ShopManager patch (shop populate cap)

**Files:**
- Create: `mods/unchained-indestructibles/src/Patches/ShopManagerPatches.cs`

- [ ] **Step 1: Create `src/Patches/ShopManagerPatches.cs`**

```csharp
using HarmonyLib;

namespace UnchainedIndestructibles.Patches
{
    // Scoped cap override: temporarily raise the drone Item's maxAmountInShop
    // so ShopManager.GetAllItemsFromStatsManager (which reads the field inline
    // at decompile line 233) treats the drone as having a higher cap, then
    // restore it in the finalizer. Method is host-only in vanilla (early-returns
    // on SemiFunc.IsNotMasterClient at line 203), so this only runs on the host.
    [HarmonyPatch(typeof(ShopManager), "GetAllItemsFromStatsManager")]
    internal static class ShopManager_GetAllItemsFromStatsManager_Patch
    {
        // Per-call snapshot. The patched method is not reentrant (it iterates
        // itemDictionary serially), so a single static slot is safe.
        private static Item? _drone;
        private static int _originalMaxAmountInShop;
        private static bool _restorePending;

        public static void Prefix()
        {
            _restorePending = false;
            _drone = DroneItem.Resolve();
            if (_drone == null) return;

            _originalMaxAmountInShop = _drone.maxAmountInShop;
            _drone.maxAmountInShop = Config.MaxAmountInShop.Value;
            _restorePending = true;
        }

        public static void Finalizer()
        {
            if (!_restorePending || _drone == null) return;
            _drone.maxAmountInShop = _originalMaxAmountInShop;
            _restorePending = false;
            _drone = null;
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
cd mods/unchained-indestructibles
GAME_DIR="/var/mnt/data/SteamLibrary/steamapps/common/REPO/" dotnet build UnchainedIndestructibles.csproj --configuration Release
```

Expected: success. The `Item.maxAmountInShop` field is public per the decompile (`Item.cs:34`); if the build complains about access, re-check field name.

- [ ] **Step 3: Commit**

```bash
git add mods/unchained-indestructibles/src/Patches/ShopManagerPatches.cs
git commit -m "UnchainedIndestructibles: patch ShopManager.GetAllItemsFromStatsManager"
```

---

## Task 4: ItemManager patch (run inventory cap)

**Files:**
- Create: `mods/unchained-indestructibles/src/Patches/ItemManagerPatches.cs`

- [ ] **Step 1: Create `src/Patches/ItemManagerPatches.cs`**

```csharp
using HarmonyLib;

namespace UnchainedIndestructibles.Patches
{
    // Scoped cap override: temporarily raise the drone Item's maxAmount so
    // ItemManager.GetPurchasedItems (decompile line 153 -- Mathf.Clamp(value, 0,
    // item.maxAmount)) clamps to our configured value instead of the vanilla 1.
    // Restored in the finalizer.
    [HarmonyPatch(typeof(ItemManager), "GetPurchasedItems")]
    internal static class ItemManager_GetPurchasedItems_Patch
    {
        private static Item? _drone;
        private static int _originalMaxAmount;
        private static bool _restorePending;

        public static void Prefix()
        {
            _restorePending = false;
            _drone = DroneItem.Resolve();
            if (_drone == null) return;

            _originalMaxAmount = _drone.maxAmount;
            _drone.maxAmount = Config.MaxAmount.Value;
            _restorePending = true;
        }

        public static void Finalizer()
        {
            if (!_restorePending || _drone == null) return;
            _drone.maxAmount = _originalMaxAmount;
            _restorePending = false;
            _drone = null;
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
cd mods/unchained-indestructibles
GAME_DIR="/var/mnt/data/SteamLibrary/steamapps/common/REPO/" dotnet build UnchainedIndestructibles.csproj --configuration Release
```

Expected: success. Note `GetPurchasedItems` is a `private` method on `ItemManager` per the decompile — `[HarmonyPatch(typeof(...), "GetPurchasedItems")]` patches private methods fine because HarmonyX resolves by name, but if for some reason it doesn't find the method, switch to `AccessTools.Method(typeof(ItemManager), "GetPurchasedItems")` and explicit manual patching.

- [ ] **Step 3: Commit**

```bash
git add mods/unchained-indestructibles/src/Patches/ItemManagerPatches.cs
git commit -m "UnchainedIndestructibles: patch ItemManager.GetPurchasedItems"
```

---

## Task 5: Package and in-game smoke verification

This task is the closest equivalent to a test pass for this mod — there is no automated test harness.

**Files:**
- Modify: `mods/unchained-indestructibles/CHANGELOG.md`

- [ ] **Step 1: Update CHANGELOG**

Replace contents of `mods/unchained-indestructibles/CHANGELOG.md` with:

```markdown
# Changelog

## 0.1.0
- Initial release
- Raises the Indestructible Drone shop spawn cap (config `MaxAmountInShop`, default 3, range 1–99)
- Raises the Indestructible Drone run inventory cap (config `MaxAmount`, default 10, range 1–99)
- Host-authoritative — host's config applies to all players in the lobby
```

- [ ] **Step 2: Verify icon.png is present**

Run:
```bash
test -f mods/unchained-indestructibles/icon.png && file mods/unchained-indestructibles/icon.png
```

Expected: `... PNG image data, 256 x 256, ...`. If missing, the user already converted one earlier in the conversation — confirm with them before proceeding.

- [ ] **Step 3: Package**

```bash
cd mods/unchained-indestructibles
GAME_DIR="/var/mnt/data/SteamLibrary/steamapps/common/REPO/" ./package.sh
```

Expected output:
```
Using game dir: /var/mnt/data/SteamLibrary/steamapps/common/REPO/
Build succeeded.
Packaged: <repo>/builds/UnchainedIndestructibles-0.1.0.zip — install via r2modman.
```

- [ ] **Step 4: Install in r2modman and launch the game**

Hand off to the user for this step:

> "Install `builds/UnchainedIndestructibles-0.1.0.zip` via r2modman → Import local mod into your R.E.P.O. profile, then launch the game. After the main menu loads, share the BepInEx console log (or the `BepInEx/LogOutput.log` tail) so I can verify the plugin loaded clean and patched both methods."

- [ ] **Step 5: Inspect log**

Expected log lines (no specific order, no `[Error]` lines for our GUID):
```
[Info   :   BepInEx] Loading [UnchainedIndestructibles 0.1.0]
[Info   :UnchainedIndestructibles] UnchainedIndestructibles v0.1.0 loaded.
```

If `[Error]` lines mention `darkharasho.UnchainedIndestructibles` or the patched method names, capture the stack trace and stop — debug before continuing.

- [ ] **Step 6: In-game smoke check (user, in r2modman'd game)**

Tell the user to perform:

1. Start a run, reach a shop. Refresh the shop (re-enter / re-roll) until the Indestructible Drone appears. Confirm it can appear more than once per refresh (or that the cap respects `MaxAmountInShop`).
2. Purchase at least 2 Indestructible Drones. Complete the shop level. Confirm at the start of the next run that multiple drones are in inventory.

If both observations match `MaxAmountInShop` / `MaxAmount`, the mod is verified.

- [ ] **Step 7: Commit CHANGELOG bump**

```bash
git add mods/unchained-indestructibles/CHANGELOG.md
git commit -m "UnchainedIndestructibles: 0.1.0 changelog"
```

---

## Self-Review Notes

- **Spec coverage:** Both patches present (Tasks 3, 4). Config section present (Task 1). DroneItem resolver present (Task 2). Plugin wiring present (Task 1, Step 2). Multiplayer note retained in spec; no Photon sync code (correct — spec says none needed). README mentions host-authoritative behavior (already in scaffold). Edge cases (drone missing, StatsManager null, original throws) handled by null-safety + Harmony finalizers in Tasks 2, 3, 4. Testing strategy (build + r2modman + log) is Task 5.
- **Placeholders:** none.
- **Type consistency:** `Config.MaxAmount.Value` / `Config.MaxAmountInShop.Value` used consistently. `DroneItem.Resolve()` used consistently. `Plugin.Log` matches scaffold.

---

## Execution Handoff

Plan complete and saved to `mods/unchained-indestructibles/docs/superpowers/plans/2026-05-15-unchained-indestructibles-plan.md`. Two execution options:

1. **Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
