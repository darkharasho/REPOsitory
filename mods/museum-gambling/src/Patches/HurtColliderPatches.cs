using System;
using HarmonyLib;
using Photon.Pun;

namespace MuseumGambling.Patches;

[HarmonyPatch(typeof(HurtCollider), "PlayerHurt")]
internal static class HurtCollider_PlayerHurt_Prefix
{
    private static bool Prefix(HurtCollider __instance)
    {
        try
        {
            var head = __instance.GetComponentInParent<MuseumPropMoneyHead>();
            if (head == null) return true; // not a museum-head hurtcollider — leave alone

            int viewId = head.GetComponent<PhotonView>()?.ViewID ?? 0;
            bool win = WinBroadcast.ConsumePendingResult(viewId);

            if (win)
            {
                Plugin.Log.LogInfo($"[MuseumGambling] Damage suppressed for view {viewId}.");
                return false; // skip vanilla damage
            }

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"HurtCollider.PlayerHurt prefix failed: {ex}");
            return true; // fail open
        }
    }
}
