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
                if (key == null || !_avatars.Contains(key)) _stale.Add(key!);
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
