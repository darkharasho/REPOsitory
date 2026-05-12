using System;
using HarmonyLib;

namespace ChillShopKeeper.Patches;

[HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.AddToStatsManagerRPC))]
internal static class PlayerAvatar_AddToStatsManagerRPC_Postfix
{
    private static void Postfix(string _playerName, string _steamID)
    {
        try
        {
            Plugin.Players.Observe(_steamID, _playerName);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"PlayerObserver postfix failed: {ex}");
        }
    }
}
