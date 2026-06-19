using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ForceFloat
{
    /// <summary>
    /// Keeps every player permanently floating during levels by spawning the game's OWN Zero
    /// Gravity effect (<c>SemiAffectZeroGravity</c>) on each living player — the exact prefab +
    /// <c>SemiAffect.SetupSingleplayer</c> call the staff's area-of-effect uses
    /// (<c>SemiAreaOfEffect.CreateAffectsRPC</c>). Letting the real effect run hands all the
    /// tumble/physics/networking to proven game code.
    ///
    /// Every client spawns its own local copy of the effect for every player (mirroring the staff,
    /// whose effect is instantiated on all clients). The effect itself master-gates the physics and
    /// runs its local-player parts (anti-gravity, camera steering) on the owning client, so each
    /// player sees themselves float and can steer. Requires the mod on every client. Dead players
    /// are skipped.
    /// </summary>
    public class FloatDriver : MonoBehaviour
    {
        private const float AffectTime = 2f;        // SemiAffectZeroGravity doubles it -> ~4s effective
        private const float ReapplyInterval = 2.5f;

        private static readonly AccessTools.FieldRef<PlayerAvatar, PlayerTumble> AvatarTumbleRef =
            AccessTools.FieldRefAccess<PlayerAvatar, PlayerTumble>("tumble");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> AvatarDeadSetRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("deadSet");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> AvatarIsDisabledRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isDisabled");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> AvatarIsTumblingRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isTumbling");
        private static readonly AccessTools.FieldRef<PlayerTumble, PhysGrabObject> TumblePhysGrabObjectRef =
            AccessTools.FieldRefAccess<PlayerTumble, PhysGrabObject>("physGrabObject");
        private static readonly AccessTools.FieldRef<RunManager, MultiplayerPool> MultiplayerPoolRef =
            AccessTools.FieldRefAccess<RunManager, MultiplayerPool>("multiplayerPool");
        private static readonly AccessTools.FieldRef<RunManager, Dictionary<string, Object>> SingleplayerPoolRef =
            AccessTools.FieldRefAccess<RunManager, Dictionary<string, Object>>("singleplayerPool");

        private GameObject? _affectPrefab;
        private bool _searchedAndLogged;
        private readonly Dictionary<PlayerAvatar, float> _nextApply = new Dictionary<PlayerAvatar, float>();
        private float _logAccum;

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
                if (!Plugin.Enabled.Value || !ShouldFloat()) return;

                var prefab = GetAffectPrefab();
                if (prefab == null) return;

                var director = GameDirector.instance;
                if (director == null) return;

                var list = director.PlayerList;
                float now = Time.time;
                for (int i = 0; i < list.Count; i++)
                {
                    var pa = list[i];
                    if (pa == null || !IsAlive(pa)) continue;
                    if (_nextApply.TryGetValue(pa, out float next) && now < next) continue;
                    if (ApplyFloat(prefab, pa))
                        _nextApply[pa] = now + ReapplyInterval;
                }

                MaybeLog();
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"FloatDriver.Update: {e}");
            }
        }

        /// <summary>Spawn the real effect on one player, mirroring SemiAreaOfEffect.CreateAffectsRPC.</summary>
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

        /// <summary>Find the ZeroGravity effect prefab from the run's prefab pools or loaded assets (logged once).</summary>
        private GameObject? GetAffectPrefab()
        {
            if (_affectPrefab != null) return _affectPrefab;

            int mpCount = 0, spCount = 0, resCount = 0;
            GameObject? found = null;

            var rm = RunManager.instance;
            if (rm != null)
            {
                var mp = MultiplayerPoolRef(rm);
                if (mp != null && mp.ResourceCache != null)
                    foreach (var go in mp.ResourceCache.Values)
                        if (go != null && go.GetComponent<SemiAffectZeroGravity>() != null) { mpCount++; found ??= go; }

                var sp = SingleplayerPoolRef(rm);
                if (sp != null)
                    foreach (var obj in sp.Values)
                        if (obj is GameObject go && go.GetComponent<SemiAffectZeroGravity>() != null) { spCount++; found ??= go; }
            }

            var all = Resources.FindObjectsOfTypeAll<SemiAffectZeroGravity>();
            foreach (var sa in all)
            {
                if (sa == null) continue;
                resCount++;
                if (found == null && !sa.gameObject.scene.IsValid()) found = sa.gameObject;
            }
            if (found == null && all.Length > 0 && all[0] != null) found = all[0].gameObject;

            if (!_searchedAndLogged)
            {
                _searchedAndLogged = true;
                Plugin.Log.LogInfo($"[FloatDiag] prefab search: multiplayerPool={mpCount} singleplayerPool={spCount} loaded={resCount} -> {(found != null ? "FOUND" : "NOT FOUND")}");
            }
            _affectPrefab = found;
            return _affectPrefab;
        }

        private void MaybeLog()
        {
            _logAccum += Time.deltaTime;
            if (_logAccum < 1f) return;
            _logAccum = 0f;
            var me = PlayerAvatar.instance;
            if (me == null) return;
            bool master = SemiFunc.IsMasterClientOrSingleplayer();
            var tumble = AvatarTumbleRef(me);
            var pgo = tumble != null ? TumblePhysGrabObjectRef(tumble) : null;
            string y = pgo != null && pgo.rb != null ? pgo.rb.position.y.ToString("F2") : "?";
            Plugin.Log.LogInfo($"[FloatDiag] master={master} local.isTumbling={AvatarIsTumblingRef(me)} " +
                               $"activeAffect={(me.activeZeroGravityAffect != null)} bodyY={y}");
        }
    }
}
