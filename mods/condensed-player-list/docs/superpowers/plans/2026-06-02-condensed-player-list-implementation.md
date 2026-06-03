# CondensedPlayerList Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Condense R.E.P.O.'s single-column lobby/ESC player list by tightening the per-row vertical spacing so large lobbies stay on screen.

**Architecture:** A pure, Unity-free static function computes each entry's `(x, y)` from its `listSpot` and context. A single Harmony `Postfix` on `MenuPlayerListed.Update()` calls it and overwrites the entry's `localPosition` after vanilla sets it each frame. `forceCrown` (arena-winner) entries are left untouched.

**Tech Stack:** C# / netstandard2.1, BepInEx 5, HarmonyX, xUnit (net6.0) for the pure-logic tests. Game types from `Assembly-CSharp.dll` (`MenuPlayerListed`, `SemiFunc`).

---

## Reference — vanilla behavior being overridden

From decompiled `Assembly-CSharp.dll`, `MenuPlayerListed.Update()` ends with:

```csharp
if (!forceCrown)
{
    if (!SemiFunc.RunIsLobbyMenu())                          // ESC / pause menu
        base.transform.localPosition = new Vector3(-23f, -listSpot * 22, 0f);
    else                                                      // lobby menu
        base.transform.localPosition = new Vector3(0f, -listSpot * 32, 0f);
}
```

`listSpot` is `internal int`, `forceCrown` is `public bool`, both on `MenuPlayerListed`. `SemiFunc.RunIsLobbyMenu()` is a public static `bool`. Vanilla spacing: lobby 32, ESC 22. Condensed targets: lobby 22, ESC 15 (tuned later).

## File Structure

- Create: `mods/condensed-player-list/src/CondensedLayout.cs` — pure layout math, no Unity references. Returns `(float X, float Y)`.
- Create: `mods/condensed-player-list/src/Patches/MenuPlayerListedPatches.cs` — Harmony postfix, converts the tuple to `Vector3` and assigns it.
- Create: `mods/condensed-player-list/tests/CondensedPlayerList.Tests/CondensedPlayerList.Tests.csproj` — xUnit test project referencing the mod csproj.
- Create: `mods/condensed-player-list/tests/CondensedPlayerList.Tests/CondensedLayoutTests.cs` — tests for the pure math.
- Modify: none (`Plugin.cs` already calls `harmony.PatchAll()`; the attributed patch class is auto-discovered).

`CondensedLayout` returns a plain value tuple rather than `UnityEngine.Vector2` so the test project never needs to load Unity assemblies at runtime — only the patch (which already runs inside the game) touches Unity types.

---

## Task 1: Pure condensed-layout math

**Files:**
- Create: `mods/condensed-player-list/src/CondensedLayout.cs`
- Create: `mods/condensed-player-list/tests/CondensedPlayerList.Tests/CondensedPlayerList.Tests.csproj`
- Test: `mods/condensed-player-list/tests/CondensedPlayerList.Tests/CondensedLayoutTests.cs`

- [ ] **Step 1: Create the test project file**

Create `mods/condensed-player-list/tests/CondensedPlayerList.Tests/CondensedPlayerList.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <IsPackable>false</IsPackable>
    <RootNamespace>CondensedPlayerList.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.6" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.6" />
    <PackageReference Include="BepInEx.Core" Version="5.4.21.0" />
  </ItemGroup>

  <!-- BepInEx.Core.targets strips BepInEx.dll from copy-local for mod builds,
       but the referenced mod assembly may need it present at runtime. Copy it explicitly. -->
  <Target Name="CopyBepInExForTests" AfterTargets="Build">
    <Copy
      SourceFiles="$(NuGetPackageRoot)bepinex.baselib/5.4.20/lib/netstandard2.0/BepInEx.dll"
      DestinationFolder="$(OutputPath)"
      SkipUnchangedFiles="true" />
  </Target>

  <ItemGroup>
    <ProjectReference Include="..\..\CondensedPlayerList.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write the failing test**

Create `mods/condensed-player-list/tests/CondensedPlayerList.Tests/CondensedLayoutTests.cs`:

```csharp
using CondensedPlayerList;
using Xunit;

namespace CondensedPlayerList.Tests;

public class CondensedLayoutTests
{
    [Theory]
    [InlineData(true,  0)]   // lobby, first row
    [InlineData(false, 0)]   // esc, first row
    public void FirstRow_isAtOrigin_y(bool isLobby, int listSpot)
    {
        var (_, y) = CondensedLayout.CondensedPosition(listSpot, isLobby);
        Assert.Equal(0f, y);
    }

    [Fact]
    public void Lobby_usesCondensedSpacing_22()
    {
        var (x, y) = CondensedLayout.CondensedPosition(3, isLobby: true);
        Assert.Equal(0f, x);            // lobby X preserved
        Assert.Equal(-66f, y);          // -3 * 22
    }

    [Fact]
    public void Esc_usesCondensedSpacing_15()
    {
        var (x, y) = CondensedLayout.CondensedPosition(3, isLobby: false);
        Assert.Equal(-23f, x);          // esc X preserved
        Assert.Equal(-45f, y);          // -3 * 15
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Spacing_isTighterThanVanilla(bool isLobby)
    {
        float vanilla = isLobby ? 32f : 22f;
        var (_, y1) = CondensedLayout.CondensedPosition(1, isLobby);
        Assert.True(-y1 < vanilla, $"row gap {-y1} should be < vanilla {vanilla}");
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

```bash
cd mods/condensed-player-list
dotnet test tests/CondensedPlayerList.Tests/CondensedPlayerList.Tests.csproj /p:GameDir="$GAME_DIR"
```

Expected: FAIL — compile error, `CondensedLayout` does not exist. (`$GAME_DIR` must point at the R.E.P.O. install so the referenced mod csproj resolves its Unity/Assembly-CSharp HintPaths, e.g. `/var/mnt/data/SteamLibrary/steamapps/common/REPO`.)

- [ ] **Step 4: Write the minimal implementation**

Create `mods/condensed-player-list/src/CondensedLayout.cs`:

```csharp
namespace CondensedPlayerList
{
    /// <summary>
    /// Pure layout math for the condensed single-column player list.
    /// No Unity dependency so it can be unit-tested in isolation.
    /// </summary>
    public static class CondensedLayout
    {
        // Vanilla per-row spacing (from MenuPlayerListed.Update): lobby 32, esc 22.
        // Condensed targets — tighter gaps so more rows fit on screen. Tuned in-game.
        public const float LobbySpacing = 22f;
        public const float EscSpacing = 15f;

        // X positions preserved exactly from vanilla per context.
        public const float LobbyX = 0f;
        public const float EscX = -23f;

        /// <summary>
        /// Returns the condensed local position (x, y) for a list entry.
        /// </summary>
        public static (float X, float Y) CondensedPosition(int listSpot, bool isLobby)
        {
            float x = isLobby ? LobbyX : EscX;
            float spacing = isLobby ? LobbySpacing : EscSpacing;
            return (x, -listSpot * spacing);
        }
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

```bash
cd mods/condensed-player-list
dotnet test tests/CondensedPlayerList.Tests/CondensedPlayerList.Tests.csproj /p:GameDir="$GAME_DIR"
```

Expected: PASS — all 6 test cases green.

- [ ] **Step 6: Commit**

```bash
git add mods/condensed-player-list/src/CondensedLayout.cs \
        mods/condensed-player-list/tests/
git commit -m "CondensedPlayerList: pure condensed-layout math + tests"
```

---

## Task 2: Harmony postfix patch

**Files:**
- Create: `mods/condensed-player-list/src/Patches/MenuPlayerListedPatches.cs`

This task has no unit test — it wires the pure math into a Harmony patch against a game type that only exists at runtime in-game. Correctness of the math is covered by Task 1; correctness of the wiring is verified in-game in Task 3.

- [ ] **Step 1: Write the patch class**

Create `mods/condensed-player-list/src/Patches/MenuPlayerListedPatches.cs`:

```csharp
using System;
using HarmonyLib;
using UnityEngine;

namespace CondensedPlayerList.Patches
{
    // Vanilla MenuPlayerListed.Update() sets localPosition every frame as one tall
    // column (lobby spacing 32, esc 22). This postfix runs after and overwrites it
    // with the condensed spacing. forceCrown entries (arena winners) are left alone.
    [HarmonyPatch(typeof(MenuPlayerListed), "Update")]
    internal static class MenuPlayerListed_Update_Postfix
    {
        private static readonly AccessTools.FieldRef<MenuPlayerListed, int> ListSpotRef =
            AccessTools.FieldRefAccess<MenuPlayerListed, int>("listSpot");

        private static void Postfix(MenuPlayerListed __instance)
        {
            try
            {
                if (__instance == null || __instance.forceCrown)
                    return;

                int listSpot = ListSpotRef(__instance);
                bool isLobby = SemiFunc.RunIsLobbyMenu();
                var (x, y) = CondensedLayout.CondensedPosition(listSpot, isLobby);
                __instance.transform.localPosition = new Vector3(x, y, 0f);
            }
            catch (Exception ex)
            {
                // Never let a per-frame postfix throw repeatedly.
                Plugin.Log.LogError($"MenuPlayerListed Update postfix failed: {ex}");
            }
        }
    }
}
```

Notes for the implementer:
- `listSpot` is `internal`, so it is read via `AccessTools.FieldRefAccess` (same pattern as `ChillShopKeeper`'s `steamID` ref). `forceCrown` is `public` and accessed directly.
- `__instance.transform` is `UnityEngine.Component.transform` — available without reflection.
- The postfix overwrites the same `localPosition` vanilla just set, so order vs. vanilla is guaranteed (postfix always runs after).

- [ ] **Step 2: Build the mod to verify it compiles**

```bash
cd mods/condensed-player-list
dotnet build CondensedPlayerList.csproj --configuration Release /p:GameDir="$GAME_DIR"
```

Expected: Build succeeded, `bin/Release/netstandard2.1/CondensedPlayerList.dll` produced, no warnings about unresolved `MenuPlayerListed` / `SemiFunc`.

- [ ] **Step 3: Re-run the unit tests (nothing should have broken)**

```bash
cd mods/condensed-player-list
dotnet test tests/CondensedPlayerList.Tests/CondensedPlayerList.Tests.csproj /p:GameDir="$GAME_DIR"
```

Expected: PASS — all Task 1 tests still green.

- [ ] **Step 4: Commit**

```bash
git add mods/condensed-player-list/src/Patches/MenuPlayerListedPatches.cs
git commit -m "CondensedPlayerList: Harmony postfix condensing the player list"
```

---

## Task 3: Package, in-game verification, and tuning

**Files:**
- Modify (tuning only, if needed): `mods/condensed-player-list/src/CondensedLayout.cs` (`LobbySpacing` / `EscSpacing`)

- [ ] **Step 1: Add an icon and package**

A 256×256 `icon.png` must exist in `mods/condensed-player-list/` before packaging (not committed). Then:

```bash
cd mods/condensed-player-list
GAME_DIR="$GAME_DIR" ./package.sh
```

Expected: `Packaged: <repo>/builds/CondensedPlayerList-0.1.0.zip`.

(Alternatively run the `/build condensed-player-list` skill.)

- [ ] **Step 2: Install and verify in-game**

Install the zip via r2modman, launch R.E.P.O., and check:
- **Lobby menu:** the player list rows are visibly tighter than vanilla and a full lobby fits on screen.
- **ESC / pause menu (in a run):** same — tighter rows, no overflow.
- Player **heads** and **name labels** track the tighter rows (see risk below).
- An **arena winner** (crown) entry is positioned exactly as vanilla (unaffected).

- [ ] **Step 3: Tune spacing if needed**

If rows are too tight or too loose, adjust `LobbySpacing` / `EscSpacing` in `src/CondensedLayout.cs`, update the corresponding expected values in `CondensedLayoutTests.cs`, re-run `dotnet test`, rebuild, and re-verify in-game.

- [ ] **Step 4: Handle head-focus drift if observed**

Risk: `MenuPlayerListed` player heads render via focus points parented to the list container, not the entry transform, so they may not follow the tighter rows. If heads visibly drift from their rows in Step 2:
- Inspect the head focus fields on `MenuPlayerListed` (`playerHead.myFocusPoint` / `playerHead.focusPoint`) in the decompiled source.
- Apply a matching condensed offset to the head focus point in the same postfix (reuse `CondensedPosition`).
- If heads track correctly, do nothing — no extra code.

- [ ] **Step 5: Update changelog and commit**

Edit `mods/condensed-player-list/CHANGELOG.md`:

```markdown
# Changelog

## 0.1.0
- Initial release: condenses the lobby and ESC-menu player list into tighter single-column rows so large lobbies fit on screen.
```

```bash
git add mods/condensed-player-list/CHANGELOG.md mods/condensed-player-list/src/CondensedLayout.cs
git commit -m "CondensedPlayerList: tune spacing and finalize 0.1.0 changelog"
```

---

## Self-Review

**Spec coverage:**
- Single Harmony postfix on `MenuPlayerListed.Update()` → Task 2. ✓
- Condensed fixed spacing lobby 32→22, ESC 22→15 → `CondensedLayout` constants, Task 1. ✓
- X preserved (lobby 0, ESC -23) → `LobbyX`/`EscX`, Task 1. ✓
- `forceCrown` entries skipped → Task 2 postfix guard. ✓
- Context via `SemiFunc.RunIsLobbyMenu()` → Task 2. ✓
- Pure `CondensedPosition` unit-tested first (TDD) → Task 1. ✓
- Patch in `src/Patches/MenuPlayerListedPatches.cs` → Task 2. ✓
- Build via package.sh / build skill; tune in-game → Task 3. ✓
- Risk: head focus points may need parallel offset → Task 3 Step 4. ✓
- No Photon/config (out of scope) → not implemented. ✓

**Placeholder scan:** none — every code/command step shows full content.

**Type consistency:** `CondensedLayout.CondensedPosition(int, bool) → (float X, float Y)`, `LobbySpacing`/`EscSpacing`/`LobbyX`/`EscX` consts, and `ListSpotRef` are referenced identically across Task 1 and Task 2.
