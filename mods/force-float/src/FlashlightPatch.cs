using UnityEngine;
using HarmonyLib;

namespace ForceFloat
{
    /// <summary>
    /// Keeps the flashlight on while floating. The game's <c>FlashlightController.Update</c> forces
    /// the light off whenever the local player is tumbling / crouching / sliding. Since ForceFloat
    /// keeps players permanently tumbling, we briefly mask those three flags to false for the
    /// duration of that one Update call (then restore them in the postfix), so the flashlight
    /// behaves as if upright. Nothing else observes the change — set and reverted within the same
    /// synchronous Update.
    /// </summary>
    [HarmonyPatch(typeof(FlashlightController), "Update")]
    internal static class FlashlightTumblePatch
    {
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> IsTumblingRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isTumbling");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> IsCrouchingRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isCrouching");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> IsSlidingRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isSliding");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> IsLocalRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isLocal");
        private static readonly AccessTools.FieldRef<FlashlightController, bool> ActiveRef =
            AccessTools.FieldRefAccess<FlashlightController, bool>("active");
        private static readonly AccessTools.FieldRef<FlashlightController, bool> HideFlashlightRef =
            AccessTools.FieldRefAccess<FlashlightController, bool>("hideFlashlight");

        private static float _logAccum;

        [HarmonyPrefix]
        private static void Prefix(FlashlightController __instance, ref ForceFloat.FlashState __state)
        {
            __state = default;
            try
            {
                if (!Plugin.Enabled.Value || !Plugin.Flashlight.Value) return;
                if (RunManager.instance == null || SemiFunc.MenuLevel() || !SemiFunc.RunIsLevel()) return;

                var pa = __instance.PlayerAvatar;
                if (pa == null || !IsLocalRef(pa)) return;

                __state.masked = true;
                __state.tumbling = IsTumblingRef(pa);
                __state.crouching = IsCrouchingRef(pa);
                __state.sliding = IsSlidingRef(pa);
                IsTumblingRef(pa) = false;
                IsCrouchingRef(pa) = false;
                IsSlidingRef(pa) = false;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"FlashlightTumblePatch.Prefix: {e}");
            }
        }

        [HarmonyPostfix]
        private static void Postfix(FlashlightController __instance, ForceFloat.FlashState __state)
        {
            if (!__state.masked) return;
            try
            {
                var pa = __instance.PlayerAvatar;
                if (pa != null)
                {
                    IsTumblingRef(pa) = __state.tumbling;
                    IsCrouchingRef(pa) = __state.crouching;
                    IsSlidingRef(pa) = __state.sliding;
                }

                _logAccum += Time.deltaTime;
                if (_logAccum >= 1f)
                {
                    _logAccum = 0f;
                    Plugin.Log.LogInfo($"[FloatDiag] flashlight active={ActiveRef(__instance)} " +
                                       $"LightActive={__instance.LightActive} hide={HideFlashlightRef(__instance)} " +
                                       $"wasTumbling={__state.tumbling} wasCrouching={__state.crouching}");
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"FlashlightTumblePatch.Postfix: {e}");
            }
        }
    }

    /// <summary>Per-call state shuttled from the flashlight prefix to its postfix.</summary>
    internal struct FlashState
    {
        public bool masked;
        public bool tumbling;
        public bool crouching;
        public bool sliding;
    }
}
