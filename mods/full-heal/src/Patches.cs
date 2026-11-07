using HarmonyLib;

namespace FullHeal
{
    internal static class RoundStart
    {
        // Same gating as FullHealth: the singleplayer path hooks StartRound, the multiplayer path
        // hooks StartRoundRPC (which every client receives once the host has set the haul goal).
        internal static void Handle(bool multiplayerPath)
        {
            SurvivorTracker.OnRoundStart();

            if (!Settings.Start.Enabled.Value
                || SemiFunc.IsMultiplayer() != multiplayerPath
                || !Healer.WorkModeAllows(HealTrigger.Start)
                || !Healer.PhaseAllows(HealTrigger.Start))
            {
                SurvivorTracker.MarkAllAliveIfLevel();
                return;
            }

            Healer.StartRoundHeal();
        }
    }

    // StartRound is a coroutine; this postfix runs when RoundDirector.Start() kicks it off.
    [HarmonyPatch(typeof(RoundDirector), "StartRound")]
    internal static class RoundDirectorStartRoundPatch
    {
        private static void Postfix() => RoundStart.Handle(multiplayerPath: false);
    }

    [HarmonyPatch(typeof(RoundDirector), "StartRoundRPC")]
    internal static class RoundDirectorStartRoundRPCPatch
    {
        private static void Postfix() => RoundStart.Handle(multiplayerPath: true);
    }

    [HarmonyPatch(typeof(PlayerAvatar), "ReviveRPC")]
    internal static class PlayerAvatarReviveRPCPatch
    {
        private static void Postfix(PlayerAvatar __instance)
        {
            SurvivorTracker.Revived(__instance);
            if (!Settings.Revival.Enabled.Value) return;
            if (Healer.WorkModeAllows(HealTrigger.Revival) && Healer.PhaseAllows(HealTrigger.Revival))
                Healer.Heal(HealTrigger.Revival, __instance, Players.All().Count);
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "PlayerDeathRPC")]
    internal static class PlayerAvatarPlayerDeathRPCPatch
    {
        private static void Postfix(PlayerAvatar __instance) => SurvivorTracker.Died(__instance);
    }
}
