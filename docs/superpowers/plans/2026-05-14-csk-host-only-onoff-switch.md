# ChillShopKeeper Host-Only On/Off Switch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an opt-in BepInEx config toggle `HostOnlyOnOffSwitch` to ChillShopKeeper that blocks non-host players from pressing the ShopKeeper's in-shop on/off switch.

**Architecture:** One new `ConfigEntry<bool>` in `Plugin.cs` (default `false`), and one new Harmony prefix patch on `ShopKeeper.WakeUpOrSleepLogic()` that returns `false` (skip vanilla) when the toggle is on, we're in multiplayer, and the local player is not the Photon master client. Fail-open on exceptions, consistent with the existing `ShopKeeper_AddRuckusScore_Prefix` patch.

**Tech Stack:** C#, .NET Standard 2.1, BepInEx 5.4.21 (`BepInEx.Core`, `BepInEx.Configuration`, `BepInEx.Logging`), HarmonyLib, PhotonUnityNetworking, Unity (game DLL `Assembly-CSharp`).

**Spec:** `docs/superpowers/specs/2026-05-14-csk-host-only-onoff-switch-design.md`

**Versioning:** Ride along in the unreleased `0.3.0` (which already has `FixCartCannonDetection`). `manifest.json` is already at `0.3.0` — no version bump needed.

---

## File Structure

- **Create:** `mods/chill-shop-keeper/src/Patches/OnOffSwitchHostOnlyPatches.cs` — single Harmony prefix patch class targeting `ShopKeeper.WakeUpOrSleepLogic`.
- **Modify:** `mods/chill-shop-keeper/src/Plugin.cs` — add one `internal static ConfigEntry<bool> HostOnlyOnOffSwitch` field and its `Config.Bind` call in `Awake()`.
- **Modify:** `mods/chill-shop-keeper/CHANGELOG.md` — append a bullet to the existing `## 0.3.0` section.

No new files, classes, or assemblies are needed beyond the one patch file. `PhotonUnityNetworking` is already a `<Reference>` in `ChillShopKeeper.csproj`, so `PhotonNetwork.IsMasterClient` is available without csproj edits.

---

## Reference: Existing Patterns

Read these before starting; the new code should match these styles exactly.

- **Plugin.cs config binding pattern** — `mods/chill-shop-keeper/src/Plugin.cs:21-31` shows the existing `DisableGlobally` and `FixCartCannonDetection` bindings in section `"General"`. Match the formatting (3-arg defaults + multi-line description).
- **Harmony prefix pattern with fail-open** — `mods/chill-shop-keeper/src/Patches/ShopKeeperPatches.cs:6-35` shows the `[HarmonyPatch(typeof(ShopKeeper), "AddRuckusScore")]` prefix with try/catch returning `true` on exception. Match this structure.
- **Multiplayer/master check** — vanilla code in `ShopKeeper.WakeUpOrSleepLogic` uses `SemiFunc.IsMultiplayer()` to gate the RPC vs. direct-call path. Use `SemiFunc.IsMultiplayer()` and `Photon.Pun.PhotonNetwork.IsMasterClient` here too.

---

## Task 1: Add `HostOnlyOnOffSwitch` config entry

**Files:**
- Modify: `mods/chill-shop-keeper/src/Plugin.cs`

- [ ] **Step 1: Add the static field**

In `mods/chill-shop-keeper/src/Plugin.cs`, just after the existing `FixCartCannonDetection` field declaration (currently line 14), add:

```csharp
    internal static ConfigEntry<bool> HostOnlyOnOffSwitch = null!;
```

The fields block should read:

```csharp
    internal static ConfigEntry<bool> DisableGlobally = null!;
    internal static ConfigEntry<bool> FixCartCannonDetection = null!;
    internal static ConfigEntry<bool> HostOnlyOnOffSwitch = null!;
    internal static PlayerRegistry Players = null!;
```

- [ ] **Step 2: Bind the config entry in `Awake()`**

In the same file, immediately after the existing `FixCartCannonDetection = Config.Bind(...)` block (ends around line 31, just before `Players = new PlayerRegistry(Config);`), add:

```csharp
        HostOnlyOnOffSwitch = Config.Bind(
            "General",
            "HostOnlyOnOffSwitch",
            false,
            "When true, only the host (Photon master client) can press the ShopKeeper's on/off switch. Non-host players' presses are blocked locally before any animation, cooldown, or RPC. Default false preserves vanilla button feel.");
```

- [ ] **Step 3: Build the mod**

Run from the repo root:

```bash
GAME_DIR="/var/mnt/data/SteamLibrary/steamapps/common/REPO" \
  bash mods/chill-shop-keeper/package.sh
```

Expected: build succeeds, prints `Packaged: <repo>/builds/ChillShopKeeper-0.3.0.zip — install via r2modman.` No compiler warnings about the new field/binding.

If the build fails with `CS0103` or `CS0246`, double-check the namespace and that `ConfigEntry<bool>` is already imported (it is — `using BepInEx.Configuration;` is at the top of `Plugin.cs`).

- [ ] **Step 4: Commit**

```bash
git add mods/chill-shop-keeper/src/Plugin.cs
git commit -m "ChillShopKeeper: add HostOnlyOnOffSwitch config entry"
```

---

## Task 2: Add Harmony prefix patch blocking non-host presses

**Files:**
- Create: `mods/chill-shop-keeper/src/Patches/OnOffSwitchHostOnlyPatches.cs`

- [ ] **Step 1: Create the patch file**

Create `mods/chill-shop-keeper/src/Patches/OnOffSwitchHostOnlyPatches.cs` with this exact content:

```csharp
using System;
using HarmonyLib;
using Photon.Pun;

namespace ChillShopKeeper.Patches;

[HarmonyPatch(typeof(ShopKeeper), "WakeUpOrSleepLogic")]
internal static class ShopKeeper_WakeUpOrSleepLogic_Prefix
{
    private static bool Prefix()
    {
        try
        {
            if (!Plugin.HostOnlyOnOffSwitch.Value)
                return true;

            if (!SemiFunc.IsMultiplayer())
                return true;

            if (!PhotonNetwork.IsMasterClient)
                return false;

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"WakeUpOrSleepLogic prefix failed: {ex}");
            return true;
        }
    }
}
```

Notes for the implementer:
- `ShopKeeper` and `SemiFunc` are types from the game's `Assembly-CSharp.dll` and resolve without `using` directives because `ChillShopKeeper.csproj` references that assembly (see lines 41-44 of the csproj).
- `Photon.Pun.PhotonNetwork.IsMasterClient` is the standard PUN check used throughout the game.
- The patch target `"WakeUpOrSleepLogic"` is a `public void` instance method — Harmony resolves it by name; no `MethodType` or argument list needed.
- `Plugin.HostOnlyOnOffSwitch` and `Plugin.Log` were defined in Task 1 and in the existing `Plugin.cs` respectively.
- `harmony.PatchAll()` in `Plugin.Awake()` (line 36 of `Plugin.cs`) auto-discovers any `[HarmonyPatch]` class in the assembly — no registration step needed.

- [ ] **Step 2: Build the mod**

Run from the repo root:

```bash
GAME_DIR="/var/mnt/data/SteamLibrary/steamapps/common/REPO" \
  bash mods/chill-shop-keeper/package.sh
```

Expected: build succeeds with no errors or warnings, produces `builds/ChillShopKeeper-0.3.0.zip`.

If you see `CS0246: The type or namespace name 'Photon' could not be found`, verify `ChillShopKeeper.csproj` has the `PhotonUnityNetworking` reference (it does, around line 47) and that the game install at `GAME_DIR` actually has `PhotonUnityNetworking.dll` under `REPO_Data/Managed/`.

If you see `CS0117: 'ShopKeeper' does not contain a definition for 'WakeUpOrSleepLogic'`, the method name has changed in the game — bail out and notify the user; do not guess at alternatives.

- [ ] **Step 3: Commit**

```bash
git add mods/chill-shop-keeper/src/Patches/OnOffSwitchHostOnlyPatches.cs
git commit -m "ChillShopKeeper: block non-host on/off switch presses when HostOnlyOnOffSwitch enabled"
```

---

## Task 3: Update CHANGELOG

**Files:**
- Modify: `mods/chill-shop-keeper/CHANGELOG.md`

- [ ] **Step 1: Append bullet to the existing 0.3.0 entry**

In `mods/chill-shop-keeper/CHANGELOG.md`, the current `## 0.3.0` section contains one bullet. Add a second bullet directly below it so the section reads:

```markdown
## 0.3.0
- New opt-in `FixCartCannonDetection` toggle (default off) that fixes a vanilla R.E.P.O. bug where Cart Cannon bullets aren't attributed to the shooter, causing the ShopKeeper to ignore that damage. Enable it if you want the ShopKeeper to punish Cart Cannon users.
- New opt-in `HostOnlyOnOffSwitch` toggle (default off) that blocks non-host players from pressing the ShopKeeper's on/off switch. Vanilla already silently drops non-host presses on the host; this just prevents the wasted local press for clarity.
```

- [ ] **Step 2: Commit**

```bash
git add mods/chill-shop-keeper/CHANGELOG.md
git commit -m "ChillShopKeeper: document HostOnlyOnOffSwitch in 0.3.0 changelog"
```

---

## Task 4: Manual verification in r2modman

This mod has no automated tests — verification is manual via r2modman + the actual game. The implementer should walk through these checks and report results to the user; do **not** mark tasks complete without doing them (or explicitly confirm with the user that they want to skip in-game verification).

**Files:** none — runtime verification only.

- [ ] **Step 1: Install the build**

Take `builds/ChillShopKeeper-0.3.0.zip` and install it via r2modman into the active profile (typically as an "Unknown" mod since this is a local build).

- [ ] **Step 2: Verify config entry appears**

Launch the game once with the new build. Then check the BepInEx config file:

```bash
ls -la "$HOME/.config/r2modmanPlus-local/REPO/profiles/"*/BepInEx/config/darkharasho.ChillShopKeeper.cfg
```

(Path may vary by distro/profile name. If r2modman uses Flatpak, the path is under `~/.var/app/io.github.ebkr.r2modmanPlus/config/r2modmanPlus-local/...`.)

Open the cfg and confirm a new entry under `[General]`:

```
## When true, only the host (Photon master client) can press the ShopKeeper's on/off switch. ...
# Setting type: Boolean
# Default value: false
HostOnlyOnOffSwitch = false
```

Also confirm in `BepInEx/LogOutput.log` that the line `ChillShopKeeper v0.3.0 loaded.` appears and no Harmony patch errors are logged for `ShopKeeper`.

- [ ] **Step 3: Verify default (false) = vanilla feel**

With `HostOnlyOnOffSwitch = false`, start a lobby with at least one client. Have the client walk up and press the ShopKeeper's on/off switch. Expected: button press behaves as it does in vanilla (whatever local feedback vanilla gives — typically nothing visible since the host-only RPC is dropped, but no patch interference).

- [ ] **Step 4: Verify enabled (true), non-host blocked**

Edit the cfg, set `HostOnlyOnOffSwitch = true`, restart the game (or use `F5` reload if the in-game config manager supports it). With at least one non-host client, have the client try to press the switch. Expected: nothing happens locally — no cooldown, no RPC, no animation. The host can still press the switch normally.

- [ ] **Step 5: Verify enabled (true), host works**

In the same session, the host presses the switch. Expected: ShopKeeper toggles between Sleep/WakeUp normally, with full vanilla animation and sound.

- [ ] **Step 6: Verify singleplayer + enabled (true)**

Start a singleplayer run with `HostOnlyOnOffSwitch = true`. Press the switch. Expected: works normally (singleplayer = `SemiFunc.IsMultiplayer()` returns false, so the patch returns `true` and vanilla runs).

- [ ] **Step 7: Report results**

Summarize the four verification scenarios (steps 3–6) and any anomalies to the user. If any check fails, do **not** declare the feature complete — debug and fix before closing out.

---

## Self-Review Notes

- **Spec coverage:** All spec sections map to tasks — Config (Task 1), Harmony patch (Task 2), CHANGELOG bump (Task 3), Manual testing (Task 4). The spec's `manifest.json` bump is **not** needed because `manifest.json` is already at `0.3.0`; the plan calls this out at the top instead of creating a no-op task.
- **No placeholders.** Every code step shows actual code, every command shows actual flags, every "expected" line shows actual expected output.
- **Type consistency.** `HostOnlyOnOffSwitch` is spelled identically in Plugin.cs, the patch file, and the CHANGELOG. Patch target string `"WakeUpOrSleepLogic"` matches the decompiled game method name verbatim.
- **Risk noted in spec is preserved:** the patch fails open on any exception, matching `ShopKeeper_AddRuckusScore_Prefix`.
