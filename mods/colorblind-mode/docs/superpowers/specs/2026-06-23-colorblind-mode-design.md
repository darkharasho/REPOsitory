# ColorblindMode — design

Date: 2026-06-23
Status: approved (ready for implementation plan)

## Goal

An on/off colorblind accessibility mode for R.E.P.O. that applies a full-screen color-correction
(daltonization) filter so colorblind players can better distinguish hues. The player picks their
colorblindness type; the correction affects the entire rendered image (all game colors), not
individual objects.

## Scope

- Per-type **correction** (daltonization), not simulation.
- Three common types plus off: `Off / Deuteranopia / Protanopia / Tritanopia`.
- An intensity control (0–1) to scale the effect.
- Purely client-side and visual. No Photon networking, no host authority — each player sets their
  own correction.

Out of scope: per-object recoloring, UI-only recoloring, custom shaders/AssetBundles, simulation
mode, more exotic colorblindness types (achromatopsia, anomalous trichromacy variants).

## Approach — pure-code PPv2 Channel Mixer

R.E.P.O. uses Unity's **built-in render pipeline + Post Processing Stack v2** (PPv2;
`Unity.Postprocessing.Runtime.dll` is in `REPO_Data/Managed`, no URP/HDRP present). The game has a
`PostProcessing` singleton (`PostProcessing.Instance`) that owns a `public PostProcessVolume volume`
with an active `PostProcessLayer`-driven setup and existing overrides (Bloom, ColorGrading,
Vignette, …).

PPv2's **Channel Mixer is part of the `ColorGrading` effect**: the nine
`mixerRedOutRedIn / mixerRedOutGreenIn / mixerRedOutBlueIn / mixerGreenOutRedIn / … /
mixerBlueOutBlueIn` fields form a 3×3 linear RGB matrix applied to every pixel. A daltonization
correction is exactly such a 3×3 matrix, so no custom shader is needed.

### Why our own volume (not the game's)

We create our **own** global `PostProcessVolume` + a runtime `PostProcessProfile` containing a
single `ColorGrading` override, rather than mutating `PostProcessing.Instance.volume`:

- The game actively drives its own ColorGrading (saturation/contrast) per frame; writing to it
  would be overwritten and could break the game's look.
- Our volume gets a **higher `priority`** and is placed on the **same GameObject layer** as
  `PostProcessing.Instance.volume.gameObject`, so the existing `PostProcessLayer.volumeLayer` mask
  already includes it and blends it on top. We only override the nine mixer fields (leave all other
  ColorGrading params at default `overrideState = false`), so we don't disturb the game's grading.

## Components

1. **`Plugin.cs`** — binds config, starts the driver, logs load. Harmony only if needed to hook
   scene/camera readiness (likely not — the driver can poll for `PostProcessing.Instance`).
2. **`ColorblindController : MonoBehaviour`** (on the plugin GameObject, persists across levels):
   - Waits until `PostProcessing.Instance?.volume` exists; then lazily creates our volume + profile
     once (`PostProcessManager.instance.QuickVolume(layer, priority, colorGrading)` or a manual
     `new GameObject` + `PostProcessVolume` with `isGlobal = true`).
   - On scene load (`SceneManager.sceneLoaded`) re-verifies our volume still exists / re-resolves
     the layer (mirrors the FaerieFlight per-scene reset pattern).
   - Applies settings whenever config changes (subscribe to `ConfigEntry.SettingChanged`) and on
     creation.
3. **`ColorMatrices.cs`** — the three fixed daltonize matrices (constants) + an `Identity` + a
   `Lerp(identity, matrix, t)` helper. Pure, unit-testable.

## Color math

- Each type maps to a fixed 3×3 daltonization correction matrix `M` (published constants; rows =
  output R/G/B, cols = input R/G/B).
- Effective matrix `Me = Lerp(Identity, M, Intensity)`.
- Write to PPv2 (values are **percent**, identity diagonal = 100): `mixerXOutYIn = Me[x][y] * 100`,
  with each mixer param's `overrideState = true`.
- `Off` → disable our volume (`volume.enabled = false`) so rendering is pristine.
- PPv2 clamps mixer params to [-200, 200]; daltonize entries (~[-0.4, 1.4] ×100) stay in range.

## Config (auto-discovered by REPOConfig)

File: `BepInEx/config/darkharasho.ColorblindMode.cfg`

| Key | Type | Default | Range |
|-----|------|---------|-------|
| `Type` | enum `Off/Deuteranopia/Protanopia/Tritanopia` | `Off` | — |
| `Intensity` | float | `1.0` | `0.0`–`1.0` |

Enum + `AcceptableValueRange<float>` render as a dropdown + slider in REPOConfig with no extra work.
Changes apply live via `SettingChanged`.

## csproj

Add a `<Reference>` to `$(ManagedDir)/Unity.Postprocessing.Runtime.dll` (Private=false), alongside
the standard UnityEngine modules already in the scaffold.

## Testing

- **Unit-testable (no game):** `ColorMatrices.Lerp` and the matrix→mixer mapping (identity at
  Intensity 0, exact matrix at 1, correct ×100 scaling, clamping). Follow TDD for these.
- **In-game (visual):** toggle each type/intensity; confirm hue separation shifts globally and
  `Off`/`Intensity 0` is byte-pristine. Verify across a level transition and that the game's own
  grading (bloom/vignette/saturation) is unchanged.

## Risks / open items

- If `PostProcessManager.QuickVolume` proves awkward from BepInEx, fall back to a manually
  constructed `PostProcessVolume` GameObject — same outcome.
- Exact daltonize matrices to use (e.g. Machado et al. 2009 vs the common GIMP/“daltonize”
  matrices) is an implementation detail; pick one well-cited set and cite it in code comments.
