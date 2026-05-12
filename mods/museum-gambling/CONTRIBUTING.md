# Building & Contributing

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download)
- A R.E.P.O. install (for game DLLs — BepInEx is pulled via NuGet automatically)

## Building

**Windows:**
```bash
dotnet build MuseumGambling.csproj --configuration Release
# Override game path if not the default Steam location:
dotnet build MuseumGambling.csproj --configuration Release /p:GameDir="D:\Games\REPO"
```

**Linux (Steam):**
```bash
./package.sh
# Override game path:
GAME_DIR="/path/to/steamapps/common/REPO" ./package.sh
```

Output DLL: `bin/Release/netstandard2.1/MuseumGambling.dll`

## Packaging for Thunderstore

A 256×256 `icon.png` must exist at the repo root before packaging (not committed — create your own).

```bash
./package.sh    # builds and zips in one step
```

Outputs `MuseumGambling-<version>.zip` ready for Thunderstore upload.

**Manual zip** (no subdirectory):
- `manifest.json`
- `icon.png`
- `README.md`
- `bin/Release/netstandard2.1/MuseumGambling.dll`

## Releasing

1. Bump `version_number` in `manifest.json` (`package.sh` reads it automatically)
2. Add an entry to `CHANGELOG.md`
3. Run `./package.sh` to build and package
4. Upload the zip to [Thunderstore](https://thunderstore.io/c/repo/) — each release requires a new version number
