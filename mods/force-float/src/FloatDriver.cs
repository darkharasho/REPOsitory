using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ForceFloat
{
    /// <summary>
    /// Keeps every player permanently floating during levels by spawning the game's OWN Zero
    /// Gravity effect (<c>SemiAffectZeroGravity</c>) on each living player — the exact prefab +
    /// <c>SemiAffect.SetupSingleplayer</c> call the staff's area-of-effect uses, so the real game
    /// code drives all tumble/physics/networking.
    ///
    /// The effect prefab isn't kept in memory unless a zero-gravity item is present, so we obtain it
    /// from the item database: a zero-gravity item's serialized projectile → its
    /// <c>SemiAreaOfEffect.semiAffectPrefab</c>, loaded on demand via <c>PrefabRef.Prefab</c>.
    ///
    /// Every client spawns its own local copy for every player (mirroring the staff). Dead players
    /// are skipped. Requires the mod on every client.
    /// </summary>
    public class FloatDriver : MonoBehaviour
    {
        private const float AffectTime = 5f;        // SemiAffectZeroGravity doubles it -> ~10s effective

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
        private static readonly AccessTools.FieldRef<SemiAffect, float> AffectTimerRef =
            AccessTools.FieldRefAccess<SemiAffect, float>("timer");
        private static readonly AccessTools.FieldRef<SemiAffect, float> AffectTimerTotalRef =
            AccessTools.FieldRefAccess<SemiAffect, float>("timerTotal");

        private GameObject? _affectPrefab;
        private bool _searched;
        // The live effect we spawned per player, and the earliest time we may (re)try one.
        private readonly Dictionary<PlayerAvatar, SemiAffect> _active = new Dictionary<PlayerAvatar, SemiAffect>();
        private readonly Dictionary<PlayerAvatar, float> _nextTry = new Dictionary<PlayerAvatar, float>();
        private const float RetryInterval = 2f;
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

                    bool tumbling = AvatarIsTumblingRef(pa);
                    bool affectAlive = _active.TryGetValue(pa, out var existing) && existing != null;

                    if (affectAlive && existing != null)
                    {
                        if (tumbling)
                        {
                            // Healthy: keep the SAME effect alive by topping its timer back up, so it
                            // never expires (no fall) and we never stack a second one (no launch).
                            AffectTimerRef(existing) = AffectTimerTotalRef(existing);
                            continue;
                        }
                        // Alive but not tumbling = it failed to engage; drop it and respawn below.
                        Object.Destroy(existing.gameObject);
                        _active.Remove(pa);
                    }

                    // (Re)try, throttled: covers a missing effect AND clients whose body wasn't
                    // network-ready on the first try (the case a cart-grab teleport "fixed").
                    if (_nextTry.TryGetValue(pa, out float next) && now < next) continue;
                    _nextTry[pa] = now + RetryInterval;

                    var spawned = ApplyFloat(prefab, pa);
                    if (spawned != null) _active[pa] = spawned;
                }

                MaybeLog();
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"FloatDriver.Update: {e}");
            }
        }

        private SemiAffect? ApplyFloat(GameObject prefab, PlayerAvatar pa)
        {
            var tumble = AvatarTumbleRef(pa);
            if (tumble == null) return null;
            var pgo = TumblePhysGrabObjectRef(tumble);
            if (pgo == null) return null;

            var go = Object.Instantiate(prefab, pa.transform.position, Quaternion.identity);
            var affect = go.GetComponent<SemiAffect>();
            if (affect == null) { Object.Destroy(go); return null; }

            affect.direction = Vector3.up;
            affect.positionOfOriginalAreaOfEffect = pa.transform.position;
            affect.SetupSingleplayer(pa.transform, pgo, AffectTime, pa);
            return affect;
        }

        /// <summary>
        /// Obtain the ZeroGravity effect prefab by walking the item database:
        /// item.prefab -> zero-gravity item's projectilePrefab -> projectile's
        /// SemiAreaOfEffect.semiAffectPrefab. Each PrefabRef.Prefab access loads from the bundle.
        /// </summary>
        private GameObject? GetAffectPrefab()
        {
            if (_affectPrefab != null) return _affectPrefab;
            if (_searched && _affectPrefab == null) { /* keep retrying each frame until items load */ }

            var sm = StatsManager.instance;
            if (sm == null || sm.itemDictionary == null) return null;

            int gravityItems = 0;
            GameObject? affect = null;
            foreach (var item in sm.itemDictionary.Values)
            {
                if (item == null || item.prefab == null) continue;
                string name = (item.itemName ?? "").ToLowerInvariant();
                if (!name.Contains("gravity")) continue;
                gravityItems++;

                GameObject itemGO;
                try { itemGO = item.prefab.Prefab; } catch { continue; }
                if (itemGO == null) continue;

                PrefabRef? projRef = ExtractProjectileRef(itemGO);
                if (projRef == null) continue;

                GameObject projGO;
                try { projGO = projRef.Prefab; } catch { continue; }
                if (projGO == null) continue;

                var aoe = projGO.GetComponentInChildren<SemiAreaOfEffect>(true);
                if (aoe == null || aoe.semiAffectPrefab == null) continue;

                GameObject affectGO;
                try { affectGO = aoe.semiAffectPrefab.Prefab; } catch { continue; }
                if (affectGO != null && affectGO.GetComponent<SemiAffectZeroGravity>() != null)
                {
                    affect = affectGO;
                    break;
                }
            }

            if (!_searched || affect != null)
            {
                _searched = true;
                Plugin.Log.LogInfo($"[FloatDiag] prefab via item DB: gravityItems={gravityItems} -> {(affect != null ? "FOUND" : "not yet")}");
            }
            _affectPrefab = affect;
            return _affectPrefab;
        }

        /// <summary>Read the projectilePrefab from the zero-gravity staff component on the item prefab.</summary>
        private static PrefabRef? ExtractProjectileRef(GameObject itemGO)
        {
            var staff = itemGO.GetComponent<ItemStaffZeroGravity>();
            return staff != null ? staff.projectilePrefab : null;
        }

        private void MaybeLog()
        {
            _logAccum += Time.deltaTime;
            if (_logAccum < 1f) return;
            _logAccum = 0f;
            var me = PlayerAvatar.instance;
            if (me == null) return;
            var tumble = AvatarTumbleRef(me);
            var pgo = tumble != null ? TumblePhysGrabObjectRef(tumble) : null;
            string y = pgo != null && pgo.rb != null ? pgo.rb.position.y.ToString("F2") : "?";
            Plugin.Log.LogInfo($"[FloatDiag] master={SemiFunc.IsMasterClientOrSingleplayer()} " +
                               $"local.isTumbling={AvatarIsTumblingRef(me)} activeAffect={(me.activeZeroGravityAffect != null)} bodyY={y}");
        }
    }
}
