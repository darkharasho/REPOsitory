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
            bool isMaster = !SemiFunc.IsMultiplayer() || PhotonNetwork.IsMasterClient;
            Plugin.Log.LogInfo($"[diag] WakeUpOrSleepLogic invoked. IsMaster={isMaster} HostOnly={Plugin.HostOnlyOnOffSwitch.Value} Multiplayer={SemiFunc.IsMultiplayer()}");

            if (!Plugin.HostOnlyOnOffSwitch.Value)
                return true;

            if (!SemiFunc.IsMultiplayer())
                return true;

            if (!PhotonNetwork.IsMasterClient)
            {
                Plugin.Log.LogInfo("[diag] blocked non-host on/off press");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"WakeUpOrSleepLogic prefix failed: {ex}");
            return true;
        }
    }
}

[HarmonyPatch(typeof(ShopKeeper), "WakeUpRPC")]
internal static class ShopKeeper_WakeUpRPC_Diag
{
    private static void Prefix(Photon.Pun.PhotonMessageInfo _info)
    {
        try
        {
            string sender = _info.Sender?.NickName ?? "<null>";
            bool senderIsMaster = _info.Sender == PhotonNetwork.MasterClient;
            Plugin.Log.LogInfo($"[diag] WakeUpRPC received. Sender='{sender}' SenderIsMaster={senderIsMaster} LocalIsMaster={PhotonNetwork.IsMasterClient}");
        }
        catch (Exception ex) { Plugin.Log.LogError($"WakeUpRPC diag failed: {ex}"); }
    }
}

[HarmonyPatch(typeof(ShopKeeper), "SleepRPC")]
internal static class ShopKeeper_SleepRPC_Diag
{
    private static void Prefix(Photon.Pun.PhotonMessageInfo _info)
    {
        try
        {
            string sender = _info.Sender?.NickName ?? "<null>";
            bool senderIsMaster = _info.Sender == PhotonNetwork.MasterClient;
            Plugin.Log.LogInfo($"[diag] SleepRPC received. Sender='{sender}' SenderIsMaster={senderIsMaster} LocalIsMaster={PhotonNetwork.IsMasterClient}");
        }
        catch (Exception ex) { Plugin.Log.LogError($"SleepRPC diag failed: {ex}"); }
    }
}
