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
        private const float PrefabScanCooldown = 2f;    // failed prefab scans retry at most this often
        private const float PrefabScanSettleDelay = 1.5f; // no scan this soon after a scene load (bundles in flight)
        private const float GrabOverrideTime = 0.2f;    // grab-strength override window, refreshed each frame
        private const float ClimbSuppressTime = 0.5f;   // climb kill-switch window, refreshed each frame
        private const float MissingEffectRebroadcastDelay = 0.25f; // debounce before an early roster re-send
        // Vanilla itemDictionary key of the Zero Gravity staff (Item.name). Tried first so the
        // usual case is one dictionary lookup instead of a full fuzzy walk.
        private const string StaffItemKey = "Item Staff Zero Gravity";
        // Photon custom event codes are 0..199 (200+ are Photon-internal). REPO's own Assembly-CSharp
        // RAISES on these custom codes: 199 = "you were kicked by the server" (OnEventReceivedCustom ->
        // OutroStart + leave room), 123 = kick-by-actor, 124 = ban-by-actor. Broadcasting on any of them
        // kicks every client (it's why 0.2.0 left clients stuck on the loading screen). Stay clear of all
        // three; 117 sits below the game codes and well above REPOLib's low auto-allocated range.
        private const byte FloatEventCode = 117;
        // Codes REPO itself handles on the custom-event channel. We must never raise on these.
        private static readonly byte[] RepoReservedEventCodes = { 123, 124, 199 };

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
        private static readonly AccessTools.FieldRef<PhysGrabber, PhysGrabObject> GrabbedPhysGrabObjectRef =
            AccessTools.FieldRefAccess<PhysGrabber, PhysGrabObject>("grabbedPhysGrabObject");
        private static readonly AccessTools.FieldRef<LoadingUI, bool> LevelAnimationCompletedRef =
            AccessTools.FieldRefAccess<LoadingUI, bool>("levelAnimationCompleted");
        private static readonly AccessTools.FieldRef<SemiAffect, float> AffectTimerRef =
            AccessTools.FieldRefAccess<SemiAffect, float>("timer");
        private static readonly AccessTools.FieldRef<SemiAffect, float> AffectTimerTotalRef =
            AccessTools.FieldRefAccess<SemiAffect, float>("timerTotal");

        private GameObject? _affectPrefab;
        private bool _searched;
        private float _nextPrefabScan;
        // Whether our IOnEventCallback is currently registered with PUN. Never touch
        // PhotonNetwork before the game does (see FloatGate.PhotonCallbackDesired) — this
        // flag guarantees Remove is only ever called after a successful (post-game-init)
        // Add.
        private bool _photonCallbackRegistered;
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

        /// <summary>The level-load animation has finished — the game's own "safe to play" flag.</summary>
        private static bool LevelFullyLoaded()
        {
            var ui = LoadingUI.instance;
            return ui != null && LevelAnimationCompletedRef(ui);
        }

        private void OnEnable()
        {
            // Defense-in-depth: refuse to broadcast on a code REPO treats as kick/ban. If this ever
            // trips, every client would be force-kicked the moment the host broadcasts the roster.
            if (System.Array.IndexOf(RepoReservedEventCodes, FloatEventCode) >= 0)
            {
                Plugin.Log.LogError(
                    $"FloatEventCode {FloatEventCode} collides with a REPO-reserved event (kick/ban); " +
                    "networked float disabled to avoid kicking clients. Pick an unreserved code.");
                enabled = false;
                return;
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Deliberately NO PhotonNetwork.AddCallbackTarget here: this runs at BepInEx
            // boot, and the first PhotonNetwork touch force-runs PUN's static init before
            // the game's own Photon setup — freezing lobby creation at 100% CPU (bisected
            // live 2026-08-05). Registration is lazy, in Update, once floating is active.
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnregisterPhotonCallback();
        }

        private void RegisterPhotonCallback()
        {
            if (_photonCallbackRegistered) return;
            PhotonNetwork.AddCallbackTarget(this);
            _photonCallbackRegistered = true;
        }

        private void UnregisterPhotonCallback()
        {
            if (!_photonCallbackRegistered) return;
            PhotonNetwork.RemoveCallbackTarget(this);
            _photonCallbackRegistered = false;
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
            // Never scan on the load frame itself: this callback fires mid-transition, when
            // REPOLib re-registration and level-bundle IO are still in flight — the window
            // where a synchronous PrefabRef.Prefab load can block the main thread for good.
            _nextPrefabScan = Time.time + PrefabScanSettleDelay;
            Plugin.Log.LogInfo($"[FloatDiag] scene '{scene.name}' loaded -> reset prefab + per-player state");
        }

        private void Update()
        {
            try
            {
                bool active = FloatGate.ShouldRunFloatWork(
                    Plugin.Enabled.Value, ShouldFloat(), LevelFullyLoaded());
                GameObject? prefab = active ? GetAffectPrefab() : null;
                var director = GameDirector.instance;

                if (!active || prefab == null || director == null)
                {
                    // Disabled / menu / between levels: tear down anything still alive.
                    // Dropping the Photon registration here also keeps us out of PUN's
                    // way during every transition and lobby create.
                    UnregisterPhotonCallback();
                    if (_active.Count > 0) DestroyAllEffects();
                    return;
                }

                if (FloatGate.PhotonCallbackDesired(active, SemiFunc.IsMultiplayer()))
                    RegisterPhotonCallback();

                // Maintenance (runs on every machine): drop dead/ineligible effects and keep the
                // live ones topped up so they never expire (no fall gap). No isTumbling thrash.
                MaintainEffects();

                // Climb entry is decided on the grabbing player's own machine, so every
                // machine suppresses just its own local player.
                SuppressClimbWhileFloating();

                // Physics is master-authoritative, so the strength restore only matters there.
                if (SemiFunc.IsMasterClientOrSingleplayer())
                    CompensateGrabStrength(director);

                if (!SemiFunc.IsMultiplayer())
                {
                    // Singleplayer: we are the only machine — spawn locally, no broadcast.
                    SpawnMissingLocal(prefab, director);
                }
                else if (SemiFunc.IsMasterClientOrSingleplayer())
                {
                    // Host: broadcast the float roster; every client (incl. us) spawns on receipt.
                    // A spawnable player with no effect (just revived — mid-air at the head's
                    // position, with vanilla gravity) pulls the broadcast forward so they don't
                    // plummet for the rest of the 4s interval.
                    float now = Time.time;
                    _nextBroadcast = FloatGate.NextBroadcastTime(
                        _nextBroadcast, now, AnyEligiblePlayerMissingEffect(director), MissingEffectRebroadcastDelay);
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

        /// <summary>
        /// The Tumble Climb upgrade turns any grab that lands on static geometry into a
        /// climb — and under permanent float everyone is always tumbling, so every wall
        /// grab hijacks the floater: the climb spring out-pulls the zero-grav drift and
        /// yanks them to the grab point ("dragged down to the ground"). Refreshing the
        /// game's own timed climb kill-switch gates climb entry and detaches an
        /// in-progress climb; the timer just lapses when the effect ends. FloatGate holds
        /// the decision rule.
        /// </summary>
        private void SuppressClimbWhileFloating()
        {
            foreach (var kv in _active)
            {
                var pa = kv.Key;
                if (pa == null || kv.Value == null || !AvatarIsLocalRef(pa)) continue;
                if (FloatGate.ShouldSuppressClimb(Plugin.DisableWallClimbWhileFloating.Value, localEffectActive: true)
                    && pa.physGrabber != null)
                {
                    pa.physGrabber.OverrideDisableSpecialGrabPowers(ClimbSuppressTime);
                }
                return;
            }
        }

        /// <summary>
        /// A revived player spawns at their death head's position — mid-air whenever a
        /// floating teammate carried the head — and has vanilla gravity until a roster
        /// broadcast re-floats them. True when any player could be floated right now
        /// (alive, tumble linked) but has no live effect, so the master can pull the next
        /// broadcast forward instead of letting them fall for up to 4 seconds.
        /// </summary>
        private bool AnyEligiblePlayerMissingEffect(GameDirector director)
        {
            var list = director.PlayerList;
            for (int i = 0; i < list.Count; i++)
            {
                var pa = list[i];
                if (pa == null || !IsAlive(pa)) continue;
                var tumble = AvatarTumbleRef(pa);
                if (tumble == null || TumblePhysGrabObjectRef(tumble) == null) continue;
                if (!_active.TryGetValue(pa, out var effect) || effect == null) return true;
            }
            return false;
        }

        private void DestroyAllEffects()
        {
            foreach (var kv in _active)
                if (kv.Value != null) Object.Destroy(kv.Value.gameObject);
            _active.Clear();
        }

        /// <summary>
        /// While tumbling, the game's grab-force code clamps the grabber's strength to base
        /// (1f) — under permanent float that permanently disables Strength upgrades. The
        /// object-level <c>PhysGrabObject.OverrideGrabStrength</c> is applied AFTER that
        /// clamp, so forcing a softened upgraded value through it restores them. Master-only
        /// (physics authority); FloatGate holds the decision rules.
        /// </summary>
        private void CompensateGrabStrength(GameDirector director)
        {
            if (!Plugin.RestoreGrabUpgrades.Value) return;
            var list = director.PlayerList;
            for (int i = 0; i < list.Count; i++)
            {
                var pa = list[i];
                if (pa == null || pa.physGrabber == null) continue;
                var grabbed = GrabbedPhysGrabObjectRef(pa.physGrabber);
                if (grabbed == null) continue;

                float? forced = FloatGate.CompensatedGrabStrength(
                    effectActive: _active.TryGetValue(pa, out var effect) && effect != null,
                    grabbedIsEnemy: grabbed.GetComponent<EnemyRigidbody>() != null,
                    upgradedStrength: pa.physGrabber.grabStrength,
                    softening: Plugin.StrengthSoftening.Value);
                if (forced is float strength)
                    grabbed.OverrideGrabStrength(strength, GrabOverrideTime);
            }
        }

        /// <summary>Singleplayer spawn: bind the effect with local references.</summary>
        private void SpawnMissingLocal(GameObject prefab, GameDirector director)
        {
            var list = director.PlayerList;
            for (int i = 0; i < list.Count; i++)
            {
                var pa = list[i];
                if (pa == null) continue;
                var tumble = AvatarTumbleRef(pa);
                var pgo = tumble != null ? TumblePhysGrabObjectRef(tumble) : null;
                if (!FloatGate.CanSpawnEffect(
                        Plugin.Enabled.Value, ShouldFloat(), IsAlive(pa),
                        tumbleLinked: tumble != null,
                        physGrabObjectLinked: pgo != null,
                        effectAlreadyActive: _active.TryGetValue(pa, out var existing) && existing != null))
                    continue;

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

        /// <summary>
        /// Every client (incl. host) receives the roster and spawns missing effects. Runs inside
        /// Photon's event dispatch, so it must never throw — at level start a roster can name
        /// players whose PlayerTumble isn't linked yet (<c>SetupRPC</c> lands after level gen),
        /// and <c>SemiAffect.Setup</c> dereferences <c>tumble.physGrabObject</c> unguarded.
        /// FloatGate skips those entries; the next 4s broadcast picks them up.
        /// </summary>
        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != FloatEventCode) return;
            try
            {
                // Master-only: ignore rosters not sent by the host (mirrors SemiFunc.MasterOnlyRPC).
                if (PhotonNetwork.MasterClient == null ||
                    photonEvent.Sender != PhotonNetwork.MasterClient.ActorNumber) return;
                if (!(photonEvent.CustomData is int[] ids)) return;

                if (!FloatGate.ShouldRunFloatWork(Plugin.Enabled.Value, ShouldFloat(), LevelFullyLoaded()))
                    return; // stale roster (mid-load/transition) or locally disabled — next broadcast retries
                bool enabled = Plugin.Enabled.Value;
                bool floatable = ShouldFloat();

                var prefab = GetAffectPrefab();
                if (prefab == null) return;

                foreach (int viewID in ids)
                {
                    var pa = SemiFunc.PlayerAvatarGetFromPhotonID(viewID);
                    if (pa == null) continue;
                    var tumble = AvatarTumbleRef(pa);
                    var pgo = tumble != null ? TumblePhysGrabObjectRef(tumble) : null;
                    if (!FloatGate.CanSpawnEffect(
                            enabled, floatable, IsAlive(pa),
                            tumbleLinked: tumble != null,
                            physGrabObjectLinked: pgo != null,
                            effectAlreadyActive: _active.TryGetValue(pa, out var existing) && existing != null))
                        continue;

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
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"FloatDriver.OnEvent: {e}");
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
            // A failed scan must not retry every frame: each miss below re-walks the item
            // dictionary doing synchronous bundle loads, which freezes the game when the
            // staff is unresolvable (removed by a spawn/shop mod, custom-level bundle
            // mid-unload). Callers treat null as "not yet" and retry via the next broadcast.
            if (!FloatGate.ShouldScanPrefab(_affectPrefab != null, Time.time, _nextPrefabScan)) return null;
            _nextPrefabScan = Time.time + PrefabScanCooldown;

            var sm = StatsManager.instance;
            if (sm == null || sm.itemDictionary == null) return null;

            // Exact vanilla key first: one dictionary lookup, ≤3 prefab derefs. The fuzzy
            // walk only runs when that misses (staff renamed/replaced by another mod).
            GameObject? affect = null;
            int gravityItems = 0;
            bool exactHit = sm.itemDictionary.TryGetValue(StaffItemKey, out var staffItem)
                && (affect = TryResolveAffect(staffItem)) != null;
            if (affect == null)
            {
                foreach (var item in sm.itemDictionary.Values)
                {
                    if (item == null || item.prefab == null) continue;
                    string name = (item.itemName ?? "").ToLowerInvariant();
                    if (!name.Contains("gravity")) continue;
                    gravityItems++;
                    affect = TryResolveAffect(item);
                    if (affect != null) break;
                }
            }

            if (!_searched || affect != null)
            {
                _searched = true;
                string via = exactHit ? "via staff key" : $"via item DB: gravityItems={gravityItems}";
                Plugin.Log.LogInfo($"[FloatDiag] prefab {via} -> {(affect != null ? "FOUND" : "not yet")}");
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

        /// <summary>
        /// Resolve one item's staff -> projectile -> affect-prefab chain, or null. Every
        /// .Prefab access is a synchronous bundle load and may throw on a broken bundle.
        /// </summary>
        private static GameObject? TryResolveAffect(Item? item)
        {
            if (item == null || item.prefab == null) return null;

            GameObject itemGO;
            try { itemGO = item.prefab.Prefab; } catch { return null; }
            if (itemGO == null) return null;

            PrefabRef? projRef = ExtractProjectileRef(itemGO);
            if (projRef == null) return null;

            GameObject projGO;
            try { projGO = projRef.Prefab; } catch { return null; }
            if (projGO == null) return null;

            var aoe = projGO.GetComponentInChildren<SemiAreaOfEffect>(true);
            if (aoe == null || aoe.semiAffectPrefab == null) return null;

            GameObject affectGO;
            try { affectGO = aoe.semiAffectPrefab.Prefab; } catch { return null; }
            return affectGO != null && affectGO.GetComponent<SemiAffectZeroGravity>() != null
                ? affectGO
                : null;
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
