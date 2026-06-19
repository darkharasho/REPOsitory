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
        private const float CartRescanInterval = 1f;

        private readonly Dictionary<PlayerAvatar, LineRenderer> _lines =
            new Dictionary<PlayerAvatar, LineRenderer>();
        private readonly List<PlayerState> _states = new List<PlayerState>();
        private readonly List<PlayerAvatar> _avatars = new List<PlayerAvatar>();
        private readonly List<PlayerAvatar> _stale = new List<PlayerAvatar>();
        private readonly List<PhysGrabCart> _carts = new List<PhysGrabCart>();
        private readonly List<Vec3> _cartPositions = new List<Vec3>();

        // Instance-scoped so the material's lifetime matches this renderer (which BepInEx keeps
        // alive across level loads) — avoids a destroyed shared material leaving stale beams.
        private Material? _material;
        private float _cartRescan;

        private void Update()
        {
            // Beams display the host's rule, so gate on the synced ActiveEnabled, plus the local
            // display toggle. (ActiveEnabled == local Enabled on the host / in singleplayer.)
            if (!Plugin.ActiveEnabled || !Plugin.BeamsEnabled.Value || !Plugin.IsInGameplay())
            {
                HideAll();
                return;
            }

            var list = GameDirector.instance?.PlayerList;
            if (list == null) { HideAll(); return; }

            AnchorMode mode = Plugin.ActiveMode;
            bool cartMode = mode == AnchorMode.Cart;

            // Re-scan the cart roster on an interval (or when empty); rebuild positions every frame
            // from the cached references since carts move. Destroyed carts read as null and skip.
            _cartRescan -= Time.deltaTime;
            if (!cartMode) _carts.Clear(); // drop stale cart refs when not in Cart mode
            else if (_carts.Count == 0 || _cartRescan <= 0f)
            {
                CartLocator.FindMainCarts(_carts);
                _cartRescan = CartRescanInterval;
            }

            _states.Clear();
            _avatars.Clear();
            foreach (var pa in list)
            {
                if (pa == null) continue;
                Vector3 pos = pa.transform.position;
                _states.Add(new PlayerState(pos.x, pos.y, pos.z,
                    PlayerLiveness.IsAlive(pa), PlayerLiveness.IsInTruck(pa)));
                _avatars.Add(pa);
            }

            _cartPositions.Clear();
            if (cartMode)
            {
                foreach (PhysGrabCart c in _carts)
                {
                    if (c == null) continue;
                    Vector3 cp = c.transform.position;
                    _cartPositions.Add(new Vec3(cp.x, cp.y, cp.z));
                }
            }

            AnchorResult[] anchors =
                DamageCalculator.ResolveAnchors(_states, mode, _cartPositions, Plugin.ActiveIncludeHeight);

            PlayerAvatar? local = PlayerAvatar.instance;
            bool showAll = Plugin.BeamsShowAll.Value;
            float safe = Plugin.ActiveSafeDistance;
            float warn = Plugin.WarnFraction;
            float width = Plugin.BeamWidthWorld;
            float opacity = Plugin.BeamOpacity;

            for (int i = 0; i < _avatars.Count; i++)
            {
                PlayerAvatar pa = _avatars[i];
                AnchorResult a = anchors[i];
                BeamZone zone = DamageCalculator.ZoneForAnchor(a, safe, warn);
                bool render = DamageCalculator.ShouldDrawBeam(mode, zone, a.HasAnchor)
                    && (showAll || pa == local);
                if (!render) { HideLine(pa); continue; }

                LineRenderer lr = GetLine(pa);
                PlayerState self = _states[i];
                Vector3 from = new Vector3(self.X, self.Y, self.Z) + Vector3.up * BeamHeight;
                Vector3 to = new Vector3(a.X, a.Y, a.Z) + Vector3.up * BeamHeight;
                lr.enabled = true;
                lr.widthMultiplier = width;
                lr.SetPosition(0, from);
                lr.SetPosition(1, to);
                Color c = ZoneColor(zone);
                c.a = opacity;
                lr.startColor = c;
                lr.endColor = c;
            }

            CleanupDeparted();
        }

        private void OnDisable() => HideAll();

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        private LineRenderer GetLine(PlayerAvatar pa)
        {
            if (_lines.TryGetValue(pa, out var existing) && existing != null) return existing;

            var go = new GameObject("FF_Beam");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = BeamMaterial();
            lr.positionCount = 2;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.useWorldSpace = true;
            lr.textureMode = LineTextureMode.Stretch;
            lr.alignment = LineAlignment.View; // face the camera so a thin beam stays visible
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

        // Alpha-blended unlit line — translucent and soft (the LineRenderer's vertex-color alpha,
        // driven by Beams/Opacity, shows through), rather than an additive neon glow. Sprites/Default
        // is a built-in transparent unlit shader present in every Unity build.
        private Material BeamMaterial()
        {
            if (_material == null)
            {
                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
                _material = new Material(shader);
            }
            return _material;
        }

        // Muted (not full-saturation) so even at high opacity the beam reads as a soft tether.
        private static Color ZoneColor(BeamZone zone)
        {
            switch (zone)
            {
                case BeamZone.Danger: return new Color(0.85f, 0.20f, 0.16f);
                case BeamZone.Warn: return new Color(0.85f, 0.70f, 0.15f);
                default: return new Color(0.25f, 0.75f, 0.30f);
            }
        }
    }
}
