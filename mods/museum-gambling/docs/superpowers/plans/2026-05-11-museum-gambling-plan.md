# MuseumGambling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a BepInEx 5 R.E.P.O. mod where clicking the Museum Head triggers a host-authoritative roll — by default 5% of the time a 50,000-value money-bag valuable spawns instead of the head dealing its normal damage.

**Architecture:** A single Harmony prefix on the `MuseumPropMoneyHead` damage call. On host (or singleplayer) the prefix rolls; on a win it suppresses the damage and spawns a money-bag valuable at the head's position; on a loss it returns `true` so vanilla damage runs. No Photon room-property sync — host config decides everything. Pure logic (the win/lose decision) is unit-tested; patches are integration-tested manually.

**Tech Stack:** netstandard2.1, BepInEx 5.4.21, HarmonyX (via `BepInEx.Core`), C# `Nullable enable`, xUnit 2.6.6 on net6.0 for tests.

**Spec:** `mods/museum-gambling/docs/superpowers/specs/2026-05-11-museum-gambling-design.md` — read this first if anything below is ambiguous.

---

## File Structure

After this plan completes, the mod tree will look like:

```
mods/museum-gambling/
  src/
    Plugin.cs                              # BepInEx entry, config binding, Harmony.PatchAll
    Outcome.cs                             # Pure static helper: ShouldWin(roll, winChancePercent)
    Payout.cs                              # Spawns the money-bag valuable, host-side
    Patches/
      MuseumHeadPatches.cs                 # Harmony prefix on the head's damage method
  tests/
    MuseumGambling.Tests/
      MuseumGambling.Tests.csproj          # xUnit on net6.0, references main csproj
      OutcomeTests.cs                      # 8 cases per spec
  .research/                               # GITIGNORED — decompiled C# scratch
    MuseumPropMoneyHead.cs
    Valuables.cs
    NOTES.md                               # Resolved patch target + spawn API
  manifest.json                            # existing (version bumps live here)
  MuseumGambling.csproj                    # existing
  package.sh                                # existing
  README.md                                # gets a Config section update in Phase 5
  CHANGELOG.md                             # gets the 0.1.0 entry in Phase 5
  .gitignore                               # add `.research/`
  src/Plugin.cs                            # currently a no-op skeleton, grown in Phase 2
```

**Responsibility split:**
- `Outcome.cs` — pure logic only, no Unity/BepInEx refs. Unit-testable without the game.
- `Payout.cs` — all game-side spawn API knowledge. Easy to swap if the API changes.
- `Patches/MuseumHeadPatches.cs` — wiring only. Decides host vs client, rolls, calls into `Outcome` and `Payout`.
- `Plugin.cs` — config binding + Harmony lifecycle. All static config refs live here as `internal static`, matching `mods/chill-shop-keeper/src/Plugin.cs`.

---

## Phase 0 — Research (resolve the two unknowns from the spec)

The spec has two unresolved research items. Phase 0 produces concrete answers and writes them to `.research/NOTES.md`. **If findings substantively change the design (e.g. there's no single damage-step method, or the spawn API requires per-client RPCs), STOP and loop back to the user before continuing.**

### Task 0.1: Set up a decompiler

**Files:**
- Create: `mods/museum-gambling/.research/` (directory)
- Modify: `mods/museum-gambling/.gitignore` — add `.research/` entry

The host machine has `dotnet 6` (`/home/linuxbrew/.linuxbrew/bin/dotnet`); `ilspycmd` is installed as a global tool but requires .NET 8. We need a working decompiler.

- [ ] **Step 1: Check whether dotnet 8 can be installed via brew without disturbing the existing dotnet 6**

```bash
brew search dotnet@
```

Expected: shows `dotnet@8` (or similar) available. If yes, proceed to step 2. If not, jump to step 3 (portable install).

- [ ] **Step 2: Install dotnet 8 via brew**

```bash
brew install dotnet@8
brew link --overwrite dotnet@8 || true
dotnet --list-runtimes | grep '8\.'
```

Expected: at least one `Microsoft.NETCore.App 8.x.x` entry. If this works, skip step 3 and go to step 4.

- [ ] **Step 3 (fallback): Portable dotnet 8 install**

```bash
mkdir -p ~/.dotnet8
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir "$HOME/.dotnet8"
"$HOME/.dotnet8/dotnet" --list-runtimes | grep '8\.'
```

Expected: at least one `Microsoft.NETCore.App 8.x.x` entry. Then re-target `ilspycmd` by setting `DOTNET_ROOT=$HOME/.dotnet8` when invoking it.

- [ ] **Step 4: Verify ilspycmd runs**

```bash
# Use whichever dotnet from step 2 or 3
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet8}" PATH="$DOTNET_ROOT:$PATH" \
  ~/.dotnet/tools/ilspycmd --help 2>&1 | head -5
```

Expected: prints ilspycmd help text (no "must install .NET" error).

- [ ] **Step 5: Create research scratch dir + gitignore entry**

```bash
mkdir -p mods/museum-gambling/.research
```

Edit `mods/museum-gambling/.gitignore` — append at the end:

```
# Phase 0 decompilation scratch — not committed
.research/
```

- [ ] **Step 6: Commit**

```bash
git add mods/museum-gambling/.gitignore
git commit -m "Ignore Phase 0 .research/ scratch dir"
```

---

### Task 0.2: Decompile MuseumPropMoneyHead and find the damage-step method

**Files:**
- Create: `mods/museum-gambling/.research/MuseumPropMoneyHead.cs`
- Create: `mods/museum-gambling/.research/NOTES.md` (start it)

- [ ] **Step 1: Decompile the class**

```bash
cd mods/museum-gambling
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet8}" PATH="$DOTNET_ROOT:$PATH" \
  ~/.dotnet/tools/ilspycmd \
  /var/mnt/data/SteamLibrary/steamapps/common/REPO/REPO_Data/Managed/Assembly-CSharp.dll \
  -t MuseumPropMoneyHead \
  > .research/MuseumPropMoneyHead.cs
wc -l .research/MuseumPropMoneyHead.cs
```

Expected: file is non-empty (likely 100–500 lines).

- [ ] **Step 2: Identify the damage call site**

Read `.research/MuseumPropMoneyHead.cs` and find:

1. The grab/click entry point — likely overrides `OnGrabbed`, `OnPrimaryActionDown`, or similar on a base interactable class.
2. The suck-in implementation — likely a coroutine (look for `IEnumerator`) or an `Update`/`FixedUpdate`-driven state machine.
3. The damage application — look for calls to `PlayerAvatar.PlayerHurt`, `PlayerHealth.Hurt`, `.Hurt(`, or a `PunRPC` like `DamageRPC` / `HurtRPC`.

Document the finding in `.research/NOTES.md`:

```markdown
# Research Notes — MuseumGambling

## Patch target on MuseumPropMoneyHead

- Method to patch: `MuseumPropMoneyHead.<ExactMethodName>`
- Signature: `<return-type> <Name>(<params>)`
- How clicking PlayerAvatar is reachable from the prefix:
  - [ ] via `__instance` field `<fieldName>` (e.g. `__instance._grabber`)
  - [ ] via `__args[N]` (parameter index N is `PlayerAvatar`)
  - [ ] not directly reachable — must read from a singleton (e.g. `PlayerAvatar.instance`)
- Whether the method is a PunRPC: yes / no
- Whether returning false from the prefix correctly suppresses ONLY the damage (not the suck-in animation): yes / no — explain

## Acceptance check
- The method runs once per click, AFTER suck-in plays, on the host. Confirmed by reading IL/decompilation: <quote 3-5 lines showing the damage call>.
```

- [ ] **Step 3: If no single clean damage-step method exists, STOP**

If the damage is applied inline in a coroutine without a callable seam, or if the only damage call also drives the suck-in animation (so suppressing it kills the animation), this design needs to change. STOP, summarize the finding, and loop back to the user with options (e.g. detour the damage call, patch `PlayerAvatar.PlayerHurt` with a context flag, or restructure to "spawn AND damage" rather than "spawn instead of damage").

Acceptance criteria for proceeding: the NOTES.md entry above is filled in with a concrete method name and the answer to "does returning false suppress ONLY damage" is **yes**.

- [ ] **Step 4: Commit the notes (not the decompilation scratch — it's gitignored)**

```bash
# Notes are inside .research/ which is gitignored, so we have to force-add the file
# OR move NOTES.md outside .research/. Move it:
mv .research/NOTES.md docs/superpowers/RESEARCH.md
git add docs/superpowers/RESEARCH.md
git commit -m "Document MuseumPropMoneyHead patch target"
```

(For the rest of the plan, references to "RESEARCH.md" mean `mods/museum-gambling/docs/superpowers/RESEARCH.md`.)

---

### Task 0.3: Identify the money-bag valuable + spawn API

**Files:**
- Create: `mods/museum-gambling/.research/Valuables.cs`
- Modify: `mods/museum-gambling/docs/superpowers/RESEARCH.md`

- [ ] **Step 1: Find candidate valuable / spawner classes**

```bash
strings /var/mnt/data/SteamLibrary/steamapps/common/REPO/REPO_Data/Managed/Assembly-CSharp.dll \
  | grep -iE "^(Valuable|MoneyBag|Director|ValuableObject)" | sort -u | head -40
```

Expected: a list including likely names such as `ValuableObject`, `ValuableDirector`, `ValuablePropSwitch`, `MoneyBag`, etc.

- [ ] **Step 2: Decompile the top candidates**

For each interesting class name from step 1 (focus on `ValuableObject`, `ValuableDirector`, and anything matching `*MoneyBag*` or `*Valuable*Spawner*`):

```bash
cd mods/museum-gambling
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet8}" PATH="$DOTNET_ROOT:$PATH" \
  ~/.dotnet/tools/ilspycmd \
  /var/mnt/data/SteamLibrary/steamapps/common/REPO/REPO_Data/Managed/Assembly-CSharp.dll \
  -t ValuableObject -t ValuableDirector \
  > .research/Valuables.cs
```

(Adjust `-t` args to whatever step 1 returned.)

- [ ] **Step 3: Resolve the spawn API**

Read `.research/Valuables.cs` and document in `docs/superpowers/RESEARCH.md`:

```markdown
## Money-bag spawn API

- Prefab name / asset reference for the money bag: `<exact string or asset path>`
- Spawn method: `<ClassName>.<MethodName>(<signature>)`
- Whether the method internally calls `PhotonNetwork.Instantiate`: yes / no
- Field/property for the per-instance dollar value: `<ClassName>.<fieldName>` (type: `<int/float>`)
- Whether the value must be set before or after the spawn call to replicate to clients: before / after / via PunRPC named `<X>`
- Code shape we'll use in Payout.cs:

\`\`\`csharp
// pseudo
var go = ValuableDirector.instance.SpawnSomething(prefab, position, rotation);
var v = go.GetComponent<ValuableObject>();
v.dollarValue = value;            // or v.SetValueRPC(value)
\`\`\`
```

- [ ] **Step 4: If the spawn API is host-only (uses `PhotonNetwork.IsMasterClient` internally) — note that**

This affects whether we need an explicit `IsMasterClient` guard inside `Payout.Spawn`. Note the answer.

- [ ] **Step 5: If no clean money-bag prefab exists, STOP and loop back**

Fallback options to discuss with user: (a) use a different valuable prefab and just override its value to 50k, (b) credit the extraction haul directly (changes the spec).

- [ ] **Step 6: Commit**

```bash
git add mods/museum-gambling/docs/superpowers/RESEARCH.md
git commit -m "Document money-bag valuable + spawn API"
```

---

## Phase 1 — Pure logic (TDD)

### Task 1.1: Create the test project

**Files:**
- Create: `mods/museum-gambling/tests/MuseumGambling.Tests/MuseumGambling.Tests.csproj`
- Create: `mods/museum-gambling/tests/MuseumGambling.Tests/SmokeTest.cs`

Lift the proven shape from `mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/ChillShopKeeper.Tests.csproj` — same xUnit version, same BepInEx-copy workaround.

- [ ] **Step 1: Write the csproj**

`mods/museum-gambling/tests/MuseumGambling.Tests/MuseumGambling.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <IsPackable>false</IsPackable>
    <RootNamespace>MuseumGambling.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.6" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.6" />
    <PackageReference Include="BepInEx.Core" Version="5.4.21.0" />
  </ItemGroup>

  <!-- BepInEx.Core.targets strips BepInEx.dll from copy-local for mod builds,
       but unit tests need it present at runtime. Copy it explicitly after build. -->
  <Target Name="CopyBepInExForTests" AfterTargets="Build">
    <Copy
      SourceFiles="$(NuGetPackageRoot)bepinex.baselib/5.4.20/lib/netstandard2.0/BepInEx.dll"
      DestinationFolder="$(OutputPath)"
      SkipUnchangedFiles="true" />
  </Target>

  <ItemGroup>
    <ProjectReference Include="..\..\MuseumGambling.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write the smoke test**

`mods/museum-gambling/tests/MuseumGambling.Tests/SmokeTest.cs`:

```csharp
using Xunit;

namespace MuseumGambling.Tests;

public class SmokeTest
{
    [Fact]
    public void Runner_works() => Assert.True(true);
}
```

- [ ] **Step 3: Build and run**

```bash
cd mods/museum-gambling/tests/MuseumGambling.Tests
dotnet test
```

Expected: 1 test passed. If the build fails because the main csproj references game DLLs that aren't on the test runner's path, do **not** "fix" by adding the game refs to the test csproj. The main csproj already has `<Private>false</Private>` on game DLL refs — the test runner will just resolve types it actually uses. If types it does use are missing, restructure to keep `Outcome.cs` free of Unity dependencies (which it should be — pure ints).

- [ ] **Step 4: Commit**

```bash
git add mods/museum-gambling/tests/
git commit -m "Add xUnit test project for MuseumGambling"
```

---

### Task 1.2: TDD `Outcome.ShouldWin`

**Files:**
- Create: `mods/museum-gambling/tests/MuseumGambling.Tests/OutcomeTests.cs`
- Create: `mods/museum-gambling/src/Outcome.cs`

- [ ] **Step 1: Write the failing tests**

`mods/museum-gambling/tests/MuseumGambling.Tests/OutcomeTests.cs`:

```csharp
using Xunit;

namespace MuseumGambling.Tests;

public class OutcomeTests
{
    [Theory]
    [InlineData(1, 0, false)]      // never-win, lowest roll → lose
    [InlineData(100, 0, false)]    // never-win, highest roll → lose
    [InlineData(1, 100, true)]     // always-win, lowest roll → win
    [InlineData(100, 100, true)]   // always-win, highest roll → win
    [InlineData(5, 5, true)]       // boundary: roll equals chance → win
    [InlineData(6, 5, false)]      // boundary: roll just above chance → lose
    [InlineData(50, -1, false)]    // defensive: negative chance → never wins
    [InlineData(50, 101, true)]    // defensive: >100 chance → always wins (rolls capped 1..100)
    public void ShouldWin_truth_table(int roll, int winChancePercent, bool expected)
        => Assert.Equal(expected, Outcome.ShouldWin(roll, winChancePercent));
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

```bash
cd mods/museum-gambling/tests/MuseumGambling.Tests
dotnet test
```

Expected: compile error — `Outcome` is not defined. (That's the failing state.)

- [ ] **Step 3: Implement the minimal `Outcome.cs`**

`mods/museum-gambling/src/Outcome.cs`:

```csharp
namespace MuseumGambling;

internal static class Outcome
{
    internal static bool ShouldWin(int roll, int winChancePercent)
        => winChancePercent > 0 && roll <= winChancePercent;
}
```

Note: this class is `internal`. For xUnit to see it, add `InternalsVisibleTo` to the main csproj.

- [ ] **Step 4: Add `InternalsVisibleTo` to the main csproj**

Edit `mods/museum-gambling/MuseumGambling.csproj` — inside the first `<PropertyGroup>` (the one with `<AssemblyName>`), add:

```xml
    <InternalsVisibleTo Include="MuseumGambling.Tests" />
```

Wait — `InternalsVisibleTo` is an `<ItemGroup>` entry in modern SDK projects, not a property. The correct addition is a new ItemGroup:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="MuseumGambling.Tests" />
  </ItemGroup>
```

Add this ItemGroup directly after the existing `<PackageReference>` ItemGroup.

- [ ] **Step 5: Run the tests and confirm they pass**

```bash
cd mods/museum-gambling/tests/MuseumGambling.Tests
dotnet test
```

Expected: 9 tests passed (8 theory cases + 1 smoke).

- [ ] **Step 6: Commit**

```bash
git add mods/museum-gambling/src/Outcome.cs \
        mods/museum-gambling/tests/MuseumGambling.Tests/OutcomeTests.cs \
        mods/museum-gambling/MuseumGambling.csproj
git commit -m "Add Outcome.ShouldWin with full truth-table tests"
```

---

## Phase 2 — Config binding

### Task 2.1: Grow Plugin.cs into the spec's shape

**Files:**
- Modify: `mods/museum-gambling/src/Plugin.cs`

- [ ] **Step 1: Replace `src/Plugin.cs` with the full config-binding version**

```csharp
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace MuseumGambling;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log = null!;

    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<int> WinChancePercent = null!;
    internal static ConfigEntry<int> PayoutValue = null!;

    private void Awake()
    {
        Log = Logger;

        Enabled = Config.Bind(
            "General",
            "Enabled",
            true,
            "Global kill-switch. When false, the Museum Head behaves vanilla (no gambling).");

        WinChancePercent = Config.Bind(
            "General",
            "WinChancePercent",
            5,
            new ConfigDescription(
                "Percent chance (whole number, 0–100) that a click pays out instead of dealing damage.",
                new AcceptableValueRange<int>(0, 100)));

        PayoutValue = Config.Bind(
            "General",
            "PayoutValue",
            50000,
            new ConfigDescription(
                "Dollar value stamped on the spawned money-bag valuable on a win.",
                new AcceptableValueRange<int>(0, 1_000_000)));

        var harmony = new Harmony("darkharasho.MuseumGambling");
        harmony.PatchAll();
        Log.LogInfo($"MuseumGambling v{PluginInfo.PLUGIN_VERSION} loaded.");
    }
}
```

- [ ] **Step 2: Build to confirm it compiles**

```bash
cd mods/museum-gambling
dotnet build MuseumGambling.csproj --configuration Release \
  /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO/
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Verify the config file shape by loading once in-game**

```bash
# Copy DLL into the user's BepInEx plugins dir (path may vary — use r2modman's profile if installed)
DLL_OUT="$HOME/.config/r2modmanPlus-local/REPO/profiles/Default/BepInEx/plugins/MuseumGambling"
mkdir -p "$DLL_OUT"
cp mods/museum-gambling/bin/Release/netstandard2.1/MuseumGambling.dll "$DLL_OUT/"
```

Ask the user to launch the game once (just to main menu — no need to start a run) and then quit. After that:

```bash
cat "$HOME/.config/r2modmanPlus-local/REPO/profiles/Default/BepInEx/config/darkharasho.MuseumGambling.cfg"
```

Expected output:

```
[General]
Enabled = true
WinChancePercent = 5
PayoutValue = 50000
```

If the user's profile path differs (steam install path, no r2modman, etc.), ask them where their BepInEx config dir is and adapt. **Do not skip this verification** — it's the only way to confirm `AcceptableValueRange` doesn't trip on the int defaults.

- [ ] **Step 4: Commit**

```bash
git add mods/museum-gambling/src/Plugin.cs
git commit -m "Bind Enabled/WinChancePercent/PayoutValue config"
```

---

## Phase 3 — Patch wiring (no payout yet)

### Task 3.1: Write the Harmony prefix (log-only on win)

**Files:**
- Create: `mods/museum-gambling/src/Patches/MuseumHeadPatches.cs`

Use the exact patch target identified in `docs/superpowers/RESEARCH.md` from Phase 0. The example below uses `<DamageMethod>` as a placeholder — **replace with the real method name before pasting**.

- [ ] **Step 1: Write the prefix**

`mods/museum-gambling/src/Patches/MuseumHeadPatches.cs`:

```csharp
using System;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace MuseumGambling.Patches;

[HarmonyPatch(typeof(MuseumPropMoneyHead), "<DamageMethod>")] // <-- FILL FROM RESEARCH.md
internal static class MuseumPropMoneyHead_DamageMethod_Prefix
{
    private static bool Prefix(MuseumPropMoneyHead __instance)
    {
        try
        {
            // Defer to host. In singleplayer InRoom is false → we proceed.
            if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
                return true;

            if (!Plugin.Enabled.Value)
                return true;

            int roll = UnityEngine.Random.Range(1, 101); // 1..100 inclusive
            int chance = Plugin.WinChancePercent.Value;
            bool win = Outcome.ShouldWin(roll, chance);

            Plugin.Log.LogInfo(
                $"[MuseumGambling] roll={roll} chance={chance} win={win}");

            if (win)
            {
                // Phase 3: log only. Payout wired in Phase 4.
                Plugin.Log.LogInfo("[MuseumGambling] WIN — damage suppressed (no payout yet).");
                return false; // skip vanilla damage
            }

            return true; // loss → vanilla damage runs
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"MuseumHead prefix failed: {ex}");
            return true; // fail open
        }
    }
}
```

If the RESEARCH.md notes say the clicking `PlayerAvatar` is reachable via `__args` or a field, the prefix signature changes — but for Phase 3 we don't need the player reference (we just suppress the damage; vanilla never runs so the player is never hurt). Add the `PlayerAvatar` parameter in Phase 4 only if `Payout.Spawn` needs it for positioning (it shouldn't — position comes from `__instance.transform`).

- [ ] **Step 2: Build**

```bash
cd mods/museum-gambling
dotnet build MuseumGambling.csproj --configuration Release \
  /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO/
```

Expected: clean build.

- [ ] **Step 3: Deploy + manual smoke test (always-win)**

```bash
cp mods/museum-gambling/bin/Release/netstandard2.1/MuseumGambling.dll \
   "$HOME/.config/r2modmanPlus-local/REPO/profiles/Default/BepInEx/plugins/MuseumGambling/"
```

Edit the config file:

```
WinChancePercent = 100
```

Ask the user to:
1. Host a lobby (singleplayer is fine for testing).
2. Get to the Museum.
3. Click the Money Head once.
4. Quit.

Then check the BepInEx log:

```bash
tail -50 "$HOME/.config/r2modmanPlus-local/REPO/profiles/Default/BepInEx/LogOutput.log" \
  | grep -i MuseumGambling
```

Expected lines:
```
[Info   :MuseumGambling] [MuseumGambling] roll=N chance=100 win=True
[Info   :MuseumGambling] [MuseumGambling] WIN — damage suppressed (no payout yet).
```

Acceptance: the player's health did NOT drop after clicking the head.

- [ ] **Step 4: Manual smoke test (never-win)**

Set `WinChancePercent = 0`. Repeat the click test.

Expected log:
```
[Info   :MuseumGambling] [MuseumGambling] roll=N chance=0 win=False
```

Acceptance: vanilla damage occurred (player health dropped by ~100).

- [ ] **Step 5: Commit**

```bash
git add mods/museum-gambling/src/Patches/MuseumHeadPatches.cs
git commit -m "Harmony prefix on Museum Head damage step (log-only on win)"
```

---

## Phase 4 — Payout spawn

### Task 4.1: Implement `Payout.Spawn`

**Files:**
- Create: `mods/museum-gambling/src/Payout.cs`

Use the spawn API documented in `docs/superpowers/RESEARCH.md` from Phase 0. The skeleton below is a placeholder structure — **fill in the real API calls before pasting**.

- [ ] **Step 1: Write `Payout.cs`**

`mods/museum-gambling/src/Payout.cs`:

```csharp
using UnityEngine;

namespace MuseumGambling;

internal static class Payout
{
    // Replace with the real prefab reference from RESEARCH.md.
    // Most likely sourced from a director singleton — e.g. ValuableDirector.instance.moneyBagPrefab
    // or a Resources.Load("Valuables/MoneyBag") path.
    internal static void Spawn(Vector3 position, int value)
    {
        try
        {
            // PSEUDO — replace with real API call from RESEARCH.md
            //
            // var prefab = ValuableDirector.instance.moneyBagPrefab;
            // var go = PhotonNetwork.InstantiateRoomObject(prefab.name, position, Quaternion.identity);
            // var valuable = go.GetComponent<ValuableObject>();
            // valuable.dollarValue = value;

            Plugin.Log.LogInfo(
                $"[MuseumGambling] Spawned money bag worth {value} at {position}.");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"Payout.Spawn failed: {ex}");
        }
    }
}
```

- [ ] **Step 2: Build to confirm the real API resolves**

```bash
cd mods/museum-gambling
dotnet build MuseumGambling.csproj --configuration Release \
  /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO/
```

Expected: clean build. If the real spawn API references a type that's not in any of the DLLs the csproj already references, add the missing `<Reference>` to the csproj — copy the pattern from the existing `Assembly-CSharp` reference.

- [ ] **Step 3: Wire `Payout.Spawn` into the prefix**

Edit `mods/museum-gambling/src/Patches/MuseumHeadPatches.cs` — in the `if (win)` branch, before `return false`:

```csharp
if (win)
{
    Plugin.Log.LogInfo("[MuseumGambling] WIN — spawning money bag.");
    Payout.Spawn(__instance.transform.position, Plugin.PayoutValue.Value);
    return false;
}
```

Remove the old "no payout yet" log line.

- [ ] **Step 4: Build + deploy**

```bash
cd mods/museum-gambling
dotnet build MuseumGambling.csproj --configuration Release \
  /p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO/
cp bin/Release/netstandard2.1/MuseumGambling.dll \
   "$HOME/.config/r2modmanPlus-local/REPO/profiles/Default/BepInEx/plugins/MuseumGambling/"
```

- [ ] **Step 5: Run spec test cases 1–5**

Ask the user to run each manual test and confirm acceptance:

| # | Config | Action | Expected |
|---|---|---|---|
| 1 | `WinChancePercent=100` | Click head once | Suck-in plays, no damage, one money bag at head's feet worth $50,000 |
| 2 | `WinChancePercent=0` | Click head once | Vanilla suck-in + ~100 damage, no bag |
| 3 | `WinChancePercent=5` | Click head ~40 times (at full HP between) | Roughly 2 wins (smoke check, not statistical) |
| 4 | `Enabled=false` | Click head once | Vanilla damage, no log lines from MuseumGambling |
| 5 | `WinChancePercent=100`, `PayoutValue=1` | Click head once | Money bag worth $1 spawns |

For each case, the user reports pass/fail. Any failure → STOP and diagnose before continuing.

- [ ] **Step 6: Commit**

```bash
git add mods/museum-gambling/src/Payout.cs \
        mods/museum-gambling/src/Patches/MuseumHeadPatches.cs
git commit -m "Spawn money-bag valuable on win"
```

---

### Task 4.2: Multiplayer authority test

- [ ] **Step 1: Run spec test cases 6 & 7**

Requires a second machine or a friend with the game. If unavailable, **skip and note in the verification step at the end** — manual MP test is a known gap.

| # | Setup | Expected |
|---|---|---|
| 6 | Client has mod, host does NOT | Client clicks head → never wins (prefix early-returns on `IsMasterClient` check) → vanilla damage every click |
| 7 | Host has mod, client does NOT | Host or client clicks head → on host's win, the money bag appears for both (replicates via the game's valuable network path) |

- [ ] **Step 2: Document MP test results in CHANGELOG or RESEARCH.md**

If both tests pass: nothing further. If test 7 fails (bag doesn't replicate to client), revisit `Payout.cs` — the spawn API may require `PhotonNetwork.InstantiateRoomObject` rather than `Instantiate`. Re-run after fix.

---

## Phase 5 — Packaging & polish

### Task 5.1: Update CHANGELOG and README

**Files:**
- Modify: `mods/museum-gambling/CHANGELOG.md`
- Modify: `mods/museum-gambling/README.md`

- [ ] **Step 1: Update CHANGELOG**

Replace the placeholder `0.1.0` entry in `mods/museum-gambling/CHANGELOG.md` with:

```markdown
# Changelog

## 0.1.0
- Initial release: gamble the Museum Head — clicking it has a configurable percent chance to spawn a money-bag valuable instead of dealing the normal damage. Host-authoritative.
- Config: `Enabled` (default true), `WinChancePercent` (default 5, range 0–100), `PayoutValue` (default 50000, range 0–1,000,000).
```

- [ ] **Step 2: Update README config section**

Edit `mods/museum-gambling/README.md` — replace the existing `## Configuration` section with:

```markdown
## Configuration

Config file: `BepInEx/config/darkharasho.MuseumGambling.cfg`

```ini
[General]
Enabled = true
WinChancePercent = 5
PayoutValue = 50000
```

| Key | Default | Range | Description |
|---|---|---|---|
| `Enabled` | `true` | — | When false, the Museum Head behaves vanilla (no gambling). |
| `WinChancePercent` | `5` | `0–100` | Whole-number percent chance a click pays out instead of dealing damage. |
| `PayoutValue` | `50000` | `0–1,000,000` | Dollar value stamped on the spawned money bag. |

Config is read live — edit values mid-run via [REPOConfig](https://thunderstore.io/c/repo/p/nickklmao/REPOConfig/) or by editing the file and reloading.
```

- [ ] **Step 3: Commit**

```bash
git add mods/museum-gambling/CHANGELOG.md mods/museum-gambling/README.md
git commit -m "Document v0.1.0 config and release notes"
```

---

### Task 5.2: Package the Thunderstore zip

**Files:**
- Modify: `mods/museum-gambling/icon.png` (user-supplied, NOT committed)

- [ ] **Step 1: Confirm icon.png is present**

```bash
ls -la mods/museum-gambling/icon.png
```

Expected: file exists, 256×256 PNG. **If missing, STOP** and ask the user to provide one — the build script will fail without it.

- [ ] **Step 2: Run `package.sh`**

```bash
cd mods/museum-gambling
./package.sh
```

Expected output:
```
Using game dir: /var/mnt/data/SteamLibrary/steamapps/common/REPO
...
Build succeeded.
Packaged: /var/home/.../REPOsitory/builds/MuseumGambling-0.1.0.zip — install via r2modman.
```

- [ ] **Step 3: Verify zip contents**

```bash
unzip -l ../../builds/MuseumGambling-0.1.0.zip
```

Expected: `manifest.json`, `icon.png`, `README.md`, `MuseumGambling.dll` — exactly 4 files, no subdirectories.

---

### Task 5.3: Final verification

- [ ] **Step 1: Run `verification-before-completion` skill checklist**

Invoke `superpowers:verification-before-completion`. At minimum confirm:

- `dotnet test mods/museum-gambling/tests/MuseumGambling.Tests/MuseumGambling.Tests.csproj` → 9 passed
- `dotnet build mods/museum-gambling/MuseumGambling.csproj --configuration Release /p:GameDir=...` → 0 errors, 0 warnings (warnings OK if pre-existing)
- Spec test cases 1–5 all manually confirmed passing (from Task 4.1 Step 5)
- Spec test cases 6 & 7 either confirmed passing OR explicitly noted as untested (Task 4.2 Step 2)
- `builds/MuseumGambling-0.1.0.zip` exists and contains the 4 expected files

- [ ] **Step 2: Final commit (if anything's left)**

```bash
git status
```

If clean, you're done. Report:
- Zip path
- Summary of which spec test cases passed
- Any MP gaps noted

---

## Self-review notes

- **Spec coverage:** Behavior (Phase 3+4), Architecture (file structure section + Phases 1–4), Multiplayer authority (Phase 3 prefix host-check + Phase 4.2 MP tests), Configuration (Phase 2), Testing unit cases (Phase 1.2), Testing integration cases 1–7 (Phase 4.1+4.2), Research notes (Phase 0). All covered.
- **Placeholder scan:** Phase 3 patch target name and Phase 4 spawn API are genuine research outputs from Phase 0, not plan placeholders — they're explicitly fetched from RESEARCH.md before pasting. The `<DamageMethod>` and pseudo-code in Payout are flagged with explicit "replace from RESEARCH.md" callouts.
- **Type consistency:** `Outcome.ShouldWin(int, int) → bool` used identically in OutcomeTests and MuseumHeadPatches. `Plugin.Enabled.Value` / `Plugin.WinChancePercent.Value` / `Plugin.PayoutValue.Value` consistent across Plugin.cs and the patch. `Payout.Spawn(Vector3, int)` consistent across Payout.cs and the patch.
- **Known dependencies on Phase 0:** Phases 3 and 4 hard-depend on RESEARCH.md being filled in. The plan stops cleanly in Phase 0 if research findings break the design assumption.
