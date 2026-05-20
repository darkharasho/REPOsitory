# Overstaffed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a BepInEx mod for R.E.P.O. 0.4.0 that raises the in-lobby player cap above the vanilla 6, configurable up to 20.

**Architecture:** Three Harmony patches on `GameManager` and `NetworkConnect` that read a single `[General] MaxPlayers` config value. The 0.4.0 game already centralizes lobby/room sizing on `GameManager.maxPlayers`, so the load-bearing change is raising that field early in the lifecycle.

**Tech Stack:** .NET Standard 2.1, BepInEx 5.4.21, Harmony 2.x (bundled with BepInEx), targets R.E.P.O. Steam build (`Assembly-CSharp.dll`).

**Reference spec:** `docs/superpowers/specs/2026-05-19-overstaffed-design.md`
**Reference template mod:** `mods/mini-eepo/` — same csproj/package.sh/Plugin.cs patterns.

**Testing note:** No automated tests — manual integration testing only, consistent with the repo's existing mods and the user's package.sh-builds-zip workflow. The plan uses build-and-run checkpoints in place of unit tests.

---

## File Structure

```
mods/overstaffed/
├── CHANGELOG.md             # 0.1.0 initial release notes
├── CLAUDE.md                # placeholder (matches mini-eepo)
├── manifest.json            # name, version, deps for Thunderstore/r2modman
├── nuget.config             # nuget.org source (copied from mini-eepo)
├── Overstaffed.csproj       # build definition, references, version-from-manifest
├── package.sh               # build → deploy to r2modman → zip into builds/
├── README.md                # short user-facing description
├── icon.png                 # 256x256 mod icon (copied from existing mod, can be replaced later)
└── src/
    ├── Plugin.cs                          # BepInPlugin, config binding, Harmony.PatchAll
    └── Patches/
        ├── GameManagerPatches.cs          # Patch 1 (Awake postfix), Patch 2 (SetMaxPlayers prefix)
        └── NetworkConnectPatches.cs       # Patch 3 (OnConnectedToMaster transpiler)
```

**Responsibility split:**
- `Plugin.cs` owns plugin metadata, config binding, log source, and Harmony bootstrap.
- `Patches/GameManagerPatches.cs` owns everything that touches `GameManager` fields — both patches live together because they share knowledge of `maxPlayersPhoton`.
- `Patches/NetworkConnectPatches.cs` owns the matchmaking IL substitution. Kept separate because it's the only patch that uses a transpiler and the only one that can fail to apply due to IL drift.

---

## Task 1: Scaffold mod directory and project files

**Files:**
- Create: `mods/overstaffed/manifest.json`
- Create: `mods/overstaffed/nuget.config`
- Create: `mods/overstaffed/Overstaffed.csproj`
- Create: `mods/overstaffed/CHANGELOG.md`
- Create: `mods/overstaffed/CLAUDE.md`
- Create: `mods/overstaffed/README.md`
- Create: `mods/overstaffed/icon.png` (copy from `mods/mini-eepo/icon.png` as placeholder)

- [ ] **Step 1: Create manifest.json**

`mods/overstaffed/manifest.json`:

```json
{
    "name": "Overstaffed",
    "version_number": "0.1.0",
    "website_url": "https://github.com/darkharasho/REPOsitory",
    "description": "Raises R.E.P.O.'s max player count above 6. Configurable up to 20.",
    "dependencies": [
        "BepInEx-BepInExPack-5.4.2100"
    ]
}
```

- [ ] **Step 2: Create nuget.config**

Copy verbatim from `mods/mini-eepo/nuget.config`:

```bash
cp mods/mini-eepo/nuget.config mods/overstaffed/nuget.config
```

- [ ] **Step 3: Create Overstaffed.csproj**

`mods/overstaffed/Overstaffed.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <AssemblyName>Overstaffed</AssemblyName>
    <RootNamespace>Overstaffed</RootNamespace>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <!-- Version is the single source of truth — read from manifest.json at build time -->
    <ManifestJson>$([System.IO.File]::ReadAllText('$(MSBuildProjectDirectory)/manifest.json'))</ManifestJson>
    <Version>$([System.Text.RegularExpressions.Regex]::Match($(ManifestJson), '"version_number"\s*:\s*"([^"]+)"').Groups[1].Value)</Version>
    <BepInExPluginGuid>darkharasho.Overstaffed</BepInExPluginGuid>
    <BepInExPluginName>Overstaffed</BepInExPluginName>
    <BepInExPluginVersion>$(Version)</BepInExPluginVersion>
  </PropertyGroup>

  <!-- Override GameDir via env var or MSBuild property: dotnet build /p:GameDir="/path/to/REPO" -->
  <PropertyGroup Condition="'$(GameDir)' == ''">
    <GameDir>C:\Program Files (x86)\Steam\steamapps\common\REPO</GameDir>
  </PropertyGroup>
  <PropertyGroup>
    <ManagedDir>$(GameDir)/REPO_Data/Managed</ManagedDir>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="BepInEx.Core" Version="5.4.21.0">
      <ExcludeAssets>runtime</ExcludeAssets>
    </PackageReference>
    <PackageReference Include="BepInEx.PluginInfoProps" Version="1.1.0" />
  </ItemGroup>

  <ItemGroup>
    <Reference Include="UnityEngine">
      <HintPath>$(ManagedDir)/UnityEngine.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(ManagedDir)/UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Assembly-CSharp">
      <HintPath>$(ManagedDir)/Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="PhotonUnityNetworking">
      <HintPath>$(ManagedDir)/PhotonUnityNetworking.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="PhotonRealtime">
      <HintPath>$(ManagedDir)/PhotonRealtime.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

</Project>
```

(No `ScalerCore` reference — Overstaffed has no dependency on it.)

- [ ] **Step 4: Create CHANGELOG.md**

`mods/overstaffed/CHANGELOG.md`:

```markdown
# Overstaffed Changelog

## 0.1.0

- Initial release. Raises R.E.P.O. 0.4.0's max player count above 6 (configurable 1–20).
- Replaces the unmaintained `Spindles-MorePlayersImproved`, whose patch targets no longer exist in 0.4.0.
```

- [ ] **Step 5: Create CLAUDE.md placeholder**

`mods/overstaffed/CLAUDE.md`:

```markdown
## Project Context

_No context provided._
```

- [ ] **Step 6: Create README.md**

`mods/overstaffed/README.md`:

````markdown
# Overstaffed

Raises R.E.P.O.'s max player count above the vanilla 6. Configurable from 1 to 20.

Replacement for the unmaintained `Spindles-MorePlayersImproved` on R.E.P.O. 0.4.0+.

## Configuration

After first launch, edit `BepInEx/config/darkharasho.Overstaffed.cfg`:

```
[General]

## The maximum number of players allowed in a server.
# Setting type: Int32
# Default value: 10
# Acceptable value range: From 1 to 20
MaxPlayers = 10
```

> ⚠️ Values above ~10 may exhibit Photon networking instability — Photon's per-room player ceiling isn't designed for very high counts. Both host and joining players need this mod installed to use a raised cap.
````

- [ ] **Step 7: Copy placeholder icon**

```bash
cp mods/mini-eepo/icon.png mods/overstaffed/icon.png
```

- [ ] **Step 8: Commit**

```bash
git add mods/overstaffed/
git commit -m "Overstaffed: scaffold mod directory from mini-eepo template"
```

---

## Task 2: Write Plugin.cs entrypoint with config binding

**Files:**
- Create: `mods/overstaffed/src/Plugin.cs`

- [ ] **Step 1: Write Plugin.cs**

`mods/overstaffed/src/Plugin.cs`:

```csharp
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace Overstaffed
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal const int MaxAllowedPlayers = 20;
        internal const int MinAllowedPlayers = 1;
        internal const int DefaultPlayers = 10;

        internal static ManualLogSource Log = null!;
        internal static ConfigEntry<int> ConfigMaxPlayers = null!;

        private void Awake()
        {
            Log = Logger;

            ConfigMaxPlayers = Config.Bind(
                "General",
                "MaxPlayers",
                DefaultPlayers,
                new ConfigDescription(
                    "The maximum number of players allowed in a server.",
                    new AcceptableValueRange<int>(MinAllowedPlayers, MaxAllowedPlayers)));

            Log.LogInfo($"Overstaffed v{PluginInfo.PLUGIN_VERSION} loading — MaxPlayers={ConfigMaxPlayers.Value}");

            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            Log.LogInfo("Overstaffed loaded.");
        }
    }
}
```

- [ ] **Step 2: Verify build (will fail — no patches yet, but should compile)**

```bash
cd mods/overstaffed && dotnet build Overstaffed.csproj --configuration Release /p:GameDir="$HOME/.steam/steam/steamapps/common/REPO"
```

Expected: Build succeeds with 0 errors. (`GAME_DIR` is the user's R.E.P.O. install — they may need to substitute `/var/mnt/data/SteamLibrary/steamapps/common/REPO` per their memory entry. Adjust to whatever resolves.)

If the build fails because the game path doesn't exist, fall back to:

```bash
dotnet build Overstaffed.csproj --configuration Release /p:GameDir="/var/mnt/data/SteamLibrary/steamapps/common/REPO"
```

- [ ] **Step 3: Commit**

```bash
git add mods/overstaffed/src/Plugin.cs
git commit -m "Overstaffed: plugin entrypoint with MaxPlayers config binding"
```

---

## Task 3: Implement GameManager patches (Patch 1 + Patch 2)

**Files:**
- Create: `mods/overstaffed/src/Patches/GameManagerPatches.cs`

This task implements the two load-bearing patches together because they share knowledge of how `maxPlayers` and `maxPlayersPhoton` interact.

- [ ] **Step 1: Write GameManagerPatches.cs**

`mods/overstaffed/src/Patches/GameManagerPatches.cs`:

```csharp
using HarmonyLib;

namespace Overstaffed.Patches
{
    // Patch 1 (primary): raise GameManager.maxPlayers and maxPlayersPhoton immediately after
    // GameManager.Awake runs. Every consumer of these fields (SteamManager.HostLobby,
    // NetworkConnect's room create / room join, OnLobbyMemberLeft reconciliation) reads them
    // each time they need a player cap, so updating once on init is sufficient for those paths.
    [HarmonyPatch(typeof(GameManager), "Awake")]
    internal static class GameManagerAwakePatch
    {
        private static void Postfix(GameManager __instance)
        {
            int target = Plugin.ConfigMaxPlayers.Value;

            int oldPhoton = __instance.maxPlayersPhoton;
            int oldMax = __instance.maxPlayers;

            // Raise the ceiling first — SetMaxPlayers clamps to [1, maxPlayersPhoton], and other
            // game code reads maxPlayersPhoton when applying the "max players peer room too big"
            // auto-adjust on OnCreateRoomFailed / OnJoinRoomFailed.
            if (target > __instance.maxPlayersPhoton)
                __instance.maxPlayersPhoton = target;

            __instance.maxPlayers = target;

            Plugin.Log.LogInfo(
                $"[GameManager.Awake] maxPlayers {oldMax} -> {__instance.maxPlayers}, " +
                $"maxPlayersPhoton {oldPhoton} -> {__instance.maxPlayersPhoton}");
        }
    }

    // Patch 2 (safety net): the game calls SetMaxPlayers in reconciliation paths
    // (e.g. SteamManager.OnLobbyMemberLeft when maxPlayers < lobby.MaxMembers). SetMaxPlayers
    // clamps its argument to [1, maxPlayersPhoton]. If anything ever passes a target above the
    // vanilla ceiling, the clamp would silently trim us; raise the ceiling first.
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.SetMaxPlayers))]
    internal static class GameManagerSetMaxPlayersPatch
    {
        private static void Prefix(GameManager __instance, ref int _target)
        {
            if (_target > __instance.maxPlayersPhoton)
                __instance.maxPlayersPhoton = _target;
        }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
cd mods/overstaffed && dotnet build Overstaffed.csproj --configuration Release /p:GameDir="$HOME/.steam/steam/steamapps/common/REPO"
```

Expected: 0 errors.

If the build complains that `GameManager.SetMaxPlayers` isn't accessible, replace `nameof(GameManager.SetMaxPlayers)` with the string literal `"SetMaxPlayers"` (the method *is* public on 0.4.0 per the spec's decompilation, but the string form is robust to access-modifier drift).

- [ ] **Step 3: Commit**

```bash
git add mods/overstaffed/src/Patches/GameManagerPatches.cs
git commit -m "Overstaffed: patch GameManager.Awake + SetMaxPlayers to raise player cap"
```

---

## Task 4: Implement NetworkConnect transpiler (Patch 3)

**Files:**
- Create: `mods/overstaffed/src/Patches/NetworkConnectPatches.cs`

This patch targets the public/random matchmaking branch in `NetworkConnect.OnConnectedToMaster`, where the game computes `expectedMaxPlayers` as a hardcoded `(byte)6` when `GameManager.matchmakingMaxPlayers` is true. The transpiler replaces the `ldc.i4.6` instruction in that arithmetic with a call to a helper that returns our configured value.

**Reference IL pattern (from spec's decompilation):**

```csharp
byte expectedMaxPlayers = (byte)(GameManager.instance.matchmakingMaxPlayers ? 6u : 0u);
```

This compiles to a short sequence ending in `ldc.i4.6` for the true-branch literal. We replace `ldc.i4.6` with a call returning `int` (which then flows into the existing `conv.u1` to make the byte).

- [ ] **Step 1: Write NetworkConnectPatches.cs**

`mods/overstaffed/src/Patches/NetworkConnectPatches.cs`:

```csharp
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace Overstaffed.Patches
{
    // Patch 3 (nice-to-have): the public/random matchmaking branch in
    // NetworkConnect.OnConnectedToMaster builds expectedMaxPlayers from a hardcoded
    // (byte)(matchmakingMaxPlayers ? 6u : 0u). The literal 6 is what restricts
    // PhotonNetwork.JoinRandomRoom / JoinRandomOrCreateRoom to vanilla-sized rooms.
    //
    // Replace the ldc.i4.6 with a call to GetExpectedMaxPlayers(), which returns our
    // configured cap. The conv.u1 immediately following keeps converting to byte.
    [HarmonyPatch(typeof(NetworkConnect), "OnConnectedToMaster")]
    internal static class NetworkConnectOnConnectedToMasterPatch
    {
        // Returns int (not byte) — the surrounding IL already does conv.u1 after this value.
        // Clamped to [1, 255] so we never overflow when the conv.u1 runs.
        public static int GetExpectedMaxPlayers()
        {
            int v = Plugin.ConfigMaxPlayers.Value;
            if (v < 1) return 1;
            if (v > 255) return 255;
            return v;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var helper = AccessTools.Method(
                typeof(NetworkConnectOnConnectedToMasterPatch),
                nameof(GetExpectedMaxPlayers));

            int replaced = 0;
            foreach (var instr in instructions)
            {
                // ldc.i4.6 is the short-form one-byte opcode used by the C# compiler for the
                // literal 6 in `matchmakingMaxPlayers ? 6u : 0u`. There is exactly one of these
                // in the method body (verified against 0.4.0 Assembly-CSharp.dll); the other
                // numeric literals in the method are 0, which uses ldc.i4.0.
                if (instr.opcode == OpCodes.Ldc_I4_6 && replaced == 0)
                {
                    replaced++;
                    yield return new CodeInstruction(OpCodes.Call, helper);
                    continue;
                }
                yield return instr;
            }

            if (replaced == 0)
                Plugin.Log.LogWarning(
                    "[NetworkConnect.OnConnectedToMaster] transpiler did NOT find ldc.i4.6 — " +
                    "public matchmaking expectedMaxPlayers will stay at vanilla 6. " +
                    "Private/friends lobbies still respect MaxPlayers via the GameManager patches.");
            else
                Plugin.Log.LogInfo("[NetworkConnect.OnConnectedToMaster] transpiler replaced ldc.i4.6 with GetExpectedMaxPlayers()");
        }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
cd mods/overstaffed && dotnet build Overstaffed.csproj --configuration Release /p:GameDir="$HOME/.steam/steam/steamapps/common/REPO"
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add mods/overstaffed/src/Patches/NetworkConnectPatches.cs
git commit -m "Overstaffed: transpile NetworkConnect.OnConnectedToMaster matchmaking cap"
```

---

## Task 5: Add per-patch failure isolation in Plugin.Awake

**Files:**
- Modify: `mods/overstaffed/src/Plugin.cs`

`harmony.PatchAll()` registers all `[HarmonyPatch]`-decorated classes in one call. If any single patch throws during registration (e.g. game updates change a method signature), `PatchAll` aborts and no patches apply. We want partial success — losing Patch 3 to IL drift shouldn't lose Patches 1 and 2.

- [ ] **Step 1: Replace the harmony.PatchAll() call site**

In `mods/overstaffed/src/Plugin.cs`, replace:

```csharp
            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            Log.LogInfo("Overstaffed loaded.");
```

with:

```csharp
            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            TryPatch(harmony, typeof(Patches.GameManagerAwakePatch));
            TryPatch(harmony, typeof(Patches.GameManagerSetMaxPlayersPatch));
            TryPatch(harmony, typeof(Patches.NetworkConnectOnConnectedToMasterPatch));

            Log.LogInfo("Overstaffed loaded.");
        }

        private static void TryPatch(Harmony harmony, System.Type patchType)
        {
            try
            {
                harmony.CreateClassProcessor(patchType).Patch();
            }
            catch (System.Exception e)
            {
                Log.LogWarning($"[Overstaffed] Failed to apply {patchType.Name}: {e.GetType().Name}: {e.Message}");
            }
```

Make sure to add `using Overstaffed.Patches;` — actually, leave the fully-qualified `Patches.X` references; no extra `using` needed.

- [ ] **Step 2: Verify build**

```bash
cd mods/overstaffed && dotnet build Overstaffed.csproj --configuration Release /p:GameDir="$HOME/.steam/steam/steamapps/common/REPO"
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add mods/overstaffed/src/Plugin.cs
git commit -m "Overstaffed: isolate patch registration failures per patch"
```

---

## Task 6: Write package.sh

**Files:**
- Create: `mods/overstaffed/package.sh`

- [ ] **Step 1: Write package.sh**

`mods/overstaffed/package.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

VERSION="$(python3 -c "import json; print(json.load(open('manifest.json'))['version_number'])")"
DLL="bin/Release/netstandard2.1/Overstaffed.dll"
BUILDS_DIR="$(cd "../.." && pwd)/builds"
mkdir -p "$BUILDS_DIR"
OUT="$BUILDS_DIR/Overstaffed-${VERSION}.zip"
R2_PROFILE="${R2_PROFILE:-10-30-update}"
R2_PLUGINS="$HOME/.config/r2modmanPlus-local/REPO/profiles/$R2_PROFILE/BepInEx/plugins/Overstaffed"

if [ -z "${GAME_DIR:-}" ]; then
    for candidate in \
        "$HOME/.steam/steam/steamapps/common/REPO" \
        "$HOME/.local/share/Steam/steamapps/common/REPO" \
        "/var/mnt/data/SteamLibrary/steamapps/common/REPO"
    do
        if [ -d "$candidate" ]; then
            GAME_DIR="$candidate"
            break
        fi
    done
fi

if [ -z "${GAME_DIR:-}" ]; then
    echo "ERROR: Could not find R.E.P.O. install. Set GAME_DIR manually:"
    echo "  GAME_DIR=\"/path/to/REPO\" ./package.sh"
    exit 1
fi

echo "Using game dir: $GAME_DIR"

dotnet build Overstaffed.csproj --configuration Release /p:GameDir="$GAME_DIR"

mkdir -p "$R2_PLUGINS"
cp "$DLL" "$R2_PLUGINS/"
echo "Deployed to r2modman profile: $R2_PROFILE"

if [ ! -f "icon.png" ]; then
    echo "WARNING: icon.png not found — skipping Thunderstore zip."
    exit 0
fi

rm -f "$OUT"
zip -j "$OUT" manifest.json icon.png README.md "$DLL"
echo "Packaged: $OUT"
```

- [ ] **Step 2: Make executable**

```bash
chmod +x mods/overstaffed/package.sh
```

- [ ] **Step 3: Run package.sh end-to-end**

```bash
cd mods/overstaffed && ./package.sh
```

Expected:
- "Using game dir: …" line
- `dotnet build` succeeds
- "Deployed to r2modman profile: 10-30-update"
- "Packaged: …/builds/Overstaffed-0.1.0.zip"

If the r2modman profile `10-30-update` doesn't exist on this machine, set `R2_PROFILE` to an existing one — e.g. `R2_PROFILE="0.4.0" ./package.sh`. List available profiles with:

```bash
ls "$HOME/.config/r2modmanPlus-local/REPO/profiles/"
```

- [ ] **Step 4: Verify zip contents**

```bash
unzip -l builds/Overstaffed-0.1.0.zip
```

Expected: zip contains exactly `manifest.json`, `icon.png`, `README.md`, `Overstaffed.dll`.

- [ ] **Step 5: Commit**

```bash
git add mods/overstaffed/package.sh
git commit -m "Overstaffed: add package.sh build/deploy/zip script"
```

---

## Task 7: Manual integration test on R.E.P.O. 0.4.0

This task verifies the mod works in-game. It is not automatable. The implementing agent should report status and prompt the user to perform the playtest steps.

**Files:** none

- [ ] **Step 1: Re-run package.sh to ensure fresh deploy**

```bash
cd mods/overstaffed && ./package.sh
```

- [ ] **Step 2: Ask the user to launch R.E.P.O. through r2modman**

Prompt the user:

> "I've built and deployed Overstaffed 0.1.0 to your r2modman `10-30-update` profile (or whichever profile R2_PROFILE points to). Please launch R.E.P.O. through r2modman and then quit back to desktop. I'll inspect the BepInEx log to confirm patches applied."

- [ ] **Step 3: Inspect BepInEx log for patch confirmation**

After the user reports they've launched and quit:

```bash
grep -i 'overstaffed\|maxplayers\|matchmaking\|expectedmax' \
    "$HOME/.config/r2modmanPlus-local/REPO/profiles/${R2_PROFILE:-10-30-update}/BepInEx/LogOutput.log"
```

Expected lines (order may vary):
- `[Info   :Overstaffed] Overstaffed v0.1.0 loading — MaxPlayers=10`
- `[Info   :Overstaffed] [GameManager.Awake] maxPlayers 6 -> 10, maxPlayersPhoton 20 -> 20`
- `[Info   :Overstaffed] [NetworkConnect.OnConnectedToMaster] transpiler replaced ldc.i4.6 with GetExpectedMaxPlayers()`
- `[Info   :Overstaffed] Overstaffed loaded.`

If any expected line is **missing**, that patch failed — investigate the corresponding `Patches/*.cs` file. If the transpiler warning fires (`did NOT find ldc.i4.6`), it means the game's IL doesn't match the expected pattern; this is the documented partial-failure mode and only affects public matchmaking — Patches 1 and 2 still cover all private-lobby cases.

- [ ] **Step 4: Ask user to host a >6-player private lobby**

Prompt the user:

> "Logs confirm the patches applied. To verify the actual cap raise, please host a private/friends lobby in R.E.P.O. and invite 7+ Steam friends. Confirm they can all join. (If you can't easily round up 7 people, the log evidence above is sufficient for now — we can ship and verify in real play.)"

- [ ] **Step 5: No commit needed — testing only**

---

## Task 8: Final cleanup commit and PR-readiness check

**Files:**
- Modify: `mods/overstaffed/CHANGELOG.md` (only if integration test surfaced any fix)

- [ ] **Step 1: Verify final state**

```bash
git status
ls -la mods/overstaffed/
git log --oneline -- mods/overstaffed/
```

Expected: clean working tree, all Task 1–6 commits present, `mods/overstaffed/` contains the full file structure from the plan header.

- [ ] **Step 2: Report to user**

Summarize what was built, point at `builds/Overstaffed-0.1.0.zip`, and flag any deviations from the spec (e.g. if the Patch 3 transpiler had to fall back to a prefix). The user will decide on a publish step (out of scope for this plan).

---

## Self-Review Checklist

- **Spec coverage:** All three patches in the spec map to tasks (Task 3 = Patches 1+2, Task 4 = Patch 3). Config, mod identity, file layout, manifest, logging, and testing plan all have explicit tasks.
- **Placeholders:** No "TBD", no "implement later", no "add error handling" — every code block is complete.
- **Type consistency:** `ConfigMaxPlayers` (static field) is used consistently across `Plugin.cs`, `GameManagerAwakePatch`, `GameManagerSetMaxPlayersPatch`, and `NetworkConnectOnConnectedToMasterPatch`. `PluginInfo.PLUGIN_GUID/NAME/VERSION` are generated by `BepInEx.PluginInfoProps` per the csproj — same pattern as `mods/mini-eepo/`.
- **Granularity:** Each task ends in a build/commit checkpoint; tasks 1–6 produce a buildable zip even if the playtest in Task 7 finds something to fix.
