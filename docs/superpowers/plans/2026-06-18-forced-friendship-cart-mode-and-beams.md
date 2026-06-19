# ForcedFriendship Cart Mode + Tether Beams Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an anchor-mode setting (Buddy vs Cart) and colored client-side tether beams to the ForcedFriendship R.E.P.O. mod.

**Architecture:** The pure damage math (`DamageCalculator`) gains anchor-resolution and beam-zone classification so the host damage loop and a new per-client `BeamRenderer` share one source of truth. The host-only driver branches on the new mode; the renderer draws `LineRenderer` tethers on every client. Beams are cosmetic; damage stays host-authoritative.

**Tech Stack:** C# (netstandard2.1), BepInEx 5.4.21, HarmonyLib, Photon PUN, UnityEngine, xUnit (net6.0 test project).

## Global Constraints

- Target framework for the mod: `netstandard2.1`. Test project: `net6.0`.
- Version floor: this release is `0.2.0` — update `manifest.json` (single source; csproj/PluginInfo derive from it).
- Mod GUID/name unchanged: `darkharasho.ForcedFriendship` / `ForcedFriendship`.
- `DamageCalculator` and all types it references MUST stay Unity-free (no UnityEngine/Photon/BepInEx) so they remain unit-testable.
- Game-internal fields (`PhysGrabCart.isSmallCart` is public; `PlayerAvatar.deadSet`/`isDisabled` are internal) — read internals via `AccessTools.FieldRefAccess`, public ones directly.
- Beams must never mutate gameplay state or run host-authoritative logic.
- **TEST_CMD** (run from `mods/forced-friendship/`):
  ```bash
  DOTNET_ROOT=/var/home/linuxbrew/.linuxbrew/Cellar/dotnet/10.0.107/libexec \
  DOTNET_ROLL_FORWARD=Major \
  GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO \
  dotnet test tests/ForcedFriendship.Tests/ForcedFriendship.Tests.csproj
  ```
  (Roll-forward is required because only the .NET 10 runtime is installed; the test project targets net6.0.)
- **BUILD_CMD** (run from `mods/forced-friendship/`):
  ```bash
  GameDir="/var/mnt/data/SteamLibrary/steamapps/common/REPO" dotnet build ForcedFriendship.csproj -c Release
  ```
- **PACKAGE_CMD** (run from `mods/forced-friendship/`):
  ```bash
  GAME_DIR="/var/mnt/data/SteamLibrary/steamapps/common/REPO/" ./package.sh
  ```

---

### Task 1: Pure anchor + beam-zone model in `DamageCalculator`

Add anchor resolution, damage-from-anchors, and beam-zone classification as pure
functions; refactor the existing `Evaluate` to use them (Buddy mode) so all 16
existing tests stay green.

**Files:**
- Modify: `mods/forced-friendship/src/DamageCalculator.cs`
- Test: `mods/forced-friendship/tests/ForcedFriendship.Tests/DamageCalculatorTests.cs`

**Interfaces:**
- Consumes: existing `PlayerState`, `DamageSettings`, `Distance`, `Band`.
- Produces (relied on by Tasks 3 & 4):
  - `enum AnchorMode { Buddy, Cart }`
  - `enum BeamZone { Safe, Warn, Danger }`
  - `readonly struct AnchorResult` with `float X, Y, Z; float Distance; bool HasAnchor;`, ctor `AnchorResult(float x, float y, float z, float distance, bool hasAnchor)`, and `static AnchorResult None { get; }`
  - `static AnchorResult[] ResolveAnchors(IReadOnlyList<PlayerState> players, AnchorMode mode, bool hasCart, float cartX, float cartY, float cartZ)`
  - `static int[] EvaluateDamage(IReadOnlyList<AnchorResult> anchors, in DamageSettings s)`
  - `static BeamZone Classify(float distance, float safeDistance, float warnPercent)`
  - `Evaluate(players, settings)` keeps its existing signature/behavior.

- [ ] **Step 1: Write the failing tests**

Append to `tests/ForcedFriendship.Tests/DamageCalculatorTests.cs` (inside the class):

```csharp
    // --- Classify: safeDistance=15, warnPercent=0.25 -> warnStart=11.25 ---

    [Theory]
    [InlineData(0f, BeamZone.Safe)]
    [InlineData(11.24f, BeamZone.Safe)]
    [InlineData(11.25f, BeamZone.Warn)]   // exactly at warn edge
    [InlineData(15f, BeamZone.Warn)]      // exactly at safe radius -> still warn (not damaged yet)
    [InlineData(15.01f, BeamZone.Danger)] // past safe radius -> taking damage
    public void Classify_maps_distance_to_zone(float distance, BeamZone expected)
    {
        Assert.Equal(expected, DamageCalculator.Classify(distance, safeDistance: 15f, warnPercent: 0.25f));
    }

    [Fact]
    public void Classify_warnPercent_zero_has_no_yellow_zone()
    {
        Assert.Equal(BeamZone.Safe, DamageCalculator.Classify(14.9f, 15f, 0f));
        Assert.Equal(BeamZone.Danger, DamageCalculator.Classify(15.1f, 15f, 0f));
    }

    // --- ResolveAnchors ---

    [Fact]
    public void ResolveAnchors_buddy_picks_nearest_living_other()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(100f, 0f, 0f, alive: true),
            new PlayerState(31f, 0f, 0f, alive: true), // nearest to player 0
        };
        var anchors = DamageCalculator.ResolveAnchors(players, AnchorMode.Buddy, hasCart: false, 0f, 0f, 0f);
        Assert.True(anchors[0].HasAnchor);
        Assert.Equal(31f, anchors[0].Distance, precision: 4);
        Assert.Equal(31f, anchors[0].X, precision: 4); // anchored on the nearest other's position
    }

    [Fact]
    public void ResolveAnchors_buddy_lone_living_player_has_no_anchor()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(5f, 0f, 0f, alive: false),
        };
        var anchors = DamageCalculator.ResolveAnchors(players, AnchorMode.Buddy, hasCart: false, 0f, 0f, 0f);
        Assert.False(anchors[0].HasAnchor);
    }

    [Fact]
    public void ResolveAnchors_dead_self_has_no_anchor()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: false),
            new PlayerState(3f, 0f, 0f, alive: true),
        };
        var anchors = DamageCalculator.ResolveAnchors(players, AnchorMode.Buddy, hasCart: false, 0f, 0f, 0f);
        Assert.False(anchors[0].HasAnchor);
    }

    [Fact]
    public void ResolveAnchors_cart_measures_every_living_player_to_cart()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(3f, 0f, 0f, alive: true),
        };
        var anchors = DamageCalculator.ResolveAnchors(players, AnchorMode.Cart, hasCart: true, 0f, 0f, 4f);
        Assert.Equal(4f, anchors[0].Distance, precision: 4);
        Assert.Equal(5f, anchors[1].Distance, precision: 4); // (3,0,0)->(0,0,4) = 5
        Assert.Equal(4f, anchors[0].Z, precision: 4);        // anchored on the cart
    }

    [Fact]
    public void ResolveAnchors_cart_falls_back_to_buddy_when_no_cart()
    {
        var players = new[]
        {
            new PlayerState(0f, 0f, 0f, alive: true),
            new PlayerState(31f, 0f, 0f, alive: true),
        };
        var anchors = DamageCalculator.ResolveAnchors(players, AnchorMode.Cart, hasCart: false, 0f, 0f, 0f);
        Assert.Equal(31f, anchors[0].Distance, precision: 4); // nearest other, not the (ignored) cart args
    }

    // --- EvaluateDamage ---

    [Fact]
    public void EvaluateDamage_applies_band_formula_per_anchor()
    {
        var anchors = new[]
        {
            new AnchorResult(0f, 0f, 0f, distance: 31f, hasAnchor: true), // band 3 -> 15
            new AnchorResult(0f, 0f, 0f, distance: 0f, hasAnchor: false), // no anchor -> 0
        };
        var result = DamageCalculator.EvaluateDamage(anchors, Settings());
        Assert.Equal(new[] { 15, 0 }, result);
    }

    [Fact]
    public void EvaluateDamage_disabled_returns_all_zero()
    {
        var anchors = new[] { new AnchorResult(0f, 0f, 0f, 100f, true) };
        var result = DamageCalculator.EvaluateDamage(anchors, Settings(enabled: false));
        Assert.Equal(new[] { 0 }, result);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: TEST_CMD
Expected: FAIL — compile errors / missing `AnchorMode`, `BeamZone`, `AnchorResult`, `ResolveAnchors`, `EvaluateDamage`, `Classify`.

- [ ] **Step 3: Add the pure model to `DamageCalculator.cs`**

Add these enums/struct above `DamageCalculator` (inside `namespace ForcedFriendship`, after the `DamageSettings` struct):

```csharp
    /// <summary>Where each player measures its distance from.</summary>
    public enum AnchorMode
    {
        /// <summary>Nearest living other player (the original rule).</summary>
        Buddy,
        /// <summary>The main hauling cart.</summary>
        Cart,
    }

    /// <summary>Beam color band for the tether visual.</summary>
    public enum BeamZone
    {
        Safe,   // green
        Warn,   // yellow
        Danger, // red — taking damage
    }

    /// <summary>One player's resolved anchor position and distance to it.</summary>
    public readonly struct AnchorResult
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly float Distance;
        public readonly bool HasAnchor;

        public AnchorResult(float x, float y, float z, float distance, bool hasAnchor)
        {
            X = x;
            Y = y;
            Z = z;
            Distance = distance;
            HasAnchor = hasAnchor;
        }

        public static AnchorResult None => new AnchorResult(0f, 0f, 0f, 0f, false);
    }
```

Inside the `DamageCalculator` class, add `ResolveAnchors`, `Classify`, and `EvaluateDamage`, and refactor `Evaluate` to delegate. Replace the existing `Evaluate` method body with the delegation and add the new methods alongside it:

```csharp
        /// <summary>
        /// Resolves each player's anchor position and distance. In Buddy mode the anchor
        /// is the nearest living OTHER player. In Cart mode every living player anchors on
        /// the cart; if <paramref name="hasCart"/> is false, Cart mode falls back to Buddy.
        /// Dead players get <see cref="AnchorResult.None"/>.
        /// </summary>
        public static AnchorResult[] ResolveAnchors(
            IReadOnlyList<PlayerState> players,
            AnchorMode mode, bool hasCart, float cartX, float cartY, float cartZ)
        {
            var result = new AnchorResult[players.Count];
            bool useCart = mode == AnchorMode.Cart && hasCart;
            var cart = new PlayerState(cartX, cartY, cartZ, true);

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState self = players[i];
                if (!self.Alive) { result[i] = AnchorResult.None; continue; }

                if (useCart)
                {
                    result[i] = new AnchorResult(cartX, cartY, cartZ, Distance(self, cart), true);
                    continue;
                }

                float nearest = float.PositiveInfinity;
                int nearestIdx = -1;
                for (int j = 0; j < players.Count; j++)
                {
                    if (j == i) continue;
                    PlayerState other = players[j];
                    if (!other.Alive) continue;

                    float d = Distance(self, other);
                    if (d < nearest) { nearest = d; nearestIdx = j; }
                }

                if (nearestIdx < 0) { result[i] = AnchorResult.None; continue; }
                PlayerState n = players[nearestIdx];
                result[i] = new AnchorResult(n.X, n.Y, n.Z, nearest, true);
            }

            return result;
        }

        /// <summary>
        /// Classifies a distance into a beam color band. Past the safe radius is Danger
        /// (taking damage). Within the last <paramref name="warnPercent"/> fraction of the
        /// safe radius (and up to/including it) is Warn. Otherwise Safe. A warnPercent of 0
        /// (or less) removes the yellow zone; values are clamped to [0, 1].
        /// </summary>
        public static BeamZone Classify(float distance, float safeDistance, float warnPercent)
        {
            if (distance > safeDistance) return BeamZone.Danger;
            if (warnPercent <= 0f) return BeamZone.Safe;
            float p = warnPercent > 1f ? 1f : warnPercent;
            float warnStart = safeDistance * (1f - p);
            return distance >= warnStart ? BeamZone.Warn : BeamZone.Safe;
        }

        /// <summary>
        /// HP to apply per player this tick from precomputed anchors. A player with no
        /// anchor takes none; otherwise damage = band(distance) * DamagePerBand.
        /// </summary>
        public static int[] EvaluateDamage(IReadOnlyList<AnchorResult> anchors, in DamageSettings s)
        {
            var result = new int[anchors.Count];
            if (!s.Enabled) return result;

            for (int i = 0; i < anchors.Count; i++)
            {
                AnchorResult a = anchors[i];
                if (!a.HasAnchor) continue;
                int band = Band(a.Distance, s.SafeDistance, s.BandWidth);
                result[i] = band * s.DamagePerBand;
            }

            return result;
        }
```

Then replace the existing `Evaluate` method body so it delegates (keeps Buddy behavior + 16 existing tests green):

```csharp
        /// <summary>
        /// Buddy-mode convenience: each living player is damaged by the band of the distance
        /// to its nearest living other player. Equivalent to ResolveAnchors(Buddy) + EvaluateDamage.
        /// </summary>
        public static int[] Evaluate(IReadOnlyList<PlayerState> players, in DamageSettings s)
        {
            AnchorResult[] anchors = ResolveAnchors(players, AnchorMode.Buddy, false, 0f, 0f, 0f);
            return EvaluateDamage(anchors, s);
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: TEST_CMD
Expected: PASS — all prior 16 tests plus the new Classify/ResolveAnchors/EvaluateDamage tests (≈26 total).

- [ ] **Step 5: Commit**

```bash
cd mods/forced-friendship
git add src/DamageCalculator.cs tests/ForcedFriendship.Tests/DamageCalculatorTests.cs
git commit -m "ForcedFriendship: pure anchor + beam-zone model in DamageCalculator

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: Unity helpers — `PlayerLiveness` and `CartLocator`

Extract the dead/disabled check the driver already does into a shared helper (so the
beam renderer reuses it), and add a cart finder. No unit tests (these touch the game
DLL); verified by build + existing tests staying green.

**Files:**
- Create: `mods/forced-friendship/src/PlayerLiveness.cs`
- Create: `mods/forced-friendship/src/CartLocator.cs`
- Modify: `mods/forced-friendship/src/ForcedFriendshipDriver.cs` (use `PlayerLiveness`)

**Interfaces:**
- Consumes: `PlayerAvatar`, `PhysGrabCart` (Assembly-CSharp); `AccessTools` (HarmonyLib).
- Produces (relied on by Tasks 3 & 4):
  - `static class PlayerLiveness` with `static bool IsAlive(PlayerAvatar pa)`
  - `static class CartLocator` with `static PhysGrabCart? FindMainCart()` (returns the
    `!isSmallCart` cart if present, else any cart, else null)

- [ ] **Step 1: Create `src/PlayerLiveness.cs`**

```csharp
using HarmonyLib;

namespace ForcedFriendship
{
    /// <summary>
    /// Shared liveness check. PlayerAvatar.deadSet / .isDisabled are 'internal' in
    /// Assembly-CSharp and this mod builds against the un-publicized game DLL, so they
    /// are read via cached field-ref delegates.
    /// </summary>
    internal static class PlayerLiveness
    {
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> DeadSetRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("deadSet");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> IsDisabledRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isDisabled");

        internal static bool IsAlive(PlayerAvatar pa) => !DeadSetRef(pa) && !IsDisabledRef(pa);
    }
}
```

- [ ] **Step 2: Create `src/CartLocator.cs`**

```csharp
using UnityEngine;

namespace ForcedFriendship
{
    /// <summary>
    /// Finds the main hauling cart in the level. A level may contain a small cart and the
    /// main cart (PhysGrabCart.isSmallCart distinguishes them); the main cart is preferred.
    /// Returns null when no cart exists yet. Callers should cache the result — this scans.
    /// </summary>
    internal static class CartLocator
    {
        internal static PhysGrabCart? FindMainCart()
        {
            PhysGrabCart[] carts = Object.FindObjectsOfType<PhysGrabCart>();
            PhysGrabCart? fallback = null;
            foreach (PhysGrabCart c in carts)
            {
                if (c == null) continue;
                if (!c.isSmallCart) return c;
                fallback ??= c;
            }
            return fallback;
        }
    }
}
```

- [ ] **Step 3: Refactor the driver to use `PlayerLiveness`**

In `src/ForcedFriendshipDriver.cs`, delete the two private `FieldRef` fields
(`DeadSetRef`, `IsDisabledRef`) and their comment block, and replace the liveness
computation inside the foreach loop.

Remove:
```csharp
        // PlayerAvatar.deadSet / .isDisabled are 'internal' in Assembly-CSharp and this mod
        // builds against the un-publicized game DLL, so they are read via cached field-ref delegates.
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> DeadSetRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("deadSet");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> IsDisabledRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isDisabled");
```

Change:
```csharp
                bool alive = !DeadSetRef(pa) && !IsDisabledRef(pa);
```
to:
```csharp
                bool alive = PlayerLiveness.IsAlive(pa);
```

Then remove the now-unused `using HarmonyLib;` line at the top of the driver if no
other `HarmonyLib` reference remains (it does not — verify the build).

- [ ] **Step 4: Build to verify it compiles**

Run: BUILD_CMD
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 5: Run tests to verify no regression**

Run: TEST_CMD
Expected: PASS (same count as end of Task 1; the pure tests are unaffected).

- [ ] **Step 6: Commit**

```bash
cd mods/forced-friendship
git add src/PlayerLiveness.cs src/CartLocator.cs src/ForcedFriendshipDriver.cs
git commit -m "ForcedFriendship: extract PlayerLiveness + add CartLocator

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: Config entries + driver cart-mode wiring

Add the new config (anchor mode + beam settings) and make the host driver branch on
the mode using the cart locator and the pure anchor functions.

**Files:**
- Modify: `mods/forced-friendship/src/Plugin.cs`
- Modify: `mods/forced-friendship/src/ForcedFriendshipDriver.cs`

**Interfaces:**
- Consumes: `AnchorMode`, `ResolveAnchors`, `EvaluateDamage` (Task 1); `CartLocator`,
  `PlayerLiveness` (Task 2).
- Produces (relied on by Task 4):
  - `Plugin.Mode` : `ConfigEntry<AnchorMode>`
  - `Plugin.BeamsEnabled` : `ConfigEntry<bool>`
  - `Plugin.BeamsShowAll` : `ConfigEntry<bool>`
  - `Plugin.BeamsWarnPercent` : `ConfigEntry<float>`

- [ ] **Step 1: Add config fields and binds in `Plugin.cs`**

Add the fields alongside the existing `ConfigEntry` declarations:

```csharp
        internal static ConfigEntry<AnchorMode> Mode = null!;
        internal static ConfigEntry<bool> BeamsEnabled = null!;
        internal static ConfigEntry<bool> BeamsShowAll = null!;
        internal static ConfigEntry<float> BeamsWarnPercent = null!;
```

In `Awake`, after the existing `TickInterval` bind and before the `Harmony` setup,
add:

```csharp
            Mode = Config.Bind("General", "AnchorMode", AnchorMode.Buddy,
                "Buddy = stay near the nearest living player (default). " +
                "Cart = stay near the main hauling cart instead.");

            BeamsEnabled = Config.Bind("Beams", "Enabled", true,
                "Draw a tether beam from each player to their anchor.");
            BeamsShowAll = Config.Bind("Beams", "ShowAllPlayers", true,
                "Show beams for every living player. If false, only your own beam is drawn.");
            BeamsWarnPercent = Config.Bind("Beams", "WarnPercent", 0.25f,
                new ConfigDescription(
                    "Fraction of SafeDistance, at the outer edge, where the beam turns yellow " +
                    "before it turns red. 0 disables the yellow zone.",
                    new AcceptableValueRange<float>(0f, 1f)));
```

- [ ] **Step 2: Branch the driver on anchor mode**

In `src/ForcedFriendshipDriver.cs`, replace the block that builds `settings`, calls
`DamageCalculator.Evaluate`, and applies damage. Currently:

```csharp
            var settings = new DamageSettings(
                enabled: true,
                safeDistance: Plugin.SafeDistance.Value,
                bandWidth: Plugin.BandWidth.Value,
                damagePerBand: Plugin.DamagePerBand.Value);

            int[] damage = DamageCalculator.Evaluate(_states, settings);
```

Replace with:

```csharp
            var settings = new DamageSettings(
                enabled: true,
                safeDistance: Plugin.SafeDistance.Value,
                bandWidth: Plugin.BandWidth.Value,
                damagePerBand: Plugin.DamagePerBand.Value);

            bool hasCart = false;
            float cx = 0f, cy = 0f, cz = 0f;
            if (Plugin.Mode.Value == AnchorMode.Cart)
            {
                PhysGrabCart? cart = CartLocator.FindMainCart();
                if (cart != null)
                {
                    Vector3 cp = cart.transform.position;
                    hasCart = true;
                    cx = cp.x; cy = cp.y; cz = cp.z;
                }
            }

            AnchorResult[] anchors =
                DamageCalculator.ResolveAnchors(_states, Plugin.Mode.Value, hasCart, cx, cy, cz);
            int[] damage = DamageCalculator.EvaluateDamage(anchors, settings);
```

The existing damage-application loop below (`for (int i = 0; i < damage.Length; i++)`)
is unchanged.

- [ ] **Step 3: Build to verify it compiles**

Run: BUILD_CMD
Expected: `Build succeeded.` 0 errors. (Confirms `PhysGrabCart`, `AnchorResult`,
`Plugin.Mode` all resolve.)

- [ ] **Step 4: Run tests to verify no regression**

Run: TEST_CMD
Expected: PASS (unchanged count — driver is not unit-tested).

- [ ] **Step 5: Commit**

```bash
cd mods/forced-friendship
git add src/Plugin.cs src/ForcedFriendshipDriver.cs
git commit -m "ForcedFriendship: add AnchorMode + beam config and cart-mode damage

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: `BeamRenderer` (client-side tether beams)

Draw a colored `LineRenderer` from each rendered player to their anchor on every
client, sharing the pure anchor/zone logic with the host.

**Files:**
- Create: `mods/forced-friendship/src/BeamRenderer.cs`
- Modify: `mods/forced-friendship/src/Plugin.cs` (register the component in `Awake`)

**Interfaces:**
- Consumes: `Plugin.*` config (Task 3), `DamageCalculator.ResolveAnchors` /
  `Classify` / `AnchorMode` / `BeamZone` (Task 1), `CartLocator`, `PlayerLiveness`
  (Task 2), `GameDirector.instance.PlayerList`, `PlayerAvatar.instance`.
- Produces: `internal class BeamRenderer : MonoBehaviour`.

- [ ] **Step 1: Create `src/BeamRenderer.cs`**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ForcedFriendship
{
    /// <summary>
    /// Runs on every client. Each frame it draws a colored LineRenderer from each rendered
    /// player to their anchor (nearest buddy or the cart), sharing DamageCalculator's pure
    /// anchor + zone logic with the host. Purely cosmetic — it never applies damage.
    /// Green = safe, yellow = approaching the edge, red = past the safe radius (taking damage).
    /// </summary>
    internal class BeamRenderer : MonoBehaviour
    {
        private const float BeamHeight = 1f;   // raise endpoints to chest height
        private const float BeamWidth = 0.05f;
        private const float CartRescanInterval = 1f;

        private readonly Dictionary<PlayerAvatar, LineRenderer> _lines =
            new Dictionary<PlayerAvatar, LineRenderer>();
        private readonly List<PlayerState> _states = new List<PlayerState>();
        private readonly List<PlayerAvatar> _avatars = new List<PlayerAvatar>();
        private readonly List<PlayerAvatar> _stale = new List<PlayerAvatar>();

        private static Material? _material;
        private PhysGrabCart? _cart;
        private float _cartRescan;

        private void Update()
        {
            if (!Plugin.Enabled.Value || !Plugin.BeamsEnabled.Value || !Plugin.IsInGameplay())
            {
                HideAll();
                return;
            }

            var list = GameDirector.instance?.PlayerList;
            if (list == null) { HideAll(); return; }

            bool cartMode = Plugin.Mode.Value == AnchorMode.Cart;
            _cartRescan -= Time.deltaTime;
            if (cartMode && (_cart == null || _cartRescan <= 0f))
            {
                _cart = CartLocator.FindMainCart();
                _cartRescan = CartRescanInterval;
            }

            _states.Clear();
            _avatars.Clear();
            foreach (var pa in list)
            {
                if (pa == null) continue;
                Vector3 pos = pa.transform.position;
                _states.Add(new PlayerState(pos.x, pos.y, pos.z, PlayerLiveness.IsAlive(pa)));
                _avatars.Add(pa);
            }

            bool hasCart = cartMode && _cart != null;
            float cx = 0f, cy = 0f, cz = 0f;
            if (hasCart) { Vector3 cp = _cart!.transform.position; cx = cp.x; cy = cp.y; cz = cp.z; }

            AnchorResult[] anchors =
                DamageCalculator.ResolveAnchors(_states, Plugin.Mode.Value, hasCart, cx, cy, cz);

            PlayerAvatar? local = PlayerAvatar.instance;
            bool showAll = Plugin.BeamsShowAll.Value;
            float safe = Plugin.SafeDistance.Value;
            float warn = Plugin.BeamsWarnPercent.Value;

            for (int i = 0; i < _avatars.Count; i++)
            {
                PlayerAvatar pa = _avatars[i];
                AnchorResult a = anchors[i];
                bool render = a.HasAnchor && (showAll || pa == local);
                if (!render) { HideLine(pa); continue; }

                LineRenderer lr = GetLine(pa);
                PlayerState self = _states[i];
                Vector3 from = new Vector3(self.X, self.Y, self.Z) + Vector3.up * BeamHeight;
                Vector3 to = new Vector3(a.X, a.Y, a.Z) + Vector3.up * BeamHeight;
                lr.enabled = true;
                lr.SetPosition(0, from);
                lr.SetPosition(1, to);
                Color c = ZoneColor(DamageCalculator.Classify(a.Distance, safe, warn));
                lr.startColor = c;
                lr.endColor = c;
            }

            CleanupDeparted();
        }

        private void OnDisable() => HideAll();

        private LineRenderer GetLine(PlayerAvatar pa)
        {
            if (_lines.TryGetValue(pa, out var existing) && existing != null) return existing;

            var go = new GameObject("FF_Beam");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = BeamMaterial();
            lr.widthMultiplier = BeamWidth;
            lr.positionCount = 2;
            lr.numCapVertices = 2;
            lr.useWorldSpace = true;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _lines[pa] = lr;
            return lr;
        }

        private void HideLine(PlayerAvatar pa)
        {
            if (_lines.TryGetValue(pa, out var lr) && lr != null) lr.enabled = false;
        }

        private void HideAll()
        {
            foreach (var lr in _lines.Values)
                if (lr != null) lr.enabled = false;
        }

        // Destroy LineRenderers whose player left the list (e.g. disconnected).
        private void CleanupDeparted()
        {
            _stale.Clear();
            foreach (var key in _lines.Keys)
                if (key == null || !_avatars.Contains(key)) _stale.Add(key);
            foreach (var key in _stale)
            {
                if (_lines.TryGetValue(key, out var lr) && lr != null) Destroy(lr.gameObject);
                _lines.Remove(key);
            }
        }

        private static Material BeamMaterial()
        {
            if (_material == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                _material = new Material(shader);
            }
            return _material;
        }

        private static Color ZoneColor(BeamZone zone)
        {
            switch (zone)
            {
                case BeamZone.Danger: return Color.red;
                case BeamZone.Warn: return Color.yellow;
                default: return Color.green;
            }
        }
    }
}
```

- [ ] **Step 2: Register the renderer in `Plugin.Awake`**

In `src/Plugin.cs`, find:
```csharp
            gameObject.AddComponent<ForcedFriendshipDriver>();
```
and add the beam renderer right after it:
```csharp
            gameObject.AddComponent<ForcedFriendshipDriver>();
            gameObject.AddComponent<BeamRenderer>();
```

- [ ] **Step 3: Build to verify it compiles**

Run: BUILD_CMD
Expected: `Build succeeded.` 0 errors, 0 warnings.

- [ ] **Step 4: Manual in-game verification (record result)**

Build/package (PACKAGE_CMD), install the zip via r2modman, launch a level, and confirm:
- Buddy mode (default): beams point to the nearest living player; green when close,
  yellow near the edge, red while bleeding. Behavior matches 0.1.0 for damage.
- Cart mode (`AnchorMode = Cart`): beams point to the main cart; walking away turns
  the beam yellow then red and you bleed; before the cart exists it falls back to a
  buddy beam.
- `ShowAllPlayers = false`: only your own beam is drawn. `Beams/Enabled = false`: no beams.

Note: this step is observational; if no game session is available, record "deferred
to user" and proceed — the build is the gate for committing code.

- [ ] **Step 5: Commit**

```bash
cd mods/forced-friendship
git add src/BeamRenderer.cs src/Plugin.cs
git commit -m "ForcedFriendship: client-side colored tether beams (BeamRenderer)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 5: Version bump + docs + package

Ship 0.2.0: bump the version, document the new config, and produce the zip.

**Files:**
- Modify: `mods/forced-friendship/manifest.json`
- Modify: `mods/forced-friendship/CHANGELOG.md`
- Modify: `mods/forced-friendship/README.md`

**Interfaces:** none (docs/packaging only).

- [ ] **Step 1: Bump the version**

In `manifest.json`, change:
```json
    "version_number": "0.1.0",
```
to:
```json
    "version_number": "0.2.0",
```

- [ ] **Step 2: Add the CHANGELOG entry**

Insert below the `# Changelog` heading, above `## 0.1.0`:

```markdown
## 0.2.0
- New `AnchorMode` setting: `Buddy` (stay near the nearest living player, the original
  behavior) or `Cart` (stay near the main hauling cart). Cart mode falls back to the
  buddy rule until a cart exists in the level.
- Tether beams: a colored line is drawn from each player to their anchor —
  green when safe, yellow approaching the edge, red while taking damage.
- Config: `Beams/Enabled`, `Beams/ShowAllPlayers`, `Beams/WarnPercent`.

```

- [ ] **Step 3: Update the README config table and notes**

In `README.md`, replace the config table (lines for `Enabled`…`TickInterval`) with the
full set:

```markdown
| Key | Section | Default | Meaning |
|-----|---------|---------|---------|
| `Enabled` | General | `true` | Master on/off switch |
| `AnchorMode` | General | `Buddy` | `Buddy` = stay near the nearest living player; `Cart` = stay near the main hauling cart |
| `SafeDistance` | General | `15` | Units within which your anchor keeps you safe |
| `BandWidth` | General | `8` | Units per additional damage band beyond the safe radius |
| `DamagePerBand` | General | `5` | HP per tick, multiplied by the band number |
| `TickInterval` | General | `2.0` | Seconds between damage evaluations |
| `Enabled` | Beams | `true` | Draw a tether beam from each player to their anchor |
| `ShowAllPlayers` | Beams | `true` | Show every player's beam; if false, only your own |
| `WarnPercent` | Beams | `0.25` | Outer fraction of `SafeDistance` where the beam turns yellow before red (0 disables yellow) |
```

Then update the intro paragraph (line 3) to mention cart mode and beams, e.g. append:
"Choose whether your lifeline is the nearest player or the cart, and a colored beam
shows you how safe you are at a glance."

And update the "Only the host's settings apply…" paragraph to note that beam display
is local to each client (driven by that client's `Beams/*` settings), while damage
remains host-authoritative.

- [ ] **Step 4: Build, test, and package**

Run, from `mods/forced-friendship/`:
```bash
# (TEST_CMD)
DOTNET_ROOT=/var/home/linuxbrew/.linuxbrew/Cellar/dotnet/10.0.107/libexec DOTNET_ROLL_FORWARD=Major GameDir=/var/mnt/data/SteamLibrary/steamapps/common/REPO dotnet test tests/ForcedFriendship.Tests/ForcedFriendship.Tests.csproj
# (PACKAGE_CMD)
GAME_DIR="/var/mnt/data/SteamLibrary/steamapps/common/REPO/" ./package.sh
```
Expected: tests PASS; `Packaged: …/builds/ForcedFriendship-0.2.0.zip`.

- [ ] **Step 5: Commit**

```bash
cd mods/forced-friendship
git add manifest.json CHANGELOG.md README.md
git commit -m "ForcedFriendship: 0.2.0 — cart mode + tether beams (docs + version)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review Notes

- **Spec coverage:** AnchorMode setting (Task 3) ✓; Cart = `PhysGrabCart` measurement (Tasks 2–3) ✓; cart→buddy fallback (Task 1 `ResolveAnchors`, Task 3 wiring) ✓; beams to cart or players (Task 4) ✓; green/yellow/red via shared `Classify` (Tasks 1 & 4) ✓; everyone's beams + `ShowAllPlayers`/`Enabled`/`WarnPercent` config (Tasks 3–4) ✓; host-authoritative damage unchanged (Task 3 keeps the apply loop) ✓; version/CHANGELOG/README (Task 5) ✓.
- **Type consistency:** `Plugin.Mode` (ConfigEntry) vs `AnchorMode` (enum) named distinctly to avoid collision; `ResolveAnchors`/`EvaluateDamage`/`Classify`/`AnchorResult`/`BeamZone` signatures match between defining Task 1 and consuming Tasks 3–4; `PlayerLiveness.IsAlive` / `CartLocator.FindMainCart` match between Task 2 and consumers.
- **No placeholders:** all code shown in full; the only observational step (Task 4 Step 4) is explicitly allowed to defer to the user without blocking commits.
