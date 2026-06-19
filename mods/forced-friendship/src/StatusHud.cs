using System.Collections.Generic;
using UnityEngine;

namespace ForcedFriendship
{
    /// <summary>
    /// Runs on every client. Tracks each player's post-truck grace timer (the host reads it to
    /// suppress damage; clients read it for beam color), computes the local player's current
    /// safety zone, and draws the optional on-screen indicators: a grace countdown and a subtle
    /// persistent status box tinted by your current zone. Display is local — never synced.
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
            if (!Plugin.IsInGameplay())
            {
                _grace.Clear();
                LocalActive = false;
                return;
            }

            var list = GameDirector.instance?.PlayerList;
            if (list == null) { LocalActive = false; return; }

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

            // Resolve the local player's zone (same logic the beams use, plus grace -> Safe).
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

            AnchorResult[] anchors =
                DamageCalculator.ResolveAnchors(_states, mode, _cartPositions, Plugin.ActiveIncludeHeight);

            LocalActive = false;
            var local = PlayerAvatar.instance;
            if (local != null)
            {
                int idx = _avatars.IndexOf(local);
                if (idx >= 0)
                {
                    BeamZone z = DamageCalculator.ZoneForAnchor(
                        anchors[idx], Plugin.ActiveSafeDistance, Plugin.WarnFraction);
                    if (IsInGrace(local)) z = BeamZone.Safe;
                    LocalZone = z;
                    LocalActive = true;
                }
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

            // Persistent status box, tinted by your current zone.
            if (Plugin.StatusIndicator.Value)
            {
                Color c = BeamColors.For(LocalZone, Plugin.BeamsColorblind.Value);
                c.a = 0.65f;
                // Near the bottom-centre, roughly framing the health/stamina HUD.
                float w = Mathf.Min(320f, Screen.width * 0.26f);
                float h = 84f;
                var rect = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 90f, w, h);
                DrawBorder(rect, c, 3f);
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
