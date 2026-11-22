#!/usr/bin/env bash
# Regenerates lib/refs: reference-only copies (public API signatures, no method bodies) of the
# R.E.P.O. assemblies the mods compile against, so CI can build without the game installed.
# Rerun after a game update that changes these APIs. BepInEx and Harmony come from NuGet.
# Needs the refasmer tool: dotnet tool install -g JetBrains.Refasmer.CliTool
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
managed="${GAME_DIR:-/var/mnt/data/SteamLibrary/steamapps/common/REPO}/REPO_Data/Managed"
out="$root/lib/refs"

# Every game assembly any mods/*/*.csproj references through $(ManagedDir).
dlls=(Assembly-CSharp UnityEngine UnityEngine.CoreModule UnityEngine.PhysicsModule UnityEngine.IMGUIModule
  UnityEngine.TextRenderingModule UnityEngine.AudioModule PhotonUnityNetworking PhotonRealtime Photon3Unity3D)

rm -rf "$out"
mkdir -p "$out"
for name in "${dlls[@]}"; do refasmer -q -c --omit-non-api-members=true -O "$out" "$managed/$name.dll"; done
ls -l "$out"
