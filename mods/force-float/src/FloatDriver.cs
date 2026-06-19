using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ForceFloat
{
    /// <summary>
    /// Keeps every player permanently floating during levels by spawning the game's OWN
    /// Zero Gravity Staff effect (<c>SemiAffectZeroGravity</c>) on each living player — the exact
    /// same prefab + <c>SemiAffect.SetupSingleplayer</c> call the staff's area-of-effect uses
    /// (see <c>SemiAreaOfEffect.CreateAffectsRPC</c>). Re-applied before the effect expires so the
    /// float never ends.
    ///
    /// Only the master client / singleplayer host spawns the effect: tumble physics is
    /// master-authoritative in this game (<c>PlayerTumble.FixedUpdate</c> returns on non-masters),
    /// so the host drives the float for everyone. This makes the host authoritative ("host wins")
    /// and means clients float even without the mod installed.
    /// </summary>
    public class FloatDriver : MonoBehaviour
    {
        // SemiAffectZeroGravity.Start() doubles the timer, so the real on-screen duration is
        // ~2x AffectTime. Re-apply on a shorter interval than that so the float is continuous.
        private const float AffectTime = 2f;       // ~4s effective
        private const float ReapplyInterval = 2.5f;

        // Internal game fields → cached field-ref delegates (this mod builds against the
        // un-publicized DLL; same pattern as ForcedFriendship/PlayerLiveness).
        private static readonly AccessTools.FieldRef<PlayerAvatar, PlayerTumble> AvatarTumbleRef =
            AccessTools.FieldRefAccess<PlayerAvatar, PlayerTumble>("tumble");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> AvatarDeadSetRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("deadSet");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> AvatarIsDisabledRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isDisabled");
        private static readonly AccessTools.FieldRef<PlayerTumble, PhysGrabObject> TumblePhysGrabObjectRef =
            AccessTools.FieldRefAccess<PlayerTumble, PhysGrabObject>("physGrabObject");

        private GameObject? _affectPrefab;
        private readonly Dictionary<PlayerAvatar, float> _nextApply = new Dictionary<PlayerAvatar, float>();

        /// <summary>Floating is active only during real levels (not shop/lobby/menus/arena).</summary>
        private static bool ShouldFloat()
        {
            if (RunManager.instance == null) return false;
            if (SemiFunc.MenuLevel()) return false;
            return SemiFunc.RunIsLevel();
        }

        private static bool IsAlive(PlayerAvatar pa) => !AvatarDeadSetRef(pa) && !AvatarIsDisabledRef(pa);

        private void Update()
        {
            try
            {
                if (!Plugin.Enabled.Value) return;
                if (!ShouldFloat()) return;
                // Only the host drives the float (tumble physics is master-authoritative).
                if (!SemiFunc.IsMasterClientOrSingleplayer()) return;

                var prefab = GetAffectPrefab();
                if (prefab == null) return;

                var director = GameDirector.instance;
                if (director == null) return;

                var list = director.PlayerList;
                float now = Time.time;
                for (int i = 0; i < list.Count; i++)
                {
                    var pa = list[i];
                    if (pa == null || !IsAlive(pa)) continue;          // never touch dead players (their head body)

                    // Skip if this player already has a live zero-gravity affect (e.g. an actual
                    // staff hit, or our own still-running one) — only the local avatar exposes it.
                    if (pa.activeZeroGravityAffect != null) continue;

                    if (_nextApply.TryGetValue(pa, out float next) && now < next) continue;

                    if (ApplyFloat(prefab, pa))
                        _nextApply[pa] = now + ReapplyInterval;
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"FloatDriver.Update: {e}");
            }
        }

        /// <summary>
        /// Spawn the real zero-gravity effect on one player, exactly as
        /// <c>SemiAreaOfEffect.CreateAffectsRPC</c> does in its singleplayer branch.
        /// </summary>
        private bool ApplyFloat(GameObject prefab, PlayerAvatar pa)
        {
            var tumble = AvatarTumbleRef(pa);
            if (tumble == null) return false;
            var pgo = TumblePhysGrabObjectRef(tumble);
            if (pgo == null) return false;

            var go = Object.Instantiate(prefab, pa.transform.position, Quaternion.identity);
            var affect = go.GetComponent<SemiAffect>();
            if (affect == null) { Object.Destroy(go); return false; }

            affect.direction = Vector3.up;
            affect.positionOfOriginalAreaOfEffect = pa.transform.position;
            affect.SetupSingleplayer(pa.transform, pgo, AffectTime, pa);
            return true;
        }

        /// <summary>
        /// Locate the game's Zero Gravity affect prefab once. We search all loaded objects for the
        /// <c>SemiAffectZeroGravity</c> asset (the prefab, not a live scene instance) and cache it.
        /// </summary>
        private GameObject? GetAffectPrefab()
        {
            if (_affectPrefab != null) return _affectPrefab;

            var all = Resources.FindObjectsOfTypeAll<SemiAffectZeroGravity>();
            foreach (var sa in all)
            {
                if (sa == null) continue;
                var go = sa.gameObject;
                // Prefer a prefab asset (no valid scene) over a live scene instance.
                if (!go.scene.IsValid())
                {
                    _affectPrefab = go;
                    Plugin.Log.LogInfo("[ForceFloat] Found ZeroGravity affect prefab.");
                    return _affectPrefab;
                }
            }
            return null;
        }
    }
}
