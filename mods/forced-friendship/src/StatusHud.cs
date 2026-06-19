using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForcedFriendship
{
    /// <summary>
    /// Runs on every client and is the single source of truth for display: each frame it builds
    /// player states, tracks each player's post-truck grace timer (the host reads it to suppress
    /// damage), resolves anchors, and computes every player's display zone (grace/truck force
    /// Safe). BeamRenderer draws from this data so the beam and the on-screen indicator can never
    /// disagree. Also draws the local indicators: a grace countdown and a subtle status border.
    /// All display is local — never synced.
    /// </summary>
    internal class StatusHud : MonoBehaviour
    {
        internal static StatusHud? Instance;

        private readonly Dictionary<PlayerAvatar, float> _grace = new Dictionary<PlayerAvatar, float>();
        private readonly List<PlayerAvatar> _stale = new List<PlayerAvatar>();
        private readonly List<PlayerState> _states = new List<PlayerState>();
        private readonly List<PlayerAvatar> _avatars = new List<PlayerAvatar>();
        private readonly List<PhysGrabCart> _carts = new List<PhysGrabCart>();
        private readonly List<Vec3> _cartPositions = new List<Vec3>();
        private float _cartRescan;

        private AnchorResult[] _anchors = Array.Empty<AnchorResult>();
        private BeamZone[] _zones = Array.Empty<BeamZone>();

        /// <summary>Number of tracked players this frame; index range for the *At accessors.</summary>
        internal int PlayerCount { get; private set; }
        internal PlayerAvatar AvatarAt(int i) => _avatars[i];
        internal AnchorResult AnchorAt(int i) => _anchors[i];
        internal BeamZone ZoneAt(int i) => _zones[i];
        internal Vector3 PlayerPosAt(int i)
        {
            PlayerState s = _states[i];
            return new Vector3(s.X, s.Y, s.Z);
        }

        /// <summary>The local player's current zone (truck/grace force Safe). Valid when LocalActive.</summary>
        internal BeamZone LocalZone { get; private set; } = BeamZone.Safe;
        internal bool LocalActive { get; private set; }

        private static Texture2D? _tex;
        private GUIStyle? _timerStyle;

        private void Awake() => Instance = this;

        internal bool IsInGrace(PlayerAvatar pa) =>
            Plugin.ActiveGracePeriod > 0 && _grace.TryGetValue(pa, out var g) && g > 0f;

        internal float Remaining(PlayerAvatar pa) => _grace.TryGetValue(pa, out var g) ? g : 0f;

        private void Update()
        {
            // When the rule is disabled (locally or by the host), suppress ALL display — the grace
            // countdown and the screen-edge status border — exactly as the beams and damage are.
            if (!Plugin.ActiveEnabled || !Plugin.IsInGameplay())
            {
                _grace.Clear();
                PlayerCount = 0;
                LocalActive = false;
                return;
            }

            var list = GameDirector.instance?.PlayerList;
            if (list == null) { PlayerCount = 0; LocalActive = false; return; }

            float period = Plugin.ActiveGracePeriod;
            float dt = Time.deltaTime;

            // Build player states and update each player's grace timer. A player in the truck has
            // full grace; once out it counts down. Fresh/unseen players start with full grace
            // (they spawn in the truck).
            _states.Clear();
            _avatars.Clear();
            foreach (var pa in list)
            {
                if (pa == null) continue;
                bool inTruck = PlayerLiveness.IsInTruck(pa);
                if (!_grace.TryGetValue(pa, out var g)) g = period;
                g = inTruck ? period : Mathf.Max(0f, g - dt);
                if (g > period) g = period;
                _grace[pa] = g;

                Vector3 p = pa.transform.position;
                _states.Add(new PlayerState(p.x, p.y, p.z, PlayerLiveness.IsAlive(pa), inTruck));
                _avatars.Add(pa);
            }

            // Drop grace entries for players who left.
            _stale.Clear();
            foreach (var k in _grace.Keys)
                if (k == null || !_avatars.Contains(k)) _stale.Add(k!);
            foreach (var k in _stale) _grace.Remove(k);

            // Resolve anchors (with the current cart roster) and compute each player's display zone.
            AnchorMode mode = Plugin.ActiveMode;
            _cartRescan -= dt;
            if (mode != AnchorMode.Cart) _carts.Clear();
            else if (_carts.Count == 0 || _cartRescan <= 0f) { CartLocator.FindMainCarts(_carts); _cartRescan = 1f; }

            _cartPositions.Clear();
            if (mode == AnchorMode.Cart)
                foreach (var c in _carts)
                {
                    if (c == null) continue;
                    Vector3 cp = c.transform.position;
                    _cartPositions.Add(new Vec3(cp.x, cp.y, cp.z));
                }

            _anchors = DamageCalculator.ResolveAnchors(_states, mode, _cartPositions, Plugin.ActiveIncludeHeight);

            int count = _states.Count;
            if (_zones.Length < count) _zones = new BeamZone[count];
            float safeD = Plugin.ActiveSafeDistance;
            float warnF = Plugin.WarnFraction;
            for (int i = 0; i < count; i++)
            {
                BeamZone z = DamageCalculator.ZoneForAnchor(_anchors[i], safeD, warnF);
                if (IsInGrace(_avatars[i])) z = BeamZone.Safe;   // grace -> green
                _zones[i] = z;
            }
            PlayerCount = count;

            // Local player's zone (for the indicator + the local beam).
            LocalActive = false;
            var local = PlayerAvatar.instance;
            if (local != null)
            {
                int idx = _avatars.IndexOf(local);
                if (idx >= 0) { LocalZone = _zones[idx]; LocalActive = true; }
            }
        }

        private void OnGUI()
        {
            if (!LocalActive) return;
            var local = PlayerAvatar.instance;
            if (local == null) return;

            // Grace countdown — shown while you're out of the truck and still protected.
            if (Plugin.ActiveGracePeriod > 0 && !PlayerLiveness.IsInTruck(local))
            {
                float g = Remaining(local);
                if (g > 0f) DrawTimer(Mathf.CeilToInt(g));
            }

            // Persistent status indicator — a subtle screen-edge border tinted by your zone.
            // (A screen-edge frame avoids depending on the exact HUD position.)
            if (Plugin.StatusIndicator.Value)
            {
                Color c = BeamColors.For(LocalZone, Plugin.BeamsColorblind.Value);
                c.a = 0.5f;
                DrawBorder(new Rect(0f, 0f, Screen.width, Screen.height), c, 4f);
            }
        }

        private void DrawTimer(int seconds)
        {
            _timerStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            var r = new Rect(Screen.width * 0.5f - 110f, Screen.height * 0.16f, 220f, 32f);

            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);                 // shadow for legibility
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), $"Safe for {seconds}s", _timerStyle);
            GUI.color = Color.white;
            GUI.Label(r, $"Safe for {seconds}s", _timerStyle);
            GUI.color = old;
        }

        private static void DrawBorder(Rect r, Color c, float t)
        {
            Texture2D tex = WhiteTex();
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), tex);            // top
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), tex);     // bottom
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), tex);          // left
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), tex);   // right
            GUI.color = old;
        }

        private static Texture2D WhiteTex()
        {
            if (_tex == null)
            {
                _tex = new Texture2D(1, 1);
                _tex.SetPixel(0, 0, Color.white);
                _tex.Apply();
            }
            return _tex;
        }
    }
}
