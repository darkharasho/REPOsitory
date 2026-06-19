using HarmonyLib;
using UnityEngine;

namespace ForceFloat
{
    /// <summary>
    /// Keeps players permanently under the Zero Gravity Staff effect during levels. Mirrors
    /// the game's SemiAffectZeroGravity: tumble + anti-gravity + camera-directed drift for the
    /// local player, and (as master client) tumbles every other player so unmodded clients float too.
    /// </summary>
    public class FloatDriver : MonoBehaviour
    {
        private const float DriftForce = 8f;
        private bool wasActive;

        // PlayerAvatar internal fields — accessed via cached field-ref delegates (same pattern as ForcedFriendship)
        private static readonly AccessTools.FieldRef<PlayerAvatar, PlayerTumble> AvatarTumbleRef =
            AccessTools.FieldRefAccess<PlayerAvatar, PlayerTumble>("tumble");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> AvatarIsTumblingRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isTumbling");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> AvatarIsLocalRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isLocal");
        private static readonly AccessTools.FieldRef<PlayerAvatar, Vector3> AvatarInputDirectionRawRef =
            AccessTools.FieldRefAccess<PlayerAvatar, Vector3>("InputDirectionRaw");

        // PlayerTumble.rb is also internal
        private static readonly AccessTools.FieldRef<PlayerTumble, Rigidbody> TumbleRbRef =
            AccessTools.FieldRefAccess<PlayerTumble, Rigidbody>("rb");

        /// <summary>Floating is active only during real levels (not shop/lobby/menus/arena).</summary>
        private static bool ShouldFloat()
        {
            if (RunManager.instance == null) return false;
            if (SemiFunc.MenuLevel()) return false;
            return SemiFunc.RunIsLevel();
        }

        private void Update()
        {
            try
            {
                if (!ShouldFloat())
                {
                    if (wasActive) Release();
                    return;
                }

                var avatar = PlayerAvatar.instance;
                if (avatar == null) return;

                wasActive = true;

                // --- local player: tumble + anti-gravity + wings ---
                if (PlayerController.instance != null)
                    PlayerController.instance.AntiGravity(0.1f);

                var tumble = AvatarTumbleRef(avatar);
                if (tumble != null)
                {
                    if (!AvatarIsTumblingRef(avatar))
                        tumble.TumbleRequest(true, false);
                    tumble.TumbleOverrideTime(0.5f);
                }

                if (Plugin.Wings.Value)
                    avatar.UpgradeTumbleWingsVisualsActive();

                // --- master client: keep everyone else tumbling (covers unmodded players) ---
                if (SemiFunc.IsMasterClientOrSingleplayer() && GameDirector.instance != null)
                {
                    var list = GameDirector.instance.PlayerList;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var pa = list[i];
                        if (pa == null || AvatarIsLocalRef(pa)) continue;
                        var paTumble = AvatarTumbleRef(pa);
                        if (paTumble == null) continue;
                        if (!AvatarIsTumblingRef(pa))
                            paTumble.TumbleRequest(true, false);
                        paTumble.TumbleOverrideTime(0.5f);
                    }
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"FloatDriver.Update: {e}");
            }
        }

        private void FixedUpdate()
        {
            if (!Plugin.EnableDrift.Value || !ShouldFloat()) return;

            try
            {
                var avatar = PlayerAvatar.instance;
                if (avatar == null || avatar.localCamera == null) return;

                var tumble = AvatarTumbleRef(avatar);
                if (tumble == null) return;

                var rb = TumbleRbRef(tumble);
                if (rb == null) return;

                Transform cam = avatar.localCamera.GetOverrideTransform();
                if (cam == null) return;

                // Face movement direction, as the staff does.
                Quaternion target = Quaternion.LookRotation(cam.forward, Vector3.up);
                Vector3 torque = SemiFunc.PhysFollowRotation(tumble.transform, target, rb, 20f);
                rb.AddTorque(torque, ForceMode.Impulse);

                Vector3 input = AvatarInputDirectionRawRef(avatar);
                V3 dir = FloatMath.DriftDirection(
                    new V3(cam.forward.x, cam.forward.y, cam.forward.z),
                    new V3(cam.right.x, cam.right.y, cam.right.z),
                    input.x, input.z);

                if (dir.SqrMagnitude < 0.0001f) return;
                rb.AddForce(new Vector3(dir.X, dir.Y, dir.Z) * DriftForce * rb.mass, ForceMode.Force);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"FloatDriver.FixedUpdate: {e}");
            }
        }

        /// <summary>Stop floating when leaving a level so players land normally in the truck/shop.</summary>
        private void Release()
        {
            wasActive = false;
            try
            {
                var avatar = PlayerAvatar.instance;
                if (avatar == null) return;
                var tumble = AvatarTumbleRef(avatar);
                if (tumble != null && AvatarIsTumblingRef(avatar))
                    tumble.TumbleRequest(false, false);
                if (Plugin.Wings.Value)
                    avatar.UpgradeTumbleWingsVisualsActive(false);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"FloatDriver.Release: {e}");
            }
        }
    }
}
