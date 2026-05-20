using System;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace MuseumGambling.Patches;

[HarmonyPatch(typeof(MuseumPropMoneyHead), "DragInPlayerStart")]
internal static class MuseumPropMoneyHead_DragInPlayerStart_Postfix
{
    // private PlayerAvatar playerToDragIn — captured at grab time, on master.
    private static readonly FieldInfo? PlayerToDragInField =
        AccessTools.Field(typeof(MuseumPropMoneyHead), "playerToDragIn");

    private static void Postfix(MuseumPropMoneyHead __instance)
    {
        try
        {
            WinBroadcast.Register();
            if (PlayerToDragInField == null) return;
            if (PlayerToDragInField.GetValue(__instance) is not PlayerAvatar player) return;
            int viewId = __instance.GetComponent<PhotonView>()?.ViewID ?? 0;
            GrabSpot.Record(viewId, player.transform.position);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"DragInPlayerStart postfix failed: {ex}");
        }
    }
}

[HarmonyPatch(typeof(MuseumPropMoneyHead), "StateSetRPC")]
internal static class MuseumPropMoneyHead_StateSetRPC_Postfix
{
    private static void Postfix(MuseumPropMoneyHead __instance, int _newState)
    {
        try
        {
            WinBroadcast.Register();
            var state = (MuseumPropMoneyHead.State)_newState;
            int viewId = __instance.GetComponent<PhotonView>()?.ViewID ?? 0;

            if (state == MuseumPropMoneyHead.State.Closed)
            {
                if (!Plugin.Enabled.Value) return;
                if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient) return;

                // Anti-cheat: if the grabbed player ducked under the ledge and
                // never got dragged into the mouth, skip the roll entirely.
                // Vanilla damage still won't reach them, but they also can't
                // win the payout.
                if (!IsGrabbedPlayerInMouth(__instance))
                {
                    Plugin.Log.LogInfo(
                        $"[MuseumGambling] viewId={viewId} grabbed player not inside mouth box — skipping roll (cheat guard).");
                    return;
                }

                int roll = UnityEngine.Random.Range(1, 101); // 1..100 inclusive
                int chance = Plugin.WinChancePercent.Value;
                bool win = Outcome.ShouldWin(roll, chance);

                Plugin.Log.LogInfo(
                    $"[MuseumGambling] viewId={viewId} roll={roll} chance={chance} win={win}");

                WinBroadcast.Send(viewId, win);
                return;
            }

            if (state == MuseumPropMoneyHead.State.Opening)
            {
                // Clear the damage-clamp flag now that the Closed window is
                // ending — paired with PeekPendingResult in the HurtCollider
                // prefix so every re-trigger during Closed sees the win.
                WinBroadcast.ClearPendingResult(viewId);

                if (!WinBroadcast.ConsumePendingEffect(viewId)) return;

                Plugin.Log.LogInfo($"[MuseumGambling] Effects firing for view {viewId}.");

                // Master spawns the payout at the player's pre-suck position
                // (recorded by DragInPlayerStart). Lifted slightly so it doesn't
                // clip the floor. Falls back to head position if no grab record.
                if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
                {
                    Vector3 spawnPos = GrabSpot.Consume(viewId)
                        ?? __instance.transform.position;
                    spawnPos += Vector3.up * 0.5f;
                    Payout.Spawn(spawnPos, Plugin.PayoutValue.Value);
                }

                // Sound + light flash run on every client locally.
                WinSound.Play(__instance.transform.position);
                FlashEyesGreen(__instance);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"MuseumPropMoneyHead.StateSetRPC postfix failed: {ex}");
        }
    }

    // Vanilla state transitions overwrite eye light color, so the green tint
    // naturally clears as the head returns to its Open idle.
    private static void FlashEyesGreen(MuseumPropMoneyHead head)
    {
        if (head.eye1Light != null) head.eye1Light.color = Color.green;
        if (head.eye2Light != null) head.eye2Light.color = Color.green;
    }

    // Mirrors vanilla MuseumPropMoneyHead.BoxCheck: overlap the mouth-cavity
    // box and look for any PhysGrabObject. Vanilla uses the same gate to
    // decide whether to drag the player into forcePoint vs forcePointFirst,
    // so an empty box is exactly the cheat case (player hiding under the
    // ledge so the drag-in force can't reach them).
    private static bool IsGrabbedPlayerInMouth(MuseumPropMoneyHead head)
    {
        var box = head.boxColliderCheckTransform;
        if (box == null) return false;

        var hits = Physics.OverlapBox(
            box.position,
            box.localScale / 2f,
            box.rotation);

        foreach (var col in hits)
        {
            if (col.GetComponentInParent<PhysGrabObject>() != null) return true;
        }
        return false;
    }
}
