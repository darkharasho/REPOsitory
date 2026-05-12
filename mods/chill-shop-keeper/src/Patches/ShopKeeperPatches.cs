using System;
using HarmonyLib;

namespace ChillShopKeeper.Patches;

[HarmonyPatch(typeof(ShopKeeper), "AddRuckusScore")]
internal static class ShopKeeper_AddRuckusScore_Prefix
{
    // PlayerAvatar.steamID is internal; cache a delegate that reads it directly.
    private static readonly AccessTools.FieldRef<PlayerAvatar, string> SteamIdRef =
        AccessTools.FieldRefAccess<PlayerAvatar, string>("steamID");

    private static bool Prefix(PlayerAvatar _player)
    {
        try
        {
            if (_player == null) return true;

            bool playerExempt = false;
            string sid = SteamIdRef(_player);
            if (!string.IsNullOrEmpty(sid))
                playerExempt = Plugin.Players.IsExempt(sid);

            if (ChillPolicy.ShouldSkip(Plugin.DisableGlobally.Value, playerExempt))
                return false; // skip original — no score accrues

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"AddRuckusScore prefix failed: {ex}");
            return true; // fail open
        }
    }
}
