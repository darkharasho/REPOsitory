# ForcedFriendship Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A R.E.P.O. mod that makes a player take banded damage-over-time whenever they aren't within a configurable distance of another living player — the farther past the safe radius, the bigger each tick.

**Architecture:** A pure, fully unit-tested `DamageCalculator` holds all band math and rules (nearest living other player, dead-player exclusion, solo/disabled short-circuits). A thin `ForcedFriendshipDriver` MonoBehaviour runs only on the Photon host during level gameplay: every `TickInterval` seconds it snapshots `GameDirector.instance.PlayerList`, calls the calculator, and applies results via `PlayerHealth.HurtOther` (which RPCs damage to each owning client). `Plugin` binds config and registers the driver. No Photon room-property sync is needed because the host is the sole authority that computes and applies damage — clients never read the settings.

**Tech Stack:** BepInEx 5, netstandard2.1, HarmonyX, Photon (PUN), xUnit (net6.0 test project), .NET SDK.

---

## File Structure

**Main mod** (`mods/forced-friendship/`):
- `src/DamageCalculator.cs` — **create**. Pure logic: `PlayerState`, `DamageSettings`, `DamageCalculator.Distance/Band/Evaluate`. No Unity/Photon/BepInEx types. The unit-tested core.
- `src/ForcedFriendshipDriver.cs` — **create**. MonoBehaviour tick driver. Host+gameplay gating, snapshot, apply `HurtOther`.
- `src/Plugin.cs` — **modify**. Add config binding, `IsInGameplay()` helper, register the driver component.
- `ForcedFriendship.csproj` — **modify**. Add `<Compile Remove="tests/**" />` so the mod build ignores test files.

**Test project** (`mods/forced-friendship/tests/ForcedFriendship.Tests/`):
- `ForcedFriendship.Tests.csproj` — **create**. xUnit net6.0, ProjectReference to the mod (mirrors `mods/chill-shop-keeper/tests/ChillShopKeeper.Tests`).
- `SmokeTest.cs` — **create**. Proves the runner works.
- `DamageCalculatorTests.cs` — **create**. All band/rule tests.

**Conventions to follow:**
- The test project references the mod project, which references Unity DLLs via `$(GameDir)`. Building/testing therefore requires GameDir. On this machine: `/var/mnt/data/SteamLibrary/steamapps/common/REPO/`.
- All test/build commands below run from the repo root `/var/home/mstephens/Documents/GitHub/REPOsitory`.
- `DamageCalculator` and its input structs must be `public` so the test assembly can use them.

---

## Task 1: Test project scaffold + smoke test

**Files:**
- Create: `mods/forced-friendship/tests/ForcedFriendship.Tests/ForcedFriendship.Tests.csproj`
- Create: `mods/forced-friendship/tests/ForcedFriendship.Tests/SmokeTest.cs`
- Modify: `mods/forced-friendship/ForcedFriendship.csproj`

- [ ] **Step 1: Exclude tests from the mod build**

In `mods/forced-friendship/ForcedFriendship.csproj`, add this `ItemGroup` immediately after the `<ManagedDir>` PropertyGroup block (before the `BepInEx.Core` PackageReference ItemGroup):

```xml
  <ItemGroup>
    <Compile Remove="tests/**" />
  </ItemGroup>
```

- [ ] **Step 2: Create the test project file**

Create `mods/forced-friendship/tests/ForcedFriendship.Tests/ForcedFriendship.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <IsPackable>false</IsPackable>
    <RootNamespace>ForcedFriendship.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.6" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.6" />
    <PackageReference Include="BepInEx.Core" Version="5.4.21.0" />
  </ItemGroup>

  <!--
    Copy BepInEx.dll into test output so xUnit can load BepInEx types at runtime if
    a test ever touches them. BepInEx.Core.targets intentionally strips it (mod
    assemblies are loaded by the game at runtime, which provides BepInEx.dll). The
    path version (5.4.20) is the BepInEx.BaseLib transitive of BepInEx.Core 5.4.21.0
    — update if the package bumps.
  -->
  <Target Name="CopyBepInExForTests" AfterTargets="Build">
    <Copy
      SourceFiles="$(NuGetPackageRoot)bepinex.baselib/5.4.20/lib/netstandard2.0/BepInEx.dll"
      DestinationFolder="$(OutputPath)"
      SkipUnchangedFiles="true" />
  </Target>

  <ItemGroup>
    <ProjectReference Include="..\..\ForcedFriendship.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Create the smoke test**

Create `mods/forced-friendship/tests/ForcedFriendship.Tests/SmokeTest.cs`:

```csharp
using Xunit;

namespace ForcedFriendship.Tests;

public class SmokeTest
{
    [Fact]
    public void Runner_works() => Assert.True(true);
}
```

- [ ] **Step 4: Run the smoke test to verify the runner builds and passes**

Run:
```bash
dotnet test mods/forced-friendship/tests/ForcedFriendship.Tests/ForcedFriendship.Tests.csproj \
  /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO/
```
Expected: build succeeds, `Passed!  - Failed: 0, Passed: 1`. (The mod project compiles as a dependency; this confirms GameDir + the reference chain work.)

- [ ] **Step 5: Commit**

```bash
git add mods/forced-friendship/tests mods/forced-friendship/ForcedFriendship.csproj
git commit -m "ForcedFriendship: add xUnit test project + smoke test"
```

---

## Task 2: DamageCalculator — input types, Distance, Band

**Files:**
- Create: `mods/forced-friendship/src/DamageCalculator.cs`
- Test: `mods/forced-friendship/tests/ForcedFriendship.Tests/DamageCalculatorTests.cs`

- [ ] **Step 1: Write the failing tests for Band and Distance**

Create `mods/forced-friendship/tests/ForcedFriendship.Tests/DamageCalculatorTests.cs`:

```csharp
using ForcedFriendship;
using Xunit;

namespace ForcedFriendship.Tests;

public class DamageCalculatorTests
{
    // --- Distance ---

    [Fact]
    public void Distance_is_euclidean_3d()
    {
        var a = new PlayerState(0f, 0f, 0f, alive: true);
        var b = new PlayerState(3f, 0f, 4f, alive: true);
        Assert.Equal(5f, DamageCalculator.Distance(a, b), precision: 4);
    }

    // --- Band: safeDistance=15, bandWidth=8 ---

    [Theory]
    [InlineData(14f, 0)]   // inside safe radius
    [InlineData(15f, 0)]   // exactly at safe radius -> still safe (<=)
    [InlineData(16f, 1)]   // just past -> band 1
    [InlineData(22.9f, 1)] // still band 1
    [InlineData(23f, 2)]   // exactly one bandWidth past safe -> band 2
    [InlineData(31f, 3)]   // (31-15)/8 = 2.0 -> floor 2 + 1 = band 3
    public void Band_maps_distance_to_band(float distance, int expectedBand)
    {
        Assert.Equal(expectedBand, DamageCalculator.Band(distance, safeDistance: 15f, bandWidth: 8f));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```bash
dotnet test mods/forced-friendship/tests/ForcedFriendship.Tests/ForcedFriendship.Tests.csproj \
  /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO/
```
Expected: compile error / FAIL — `PlayerState` and `DamageCalculator` do not exist yet.

- [ ] **Step 3: Create DamageCalculator with the types, Distance, and Band**

Create `mods/forced-friendship/src/DamageCalculator.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace ForcedFriendship
{
    /// <summary>A Unity-free snapshot of one player for damage evaluation.</summary>
    public readonly struct PlayerState
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly bool Alive;

        public PlayerState(float x, float y, float z, bool alive)
        {
            X = x;
            Y = y;
            Z = z;
            Alive = alive;
        }
    }

    /// <summary>The effective rule values for one damage tick.</summary>
    public readonly struct DamageSettings
    {
        public readonly bool Enabled;
        public readonly float SafeDistance;
        public readonly float BandWidth;
        public readonly int DamagePerBand;

        public DamageSettings(bool enabled, float safeDistance, float bandWidth, int damagePerBand)
        {
            Enabled = enabled;
            SafeDistance = safeDistance;
            BandWidth = bandWidth;
            DamagePerBand = damagePerBand;
        }
    }

    /// <summary>Pure banded damage-over-time math. No Unity/Photon/BepInEx dependencies.</summary>
    public static class DamageCalculator
    {
        public static float Distance(in PlayerState a, in PlayerState b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return (float)Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        }

        /// <summary>
        /// 0 when within (or exactly at) the safe radius. Otherwise the band number,
        /// where each <paramref name="bandWidth"/> units past the safe radius is one
        /// more band. A distance landing exactly on a band edge counts as the higher band.
        /// </summary>
        public static int Band(float distance, float safeDistance, float bandWidth)
        {
            if (distance <= safeDistance) return 0;
            if (bandWidth <= 0f) return 1;
            return (int)Math.Floor((distance - safeDistance) / bandWidth) + 1;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:
```bash
dotnet test mods/forced-friendship/tests/ForcedFriendship.Tests/ForcedFriendship.Tests.csproj \
  /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO/
```
Expected: `Passed!` — 1 (smoke) + 1 (distance) + 6 (band theory cases) = 8 passing.

- [ ] **Step 5: Commit**

```bash
git add mods/forced-friendship/src/DamageCalculator.cs mods/forced-friendship/tests
git commit -m "ForcedFriendship: pure DamageCalculator types, Distance, Band"
```

---

## Task 3: DamageCalculator.Evaluate — nearest player, exclusions, per-tick damage

**Files:**
- Modify: `mods/forced-friendship/src/DamageCalculator.cs`
- Test: `mods/forced-friendship/tests/ForcedFriendship.Tests/DamageCalculatorTests.cs`

- [ ] **Step 1: Write the failing tests for Evaluate**

Append these methods inside the `DamageCalculatorTests` class in `mods/forced-friendship/tests/ForcedFriendship.Tests/DamageCalculatorTests.cs` (before the closing brace):

```csharp
    // --- Evaluate ---
    // Settings used below: enabled, safeDistance=15, bandWidth=8, damagePerBand=5

    private static DamageSettings Settings(bool enabled = true) =>
        new DamageSettings(enabled, safeDistance: 15f, bandWidth: 8f, damagePerBand: 5);

    [Fact]
    public void Evaluate_player_within_safe_radius_takes_no_damage()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(10f, 0f, 0f, alive: true), // 10 units away -> safe
        };
        var result = DamageCalculator.Evaluate(players, Settings());
        Assert.Equal(new[] { 0, 0 }, result);
    }

    [Fact]
    public void Evaluate_uses_nearest_living_other_player()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(100f, 0f, 0f, alive: true), // far
            new PlayerState(31f, 0f, 0f, alive: true),  // nearest -> band 3 -> 15 dmg
        };
        var result = DamageCalculator.Evaluate(players, Settings());
        Assert.Equal(15, result[0]);
    }

    [Fact]
    public void Evaluate_ignores_dead_player_as_a_safe_anchor()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(5f, 0f, 0f, alive: false), // dead, nearby -> does NOT make safe
            new PlayerState(31f, 0f, 0f, alive: true), // nearest living -> band 3 -> 15 dmg
        };
        var result = DamageCalculator.Evaluate(players, Settings());
        Assert.Equal(15, result[0]);
    }

    [Fact]
    public void Evaluate_never_damages_a_dead_player()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: false),  // dead -> 0 regardless of distance
            new PlayerState(100f, 0f, 0f, alive: true),
        };
        var result = DamageCalculator.Evaluate(players, Settings());
        Assert.Equal(0, result[0]);
    }

    [Fact]
    public void Evaluate_lone_living_player_takes_no_damage()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),    // only living player
            new PlayerState(100f, 0f, 0f, alive: false), // everyone else dead
        };
        var result = DamageCalculator.Evaluate(players, Settings());
        Assert.Equal(0, result[0]);
    }

    [Fact]
    public void Evaluate_single_player_list_takes_no_damage()
    {
        var players = new[] { new PlayerState(0f, 0f, 0f, alive: true) };
        var result = DamageCalculator.Evaluate(players, Settings());
        Assert.Equal(new[] { 0 }, result);
    }

    [Fact]
    public void Evaluate_disabled_returns_all_zero()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(100f, 0f, 0f, alive: true), // would be heavily damaged if enabled
        };
        var result = DamageCalculator.Evaluate(players, Settings(enabled: false));
        Assert.Equal(new[] { 0, 0 }, result);
    }

    [Fact]
    public void Evaluate_returns_one_entry_per_player_with_symmetric_damage()
    {
        // Two living players 31 apart: each is the other's nearest -> both band 3 -> 15.
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(31f, 0f, 0f, alive: true),
        };
        var result = DamageCalculator.Evaluate(players, Settings());
        Assert.Equal(new[] { 15, 15 }, result);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```bash
dotnet test mods/forced-friendship/tests/ForcedFriendship.Tests/ForcedFriendship.Tests.csproj \
  /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO/
```
Expected: compile error / FAIL — `DamageCalculator.Evaluate` does not exist yet.

- [ ] **Step 3: Implement Evaluate**

Add this method to the `DamageCalculator` class in `mods/forced-friendship/src/DamageCalculator.cs` (after `Band`):

```csharp
        /// <summary>
        /// Returns the HP to apply to each player this tick (same order/length as
        /// <paramref name="players"/>). A living player is damaged by the band of the
        /// distance to its nearest living OTHER player; dead players neither anchor
        /// others nor take damage; a player with no living other player takes none.
        /// </summary>
        public static int[] Evaluate(IReadOnlyList<PlayerState> players, in DamageSettings s)
        {
            var result = new int[players.Count];
            if (!s.Enabled) return result;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState self = players[i];
                if (!self.Alive) continue;

                float nearest = float.PositiveInfinity;
                for (int j = 0; j < players.Count; j++)
                {
                    if (j == i) continue;
                    PlayerState other = players[j];
                    if (!other.Alive) continue;

                    float d = Distance(self, other);
                    if (d < nearest) nearest = d;
                }

                if (float.IsPositiveInfinity(nearest)) continue; // no living other player

                int band = Band(nearest, s.SafeDistance, s.BandWidth);
                result[i] = band * s.DamagePerBand;
            }

            return result;
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:
```bash
dotnet test mods/forced-friendship/tests/ForcedFriendship.Tests/ForcedFriendship.Tests.csproj \
  /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO/
```
Expected: `Passed!` — all 16 tests pass (1 smoke + 7 from Task 2 + 8 new).

- [ ] **Step 5: Commit**

```bash
git add mods/forced-friendship/src/DamageCalculator.cs mods/forced-friendship/tests
git commit -m "ForcedFriendship: DamageCalculator.Evaluate with nearest-player + exclusion rules"
```

---

## Task 4: Plugin config binding + IsInGameplay helper

**Files:**
- Modify: `mods/forced-friendship/src/Plugin.cs`

No unit test — this is config binding and a Unity/Photon-coupled gameplay-state check verified by a clean build. (The damage rules it feeds are already covered in Tasks 2–3.)

- [ ] **Step 1: Replace Plugin.cs with the config-bound version**

Replace the entire contents of `mods/forced-friendship/src/Plugin.cs` with:

```csharp
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;

namespace ForcedFriendship
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;

        internal static ConfigEntry<bool> Enabled = null!;
        internal static ConfigEntry<float> SafeDistance = null!;
        internal static ConfigEntry<float> BandWidth = null!;
        internal static ConfigEntry<int> DamagePerBand = null!;
        internal static ConfigEntry<float> TickInterval = null!;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Master on/off switch for Forced Friendship.");
            SafeDistance = Config.Bind("General", "SafeDistance", 15f,
                new ConfigDescription(
                    "Units within which the nearest living player keeps you safe.",
                    new AcceptableValueRange<float>(0.1f, 1000f)));
            BandWidth = Config.Bind("General", "BandWidth", 8f,
                new ConfigDescription(
                    "Units per additional damage band beyond the safe radius.",
                    new AcceptableValueRange<float>(0.1f, 1000f)));
            DamagePerBand = Config.Bind("General", "DamagePerBand", 5,
                new ConfigDescription(
                    "HP per tick, multiplied by the band number.",
                    new AcceptableValueRange<int>(1, 100)));
            TickInterval = Config.Bind("General", "TickInterval", 2f,
                new ConfigDescription(
                    "Seconds between damage evaluations.",
                    new AcceptableValueRange<float>(0.1f, 60f)));

            var harmony = new Harmony("darkharasho.ForcedFriendship");
            harmony.PatchAll();
            Log.LogInfo($"ForcedFriendship v{PluginInfo.PLUGIN_VERSION} loaded.");
        }

        /// <summary>
        /// True only when the local client is in active level gameplay (in a Photon room,
        /// on a non-lobby, non-shop level). Mirrors the proven check from mini-eepo; the
        /// shop is always excluded for Forced Friendship since it is a natural cluster zone.
        /// </summary>
        internal static bool IsInGameplay()
        {
            if (!PhotonNetwork.InRoom) return false;
            var rm = RunManager.instance;
            if (rm == null) return false;
            var current = rm.levelCurrent;
            if (current == null) return false;
            if (current == rm.levelLobby || current == rm.levelLobbyMenu) return false;
            if (SemiFunc.IsLevelShop(current)) return false;
            return true;
        }
    }
}
```

- [ ] **Step 2: Build the mod to verify it compiles against the game assemblies**

Run:
```bash
dotnet build mods/forced-friendship/ForcedFriendship.csproj --configuration Release \
  /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO/
```
Expected: `Build succeeded`. If `RunManager`, `SemiFunc.IsLevelShop`, `levelCurrent`, `levelLobby`, or `levelLobbyMenu` fail to resolve, the game API has drifted — decompile `Assembly-CSharp.dll` to find the current names (see the saved `reference_decompile_repo_assemblies` memory: `ilspycmd` via dotnet) and fix the helper before proceeding.

- [ ] **Step 3: Re-run the unit tests (mod project is a test dependency)**

Run:
```bash
dotnet test mods/forced-friendship/tests/ForcedFriendship.Tests/ForcedFriendship.Tests.csproj \
  /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO/
```
Expected: `Passed!` — all 16 tests still pass.

- [ ] **Step 4: Commit**

```bash
git add mods/forced-friendship/src/Plugin.cs
git commit -m "ForcedFriendship: bind config + IsInGameplay helper"
```

---

## Task 5: ForcedFriendshipDriver — host-side tick loop applying damage

**Files:**
- Create: `mods/forced-friendship/src/ForcedFriendshipDriver.cs`
- Modify: `mods/forced-friendship/src/Plugin.cs`

No unit test — this is Unity/Photon glue (per-frame timing, master-client gating, RPC damage application) over the already-tested calculator. Verified by build + in-game playtest.

- [ ] **Step 1: Create the driver MonoBehaviour**

Create `mods/forced-friendship/src/ForcedFriendshipDriver.cs`:

```csharp
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace ForcedFriendship
{
    /// <summary>
    /// Runs only on the Photon host during level gameplay. Every TickInterval seconds it
    /// snapshots all players, asks DamageCalculator who should bleed, and applies the
    /// damage via PlayerHealth.HurtOther (which RPCs to each owning client).
    /// </summary>
    internal class ForcedFriendshipDriver : MonoBehaviour
    {
        private float _accum;
        private readonly List<PlayerState> _states = new List<PlayerState>();
        private readonly List<PlayerAvatar> _avatars = new List<PlayerAvatar>();

        private void Update()
        {
            if (!Plugin.Enabled.Value) return;

            // Only the host computes and applies damage. (IsInGameplay also requires a room.)
            if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient) return;
            if (!Plugin.IsInGameplay()) return;

            _accum += Time.deltaTime;
            if (_accum < Plugin.TickInterval.Value) return;
            _accum = 0f;

            var list = GameDirector.instance?.PlayerList;
            if (list == null) return;

            _states.Clear();
            _avatars.Clear();
            foreach (var pa in list)
            {
                if (pa == null) continue;
                Vector3 pos = pa.transform.position;
                bool alive = !pa.deadSet && !pa.isDisabled;
                _states.Add(new PlayerState(pos.x, pos.y, pos.z, alive));
                _avatars.Add(pa);
            }

            var settings = new DamageSettings(
                enabled: true,
                safeDistance: Plugin.SafeDistance.Value,
                bandWidth: Plugin.BandWidth.Value,
                damagePerBand: Plugin.DamagePerBand.Value);

            int[] damage = DamageCalculator.Evaluate(_states, settings);
            for (int i = 0; i < damage.Length; i++)
            {
                if (damage[i] <= 0) continue;
                PlayerAvatar pa = _avatars[i];
                if (pa.playerHealth == null) continue;
                pa.playerHealth.HurtOther(damage[i], pa.transform.position, savingGrace: false);
            }
        }
    }
}
```

- [ ] **Step 2: Register the driver in Plugin.Awake**

In `mods/forced-friendship/src/Plugin.cs`, add the component registration as the last line of `Awake()`, immediately after the `Log.LogInfo(...)` line:

```csharp
            gameObject.AddComponent<ForcedFriendshipDriver>();
```

- [ ] **Step 3: Build the mod to verify the game-API calls resolve**

Run:
```bash
dotnet build mods/forced-friendship/ForcedFriendship.csproj --configuration Release \
  /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO/
```
Expected: `Build succeeded`. If `PlayerAvatar.deadSet`, `PlayerAvatar.isDisabled`, `PlayerAvatar.playerHealth`, `PlayerHealth.HurtOther`, or `GameDirector.instance.PlayerList` fail to resolve, decompile `Assembly-CSharp.dll` (see `reference_decompile_repo_assemblies` memory) to find the current member names and adjust. `HurtOther(int damage, Vector3 hurtPosition, bool savingGrace, int enemyIndex = -1, bool hurtByHeal = false)` is the expected signature — the first three args are what we pass.

- [ ] **Step 4: Re-run the unit tests**

Run:
```bash
dotnet test mods/forced-friendship/tests/ForcedFriendship.Tests/ForcedFriendship.Tests.csproj \
  /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO/
```
Expected: `Passed!` — all 16 tests still pass.

- [ ] **Step 5: Commit**

```bash
git add mods/forced-friendship/src/ForcedFriendshipDriver.cs mods/forced-friendship/src/Plugin.cs
git commit -m "ForcedFriendship: host-side damage tick driver"
```

---

## Task 6: Docs + package smoke check

**Files:**
- Modify: `mods/forced-friendship/CHANGELOG.md`
- Modify: `mods/forced-friendship/README.md`

- [ ] **Step 1: Update the changelog**

Replace the contents of `mods/forced-friendship/CHANGELOG.md` with:

```markdown
# Changelog

## 0.1.0
- Initial release: take banded damage over time when not within a configurable
  distance of another living player. The farther past the safe radius, the
  bigger each tick. Host-authoritative; gameplay levels only; dead players are
  ignored as both anchors and targets.
- Config: `Enabled`, `SafeDistance`, `BandWidth`, `DamagePerBand`, `TickInterval`.
```

- [ ] **Step 2: Document the config table in the README**

In `mods/forced-friendship/README.md`, replace the line:

```markdown
Config file: `BepInEx/config/darkharasho.ForcedFriendship.cfg`
```

with:

```markdown
Config file: `BepInEx/config/darkharasho.ForcedFriendship.cfg`

| Key | Default | Meaning |
|-----|---------|---------|
| `Enabled` | `true` | Master on/off switch |
| `SafeDistance` | `15` | Units within which the nearest living player keeps you safe |
| `BandWidth` | `8` | Units per additional damage band beyond the safe radius |
| `DamagePerBand` | `5` | HP per tick, multiplied by the band number |
| `TickInterval` | `2.0` | Seconds between damage evaluations |

Only the host's settings apply in multiplayer — the host computes distances and
applies damage to everyone. The mod is active during level gameplay only (not the
shop, truck, or lobby), and dead players are never damaged.
```

- [ ] **Step 3: Verify the full Release build one more time**

Run:
```bash
dotnet build mods/forced-friendship/ForcedFriendship.csproj --configuration Release \
  /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO/
```
Expected: `Build succeeded`. (Packaging to a Thunderstore zip via `/build forced-friendship` additionally requires a 256×256 `icon.png`, which is user-supplied and not part of this plan.)

- [ ] **Step 4: Commit**

```bash
git add mods/forced-friendship/CHANGELOG.md mods/forced-friendship/README.md
git commit -m "ForcedFriendship: changelog + README config table for 0.1.0"
```

---

## Done

All spec requirements are implemented and the risky logic is unit-tested. Remaining manual step before shipping: add `icon.png` and run `/build forced-friendship`, then in-game playtest to tune the default `SafeDistance` / `BandWidth` / `DamagePerBand` / `TickInterval` to R.E.P.O.'s world scale.
