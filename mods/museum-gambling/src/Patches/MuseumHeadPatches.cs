using System;
using HarmonyLib;
using Photon.Pun;

namespace MuseumGambling.Patches;

[HarmonyPatch(typeof(MuseumPropMoneyHead), "StateSetRPC")]
internal static class MuseumPropMoneyHead_StateSetRPC_Postfix
{
    // Note: the actual StateSetRPC signature uses int, not byte.
    // The decompiled source (`.research/MuseumPropMoneyHead.cs`) confirms:
    //   private void StateSetRPC(int _newState, PhotonMessageInfo _info = default)
    private static void Postfix(MuseumPropMoneyHead __instance, int _newState)
    {
        try
        {
            if ((MuseumPropMoneyHead.State)_newState != MuseumPropMoneyHead.State.Closed)
                return;

            if (!Plugin.Enabled.Value)
                return;

            // Master-only roll. In singleplayer (no room), IsMasterClient is true by default.
            if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
                return;

            int roll = UnityEngine.Random.Range(1, 101); // 1..100 inclusive
            int chance = Plugin.WinChancePercent.Value;
            bool win = Outcome.ShouldWin(roll, chance);

            int viewId = __instance.GetComponent<PhotonView>()?.ViewID ?? 0;
            Plugin.Log.LogInfo(
                $"[MuseumGambling] viewId={viewId} roll={roll} chance={chance} win={win}");

            WinBroadcast.Send(viewId, win);

            if (win)
            {
                // Phase 3: log only. Payout.Spawn wired in Phase 4.
                Plugin.Log.LogInfo("[MuseumGambling] WIN — pending damage suppression (no payout yet).");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"MuseumPropMoneyHead.StateSetRPC postfix failed: {ex}");
        }
    }
}
