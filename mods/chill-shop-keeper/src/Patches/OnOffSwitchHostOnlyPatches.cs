using System;
using HarmonyLib;
using Photon.Pun;

namespace ChillShopKeeper.Patches;

[HarmonyPatch(typeof(ShopKeeper), "WakeUpOrSleepLogic")]
internal static class ShopKeeper_WakeUpOrSleepLogic_Prefix
{
    private static bool Prefix()
    {
        try
        {
            if (!Plugin.HostOnlyOnOffSwitch.Value)
                return true;

            if (!SemiFunc.IsMultiplayer())
                return true;

            if (!PhotonNetwork.IsMasterClient)
                return false;

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"WakeUpOrSleepLogic prefix failed: {ex}");
            return true;
        }
    }
}
