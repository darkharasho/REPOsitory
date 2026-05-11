---
description: Build a mod and package it to builds/<ModName>-<version>.zip
argument-hint: <mod_name>
allowed-tools: Bash, Read
---

Build the mod `$1` in this monorepo.

Steps:
1. Resolve the mod directory: `mods/$1` (relative to repo root `/var/home/mstephens/Documents/GitHub/REPOsitory`). If it doesn't exist, list `mods/` and ask the user which one they meant.
2. Run `./package.sh` from inside that mod directory. It builds in Release and writes the Thunderstore zip to the monorepo's top-level `builds/` directory as `builds/<ModName>-<version>.zip` (where `<ModName>` and `<version>` come from the mod's `manifest.json`).
3. On success, report the produced zip path. On failure, surface the build error and stop — do not attempt fixes unless asked.

Notes:
- `package.sh` auto-detects the R.E.P.O. game install under Steam; pass `GAME_DIR=...` via the environment if it can't find it.
- `builds/` is gitignored, so output won't be committed.
- Do not modify source code as part of this command.
