# CondensedPlayerList — Design

**Date:** 2026-06-02
**Status:** Approved (brainstorming)
**Mod:** `mods/condensed-player-list/` — BepInEx 5 / Harmony plugin for R.E.P.O., netstandard2.1

## Goal

Keep R.E.P.O.'s single-column player list but reduce the vertical gap between rows so more players fit and the list stops running off the bottom of the screen. Inspired by the condensed default view of the [MorePlayerList](https://thunderstore.io/c/repo/p/yazirushi/MorePlayerList/) mod, scoped to a single-column condense only.

## Scope

In scope:

- A condensed (tighter) single-column row spacing for the player list in both the **lobby** menu and the **ESC / pause** menu.

Out of scope:

- Multi-column layouts (the 10:10 two-column view).
- Scroll, Translation, or Custom list modes from MorePlayerList.
- Arrow-key repositioning of the list / R-key reset.
- Photon room-property / host-authoritative sync. This is a purely client-side visual change; every client renders its own list, so no networking is required.
- Config file. Spacing values are hardcoded named constants; a config is a trivial later add if desired.

## Background — how the vanilla list positions itself

Decompiled from `Assembly-CSharp.dll`:

- `MenuPageLobby` and `MenuPageEsc` each instantiate a `menuPlayerListedPrefab` (a `MenuPlayerListed`) per player and assign each a `listSpot` index (0-based, sorted by Photon view ID).
- `MenuPlayerListed.Update()` sets the entry's position **every frame**, purely from `listSpot`:

  ```csharp
  if (!forceCrown)
  {
      if (!SemiFunc.RunIsLobbyMenu())              // ESC / pause menu
          base.transform.localPosition = new Vector3(-23f, -listSpot * 22, 0f);
      else                                          // lobby menu
          base.transform.localPosition = new Vector3(0f, -listSpot * 32, 0f);
  }
  ```

  So the list is one vertical column with a fixed per-row gap of **32** (lobby) or **22** (ESC). `forceCrown` entries are arena winners positioned separately and must be left untouched.

Because position is rewritten every frame, the cleanest override is a Harmony **Postfix** on `MenuPlayerListed.Update()` that runs after vanilla and overwrites `localPosition.y`.

## Design

### Component

A single Harmony patch class, `MenuPlayerListedPatch`, with a `[HarmonyPostfix]` on `MenuPlayerListed.Update()`.

Patch behavior:

1. If the entry has `forceCrown == true`, return immediately (leave vanilla positioning — arena winners).
2. Read `listSpot` from the patched instance.
3. Pick the condensed spacing for the active context using `SemiFunc.RunIsLobbyMenu()`:
   - Lobby: `LobbySpacing` (start `22f`, vanilla is `32f`).
   - ESC: `EscSpacing` (start `15f`, vanilla is `22f`).
4. Preserve vanilla's X for the context (lobby `0f`, ESC `-23f`) and set
   `localPosition = new Vector3(x, -listSpot * spacing, 0f)`.

The X values and the structure deliberately mirror vanilla so the only visible change is the tighter vertical gap.

### Constants

Named `const float` fields on the patch class (or a small static `Layout` holder):

| Name           | Start value | Vanilla | Notes                          |
|----------------|-------------|---------|--------------------------------|
| `LobbySpacing` | `22f`       | `32f`   | Lobby-menu condensed row gap   |
| `EscSpacing`   | `15f`       | `22f`   | ESC-menu condensed row gap     |
| `LobbyX`       | `0f`        | `0f`    | Lobby X (unchanged from vanilla)|
| `EscX`         | `-23f`      | `-23f`  | ESC X (unchanged from vanilla) |

Start values are first guesses; final values are tuned in-game during implementation.

### Plugin wiring

`Plugin.Awake()` already calls `harmony.PatchAll()` (current scaffold). The patch class with Harmony attributes is discovered automatically — no per-patch registration needed.

## Data flow

```
MenuPlayerListed.Update()  (vanilla, every frame)
    └─ sets localPosition.y = -listSpot * 32|22
         └─ [HarmonyPostfix] MenuPlayerListedPatch
              ├─ forceCrown? → return (no change)
              └─ overwrite localPosition = (vanillaX, -listSpot * condensedSpacing, 0)
```

No shared state, no persistence, no networking.

## Error handling

- Null / unexpected state: the postfix touches only the instance's own `transform`, `listSpot`, and `forceCrown` (all guaranteed present on a live `MenuPlayerListed`), plus the static `SemiFunc.RunIsLobbyMenu()`. No external lookups that can fail.
- The patch is a no-op-safe overwrite: if values are wrong, the list is mispositioned but nothing throws or breaks game state. Wrap the body defensively is unnecessary given the narrow surface, but the postfix must never throw (a throwing Harmony patch spams every frame) — keep the body allocation-free and exception-free.

## Testing

TDD discipline:

1. **Unit tests (position math first):** extract the spacing/position computation into a pure, testable static function, e.g.
   `Vector2 CondensedPosition(int listSpot, bool isLobby)` returning `(x, y)`.
   Tests assert:
   - `listSpot = 0` → `y = 0` in both contexts.
   - Lobby: `y = -listSpot * 22`; ESC: `y = -listSpot * 15`.
   - X matches the context (`0` lobby, `-23` ESC).
   - Spacing is strictly less than vanilla (condensed) in both contexts.
2. **In-game verification:** load into a lobby and the ESC menu, confirm rows are tighter, the list fits more players on screen, player heads and name labels track the tighter rows, and arena-winner (`forceCrown`) entries are unaffected. Tune `LobbySpacing` / `EscSpacing` to taste.

## Risks / to validate in implementation

- **Player head focus points:** `MenuPlayerListed` heads render via focus points parented to the list container, not the entry transform. They may need a parallel offset to track the tighter rows; confirm in-game and patch the head focus if they lag.
- **Spacing values:** `15f` / `22f` are first guesses; final values are tuned visually.

## Locations

- Spec: `mods/condensed-player-list/docs/superpowers/specs/2026-06-02-condensed-player-list-design.md` (this file)
- Plan: `mods/condensed-player-list/docs/superpowers/plans/` (next step)
- Patch source: `mods/condensed-player-list/src/Patches/MenuPlayerListedPatches.cs` (new)
