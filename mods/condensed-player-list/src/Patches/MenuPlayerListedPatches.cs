using System;
using HarmonyLib;
using UnityEngine;

namespace CondensedPlayerList.Patches
{
    // Vanilla MenuPlayerListed.Update() sets localPosition every frame as one tall
    // column (lobby spacing 32, esc 22). This postfix runs after and overwrites it
    // with the condensed spacing. forceCrown entries (arena winners) are left alone.
    [HarmonyPatch(typeof(MenuPlayerListed), "Update")]
    internal static class MenuPlayerListed_Update_Postfix
    {
        private static readonly AccessTools.FieldRef<MenuPlayerListed, int> ListSpotRef =
            AccessTools.FieldRefAccess<MenuPlayerListed, int>("listSpot");

        private static bool _errorLogged;

        private static void Postfix(MenuPlayerListed __instance)
        {
            try
            {
                if (__instance.forceCrown)
                    return;

                int listSpot = ListSpotRef(__instance);
                bool isLobby = SemiFunc.RunIsLobbyMenu();
                var (x, y) = CondensedLayout.CondensedPosition(listSpot, isLobby);
                __instance.transform.localPosition = new Vector3(x, y, 0f);
            }
            catch (Exception ex)
            {
                // Never let a per-frame postfix throw repeatedly.
                if (!_errorLogged)
                {
                    Plugin.Log.LogError($"MenuPlayerListed Update postfix failed: {ex}");
                    _errorLogged = true;
                }
            }
        }
    }
}
