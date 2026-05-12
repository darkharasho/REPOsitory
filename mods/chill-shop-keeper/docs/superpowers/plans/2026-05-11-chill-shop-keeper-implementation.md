# ChillShopKeeper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a BepInEx 5 Harmony mod that prevents the in-shop `ShopKeeper` from punishing players for ruckus. Two toggles: a global kill-switch (`DisableGlobally`) and per-player exemptions (`Player_<SteamID>`) that lazily appear in the config as players are observed.

**Architecture:** Single-assembly Harmony plugin. One prefix on the private `ShopKeeper.AddRuckusScore(PlayerAvatar, float)` and one postfix on `PlayerAvatar.AddToStatsManagerRPC(string, string, ...)` for player observation. Pure-C# `ChillPolicy` predicate is unit-tested; `PlayerRegistry` is unit-tested against a real `BepInEx.Configuration.ConfigFile` on a temp path. No Photon sync (host-authoritative by game design).

**Tech Stack:** C# netstandard2.1, BepInEx 5.4.21, HarmonyX, xUnit for tests, .NET SDK 6 (`DOTNET_ROOT=/var/home/linuxbrew/.linuxbrew/Cellar/dotnet@6/6.0.136_1/libexec` on this machine for ilspycmd; the build itself just uses `dotnet`).

---

## Game-side facts established by decompilation

- `ShopKeeper.AddRuckusScore(PlayerAvatar _player, float _amount)` — **private void**. Called from many places in `ShopKeeper` itself. Skipping it prevents *any* ruckus score from accruing.
- `ShopKeeper` uses `SemiFunc.IsMasterClientOrSingleplayer()` to gate its host-only logic; our patch will only ever fire when this is true (the method is private and only called from within `ShopKeeper` host paths), but we don't need an explicit guard.
- `PlayerAvatar.steamID` and `PlayerAvatar.playerName` are `internal string` fields, set in `AddToStatsManagerRPC(string _playerName, string _steamID, PhotonMessageInfo _info = default)` at avatar networked-init time. Patching `AddToStatsManagerRPC` postfix exposes both values directly as arguments — no reflection needed for observation.
- Reading `steamID` from a `PlayerAvatar` in the `AddRuckusScore` prefix needs reflection because the field is `internal`. Use `AccessTools.FieldRefAccess<PlayerAvatar, string>("steamID")` once at startup and reuse the delegate (cheap, allocation-free).

---

## File structure

```
mods/chill-shop-keeper/
  ChillShopKeeper.csproj           # MODIFY — add InternalsVisibleTo so tests can see internals; nothing else
  src/
    Plugin.cs                      # REPLACE — config entries, Harmony patch all, logger
    ChillPolicy.cs                 # NEW — pure predicate
    PlayerRegistry.cs              # NEW — ConcurrentDictionary<string, ConfigEntry<bool>>, Bind on observe
    Patches/
      PlayerObserverPatches.cs     # NEW — postfix on PlayerAvatar.AddToStatsManagerRPC
      ShopKeeperPatches.cs         # NEW — prefix on ShopKeeper.AddRuckusScore
  tests/
    ChillShopKeeper.Tests/
      ChillShopKeeper.Tests.csproj # NEW — xUnit, net6.0, references main csproj + BepInEx packages
      ChillPolicyTests.cs          # NEW
      PlayerRegistryTests.cs       # NEW
  README.md                        # MODIFY — replace ExemptHost docs with per-player + DisableGlobally
  CLAUDE.md                        # MODIFY — final one-line description
  CHANGELOG.md                     # MODIFY — flesh out 0.1.0
```

Each source file has one responsibility:
- `Plugin.cs` — BepInEx entry point, config binding, Harmony orchestration. ~60 LOC.
- `ChillPolicy.cs` — one static method, no dependencies. ~10 LOC.
- `PlayerRegistry.cs` — config-entry registry. ~40 LOC.
- `Patches/PlayerObserverPatches.cs` — one postfix, ~15 LOC.
- `Patches/ShopKeeperPatches.cs` — one prefix with field-ref, ~30 LOC.

---

## Task 1: Stand up the xUnit test project

**Files:**
- Create: `mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/ChillShopKeeper.Tests.csproj`
- Create: `mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/SmokeTest.cs`

- [ ] **Step 1: Create the test csproj**

Create `mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/ChillShopKeeper.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <IsPackable>false</IsPackable>
    <RootNamespace>ChillShopKeeper.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.6" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.6" />
    <PackageReference Include="BepInEx.Core" Version="5.4.21.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\ChillShopKeeper.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Add a trivial smoke test so we can confirm the runner works**

Create `mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/SmokeTest.cs`:

```csharp
using Xunit;

namespace ChillShopKeeper.Tests;

public class SmokeTest
{
    [Fact]
    public void Runner_works() => Assert.True(true);
}
```

- [ ] **Step 3: Verify the test project builds and the smoke test runs**

Run from repo root:

```bash
dotnet test mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/ChillShopKeeper.Tests.csproj
```

Expected: `Passed!  - Failed: 0, Passed: 1, Skipped: 0`. If the main csproj fails to build because game DLLs aren't found, set `GameDir`:

```bash
dotnet test mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/ChillShopKeeper.Tests.csproj /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO
```

Expected: same — 1 passed.

- [ ] **Step 4: Commit**

```bash
git add mods/chill-shop-keeper/tests
git commit -m "Add xUnit test project scaffold for ChillShopKeeper"
```

---

## Task 2: ChillPolicy predicate (TDD)

**Files:**
- Create: `mods/chill-shop-keeper/src/ChillPolicy.cs`
- Create: `mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/ChillPolicyTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/ChillPolicyTests.cs`:

```csharp
using Xunit;

namespace ChillShopKeeper.Tests;

public class ChillPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true,  true)]
    [InlineData(true,  false, true)]
    [InlineData(true,  true,  true)]
    public void ShouldSkip_truth_table(bool disableGlobally, bool playerExempt, bool expected)
    {
        Assert.Equal(expected, ChillPolicy.ShouldSkip(disableGlobally, playerExempt));
    }
}
```

Note: this references `ChillShopKeeper.ChillPolicy`. The test project already references the main csproj from Task 1, so as soon as `ChillPolicy` exists in the main project's namespace, the using-less qualified reference resolves through a `using ChillShopKeeper;` directive — add that to the top of the file if needed.

Update the top of the file:

```csharp
using ChillShopKeeper;
using Xunit;
```

- [ ] **Step 2: Run tests — confirm they fail with "ChillPolicy does not exist"**

```bash
dotnet test mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/ChillShopKeeper.Tests.csproj /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO
```

Expected: build error `CS0246: The type or namespace name 'ChillPolicy' could not be found`.

- [ ] **Step 3: Implement ChillPolicy**

Create `mods/chill-shop-keeper/src/ChillPolicy.cs`:

```csharp
namespace ChillShopKeeper;

internal static class ChillPolicy
{
    public static bool ShouldSkip(bool disableGlobally, bool playerExempt)
        => disableGlobally || playerExempt;
}
```

Note: `internal` is fine because the test project has `InternalsVisibleTo` added in Task 4. For now the test will fail to see it — that's the next step. To unblock immediately, mark it `public`:

```csharp
namespace ChillShopKeeper;

public static class ChillPolicy
{
    public static bool ShouldSkip(bool disableGlobally, bool playerExempt)
        => disableGlobally || playerExempt;
}
```

(We'll keep it `public` — it's harmless surface for a mod assembly and avoids InternalsVisibleTo ceremony.)

- [ ] **Step 4: Run tests — confirm 4 cases pass**

```bash
dotnet test mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/ChillShopKeeper.Tests.csproj /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO
```

Expected: `Passed: 5` (1 smoke + 4 truth-table cases).

- [ ] **Step 5: Commit**

```bash
git add mods/chill-shop-keeper/src/ChillPolicy.cs mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/ChillPolicyTests.cs
git commit -m "Add ChillPolicy.ShouldSkip predicate with truth-table tests"
```

---

## Task 3: PlayerRegistry (TDD against real ConfigFile)

**Files:**
- Create: `mods/chill-shop-keeper/src/PlayerRegistry.cs`
- Create: `mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/PlayerRegistryTests.cs`

The registry wraps a `BepInEx.Configuration.ConfigFile`. `ConfigFile` can be instantiated directly with a file path — no BepInEx harness needed.

- [ ] **Step 1: Write the failing tests**

Create `mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/PlayerRegistryTests.cs`:

```csharp
using System.IO;
using BepInEx.Configuration;
using ChillShopKeeper;
using Xunit;

namespace ChillShopKeeper.Tests;

public class PlayerRegistryTests
{
    private static ConfigFile NewTempConfig()
    {
        var path = Path.Combine(Path.GetTempPath(), $"chill-test-{System.Guid.NewGuid():N}.cfg");
        return new ConfigFile(path, saveOnInit: true);
    }

    [Fact]
    public void Observe_creates_entry_defaulting_to_false()
    {
        var cfg = NewTempConfig();
        var reg = new PlayerRegistry(cfg);

        reg.Observe("76561198000000001", "Alice");

        Assert.False(reg.IsExempt("76561198000000001"));
    }

    [Fact]
    public void Observe_is_idempotent_and_preserves_user_value()
    {
        var cfg = NewTempConfig();
        var reg = new PlayerRegistry(cfg);

        reg.Observe("76561198000000001", "Alice");
        reg.SetExempt("76561198000000001", true);
        reg.Observe("76561198000000001", "Alice");

        Assert.True(reg.IsExempt("76561198000000001"));
    }

    [Fact]
    public void IsExempt_returns_false_for_unknown_steamID()
    {
        var cfg = NewTempConfig();
        var reg = new PlayerRegistry(cfg);

        Assert.False(reg.IsExempt("76561198999999999"));
    }

    [Fact]
    public void Values_persist_across_ConfigFile_reload()
    {
        var path = Path.Combine(Path.GetTempPath(), $"chill-test-{System.Guid.NewGuid():N}.cfg");

        {
            var cfg = new ConfigFile(path, saveOnInit: true);
            var reg = new PlayerRegistry(cfg);
            reg.Observe("76561198000000001", "Alice");
            reg.SetExempt("76561198000000001", true);
            cfg.Save();
        }

        {
            var cfg = new ConfigFile(path, saveOnInit: true);
            var reg = new PlayerRegistry(cfg);
            reg.Observe("76561198000000001", "Alice");
            Assert.True(reg.IsExempt("76561198000000001"));
        }
    }
}
```

`SetExempt` is a test-only convenience helper on `PlayerRegistry` that sets the entry's `Value` — keeps the test from poking `ConfigEntry` directly.

- [ ] **Step 2: Run tests — confirm they fail with "PlayerRegistry does not exist"**

```bash
dotnet test mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/ChillShopKeeper.Tests.csproj /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO
```

Expected: CS0246 for `PlayerRegistry`.

- [ ] **Step 3: Implement PlayerRegistry**

Create `mods/chill-shop-keeper/src/PlayerRegistry.cs`:

```csharp
using System.Collections.Concurrent;
using BepInEx.Configuration;

namespace ChillShopKeeper;

public sealed class PlayerRegistry
{
    private const string Section = "Players";
    private readonly ConfigFile _config;
    private readonly ConcurrentDictionary<string, ConfigEntry<bool>> _entries = new();

    public PlayerRegistry(ConfigFile config)
    {
        _config = config;
    }

    public void Observe(string steamID, string displayName)
    {
        if (string.IsNullOrEmpty(steamID)) return;

        _entries.GetOrAdd(steamID, sid =>
            _config.Bind(
                Section,
                $"Player_{sid}",
                false,
                $"Exempt {displayName} from ShopKeeper punishment"));
    }

    public bool IsExempt(string steamID)
    {
        if (string.IsNullOrEmpty(steamID)) return false;
        return _entries.TryGetValue(steamID, out var entry) && entry.Value;
    }

    // Test-only convenience. Safe to call in prod too; not used by the patches.
    public void SetExempt(string steamID, bool value)
    {
        if (_entries.TryGetValue(steamID, out var entry))
            entry.Value = value;
    }
}
```

- [ ] **Step 4: Run tests — confirm 4 PlayerRegistry tests pass**

```bash
dotnet test mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/ChillShopKeeper.Tests.csproj /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO
```

Expected: `Passed: 9` (1 smoke + 4 policy + 4 registry).

If the "persist across reload" test fails because `ConfigFile.Save` isn't being called, the registry might need to nudge the file. `Bind` on the *re-opened* config should pick up the persisted value automatically; if not, replace `_entries.GetOrAdd(...)` with explicit `_config.Bind(...)` and let `ConfigFile`'s built-in reading handle persistence. Investigate before "fixing" — `ConfigFile` does read on construction.

- [ ] **Step 5: Commit**

```bash
git add mods/chill-shop-keeper/src/PlayerRegistry.cs mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/PlayerRegistryTests.cs
git commit -m "Add PlayerRegistry with per-player ConfigEntry persistence"
```

---

## Task 4: Plugin.cs — config entries + Harmony orchestration

**Files:**
- Modify: `mods/chill-shop-keeper/src/Plugin.cs` (replace the no-op skeleton)

This task wires the entry point only; the patches themselves come in Tasks 5–6. Plugin.cs ends up small and stays small.

- [ ] **Step 1: Replace Plugin.cs**

Overwrite `mods/chill-shop-keeper/src/Plugin.cs`:

```csharp
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ChillShopKeeper;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static Plugin Instance = null!;
    internal static ManualLogSource Log = null!;

    internal static ConfigEntry<bool> DisableGlobally = null!;
    internal static PlayerRegistry Players = null!;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        DisableGlobally = Config.Bind(
            "General",
            "DisableGlobally",
            false,
            "When true, the ShopKeeper ignores ruckus from everyone. Overrides per-player toggles.");

        Players = new PlayerRegistry(Config);

        var harmony = new Harmony("darkharasho.ChillShopKeeper");
        harmony.PatchAll();
        Log.LogInfo($"ChillShopKeeper v{MyPluginInfo.PLUGIN_VERSION} loaded.");
    }
}
```

Note: `BepInEx.PluginInfoProps` generates `MyPluginInfo` (not `PluginInfo` — the scaffold uses the wrong name). Verify the generated class name with `dotnet build` before assuming.

- [ ] **Step 2: Build to verify**

```bash
dotnet build mods/chill-shop-keeper/ChillShopKeeper.csproj --configuration Release /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO
```

Expected: success, output DLL at `mods/chill-shop-keeper/bin/Release/netstandard2.1/ChillShopKeeper.dll`. If `MyPluginInfo` is wrong, the compile error tells you the right name — fix and rebuild.

- [ ] **Step 3: Re-run tests to confirm nothing regressed**

```bash
dotnet test mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/ChillShopKeeper.Tests.csproj /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO
```

Expected: `Passed: 9`.

- [ ] **Step 4: Commit**

```bash
git add mods/chill-shop-keeper/src/Plugin.cs
git commit -m "Wire DisableGlobally config and PlayerRegistry in Plugin"
```

---

## Task 5: PlayerObserverPatches — register players when their Steam ID is set

**Files:**
- Create: `mods/chill-shop-keeper/src/Patches/PlayerObserverPatches.cs`

- [ ] **Step 1: Create the patch class**

Create `mods/chill-shop-keeper/src/Patches/PlayerObserverPatches.cs`:

```csharp
using System;
using HarmonyLib;

namespace ChillShopKeeper.Patches;

[HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.AddToStatsManagerRPC))]
internal static class PlayerAvatar_AddToStatsManagerRPC_Postfix
{
    private static void Postfix(string _playerName, string _steamID)
    {
        try
        {
            Plugin.Players.Observe(_steamID, _playerName);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"PlayerObserver postfix failed: {ex}");
        }
    }
}
```

Note: `AddToStatsManagerRPC` is `public` (confirmed via decompilation), so `nameof(PlayerAvatar.AddToStatsManagerRPC)` resolves at compile time. If it doesn't, fall back to the string literal `"AddToStatsManagerRPC"`.

- [ ] **Step 2: Build to verify**

```bash
dotnet build mods/chill-shop-keeper/ChillShopKeeper.csproj --configuration Release /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO
```

Expected: success.

- [ ] **Step 3: Commit**

```bash
git add mods/chill-shop-keeper/src/Patches/PlayerObserverPatches.cs
git commit -m "Patch PlayerAvatar.AddToStatsManagerRPC to register player exemption entries"
```

---

## Task 6: ShopKeeperPatches — prefix on AddRuckusScore

**Files:**
- Create: `mods/chill-shop-keeper/src/Patches/ShopKeeperPatches.cs`

- [ ] **Step 1: Create the patch class**

Create `mods/chill-shop-keeper/src/Patches/ShopKeeperPatches.cs`:

```csharp
using System;
using HarmonyLib;

namespace ChillShopKeeper.Patches;

[HarmonyPatch(typeof(ShopKeeper), "AddRuckusScore")]
internal static class ShopKeeper_AddRuckusScore_Prefix
{
    // PlayerAvatar.steamID is internal; cache a delegate that reads it directly.
    private static readonly AccessTools.FieldRef<PlayerAvatar, string> SteamIdRef =
        AccessTools.FieldRefAccess<PlayerAvatar, string>("steamID");

    private static bool Prefix(PlayerAvatar _player)
    {
        try
        {
            if (_player == null) return true;

            bool playerExempt = false;
            string sid = SteamIdRef(_player);
            if (!string.IsNullOrEmpty(sid))
                playerExempt = Plugin.Players.IsExempt(sid);

            if (ChillPolicy.ShouldSkip(Plugin.DisableGlobally.Value, playerExempt))
                return false; // skip original — no score accrues

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"AddRuckusScore prefix failed: {ex}");
            return true; // fail open
        }
    }
}
```

Notes for the implementer:
- The method is private, but `[HarmonyPatch(typeof(ShopKeeper), "AddRuckusScore")]` with a string name targets it correctly. Harmony resolves private methods without `BindingFlags` extras when given a string name.
- We only need `_player` from the original args — Harmony binds prefix parameters by name to original parameters, so taking just `PlayerAvatar _player` works regardless of how many extra args the original has.
- `AccessTools.FieldRef` is a JIT-compiled delegate; one-time cost at static init, near-zero per call.

- [ ] **Step 2: Build to verify**

```bash
dotnet build mods/chill-shop-keeper/ChillShopKeeper.csproj --configuration Release /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO
```

Expected: success.

- [ ] **Step 3: Run tests to confirm nothing regressed**

```bash
dotnet test mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/ChillShopKeeper.Tests.csproj /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO
```

Expected: `Passed: 9`.

- [ ] **Step 4: Commit**

```bash
git add mods/chill-shop-keeper/src/Patches/ShopKeeperPatches.cs
git commit -m "Patch ShopKeeper.AddRuckusScore to skip exempt players"
```

---

## Task 7: Refresh README and CLAUDE.md to match final design

**Files:**
- Modify: `mods/chill-shop-keeper/README.md`
- Modify: `mods/chill-shop-keeper/CLAUDE.md`
- Modify: `mods/chill-shop-keeper/CHANGELOG.md`

- [ ] **Step 1: Replace README.md**

Overwrite `mods/chill-shop-keeper/README.md`:

```markdown
# ChillShopKeeper

A R.E.P.O. mod that prevents the in-shop ShopKeeper from punishing players for "ruckus" (gunfire, explosions, melee, attacking the shopkeeper, etc.). Configurable per-player or globally.

## Features

- `DisableGlobally` — kill-switch. When true, the ShopKeeper ignores ruckus from everyone, regardless of the in-shop toggle.
- Per-player exemptions — for each player ever observed in a lobby, a `Player_<SteamID>` toggle is added to the config. Set to `true` to exempt that player from punishment. Entries are created automatically on first observation and persist across sessions.

`DisableGlobally` overrides per-player toggles.

## Configuration

Config file: `BepInEx/config/darkharasho.ChillShopKeeper.cfg`

Example after a couple of sessions:

```
[General]
DisableGlobally = false

[Players]
Player_76561198000000001 = false   ; Exempt Alice from ShopKeeper punishment
Player_76561198000000002 = true    ; Exempt Bob from ShopKeeper punishment
```

| Section | Key | Default | Description |
|---------|-----|---------|-------------|
| `[General]` | `DisableGlobally` | `false` | ShopKeeper ignores ruckus from everyone. |
| `[Players]` | `Player_<SteamID>` | `false` | When true, the named player is exempt. Auto-added on first observation. |

Compatible with [REPOConfig](https://thunderstore.io/c/repo/p/nickklmao/REPOConfig/) — toggles can be flipped in-game.

## Multiplayer

The ShopKeeper's ruckus logic runs only on the host (master client), so only the host's `ChillShopKeeper` settings matter. Clients running the mod can toggle their own copies, but those settings have no effect when they are not the host.

## Dependencies

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)

## Installation

Install via [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager) / r2modman, or manually place `ChillShopKeeper.dll` in `BepInEx/plugins/ChillShopKeeper/`.

## Building

```bash
GAME_DIR="/path/to/REPO" ./package.sh
```

Builds the DLL and produces a Thunderstore-ready zip.
```

- [ ] **Step 2: Update CLAUDE.md**

Overwrite `mods/chill-shop-keeper/CLAUDE.md`:

```markdown
## Project Context

ChillShopKeeper — a R.E.P.O. mod that prevents the in-shop ShopKeeper from punishing players for ruckus. Configurable via a global kill-switch (`DisableGlobally`) and per-player exemption toggles (`Player_<SteamID>`) that lazily appear in the config as players are observed. Host-authoritative by game design — no Photon sync.
```

- [ ] **Step 3: Update CHANGELOG.md**

Overwrite `mods/chill-shop-keeper/CHANGELOG.md`:

```markdown
# Changelog

## 0.1.0
- Initial release.
- `DisableGlobally` toggle disables all ShopKeeper ruckus punishment.
- Per-player `Player_<SteamID>` exemptions, lazily created on first observation, persist across sessions.
- REPOConfig-compatible (plain BepInEx `ConfigEntry<bool>` entries).
```

- [ ] **Step 4: Commit**

```bash
git add mods/chill-shop-keeper/README.md mods/chill-shop-keeper/CLAUDE.md mods/chill-shop-keeper/CHANGELOG.md
git commit -m "Update README, CLAUDE.md, CHANGELOG for final ChillShopKeeper design"
```

---

## Task 8: Build the Thunderstore zip and final verification

**Files:**
- (No new files; produces `builds/ChillShopKeeper-0.1.0.zip` in the monorepo root.)

- [ ] **Step 1: Ensure icon.png exists**

The `package.sh` script will fail without `icon.png` in the mod root. If it doesn't exist yet:

```bash
ls mods/chill-shop-keeper/icon.png
```

If missing, stop and inform the user — they need to supply a 256×256 icon before packaging. (The icon is intentionally not committed; `.gitignore` excludes `*.png`.) Skip this step in the dry-run if no icon is available; the zip step will fail but the build step will succeed.

- [ ] **Step 2: Run package.sh**

```bash
cd mods/chill-shop-keeper && ./package.sh
```

Or, if `GAME_DIR` isn't auto-detected:

```bash
cd mods/chill-shop-keeper && GAME_DIR=/var/mnt/data/SteamLibrary/steamapps/common/REPO ./package.sh
```

Expected output ending with `Packaged: .../builds/ChillShopKeeper-0.1.0.zip — install via r2modman.`

If `icon.png` is missing, expected: `ERROR: icon.png not found`. That's fine for a dev-loop verification; we just want the build to succeed. To verify build-only without packaging:

```bash
dotnet build mods/chill-shop-keeper/ChillShopKeeper.csproj --configuration Release /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO
```

Expected: `Build succeeded.` and `bin/Release/netstandard2.1/ChillShopKeeper.dll` exists.

- [ ] **Step 3: Run the full test suite once more**

```bash
dotnet test mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/ChillShopKeeper.Tests.csproj /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO
```

Expected: `Passed: 9, Failed: 0`.

- [ ] **Step 4: Manual integration smoke test (user-driven, not automatable)**

Hand off to the user with a checklist:

1. Install the built DLL (or zip) into r2modman / BepInEx `plugins/ChillShopKeeper/`.
2. Start a lobby. Enter the shop. Shoot inside — verify ShopKeeper escalates and eventually punishes (baseline).
3. Quit, open `BepInEx/config/darkharasho.ChillShopKeeper.cfg`. Verify a `[Players]` section exists with a `Player_<your-steam-id>` row. Set it to `true`.
4. Restart, enter shop, shoot — verify no escalation.
5. Set `DisableGlobally = true`, `Player_<your-steam-id> = false`. Verify no escalation.
6. Live-toggle test (REPOConfig): flip `DisableGlobally` mid-shop. Verify the next ruckus action behaves accordingly.
7. Multiplayer: have a friend join. Verify their `Player_<their-id>` appears after they spawn. Toggle them on, verify only they're exempt while you still get punished.

- [ ] **Step 5: Commit anything left over (CHANGELOG bump if needed) — nothing should need committing if Tasks 1-7 left a clean tree**

```bash
git status
```

Expected: clean.

---

## Self-Review Notes (writer-side check, not a task)

- **Spec coverage check:**
  - DisableGlobally → Task 4 (config) + Task 6 (patch reads it).
  - Per-player Player_<SteamID> → Task 3 (registry) + Task 5 (observer) + Task 6 (patch reads it).
  - ChillPolicy predicate → Task 2.
  - PlayerRegistry observe-and-persist → Task 3.
  - Patch on `ShopKeeper.AddRuckusScore` → Task 6.
  - Postfix on `PlayerAvatar.AddToStatsManagerRPC` → Task 5.
  - No Photon sync — correctly omitted; no task.
  - Truth table tests → Task 2.
  - Registry unit tests → Task 3.
  - Manual integration test → Task 8 step 4.
  - REPOConfig compatibility → comes free from `ConfigEntry<bool>`; documented in Task 7 README.
  - Display-name rename behavior → not testable here; documented in spec, no plan task needed.
- **Type consistency:** `ChillPolicy.ShouldSkip(bool, bool)`, `PlayerRegistry.Observe(string, string)`, `PlayerRegistry.IsExempt(string)`, `Plugin.DisableGlobally`, `Plugin.Players` — used consistently in Tasks 2, 3, 4, 5, 6.
- **Placeholders:** None — every code block is complete.
