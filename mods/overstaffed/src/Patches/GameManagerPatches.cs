using HarmonyLib;

namespace Overstaffed.Patches
{
    // Patch 1 (primary): raise GameManager.maxPlayers and maxPlayersPhoton immediately after
    // GameManager.Awake runs. Every consumer of these fields (SteamManager.HostLobby,
    // NetworkConnect's room create / room join, OnLobbyMemberLeft reconciliation) reads them
    // each time they need a player cap, so updating once on init is sufficient for those paths.
    [HarmonyPatch(typeof(GameManager), "Awake")]
    internal static class GameManagerAwakePatch
    {
        private static void Postfix(GameManager __instance)
        {
            int target = Plugin.ConfigMaxPlayers.Value;

            int oldPhoton = __instance.maxPlayersPhoton;
            int oldMax = __instance.maxPlayers;

            // Raise the ceiling first — SetMaxPlayers clamps to [1, maxPlayersPhoton], and other
            // game code reads maxPlayersPhoton when applying the "max players peer room too big"
            // auto-adjust on OnCreateRoomFailed / OnJoinRoomFailed.
            if (target > __instance.maxPlayersPhoton)
                __instance.maxPlayersPhoton = target;

            __instance.maxPlayers = target;

            Plugin.Log.LogInfo(
                $"[GameManager.Awake] maxPlayers {oldMax} -> {__instance.maxPlayers}, " +
                $"maxPlayersPhoton {oldPhoton} -> {__instance.maxPlayersPhoton}");
        }
    }

    // Patch 2 (safety net): the game calls SetMaxPlayers in reconciliation paths
    // (e.g. SteamManager.OnLobbyMemberLeft when maxPlayers < lobby.MaxMembers). SetMaxPlayers
    // clamps its argument to [1, maxPlayersPhoton]. If anything ever passes a target above the
    // vanilla ceiling, the clamp would silently trim us; raise the ceiling first.
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.SetMaxPlayers))]
    internal static class GameManagerSetMaxPlayersPatch
    {
        private static void Prefix(GameManager __instance, ref int _target)
        {
            if (_target > __instance.maxPlayersPhoton)
                __instance.maxPlayersPhoton = _target;
        }
    }
}
