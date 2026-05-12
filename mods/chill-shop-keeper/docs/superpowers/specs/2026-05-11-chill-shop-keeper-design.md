# ChillShopKeeper — Design

## Purpose

A R.E.P.O. BepInEx mod that prevents the in-shop ShopKeeper from punishing players for "ruckus" (gunfire, explosions, melee, attacking the shopkeeper, etc.). Two layers of control:

- `DisableGlobally` — kill-switch. ShopKeeper ignores ruckus from everyone, regardless of the in-shop toggle state.
- **Per-player exemption** — for each player ever observed in a lobby, a `Player_<SteamID>` toggle. When on, the ShopKeeper ignores ruckus from that specific player. Entries are created lazily on first observation and persist across sessions.

`DisableGlobally` overrides everything. Otherwise, a player is exempt iff their personal toggle is on.

## Why no multiplayer sync

`ShopKeeper`'s ruckus methods (`AddRuckusScore`, `PlayerCausingRuckusHost`, `PlayerShopKeeper*Host`, `ResolveRuckusPunishment`) all run on the Photon master client only — the game is host-authoritative for ruckus/punishment decisions. The host's local config is therefore the source of truth by construction. No Photon room property push/pull is needed.

A client running ChillShopKeeper while not the host has no effect on punishment; the host's settings win. This is fine and expected — same model as `upgrade-limiter`'s enforcement.

## Architecture

Single-assembly Harmony plugin with three small parts.

### Components

**`Plugin`** (BepInEx `BaseUnityPlugin`)

- Declares `ConfigEntry<bool> DisableGlobally` (static `[General]` section).
- Holds the dynamic per-player `ConfigEntry<bool>` map populated by `PlayerRegistry`.
- Applies Harmony patches in `Awake`.

**`PlayerRegistry`** (static)

- Maintains `ConcurrentDictionary<string, ConfigEntry<bool>> ExemptByPlayer` keyed by Steam ID (string).
- `Observe(string steamID, string displayName)` — idempotently `Bind`s `Player_<SteamID>` in the `[Players]` section with description `"Exempt <displayName> from ShopKeeper punishment (default: false)"`. `BepInEx.Configuration.ConfigFile.Bind` returns the existing entry if the key already exists, so calling this every time a player is observed is cheap and correct. Re-observation does not update the description (BepInEx descriptions are write-once); the first-seen display name sticks for the life of the cfg file.
- `IsExempt(string steamID) → bool` — returns the entry's `Value` if registered, else `false`. Non-allocating fast path used in the patch.

**`PlayerObserverPatches`** (`[HarmonyPatch]` class)

- Postfix on `PlayerAvatar.Start` (or whichever lifecycle method runs once after the avatar is fully initialized — confirmed at planning time). Reads `steamID` and `playerName` off the avatar and calls `PlayerRegistry.Observe`.
- This way, every player encountered in any lobby — host, joiner, or solo player — gets a config entry created automatically the first time they're seen, and re-registered (no-op) on subsequent sessions.

**`ShopKeeperPatches`** (`[HarmonyPatch]` class)

- Prefix on `ShopKeeper.AddRuckusScore`. Pulls the `PlayerAvatar` argument's Steam ID, evaluates `ChillPolicy.ShouldSkip(...)`, returns `false` to skip the original method when exempt.

**`ChillPolicy`** (static helper)

- Pure predicate: `ShouldSkip(bool disableGlobally, bool playerExempt) → bool`
- Returns `disableGlobally || playerExempt`.
- Extracted so the decision is unit-testable without Unity / Photon / BepInEx.

### Patch flow

```
PlayerAvatar.Start (postfix)
  → PlayerRegistry.Observe(avatar.steamID, avatar.playerName)
       → ConfigFile.Bind("Players", $"Player_{steamID}", false, "Exempt <name> ...")
            (idempotent: returns existing entry on re-bind)

ShopKeeper.AddRuckusScore(playerArg, ...) (prefix)
  → string sid = ResolveSteamID(playerArg);
    bool exempt = PlayerRegistry.IsExempt(sid);
    if (ChillPolicy.ShouldSkip(DisableGlobally.Value, exempt))
        return false;  // skip original
    return true;       // run original
```

`DisableGlobally.Value` and individual `ConfigEntry<bool>.Value` are read directly on each call — no mirrors, no `_lastPushed`. Live toggling via REPOConfig Just Works because BepInEx writes the new value into the entry immediately.

## Configuration

File: `BepInEx/config/darkharasho.ChillShopKeeper.cfg`

Example after a couple of sessions:

```
[General]
DisableGlobally = false

[Players]
Player_76561198000000001 = false   ; Exempt Alice from ShopKeeper punishment
Player_76561198000000002 = true    ; Exempt Bob from ShopKeeper punishment
Player_76561198000000003 = false   ; Exempt Charlie from ShopKeeper punishment
```

| Key | Default | Description |
|-----|---------|-------------|
| `[General] DisableGlobally` | `false` | ShopKeeper ignores ruckus from everyone. Overrides per-player toggles. |
| `[Players] Player_<SteamID>` | `false` | When true, the named player is exempt from ShopKeeper punishment. Entry is created automatically the first time the player is observed in a lobby and persists across sessions. |

REPOConfig auto-renders both sections. Each per-player entry shows with its first-observed display name in the description column.

### Display-name rename handling

If a player changes their Steam display name after their entry exists, the description shows the stale name. The toggle still works correctly (Steam ID is the stable key). Acceptable trade-off — re-binding to update the description would require deleting and recreating the entry, which would also reset its value to `false`. If this becomes a problem in practice, a future revision can write display names to a sibling `[PlayerNames] <SteamID> = <name>` section that's regenerated each session.

## Player identification

- **Steam ID accessor on `PlayerAvatar`** — `steamID` field/property (exact accessor — field vs property, public vs internal — confirmed during the planning step via decompilation). Fallback: `SemiFunc.PlayerGetSteamID(PlayerAvatar)` static helper exists per the symbol dump.
- **Display name accessor** — `playerName` field/property on `PlayerAvatar` (confirmed at planning).
- **Resolving the player from `AddRuckusScore`'s argument** — depends on the actual signature. If the argument is a `PlayerAvatar`, read `.steamID` directly. If it's a `string steamID`, use it directly. Confirmed at planning time.

## Singleplayer / offline behavior

In solo play the local player is observed via the same `PlayerAvatar.Start` postfix and gets their own `Player_<SteamID>` entry. Toggling it exempts them. `DisableGlobally` works the same. No special-case code.

## Error handling

- The `AddRuckusScore` prefix is wrapped in `try { ... } catch (Exception ex) { Plugin.Log.LogError(ex); return true; }` — any unexpected error fails open (original method runs). A bug in this mod must never brick the shopkeeper.
- The `PlayerAvatar.Start` postfix is wrapped the same way — failure to observe a player just means no entry is created; punishment continues to work normally.
- Harmony attach failures at startup are logged but do not throw out of `Awake`; BepInEx continues loading other mods.

## Testing strategy

### Unit tests (TDD)

`ChillPolicy.ShouldSkip` truth table — 4 cases (2²):

| disableGlobally | playerExempt | expected |
|----------------:|-------------:|---------:|
| false | false | false |
| false | true  | **true** |
| true  | false | **true** |
| true  | true  | **true** |

`PlayerRegistry` is also unit-testable with a real `ConfigFile` pointed at a temp path:

- `Observe` creates an entry with default `false`.
- `Observe` is idempotent — calling it again with the same Steam ID returns the same entry and does not reset the value.
- `IsExempt` returns `false` for an unknown Steam ID.
- `IsExempt` reflects the entry's current `Value`.
- An entry's value persists across `ConfigFile` reloads from the same path.

Tests live in `mods/chill-shop-keeper/tests/ChillShopKeeper.Tests/` — exact test-project conventions for this monorepo settled at planning time (neither existing mod has tests yet; we establish the pattern here).

### Manual integration test

In-game smoke test for the full surface:

- **Baseline:** with all toggles off, verify shopkeeper punishment still works (regression check).
- **Per-player toggle:** in multiplayer, host toggles `Player_<self-steam-id>` on. Host shoots in shop — no punishment. Second player shoots — gets punished.
- **DisableGlobally:** toggle on. Nobody gets punished, including non-exempt players. Toggle off mid-session via REPOConfig — punishment returns immediately.
- **Entry creation:** join a fresh lobby with a player the host hasn't seen before. Verify a new `Player_<SteamID>` line appears in the cfg file after the player spawns.
- **Persistence:** restart the game with a previously-toggled exemption. Verify the toggle survives.

## Out of scope (YAGNI)

- Photon room property sync — not needed; host-authoritative by game design.
- Per-action granularity (e.g., "allow melee but punish guns") — not asked for.
- Display-name auto-refresh when players rename — see "Display-name rename handling".
- A UI for exempting players who have never been observed (i.e., adding a future player's Steam ID up front) — entries are observation-based; users can hand-add `Player_<id> = true` to the cfg if they really want, but it's not a designed flow.
- Visual/audio cue when punishment is suppressed — silent is fine.
- Soft caps / partial exemption — out of scope; this is a binary toggle per player.
- Exporting / importing exemption lists between players — out of scope.

## Open questions resolved during planning

- Exact `ShopKeeper.AddRuckusScore` parameter list (PlayerAvatar vs string SteamID, return type, additional args).
- Exact accessors on `PlayerAvatar`: `steamID` and `playerName` (field vs property, visibility).
- Exact lifecycle method to postfix for player observation (`Start`, `Awake`, `OnEnable`, or something else that runs once per avatar after networked init).
- Test-project conventions (location, naming, runner) for the monorepo.
