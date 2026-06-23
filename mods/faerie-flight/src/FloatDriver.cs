using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FaerieFlight
{
    /// <summary>
    /// Keeps every player permanently floating during levels by spawning the game's OWN Zero
    /// Gravity effect (<c>SemiAffectZeroGravity</c>) on each living player — the exact prefab the
    /// staff's area-of-effect uses, so the real game code drives all tumble/physics/networking.
    ///
    /// Spawning mirrors the game's <c>SemiAreaOfEffect</c>: the <b>master</b> decides who should
    /// float and broadcasts their photon view IDs to every client via <c>PhotonNetwork.RaiseEvent</c>;
    /// each client then spawns its own local copy bound by network ID through <c>SemiAffect.Setup</c>.
    /// This is the coordinated, networked path the effect was designed for. (The previous approach —
    /// every client independently spawning <c>SetupSingleplayer</c> effects and destroying/respawning
    /// them keyed on the laggy networked <c>isTumbling</c> flag — desynced clients into a tumbling,
    /// no-drift, no-collision "can't move" state. See docs/superpowers/specs.)
    ///
    /// In singleplayer there is no room to broadcast into, so the effect is spawned locally via
    /// <c>SetupSingleplayer</c> (the master is the only machine). Requires the mod on every client.
    /// </summary>
    public class FloatDriver : MonoBehaviour, IOnEventCallback
    {
        private const float AffectTime = 5f;            // SemiAffectZeroGravity doubles it -> ~10s effective
        private const float RefreshInterval = 4f;       // master re-broadcasts the float roster this often
        private const byte FloatEventCode = 199;        // Photon user event range is 1..199

        private static readonly AccessTools.FieldRef<PlayerAvatar, PlayerTumble> AvatarTumbleRef =
            AccessTools.FieldRefAccess<PlayerAvatar, PlayerTumble>("tumble");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> AvatarDeadSetRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("deadSet");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> AvatarIsDisabledRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isDisabled");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> AvatarIsTumblingRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isTumbling");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> AvatarIsLocalRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isLocal");
        private static readonly AccessTools.FieldRef<PlayerTumble, PhysGrabObject> TumblePhysGrabObjectRef =
            AccessTools.FieldRefAccess<PlayerTumble, PhysGrabObject>("physGrabObject");
        private static readonly AccessTools.FieldRef<SemiAffect, float> AffectTimerRef =
            AccessTools.FieldRefAccess<SemiAffect, float>("timer");
        private static readonly AccessTools.FieldRef<SemiAffect, float> AffectTimerTotalRef =
            AccessTools.FieldRefAccess<SemiAffect, float>("timerTotal");

        private GameObject? _affectPrefab;
        private bool _searched;
        // The live effect we spawned per player. Key is the PlayerAvatar so it works for both the
        // networked (resolved from a view ID) and singleplayer paths.
        private readonly Dictionary<PlayerAvatar, SemiAffect> _active = new Dictionary<PlayerAvatar, SemiAffect>();
        private float _nextBroadcast;
        private float _logAccum;

        private static bool ShouldFloat()
        {
            if (RunManager.instance == null) return false;
            if (SemiFunc.MenuLevel()) return false;
            return SemiFunc.RunIsLevel();
        }

        private static bool IsAlive(PlayerAvatar pa) => !AvatarDeadSetRef(pa) && !AvatarIsDisabledRef(pa);

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            PhotonNetwork.AddCallbackTarget(this);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        /// <summary>
        /// FloatDriver persists across levels (it lives on the BepInEx plugin GameObject), so its
        /// caches must NOT outlive a level. A custom level can load/unload its own asset bundles,
        /// which can unload the cached ZeroGravity prefab's sub-assets — leaving a prefab that still
        /// engages tumble but no longer provides zero-grav control ("can't move"). Resetting per scene
        /// forces a fresh, valid resolution and drops dead effect references (their GameObjects are
        /// destroyed on unload).
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _affectPrefab = null;
            _searched = false;
            _active.Clear();
            _nextBroadcast = 0f;
            Plugin.Log.LogInfo($"[FloatDiag] scene '{scene.name}' loaded -> reset prefab + per-player state");
        }

        private void Update()
        {
            try
            {
                bool active = Plugin.Enabled.Value && ShouldFloat();
                GameObject? prefab = active ? GetAffectPrefab() : null;
                var director = GameDirector.instance;

                if (!active || prefab == null || director == null)
                {
                    // Disabled / menu / between levels: tear down anything still alive.
                    if (_active.Count > 0) DestroyAllEffects();
                    return;
                }

                // Maintenance (runs on every machine): drop dead/ineligible effects and keep the
                // live ones topped up so they never expire (no fall gap). No isTumbling thrash.
                MaintainEffects();

                if (!SemiFunc.IsMultiplayer())
                {
                    // Singleplayer: we are the only machine — spawn locally, no broadcast.
                    SpawnMissingLocal(prefab, director);
                }
                else if (SemiFunc.IsMasterClientOrSingleplayer())
                {
                    // Host: broadcast the float roster; every client (incl. us) spawns on receipt.
                    float now = Time.time;
                    if (now >= _nextBroadcast)
                    {
                        _nextBroadcast = now + RefreshInterval;
                        BroadcastRoster(director);
                    }
                }
                // MP non-master clients spawn only in OnEvent; nothing to do here beyond maintenance.

                MaybeLog(director);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"FloatDriver.Update: {e}");
            }
        }

        private void MaintainEffects()
        {
            if (_active.Count == 0) return;
            List<PlayerAvatar>? drop = null;
            foreach (var kv in _active)
            {
                var pa = kv.Key;
                var effect = kv.Value;
                if (pa == null || effect == null || !IsAlive(pa))
                {
                    if (effect != null) Object.Destroy(effect.gameObject);
                    (drop ??= new List<PlayerAvatar>()).Add(kv.Key);
                    continue;
                }
                // Keep the SAME effect alive by topping its timer so it never expires or stacks.
                AffectTimerRef(effect) = AffectTimerTotalRef(effect);
            }
            if (drop != null)
                foreach (var pa in drop) _active.Remove(pa);
        }

        private void DestroyAllEffects()
        {
            foreach (var kv in _active)
                if (kv.Value != null) Object.Destroy(kv.Value.gameObject);
            _active.Clear();
        }

        /// <summary>Singleplayer spawn: bind the effect with local references.</summary>
        private void SpawnMissingLocal(GameObject prefab, GameDirector director)
        {
            var list = director.PlayerList;
            for (int i = 0; i < list.Count; i++)
            {
                var pa = list[i];
                if (pa == null || !IsAlive(pa)) continue;
                if (_active.TryGetValue(pa, out var existing) && existing != null) continue;

                var tumble = AvatarTumbleRef(pa);
                if (tumble == null) continue;
                var pgo = TumblePhysGrabObjectRef(tumble);
                if (pgo == null) continue;

                var go = Object.Instantiate(prefab, pa.transform.position, Quaternion.identity);
                var affect = go.GetComponent<SemiAffect>();
                if (affect == null) { Object.Destroy(go); continue; }
                affect.direction = Vector3.up;
                affect.positionOfOriginalAreaOfEffect = pa.transform.position;
                affect.SetupSingleplayer(pa.transform, pgo, AffectTime, pa);
                _active[pa] = affect;
            }
        }

        /// <summary>Host: send the list of alive players' photon view IDs to every client.</summary>
        private void BroadcastRoster(GameDirector director)
        {
            if (!PhotonNetwork.InRoom) return;
            var list = director.PlayerList;
            var ids = new List<int>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var pa = list[i];
                if (pa == null || !IsAlive(pa) || pa.photonView == null) continue;
                ids.Add(pa.photonView.ViewID);
            }
            if (ids.Count == 0) return;
            PhotonNetwork.RaiseEvent(
                FloatEventCode,
                ids.ToArray(),
                new RaiseEventOptions { Receivers = ReceiverGroup.All },
                SendOptions.SendReliable);
        }

        /// <summary>Every client (incl. host) receives the roster and spawns missing effects.</summary>
        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != FloatEventCode) return;
            // Master-only: ignore rosters not sent by the host (mirrors SemiFunc.MasterOnlyRPC).
            if (PhotonNetwork.MasterClient == null ||
                photonEvent.Sender != PhotonNetwork.MasterClient.ActorNumber) return;
            if (!(photonEvent.CustomData is int[] ids)) return;

            var prefab = GetAffectPrefab();
            if (prefab == null) return;

            foreach (int viewID in ids)
            {
                var pa = SemiFunc.PlayerAvatarGetFromPhotonID(viewID);
                if (pa == null || !IsAlive(pa)) continue;
                if (_active.TryGetValue(pa, out var existing) && existing != null) continue;

                var go = Object.Instantiate(prefab, pa.transform.position, Quaternion.identity);
                var affect = go.GetComponent<SemiAffect>();
                if (affect == null) { Object.Destroy(go); continue; }
                affect.direction = Vector3.up;
                affect.positionOfOriginalAreaOfEffect = pa.transform.position;
                // Networked bind: resolves the player by view ID and self-destroys if the networked
                // object isn't ready yet (the next host broadcast retries).
                affect.Setup(viewID, AffectTime);
                _active[pa] = affect;
            }
        }

        /// <summary>
        /// Obtain the ZeroGravity effect prefab by walking the item database:
        /// item.prefab -> zero-gravity item's projectilePrefab -> projectile's
        /// SemiAreaOfEffect.semiAffectPrefab. Each PrefabRef.Prefab access loads from the bundle.
        /// </summary>
        private GameObject? GetAffectPrefab()
        {
            if (_affectPrefab != null) return _affectPrefab;

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

        private void MaybeLog(GameDirector director)
        {
            _logAccum += Time.deltaTime;
            if (_logAccum < 1f) return;
            _logAccum = 0f;
            bool master = SemiFunc.IsMasterClientOrSingleplayer();
            var list = director.PlayerList;
            for (int i = 0; i < list.Count; i++)
            {
                var pa = list[i];
                if (pa == null) continue;
                bool effect = _active.TryGetValue(pa, out var e) && e != null;
                Plugin.Log.LogInfo($"[FloatDiag] master={master} p{i} local={AvatarIsLocalRef(pa)} " +
                                   $"alive={IsAlive(pa)} tumbling={AvatarIsTumblingRef(pa)} effect={effect}");
            }
        }
    }
}
