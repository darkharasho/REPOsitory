using HarmonyLib;

namespace ForceFloat
{
    /// <summary>
    /// Keeps the flashlight on while floating. The game's <c>FlashlightController.Update</c> forces
    /// the light off whenever <c>PlayerAvatar.isTumbling</c> is true. Since ForceFloat keeps players
    /// permanently tumbling, we briefly mask the local player's <c>isTumbling</c> to false for the
    /// duration of that one Update call (then restore it in the postfix), so the flashlight behaves
    /// as if upright. Nothing else observes the change — it's set and reverted within the same
    /// synchronous Update.
    /// </summary>
    [HarmonyPatch(typeof(FlashlightController), "Update")]
    internal static class FlashlightTumblePatch
    {
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> IsTumblingRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isTumbling");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> IsLocalRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isLocal");

        [HarmonyPrefix]
        private static void Prefix(FlashlightController __instance, ref bool __state)
        {
            __state = false;
            try
            {
                if (!Plugin.Enabled.Value || !Plugin.Flashlight.Value) return;
                if (RunManager.instance == null || SemiFunc.MenuLevel() || !SemiFunc.RunIsLevel()) return;

                var pa = __instance.PlayerAvatar;
                if (pa == null || !IsLocalRef(pa)) return;

                if (IsTumblingRef(pa))
                {
                    IsTumblingRef(pa) = false;   // mask for this Update only
                    __state = true;
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"FlashlightTumblePatch.Prefix: {e}");
            }
        }

        [HarmonyPostfix]
        private static void Postfix(FlashlightController __instance, bool __state)
        {
            if (!__state) return;
            try
            {
                var pa = __instance.PlayerAvatar;
                if (pa != null) IsTumblingRef(pa) = true;   // restore
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"FlashlightTumblePatch.Postfix: {e}");
            }
        }
    }
}
