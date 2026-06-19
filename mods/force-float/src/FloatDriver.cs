using HarmonyLib;
using UnityEngine;

namespace ForceFloat
{
    /// <summary>
    /// Keeps every player permanently floating during levels by reproducing the Zero Gravity
    /// Staff's effect (<c>SemiAffectZeroGravity</c>) frame-for-frame, with the SAME master/local
    /// split the staff uses:
    ///   * The staff's effect object exists on EVERY client. Per-client work (engaging tumble for
    ///     your own avatar, anti-gravity, steering with your own camera) runs locally on each
    ///     machine — that's why every player sees themselves floating and can steer.
    ///   * Physics simulation is master-authoritative (<c>PhysGrabObject.FixedUpdate</c> returns on
    ///     non-masters), so the zero-gravity / drag / lift forces for ALL players are applied by the
    ///     host, which streams the resulting positions to everyone.
    ///
    /// Requires the mod on every client (each client engages its own avatar). Dead players are
    /// never touched (their head body would fight the forces and shake the screen).
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
        private float _logAccum;

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

        private bool Enabled()
        {
            if (!Plugin.Enabled.Value) return false;
            return ShouldFloat();
        }

        private static bool IsAlive(PlayerAvatar pa) => !AvatarDeadSetRef(pa) && !AvatarIsDisabledRef(pa);

        /// <summary>Keep a player tumbling + wings on (the part the game does on the master).</summary>
        private static void EnsureTumbling(PlayerAvatar pa)
        {
            var tumble = AvatarTumbleRef(pa);
            if (tumble == null) return;
            if (!AvatarIsTumblingRef(pa))
                tumble.TumbleRequest(true, false);
            tumble.TumbleOverrideTime(0.5f);
            if (!AvatarWingsActiveRef(pa))
                pa.UpgradeTumbleWingsVisualsActive();
        }

        private void Update()
        {
            try
            {
                if (!Enabled()) return;

                // Master keeps every living player tumbling (TumbleRequest routes through the
                // master and broadcasts to all, so this also covers everyone's state).
                if (SemiFunc.IsMasterClientOrSingleplayer() && GameDirector.instance != null)
                {
                    var list = GameDirector.instance.PlayerList;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var pa = list[i];
                        if (pa != null && IsAlive(pa)) EnsureTumbling(pa);
                    }
                }
                else
                {
                    // Non-master client: at minimum keep our OWN avatar tumbling.
                    var me = PlayerAvatar.instance;
                    if (me != null && IsAlive(me)) EnsureTumbling(me);
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"FloatDriver.Update: {e}");
            }
        }

        private void FixedUpdate()
        {
            try
            {
                if (!Enabled()) return;

                bool liftTick = false;
                _liftAccum += Time.fixedDeltaTime;
                float liftStep = 1f / LiftPerSecond;
                if (_liftAccum >= liftStep) { _liftAccum -= liftStep; liftTick = true; }

                // --- Master: simulate zero-gravity + drag + lift for every living player ---
                if (SemiFunc.IsMasterClientOrSingleplayer() && GameDirector.instance != null)
                {
                    var list = GameDirector.instance.PlayerList;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var pa = list[i];
                        if (pa == null || !IsAlive(pa)) continue;
                        ApplyPhysics(pa, liftTick);
                    }
                }

                // --- Every client: steer MY OWN avatar with my camera (anti-gravity + drift) ---
                var me = PlayerAvatar.instance;
                if (me != null && IsAlive(me) && me.localCamera != null)
                {
                    if (PlayerController.instance != null)
                        PlayerController.instance.AntiGravity(0.1f);
                    SteerLocal(me);
                }

                MaybeLog();
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"FloatDriver.FixedUpdate: {e}");
            }
        }

        private void ApplyPhysics(PlayerAvatar pa, bool liftTick)
        {
            var tumble = AvatarTumbleRef(pa);
            if (tumble == null) return;
            var pgo = TumblePhysGrabObjectRef(tumble);
            if (pgo == null || pgo.rb == null) return;
            var rb = pgo.rb;

            pgo.OverrideZeroGravity();
            pgo.OverrideDrag(DriftDrag);
            pgo.OverrideAngularDrag(DriftAngularDrag);

            if (liftTick)
            {
                rb.AddForce(Vector3.up * (0.5f / LiftPerSecond) * (rb.mass * 0.2f), ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere.normalized * 0.01f / LiftPerSecond * rb.mass, ForceMode.Impulse);
            }
        }

        private void SteerLocal(PlayerAvatar me)
        {
            var tumble = AvatarTumbleRef(me);
            if (tumble == null) return;
            var pgo = TumblePhysGrabObjectRef(tumble);
            if (pgo == null || pgo.rb == null) return;
            var rb = pgo.rb;

            Transform cam = me.localCamera.GetOverrideTransform();
            if (cam == null) return;

            Quaternion target = Quaternion.LookRotation(cam.forward, Vector3.up);
            Vector3 torque = SemiFunc.PhysFollowRotation(pgo.transform, target, rb, FollowRotationFactor);
            rb.AddTorque(torque, ForceMode.Impulse);

            Vector3 input = AvatarInputDirectionRawRef(me);
            Vector3 dir = cam.forward * input.z + cam.right * input.x;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            if (dir.sqrMagnitude >= 0.0001f)
                rb.AddForce(dir * DriftForce * rb.mass, ForceMode.Force);
        }

        /// <summary>Diagnostic: log local-player float state once a second so we can see what's wrong in MP.</summary>
        private void MaybeLog()
        {
            _logAccum += Time.fixedDeltaTime;
            if (_logAccum < 1f) return;
            _logAccum = 0f;
            var me = PlayerAvatar.instance;
            if (me == null) return;
            var tumble = AvatarTumbleRef(me);
            var pgo = tumble != null ? TumblePhysGrabObjectRef(tumble) : null;
            var rb = pgo != null ? pgo.rb : null;
            bool master = SemiFunc.IsMasterClientOrSingleplayer();
            string y = rb != null ? rb.position.y.ToString("F2") : "?";
            string kin = rb != null ? rb.isKinematic.ToString() : "?";
            Plugin.Log.LogInfo($"[FloatDiag] master={master} local.isTumbling={AvatarIsTumblingRef(me)} " +
                               $"wings={AvatarWingsActiveRef(me)} bodyY={y} kinematic={kin}");
        }
    }
}
