using System;
using HarmonyLib;
using Photon.Pun;

namespace MuseumGambling.Patches;

// On a win we used to suppress damage outright — but the absence of the
// red flash / sound was a dead giveaway the moment the head closed.
// Instead, on a win we temporarily neutralise the head's lethality
// (playerKill=false, playerDamage=1) so vanilla still fires the full hit
// reaction (flash, camera glitch, tumble), and then Heal the player to
// full silently in the postfix. The damage cue plays for both outcomes;
// only the payout that spawns at State.Opening differentiates them.
[HarmonyPatch(typeof(HurtCollider), "PlayerHurt")]
internal static class HurtCollider_PlayerHurt_Patch
{
    internal struct HurtState
    {
        public bool Win;
        public PlayerAvatar? Player;
        public bool SavedPlayerKill;
        public int SavedPlayerDamage;
    }

    private static bool Prefix(HurtCollider __instance, PlayerAvatar _player, out HurtState __state)
    {
        __state = default;
        try
        {
            var head = __instance.GetComponentInParent<MuseumPropMoneyHead>();
            if (head == null) return true; // not a museum-head hurtcollider — leave alone

            int viewId = head.GetComponent<PhotonView>()?.ViewID ?? 0;
            bool win = WinBroadcast.PeekPendingResult(viewId);
            if (!win) return true;

            __state.Win = true;
            __state.Player = _player;
            __state.SavedPlayerKill = __instance.playerKill;
            __state.SavedPlayerDamage = __instance.playerDamage;
            __instance.playerKill = false;
            __instance.playerDamage = 1;
            Plugin.Log.LogInfo($"[MuseumGambling] Win for view {viewId}: clamping damage to 1 and healing.");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"HurtCollider.PlayerHurt prefix failed: {ex}");
            return true;
        }
    }

    // Finalizer (not Postfix) so the field restore runs even if vanilla
    // PlayerHurt throws. A regular Postfix would skip on exception and leave
    // the head's hurt collider permanently neutered for the run.
    private static Exception? Finalizer(HurtCollider __instance, HurtState __state, Exception? __exception)
    {
        if (!__state.Win) return __exception;
        try
        {
            __instance.playerKill = __state.SavedPlayerKill;
            __instance.playerDamage = __state.SavedPlayerDamage;

            if (__exception == null)
            {
                var health = __state.Player?.playerHealth;
                // Heal clamps to maxHealth internally — passing MaxValue
                // sidesteps the internal `maxHealth` field.
                health?.Heal(int.MaxValue, effect: false);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"HurtCollider.PlayerHurt finalizer failed: {ex}");
        }
        return __exception; // re-throw whatever vanilla threw; never swallow
    }
}
