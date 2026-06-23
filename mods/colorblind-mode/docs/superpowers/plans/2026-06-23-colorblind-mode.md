# ColorblindMode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A R.E.P.O. mod that applies a full-screen colorblind-correction (daltonization) filter, with an in-game type selector and intensity.

**Architecture:** Pure-code Post Processing Stack v2. We add our OWN global `PostProcessVolume` with a single `ColorGrading` override (using its built-in Channel Mixer — the nine `mixer*` fields are a 3×3 RGB matrix) at higher priority than the game's volume, on the same layer so the existing `PostProcessLayer` blends it on top. The game's own volume is never mutated. The color math (cited daltonization matrices composed to a single 3×3, lerped by intensity, scaled to PPv2's percent units) is isolated in a Unity-free static class so it is unit-testable; only the thin "write nine floats onto the ColorGrading object" layer needs the game.

**Tech Stack:** C# / netstandard2.1, BepInEx 5, HarmonyX, Unity built-in RP + Post Processing Stack v2 (`Unity.Postprocessing.Runtime.dll`), xUnit (`dotnet test`) for the pure unit tests.

## Global Constraints

- Assembly / namespace / `BepInPluginName`: `ColorblindMode`; GUID `darkharasho.ColorblindMode` (already in scaffold `ColorblindMode.csproj`).
- Target framework of the mod: `netstandard2.1`. Unity DLLs referenced via `$(ManagedDir)` with `<Private>false</Private>`.
- Config file (auto-discovered by REPOConfig): `BepInEx/config/darkharasho.ColorblindMode.cfg`. Keys: `Type` (enum `Off/Deuteranopia/Protanopia/Tritanopia`, default `Off`); `Intensity` (float, default `1.0`, range `0.0–1.0`).
- Purely client-side and visual: no Photon, no Harmony game-state patches required.
- `ColorMatrices.cs` MUST NOT reference any UnityEngine type (so the test project can compile it standalone).
- PPv2 `FloatParameter` mixer values are **percent**: identity diagonal = `100`, off-diagonal = `0`; engine range is `[-200, 200]`.
- Game install for building: `GAME_DIR=/var/mnt/data/SteamLibrary/steamapps/common/REPO`. Local dotnet needs `DOTNET_ROOT=/home/linuxbrew/.linuxbrew/Cellar/dotnet/10.0.107/libexec` on PATH.

---

### Task 1: ColorMatrices — pure daltonization math + unit tests

**Files:**
- Create: `mods/colorblind-mode/src/ColorMatrices.cs`
- Create: `mods/colorblind-mode/tests/ColorblindMode.Tests.csproj`
- Create: `mods/colorblind-mode/tests/ColorMatricesTests.cs`
- Modify: `mods/colorblind-mode/ColorblindMode.csproj` (exclude `tests/` from the mod build)

**Interfaces:**
- Produces:
  - `enum ColorblindMode.ColorblindType { Off, Deuteranopia, Protanopia, Tritanopia }`
  - `static class ColorblindMode.ColorMatrices` with:
    - `static readonly float[] Identity` (length 9, row-major: index = row*3 + col, row = output channel, col = input channel)
    - `static float[] Correction(ColorblindType type)` → length-9 correction matrix (`Identity` for `Off`)
    - `static float[] ToMixerPercent(ColorblindType type, float intensity)` → length-9 array of PPv2 percent values, in ColorGrading mixer order `[RedOutRedIn, RedOutGreenIn, RedOutBlueIn, GreenOutRedIn, GreenOutGreenIn, GreenOutBlueIn, BlueOutRedIn, BlueOutGreenIn, BlueOutBlueIn]`, each clamped to `[-200, 200]`. `Off` or `intensity == 0` ⇒ identity (`100` on diagonal, `0` elsewhere).

- [ ] **Step 1: Write the mod-csproj exclusion so test files never enter the mod build**

In `mods/colorblind-mode/ColorblindMode.csproj`, add this `ItemGroup` immediately after the closing `</PropertyGroup>` of the `ManagedDir` block (before the `BepInEx.Core` `PackageReference` group):

```xml
  <ItemGroup>
    <!-- The xUnit test project lives under tests/; keep it out of the mod assembly. -->
    <Compile Remove="tests/**/*.cs" />
  </ItemGroup>
```

- [ ] **Step 2: Create the test project**

Create `mods/colorblind-mode/tests/ColorblindMode.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <!-- Compile the production math file directly; it is Unity-free by contract. -->
    <Compile Include="../src/ColorMatrices.cs" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Write the failing tests**

Create `mods/colorblind-mode/tests/ColorMatricesTests.cs`:

```csharp
using ColorblindMode;
using Xunit;

public class ColorMatricesTests
{
    private static void AssertClose(float[] expected, float[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
            Assert.True(System.Math.Abs(expected[i] - actual[i]) < 1e-3f,
                $"index {i}: expected {expected[i]}, got {actual[i]}");
    }

    [Fact]
    public void Off_IsIdentityCorrection()
    {
        AssertClose(ColorMatrices.Identity, ColorMatrices.Correction(ColorblindType.Off));
    }

    [Fact]
    public void Deuteranopia_CorrectionMatchesHandComputedValue()
    {
        // Correction = I + Err*(I - Sim_deuteranopia), hand-computed in the spec.
        float[] expected =
        {
            1f,       0f,       0f,
            -0.4375f, 1.4375f,  0f,
            0.2625f,  -0.5625f, 1.3f,
        };
        AssertClose(expected, ColorMatrices.Correction(ColorblindType.Deuteranopia));
    }

    [Fact]
    public void ToMixerPercent_IntensityZero_IsIdentityPercent()
    {
        float[] expected = { 100, 0, 0, 0, 100, 0, 0, 0, 100 };
        AssertClose(expected, ColorMatrices.ToMixerPercent(ColorblindType.Deuteranopia, 0f));
    }

    [Fact]
    public void ToMixerPercent_Off_IsIdentityPercentRegardlessOfIntensity()
    {
        float[] expected = { 100, 0, 0, 0, 100, 0, 0, 0, 100 };
        AssertClose(expected, ColorMatrices.ToMixerPercent(ColorblindType.Off, 1f));
    }

    [Fact]
    public void ToMixerPercent_IntensityOne_IsCorrectionTimes100()
    {
        float[] expected =
        {
            100f,    0f,      0f,
            -43.75f, 143.75f, 0f,
            26.25f,  -56.25f, 130f,
        };
        AssertClose(expected, ColorMatrices.ToMixerPercent(ColorblindType.Deuteranopia, 1f));
    }

    [Fact]
    public void ToMixerPercent_ClampsToEngineRange()
    {
        foreach (ColorblindType t in new[]
                 { ColorblindType.Deuteranopia, ColorblindType.Protanopia, ColorblindType.Tritanopia })
        foreach (float v in ColorMatrices.ToMixerPercent(t, 1f))
            Assert.InRange(v, -200f, 200f);
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `cd mods/colorblind-mode/tests && DOTNET_ROOT=/home/linuxbrew/.linuxbrew/Cellar/dotnet/10.0.107/libexec PATH=/home/linuxbrew/.linuxbrew/bin:$PATH dotnet test`
Expected: FAIL — `ColorMatrices` / `ColorblindType` do not exist (compile error).

- [ ] **Step 5: Implement ColorMatrices**

Create `mods/colorblind-mode/src/ColorMatrices.cs`:

```csharp
namespace ColorblindMode
{
    public enum ColorblindType { Off, Deuteranopia, Protanopia, Tritanopia }

    /// <summary>
    /// Daltonization color math, deliberately free of any UnityEngine dependency so it can be
    /// unit-tested standalone. Matrices are row-major length-9 arrays: index = row*3 + col, where
    /// row is the OUTPUT channel (R,G,B) and col is the INPUT channel (R,G,B).
    ///
    /// Correction is the standard daltonize map: C = I + E * (I - S), where S is a dichromat
    /// SIMULATION matrix (Viénot/Brettel 1999, widely reproduced) and E is the Fidaner et al.
    /// error-redistribution matrix. Both are linear, so the composite is a single 3x3 the PPv2
    /// channel mixer can apply. (Mixer runs in PPv2's LDR working space, so this is a good
    /// accessibility approximation rather than a colorimetrically exact transform.)
    /// </summary>
    public static class ColorMatrices
    {
        public static readonly float[] Identity = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };

        // Fidaner et al. error-redistribution (shifts lost red/green error into other channels).
        private static readonly float[] ErrShift = { 0, 0, 0, 0.7f, 1, 0, 0.7f, 0, 1 };

        // Viénot 1999 dichromat simulation matrices (sRGB).
        private static readonly float[] SimDeuteranopia =
            { 0.625f, 0.375f, 0f, 0.70f, 0.30f, 0f, 0f, 0.30f, 0.70f };
        private static readonly float[] SimProtanopia =
            { 0.56667f, 0.43333f, 0f, 0.55833f, 0.44167f, 0f, 0f, 0.24167f, 0.75833f };
        private static readonly float[] SimTritanopia =
            { 0.95f, 0.05f, 0f, 0f, 0.43333f, 0.56667f, 0f, 0.475f, 0.525f };

        public static float[] Correction(ColorblindType type)
        {
            float[]? sim = Sim(type);
            if (sim == null) return (float[])Identity.Clone();
            return Add(Identity, Multiply(ErrShift, Sub(Identity, sim)));
        }

        public static float[] ToMixerPercent(ColorblindType type, float intensity)
        {
            float t = intensity < 0f ? 0f : (intensity > 1f ? 1f : intensity);
            float[] m = Lerp(Identity, Correction(type), t);
            var pct = new float[9];
            for (int i = 0; i < 9; i++)
            {
                float v = m[i] * 100f;
                pct[i] = v < -200f ? -200f : (v > 200f ? 200f : v);
            }
            return pct;
        }

        private static float[]? Sim(ColorblindType type) => type switch
        {
            ColorblindType.Deuteranopia => SimDeuteranopia,
            ColorblindType.Protanopia => SimProtanopia,
            ColorblindType.Tritanopia => SimTritanopia,
            _ => null,
        };

        private static float[] Multiply(float[] a, float[] b)
        {
            var r = new float[9];
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    r[row * 3 + col] =
                        a[row * 3 + 0] * b[0 * 3 + col] +
                        a[row * 3 + 1] * b[1 * 3 + col] +
                        a[row * 3 + 2] * b[2 * 3 + col];
            return r;
        }

        private static float[] Add(float[] a, float[] b)
        {
            var r = new float[9];
            for (int i = 0; i < 9; i++) r[i] = a[i] + b[i];
            return r;
        }

        private static float[] Sub(float[] a, float[] b)
        {
            var r = new float[9];
            for (int i = 0; i < 9; i++) r[i] = a[i] - b[i];
            return r;
        }

        private static float[] Lerp(float[] a, float[] b, float t)
        {
            var r = new float[9];
            for (int i = 0; i < 9; i++) r[i] = a[i] + (b[i] - a[i]) * t;
            return r;
        }
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `cd mods/colorblind-mode/tests && DOTNET_ROOT=/home/linuxbrew/.linuxbrew/Cellar/dotnet/10.0.107/libexec PATH=/home/linuxbrew/.linuxbrew/bin:$PATH dotnet test`
Expected: PASS — 6 tests passed.

- [ ] **Step 7: Commit**

```bash
cd /var/home/mstephens/Documents/GitHub/REPOsitory
git add mods/colorblind-mode/src/ColorMatrices.cs mods/colorblind-mode/tests/ mods/colorblind-mode/ColorblindMode.csproj
git commit -m "ColorblindMode: daltonization color math + unit tests"
```

---

### Task 2: Config binding + Post Processing reference

**Files:**
- Modify: `mods/colorblind-mode/ColorblindMode.csproj` (add `Unity.Postprocessing.Runtime` reference)
- Modify: `mods/colorblind-mode/src/Plugin.cs`

**Interfaces:**
- Consumes: `ColorblindType` (Task 1).
- Produces:
  - `static ConfigEntry<ColorblindType> Plugin.Type`
  - `static ConfigEntry<float> Plugin.Intensity`
  - `static ManualLogSource Plugin.Log`

- [ ] **Step 1: Add the Post Processing reference to the csproj**

In `mods/colorblind-mode/ColorblindMode.csproj`, inside the existing `<ItemGroup>` that holds the `<Reference Include="UnityEngine">` entries, add:

```xml
    <Reference Include="Unity.Postprocessing.Runtime">
      <HintPath>$(ManagedDir)/Unity.Postprocessing.Runtime.dll</HintPath>
      <Private>false</Private>
    </Reference>
```

- [ ] **Step 2: Bind config in Plugin.cs**

Replace the body of `mods/colorblind-mode/src/Plugin.cs` with:

```csharp
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ColorblindMode
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;
        internal static ConfigEntry<ColorblindType> Type = null!;
        internal static ConfigEntry<float> Intensity = null!;

        private void Awake()
        {
            Log = Logger;

            Type = Config.Bind("General", "Type", ColorblindType.Off,
                "Colorblindness type to correct for. Off restores the game's default colors.");
            Intensity = Config.Bind("General", "Intensity", 1.0f,
                new ConfigDescription(
                    "Strength of the correction. 0 = no change, 1 = full correction.",
                    new AcceptableValueRange<float>(0f, 1f)));

            new Harmony(PluginInfo.PLUGIN_GUID).PatchAll();
            gameObject.AddComponent<ColorblindController>();
            Log.LogInfo($"ColorblindMode v{PluginInfo.PLUGIN_VERSION} loaded.");
        }
    }
}
```

> Note: this references `ColorblindController`, created in Task 3. The build will fail until Task 3 exists; that is expected and is why Step 3 only checks the config types compile via the test project is N/A here — proceed to Task 3 before building. To keep this task independently green, temporarily comment the `gameObject.AddComponent<ColorblindController>();` line, build, then uncomment in Task 3.

- [ ] **Step 3: Verify the config code compiles (controller line commented)**

Temporarily comment the `gameObject.AddComponent<ColorblindController>();` line, then run:
`cd mods/colorblind-mode && DOTNET_ROOT=/home/linuxbrew/.linuxbrew/Cellar/dotnet/10.0.107/libexec PATH=/home/linuxbrew/.linuxbrew/bin:$PATH dotnet build ColorblindMode.csproj -c Release /p:GameDir="/var/mnt/data/SteamLibrary/steamapps/common/REPO"`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
cd /var/home/mstephens/Documents/GitHub/REPOsitory
git add mods/colorblind-mode/ColorblindMode.csproj mods/colorblind-mode/src/Plugin.cs
git commit -m "ColorblindMode: bind Type/Intensity config + reference Post Processing"
```

---

### Task 3: ColorblindController — build the volume and apply the matrix

**Files:**
- Create: `mods/colorblind-mode/src/ColorblindController.cs`
- Modify: `mods/colorblind-mode/src/Plugin.cs` (uncomment the `AddComponent` line from Task 2)

**Interfaces:**
- Consumes: `ColorMatrices.ToMixerPercent` (Task 1); `Plugin.Type`, `Plugin.Intensity`, `Plugin.Log` (Task 2).
- Produces: `ColorblindController : MonoBehaviour` (no public members consumed elsewhere).

- [ ] **Step 1: Uncomment the controller hookup in Plugin.cs**

In `mods/colorblind-mode/src/Plugin.cs`, ensure the line reads (uncommented):

```csharp
            gameObject.AddComponent<ColorblindController>();
```

- [ ] **Step 2: Implement the controller**

Create `mods/colorblind-mode/src/ColorblindController.cs`:

```csharp
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

namespace ColorblindMode
{
    /// <summary>
    /// Lives on the plugin GameObject (persists across levels). Lazily creates our OWN global
    /// PostProcessVolume holding a single ColorGrading override, placed on the same layer as the
    /// game's volume so the existing PostProcessLayer blends it on top, at a higher priority. We
    /// only override ColorGrading's nine channel-mixer fields, so the game's own grading is
    /// untouched. The mixer fields are a 3x3 matrix; we feed them from ColorMatrices.
    /// </summary>
    public class ColorblindController : MonoBehaviour
    {
        private PostProcessVolume? _volume;
        private ColorGrading? _grading;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Plugin.Type.SettingChanged += OnSettingChanged;
            Plugin.Intensity.SettingChanged += OnSettingChanged;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Plugin.Type.SettingChanged -= OnSettingChanged;
            Plugin.Intensity.SettingChanged -= OnSettingChanged;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();
        private void OnSettingChanged(object sender, System.EventArgs e) => Apply();

        private void Update()
        {
            // The game's PostProcessing singleton isn't ready immediately; build once it exists.
            if (_volume == null && PostProcessing.Instance != null && PostProcessing.Instance.volume != null)
                Apply();
        }

        private void Apply()
        {
            try
            {
                if (!EnsureVolume()) return;

                var type = Plugin.Type.Value;
                bool on = type != ColorblindType.Off;
                _volume!.enabled = on;
                _grading!.enabled.Override(on);
                if (!on) return;

                float[] pct = ColorMatrices.ToMixerPercent(type, Plugin.Intensity.Value);
                _grading.gradingMode.Override(GradingMode.LowDefinitionRange);
                Set(_grading.mixerRedOutRedIn,    pct[0]);
                Set(_grading.mixerRedOutGreenIn,  pct[1]);
                Set(_grading.mixerRedOutBlueIn,   pct[2]);
                Set(_grading.mixerGreenOutRedIn,  pct[3]);
                Set(_grading.mixerGreenOutGreenIn,pct[4]);
                Set(_grading.mixerGreenOutBlueIn, pct[5]);
                Set(_grading.mixerBlueOutRedIn,   pct[6]);
                Set(_grading.mixerBlueOutGreenIn, pct[7]);
                Set(_grading.mixerBlueOutBlueIn,  pct[8]);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"ColorblindController.Apply: {ex}");
            }
        }

        private static void Set(FloatParameter p, float value)
        {
            p.overrideState = true;
            p.value = value;
        }

        private bool EnsureVolume()
        {
            if (_volume != null && _grading != null) return true;

            var gameVol = PostProcessing.Instance != null ? PostProcessing.Instance.volume : null;
            if (gameVol == null) return false;

            int layer = gameVol.gameObject.layer;          // same layer => existing PostProcessLayer sees it
            _grading = ScriptableObject.CreateInstance<ColorGrading>();
            _grading.enabled.Override(true);
            // Priority above the game's so our mixer wins where it overrides.
            _volume = PostProcessManager.instance.QuickVolume(layer, gameVol.priority + 1000f, _grading);
            _volume.isGlobal = true;
            _volume.weight = 1f;
            Object.DontDestroyOnLoad(_volume.gameObject);
            Plugin.Log.LogInfo($"[ColorblindMode] volume created on layer {layer}.");
            return true;
        }
    }
}
```

- [ ] **Step 3: Build the mod**

Run: `cd mods/colorblind-mode && DOTNET_ROOT=/home/linuxbrew/.linuxbrew/Cellar/dotnet/10.0.107/libexec PATH=/home/linuxbrew/.linuxbrew/bin:$PATH dotnet build ColorblindMode.csproj -c Release /p:GameDir="/var/mnt/data/SteamLibrary/steamapps/common/REPO"`
Expected: `Build succeeded. 0 Error(s)`. (Warnings about nullable are acceptable.)

- [ ] **Step 4: Re-run unit tests (guard against accidental ColorMatrices breakage)**

Run: `cd mods/colorblind-mode/tests && DOTNET_ROOT=/home/linuxbrew/.linuxbrew/Cellar/dotnet/10.0.107/libexec PATH=/home/linuxbrew/.linuxbrew/bin:$PATH dotnet test`
Expected: PASS — 6 tests.

- [ ] **Step 5: Commit**

```bash
cd /var/home/mstephens/Documents/GitHub/REPOsitory
git add mods/colorblind-mode/src/ColorblindController.cs mods/colorblind-mode/src/Plugin.cs
git commit -m "ColorblindMode: apply correction via own PPv2 ColorGrading volume"
```

---

### Task 4: Finalize docs/version and package

**Files:**
- Modify: `mods/colorblind-mode/CHANGELOG.md`
- Modify: `mods/colorblind-mode/manifest.json` (only if bumping; stays `0.1.0` for first release)

- [ ] **Step 1: Write the changelog entry**

Replace `mods/colorblind-mode/CHANGELOG.md` with:

```markdown
# Changelog

## 0.1.0
- Initial release: full-screen colorblind correction (daltonization) via Post Processing Stack v2 channel mixer. Config: Type (Off/Deuteranopia/Protanopia/Tritanopia) + Intensity (0–1), adjustable in-game via REPOConfig.
```

- [ ] **Step 2: Confirm an icon exists, then package**

Run:
```bash
cd mods/colorblind-mode
ls icon.png || echo "MISSING icon.png — add a 256x256 icon.png before packaging"
DOTNET_ROOT=/home/linuxbrew/.linuxbrew/Cellar/dotnet/10.0.107/libexec PATH=/home/linuxbrew/.linuxbrew/bin:$PATH GAME_DIR="/var/mnt/data/SteamLibrary/steamapps/common/REPO" ./package.sh
```
Expected: either the "MISSING icon.png" line (then the user supplies the icon and re-runs), or `Packaged: .../builds/ColorblindMode-0.1.0.zip`.

- [ ] **Step 3: Commit**

```bash
cd /var/home/mstephens/Documents/GitHub/REPOsitory
git add mods/colorblind-mode/CHANGELOG.md
git commit -m "ColorblindMode 0.1.0: changelog"
```

---

## In-game verification (manual, after Task 3)

The PPv2 wiring and visual result can't be unit-tested. After installing the zip via r2modman:

1. Launch with `Type = Off` → colors look exactly vanilla; log shows `volume created on layer N`.
2. Set `Type = Deuteranopia`, `Intensity = 1` in the REPOConfig menu → reds/greens visibly shift; effect is full-screen (world + UI).
3. Slide `Intensity` 0→1 → smooth ramp from vanilla to full correction.
4. Cycle `Protanopia` / `Tritanopia` → distinct shifts per type.
5. Trigger a game effect that uses grading (e.g. damage vignette / bloom) → confirm the game's own post FX still look normal (we didn't clobber its volume).
6. Cross a level transition → correction persists, no errors in the log.

## Self-Review

- **Spec coverage:** type selector + intensity (Task 2 config); per-type daltonize matrices + lerp + percent mapping (Task 1); own higher-priority volume on the game's layer, never mutating the game volume (Task 3 `EnsureVolume`); live apply via `SettingChanged` and scene-load re-check (Task 3); `Off` disables our volume (Task 3 `Apply`); PP reference in csproj (Task 2); REPOConfig auto-discovery via enum + `AcceptableValueRange` (Task 2). All covered.
- **Placeholder scan:** none — every code/test step is complete; matrices and expected test values are concrete and hand-derived.
- **Type consistency:** `ColorblindType`, `ColorMatrices.ToMixerPercent`, `Plugin.Type/Intensity/Log`, and the nine `mixer*` `FloatParameter` field names match across tasks and the verified PPv2 API.
