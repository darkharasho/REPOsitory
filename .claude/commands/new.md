---
description: Scaffold a new R.E.P.O. mod in mods/<kebab-name>/ and kick off superpowers brainstorming for implementation
argument-hint: <kebab-name> <one-line description...>
allowed-tools: Bash, Read, Write, Edit, Glob, Grep, Skill
---

You are scaffolding a brand-new R.E.P.O. BepInEx mod inside this monorepo and then handing off to the `superpowers:brainstorming` skill to drive design + implementation.

The first arg `$1` is the kebab-case directory name (e.g. `coin-magnet`). All remaining args `$2` onward are a free-form one-line description (e.g. "pulls nearby coins toward the player").

If the user did not provide both, ask for them and stop. Do not invent a name from the description alone.

## Step 1 — derive names and confirm with the user

Derive:
- `KEBAB` = `$1` (e.g. `coin-magnet`)
- `PASCAL` = PascalCase of `$1` with separators stripped (e.g. `CoinMagnet`) — this is the assembly name, namespace, `BepInPluginName`, and zip prefix.
- `GUID` = `darkharasho.<PASCAL>` (matches the convention used by `darkharasho.MiniEepo` / `darkharasho.UpgradeLimiter`).
- `DESCRIPTION` = `$2 $3 ...` joined as a single sentence — goes verbatim into `manifest.json`.

Before writing anything, confirm `KEBAB`, `PASCAL`, and `DESCRIPTION` back to the user in one short message and ask if dependencies beyond `BepInEx-BepInExPack-5.4.2100` are needed (e.g. `Vippy-ScalerCore-0.5.0` for scaling, REPOConfig, etc.). Default to BepInEx only if they say no.

If `mods/<KEBAB>/` already exists, stop and report — do not overwrite.

## Step 2 — scaffold the standard mod layout

Create exactly this tree under `mods/<KEBAB>/`. Every file matches the shared template extracted from `mods/mini-eepo` and `mods/upgrade-limiter`. Use Write for each file.

```
mods/<KEBAB>/
  CLAUDE.md
  CHANGELOG.md
  CONTRIBUTING.md
  README.md
  manifest.json
  nuget.config
  package.sh                  # chmod +x after writing
  <PASCAL>.csproj
  .gitignore
  .claude/settings.json
  src/Plugin.cs
  docs/superpowers/plans/.gitkeep
  docs/superpowers/specs/.gitkeep
```

### File contents

**`manifest.json`**
```json
{
    "name": "<PASCAL>",
    "version_number": "0.1.0",
    "website_url": "https://github.com/darkharasho/REPOsitory",
    "description": "<DESCRIPTION>",
    "dependencies": [
        "BepInEx-BepInExPack-5.4.2100"
    ]
}
```
(Add additional `dependencies` entries the user confirmed in step 1.)

**`<PASCAL>.csproj`** — netstandard2.1, version pulled from `manifest.json` via regex, BepInEx + Harmony from NuGet, game DLLs (UnityEngine modules, Assembly-CSharp, Photon) referenced via `$(ManagedDir)`. Copy the structure from `mods/upgrade-limiter/UpgradeLimiter.csproj` verbatim, substituting `UpgradeLimiter` → `<PASCAL>` and `darkharasho.UpgradeLimiter` → `<GUID>`. If the user requested ScalerCore, also add a `libs/ScalerCore.dll` `<Reference>` block like `mods/mini-eepo/MiniEepo.csproj` and create an empty `libs/` dir with a `.gitkeep` (the dll itself is user-supplied, not committed).

**`nuget.config`** — exact copy of `mods/mini-eepo/nuget.config` (nuget.org + BepInEx feed).

**`package.sh`** — exact same shape as `mods/upgrade-limiter/package.sh` but with `<PASCAL>` substituted. Critically, the OUT path must be `BUILDS_DIR="$(cd "$(dirname "$0")/../.." && pwd)/builds"` so the zip lands in the monorepo's top-level `builds/` — matching the convention enforced by `/build`. Make sure the file is `chmod +x`.

**`src/Plugin.cs`** — minimal but follows the conventions both existing mods share:
```csharp
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace <PASCAL>
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;

        private void Awake()
        {
            Log = Logger;
            var harmony = new Harmony("<GUID>");
            harmony.PatchAll();
            Log.LogInfo($"<PASCAL> v{PluginInfo.PLUGIN_VERSION} loaded.");
        }
    }
}
```
Do NOT scaffold a `SettingsSyncer` / Photon room-property sync upfront. That pattern (push/pull with `_lastPushed` dedup and `Active*` mirrors) is real and lives in both existing mods, but it should only be added when the brainstorming step actually decides multiplayer sync is in scope.

**`.gitignore`** — copy from `mods/upgrade-limiter/.gitignore` (covers `bin/`, `obj/`, `*.zip`, `*.png`, `.idea/`, `.vs/`, etc.).

**`.claude/settings.json`** — `{}` (empty, like the others).

**`CLAUDE.md`**
```markdown
## Project Context

<DESCRIPTION>
```

**`README.md`** — title `# <PASCAL>`, one-paragraph description, install section pointing at Thunderstore Mod Manager / r2modman and `BepInEx/plugins/<PASCAL>/`, config-file line `Config file: \`BepInEx/config/<GUID>.cfg\``. Mirror the structure of `mods/upgrade-limiter/README.md`.

**`CHANGELOG.md`**
```markdown
# Changelog

## 0.1.0
- Initial scaffold
```

**`CONTRIBUTING.md`** — copy from `mods/upgrade-limiter/CONTRIBUTING.md`, substituting names. Drop the "How discovery works" tail section unless brainstorming later decides this mod uses a similar discovery mechanism.

## Step 3 — wire up the GitHub Actions workflow

Create `.github/workflows/build-<KEBAB>.yml` at the **repo root** (not inside the mod dir — GitHub Actions only reads workflows from the root). Use the same template as `build-upgrade-limiter.yml`: path-filtered to `mods/<KEBAB>/**` and the workflow file itself, `working-directory: mods/<KEBAB>`, builds `<PASCAL>.csproj` on the self-hosted runner with `/p:GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO/` (per the saved memory of the user's R.E.P.O. install).

## Step 4 — verify scaffolding

Run `ls -R mods/<KEBAB>` and confirm the tree. Then report a short summary to the user listing what was created and reminding them:
- `icon.png` (256×256) must be added before running `/build <KEBAB>` for a Thunderstore-ready zip.
- The current `Plugin.cs` is a no-op skeleton — the next step (step 5) drives the actual design.

## Step 5 — hand off to superpowers brainstorming

This is the whole point of the command. Invoke the `superpowers:brainstorming` skill via the Skill tool with a prompt that includes:
- The mod name (`<PASCAL>`) and one-line description (`<DESCRIPTION>`).
- Where the scaffold lives (`mods/<KEBAB>/`).
- That this is a BepInEx 5 plugin for R.E.P.O. targeting netstandard2.1, Harmony-based, and that multiplayer-aware behavior (Photon room property push/pull, host-authoritative limits with `Active*` mirrors and `_lastPushed` dedup) is an available pattern in this monorepo if the mod's behavior needs to be host-authoritative.
- That design docs should land in `mods/<KEBAB>/docs/superpowers/specs/` and plans in `mods/<KEBAB>/docs/superpowers/plans/`.
- That implementation follows superpowers TDD discipline once the spec + plan are agreed.

Do NOT begin writing mod logic yourself in this command. The brainstorming skill is the entry point for design; it will route into `writing-plans` and then `executing-plans` / `subagent-driven-development` on the user's signal.
