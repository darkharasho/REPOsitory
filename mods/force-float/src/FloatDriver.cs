using HarmonyLib;
using UnityEngine;

namespace ForceFloat
{
    /// <summary>
    /// Keeps every player permanently floating during levels by reproducing the Zero Gravity
    /// Staff's effect (<c>SemiAffectZeroGravity</c>) frame-for-frame on each living player —
    /// the same public <c>PhysGrabObject</c> levers and the same constants the game uses, rather
    /// than spawning the effect prefab (which isn't loaded in memory unless a zero-gravity item
    /// is present).
    ///
    /// Only the master client / singleplayer host drives it: tumble physics is master-authoritative
    /// (<c>PlayerTumble.FixedUpdate</c> returns on non-masters), so the host floats everyone. That
    /// makes the host authoritative ("host wins") and lets clients float without the mod installed.
    /// Dead players are never touched (their head body would fight the forces and shake the screen).
    /// </summary>
    public class FloatDriver : MonoBehaviour
    {
        // Constants copied verbatim from SemiAffectZeroGravity.
        private const float DriftDrag = 1f;
        private const float DriftAngularDrag = 1.5f;
        private const float DriftForce = 8f;
        private const float FollowRotationFactor = 20f;
        private const float LiftPerSecond = 5f;                       // SemiFunc.PerSecond(5f, ...)
        private float _liftAccum;

        // Internal game fields → cached field-ref delegates (this mod builds against the
        // un-publicized DLL; same pattern as ForcedFriendship/PlayerLiveness).
        private static readonly AccessTools.FieldRef<PlayerAvatar, PlayerTumble> AvatarTumbleRef =
            AccessTools.FieldRefAccess<PlayerAvatar, PlayerTumble>("tumble");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> AvatarDeadSetRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("deadSet");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> AvatarIsDisabledRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isDisabled");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> AvatarIsLocalRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isLocal");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> AvatarIsTumblingRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isTumbling");
        private static readonly AccessTools.FieldRef<PlayerAvatar, Vector3> AvatarInputDirectionRawRef =
            AccessTools.FieldRefAccess<PlayerAvatar, Vector3>("InputDirectionRaw");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> AvatarWingsActiveRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("upgradeTumbleWingsVisualsActive");
        private static readonly AccessTools.FieldRef<PlayerTumble, PhysGrabObject> TumblePhysGrabObjectRef =
            AccessTools.FieldRefAccess<PlayerTumble, PhysGrabObject>("physGrabObject");

        /// <summary>Floating is active only during real levels (not shop/lobby/menus/arena).</summary>
        private static bool ShouldFloat()
        {
            if (RunManager.instance == null) return false;
            if (SemiFunc.MenuLevel()) return false;
            return SemiFunc.RunIsLevel();
        }

        private static bool IsAlive(PlayerAvatar pa) => !AvatarDeadSetRef(pa) && !AvatarIsDisabledRef(pa);

        private bool Active()
        {
            if (!Plugin.Enabled.Value) return false;
            if (!ShouldFloat()) return false;
            // Only the host drives the float (tumble physics is master-authoritative).
            return SemiFunc.IsMasterClientOrSingleplayer();
        }

        /// <summary>Keep every living player tumbling + wings on. (Cheap, runs every frame.)</summary>
        private void Update()
        {
            try
            {
                if (!Active()) return;
                var director = GameDirector.instance;
                if (director == null) return;

                var list = director.PlayerList;
                for (int i = 0; i < list.Count; i++)
                {
                    var pa = list[i];
                    if (pa == null || !IsAlive(pa)) continue;
                    var tumble = AvatarTumbleRef(pa);
                    if (tumble == null) continue;

                    if (!AvatarIsTumblingRef(pa))
                        tumble.TumbleRequest(true, false);
                    tumble.TumbleOverrideTime(0.5f);

                    if (!AvatarWingsActiveRef(pa))
                        pa.UpgradeTumbleWingsVisualsActive();
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"FloatDriver.Update: {e}");
            }
        }

        /// <summary>The physics: cancel the tumble body's gravity, add drag + lift, and steer the host's own avatar.</summary>
        private void FixedUpdate()
        {
            try
            {
                if (!Active()) return;
                var director = GameDirector.instance;
                if (director == null) return;

                // Lift impulse fires LiftPerSecond times a second, like SemiFunc.PerSecond(5f, ...).
                bool liftTick = false;
                _liftAccum += Time.fixedDeltaTime;
                float liftStep = 1f / LiftPerSecond;
                if (_liftAccum >= liftStep) { _liftAccum -= liftStep; liftTick = true; }

                var localCam = PlayerAvatar.instance != null ? PlayerAvatar.instance.localCamera : null;
                Transform? camT = localCam != null ? localCam.GetOverrideTransform() : null;

                var list = director.PlayerList;
                for (int i = 0; i < list.Count; i++)
                {
                    var pa = list[i];
                    if (pa == null || !IsAlive(pa)) continue;
                    var tumble = AvatarTumbleRef(pa);
                    if (tumble == null) continue;
                    var pgo = TumblePhysGrabObjectRef(tumble);
                    if (pgo == null || pgo.rb == null) continue;
                    var rb = pgo.rb;

                    // Zero gravity + drift drag (these set short timers, so re-apply every tick).
                    pgo.OverrideZeroGravity();
                    pgo.OverrideDrag(DriftDrag);
                    pgo.OverrideAngularDrag(DriftAngularDrag);

                    // Gentle lift so a grounded body actually rises (exact staff formula).
                    if (liftTick)
                    {
                        rb.AddForce(Vector3.up * (0.5f / LiftPerSecond) * (rb.mass * 0.2f), ForceMode.Impulse);
                        rb.AddTorque(Random.insideUnitSphere.normalized * 0.01f / LiftPerSecond * rb.mass, ForceMode.Impulse);
                    }

                    // Steering: only the local avatar (the host's own) — the host has no camera for
                    // remote players, same limitation as the real staff effect.
                    if (camT != null && AvatarIsLocalRef(pa))
                    {
                        Quaternion target = Quaternion.LookRotation(camT.forward, Vector3.up);
                        Vector3 torque = SemiFunc.PhysFollowRotation(pgo.transform, target, rb, FollowRotationFactor);
                        rb.AddTorque(torque, ForceMode.Impulse);

                        Vector3 input = AvatarInputDirectionRawRef(pa);
                        Vector3 dir = camT.forward * input.z + camT.right * input.x;
                        if (dir.sqrMagnitude > 1f) dir.Normalize();
                        if (dir.sqrMagnitude >= 0.0001f)
                            rb.AddForce(dir * DriftForce * rb.mass, ForceMode.Force);

                        if (PlayerController.instance != null)
                            PlayerController.instance.AntiGravity(0.1f);
                    }
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"FloatDriver.FixedUpdate: {e}");
            }
        }
    }
}
