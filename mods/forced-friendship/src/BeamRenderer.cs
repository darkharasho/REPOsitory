using System.Collections.Generic;
using UnityEngine;

namespace ForcedFriendship
{
    /// <summary>
    /// Runs on every client. Draws a colored LineRenderer tether for each player using the
    /// per-player anchors and zones computed by <see cref="StatusHud"/> — the single source of
    /// truth — so the beam color always matches the on-screen status indicator. Purely cosmetic;
    /// it never applies damage.
    /// </summary>
    internal class BeamRenderer : MonoBehaviour
    {
        private const float BeamHeight = 1f;   // raise endpoints to chest height

        private readonly Dictionary<PlayerAvatar, LineRenderer> _lines =
            new Dictionary<PlayerAvatar, LineRenderer>();
        private readonly List<PlayerAvatar> _current = new List<PlayerAvatar>();
        private readonly List<PlayerAvatar> _stale = new List<PlayerAvatar>();

        // Instance-scoped so the material's lifetime matches this renderer (which BepInEx keeps
        // alive across level loads) — avoids a destroyed shared material leaving stale beams.
        private Material? _material;

        private void Update()
        {
            if (!Plugin.ActiveEnabled || !Plugin.BeamsEnabled.Value || !Plugin.IsInGameplay())
            {
                HideAll();
                return;
            }

            var hud = StatusHud.Instance;
            if (hud == null) { HideAll(); return; }

            PlayerAvatar? local = PlayerAvatar.instance;
            bool showAll = Plugin.BeamsShowAll.Value;
            bool alwaysShow = Plugin.BeamsAlwaysShow.Value;
            bool colorblind = Plugin.BeamsColorblind.Value;
            float width = Plugin.BeamWidthWorld;
            float opacity = Plugin.BeamOpacity;

            _current.Clear();
            int n = hud.PlayerCount;
            for (int i = 0; i < n; i++)
            {
                PlayerAvatar pa = hud.AvatarAt(i);
                if (pa == null) continue;
                _current.Add(pa);

                AnchorResult a = hud.AnchorAt(i);
                BeamZone zone = hud.ZoneAt(i);
                bool render = DamageCalculator.ShouldDrawBeam(zone, a.HasAnchor, alwaysShow)
                    && (showAll || pa == local);
                if (!render) { HideLine(pa); continue; }

                LineRenderer lr = GetLine(pa);
                Vector3 from = hud.PlayerPosAt(i) + Vector3.up * BeamHeight;
                Vector3 to = new Vector3(a.X, a.Y, a.Z) + Vector3.up * BeamHeight;
                lr.enabled = true;
                // Set start/end width explicitly (not just widthMultiplier) so changing the Width
                // config takes effect immediately and doesn't depend on the width curve.
                lr.startWidth = width;
                lr.endWidth = width;
                lr.SetPosition(0, from);
                lr.SetPosition(1, to);
                Color c = BeamColors.For(zone, colorblind);
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

        // Destroy LineRenderers whose player left the roster (e.g. disconnected).
        private void CleanupDeparted()
        {
            _stale.Clear();
            foreach (var key in _lines.Keys)
                if (key == null || !_current.Contains(key)) _stale.Add(key!);
            foreach (var key in _stale)
            {
                if (_lines.TryGetValue(key, out var lr) && lr != null) Destroy(lr.gameObject);
                _lines.Remove(key);
            }
        }

        // Alpha-blended unlit line — translucent and soft (the vertex-color alpha, driven by
        // Beams/Opacity, shows through). Sprites/Default is a built-in transparent unlit shader.
        private Material BeamMaterial()
        {
            if (_material == null)
            {
                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
                _material = new Material(shader);
            }
            return _material;
        }
    }
}
