using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace FullHeal
{
    internal static class PlayerFields
    {
        // These are internal in Assembly-CSharp; read them without a publicizer.
        internal static readonly AccessTools.FieldRef<PlayerAvatar, string> SteamId =
            AccessTools.FieldRefAccess<PlayerAvatar, string>("steamID");
        internal static readonly AccessTools.FieldRef<PlayerAvatar, string> Name =
            AccessTools.FieldRefAccess<PlayerAvatar, string>("playerName");
        internal static readonly AccessTools.FieldRef<PlayerAvatar, bool> IsLocal =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isLocal");
        internal static readonly AccessTools.FieldRef<PlayerAvatar, bool> LevelAnimationCompleted =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("levelAnimationCompleted");
        internal static readonly AccessTools.FieldRef<PlayerHealth, int> Health =
            AccessTools.FieldRefAccess<PlayerHealth, int>("health");
        internal static readonly AccessTools.FieldRef<PlayerHealth, int> MaxHealth =
            AccessTools.FieldRefAccess<PlayerHealth, int>("maxHealth");
    }

    internal static class Players
    {
        public static List<PlayerAvatar> All()
        {
            try
            {
                var list = SemiFunc.PlayerGetList();
                if (list != null && list.Count > 0) return list.Where(p => p != null).ToList();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("An error occurred while retrieving the list of players: " + ex.Message);
            }
            var local = SemiFunc.PlayerAvatarLocal();
            return local != null ? new List<PlayerAvatar> { local } : new List<PlayerAvatar>();
        }
    }

    /// <summary>Tracks who survived the last round, for HealRequirementMode = Survive.</summary>
    internal static class SurvivorTracker
    {
        private static readonly HashSet<string> Alive = new HashSet<string>();
        private static bool _gameStarting;

        public static bool Survived(string steamId) => steamId != null && Alive.Contains(steamId);

        public static void Revived(PlayerAvatar player)
        {
            if (SemiFunc.RunIsLevel()) Alive.Add(PlayerFields.SteamId(player));
        }

        public static void Died(PlayerAvatar player)
        {
            if (SemiFunc.RunIsLevel()) Alive.Remove(PlayerFields.SteamId(player));
        }

        /// <summary>A fresh run counts everyone as a survivor of the (non-existent) previous round.</summary>
        public static void OnRoundStart()
        {
            if (SemiFunc.RunIsLobbyMenu())
            {
                _gameStarting = true;
            }
            else if (_gameStarting)
            {
                MarkAllAlive();
                _gameStarting = false;
            }
        }

        public static void MarkAllAliveIfLevel()
        {
            if (SemiFunc.RunIsLevel()) MarkAllAlive();
        }

        private static void MarkAllAlive()
        {
            Alive.Clear();
            foreach (var p in Players.All()) Alive.Add(PlayerFields.SteamId(p));
        }
    }

    internal static class Healer
    {
        private static readonly System.Random Rng = new System.Random();
        private static Coroutine? _pending;

        public static bool WorkModeAllows(HealTrigger trigger)
        {
            var mode = Settings.For(trigger).WorkMode.Value;
            return SemiFunc.IsMasterClientOrSingleplayer()
                ? ((byte)mode & (byte)WorkMode.Host) != 0
                : ((byte)mode & (byte)WorkMode.Client) != 0;
        }

        public static bool PhaseAllows(HealTrigger trigger)
        {
            PhaseMode phase;
            if (SemiFunc.RunIsLevel()) phase = PhaseMode.Level;
            else if (SemiFunc.RunIsShop()) phase = PhaseMode.Shop;
            else if (SemiFunc.RunIsLobby()) phase = PhaseMode.Lobby;
            else if (SemiFunc.RunIsArena()) phase = PhaseMode.Arena;
            else return false;
            return Settings.For(trigger).PhaseMode.Value.HasFlag(phase);
        }

        /// <summary>
        /// Round-start heal. Runs as a coroutine on Unity's main thread: the original ran this in
        /// Task.Run, so HealOther's Photon RPC fired from a thread-pool thread and could hang the game.
        /// </summary>
        public static void StartRoundHeal()
        {
            if (_pending != null) Plugin.Instance.StopCoroutine(_pending);
            _pending = Plugin.Instance.StartCoroutine(RoundHealRoutine());
        }

        private static IEnumerator RoundHealRoutine()
        {
            try
            {
                // Wait (up to a minute) for every player to finish the level-intro animation, as the original did.
                var deadline = Time.realtimeSinceStartup + 60f;
                var ready = false;
                while (Time.realtimeSinceStartup < deadline)
                {
                    yield return new WaitForSecondsRealtime(1f);
                    if (SemiFunc.RunIsLobby() || Players.All().All(p => PlayerFields.LevelAnimationCompleted(p)))
                    {
                        ready = true;
                        break;
                    }
                }
                if (!ready)
                {
                    Plugin.Log.LogDebug("Timed out waiting for players to load; skipping heal");
                    yield break;
                }

                var players = Players.All();
                foreach (var p in players) Heal(HealTrigger.Start, p, players.Count);
            }
            finally
            {
                SurvivorTracker.MarkAllAliveIfLevel();
                _pending = null;
            }
        }

        public static void Heal(HealTrigger trigger, PlayerAvatar player, int playerCount)
        {
            var s = Settings.For(trigger);
            var name = PlayerFields.Name(player);

            var wanted = PlayerFields.IsLocal(player) ? HealMode.Self : HealMode.Others;
            if (!s.HealMode.Value.HasFlag(wanted)) return;

            var health = player.playerHealth;
            if (health == null)
            {
                Plugin.Log.LogWarning($"Player '{name}' has no health component");
                return;
            }

            // A revived player comes back at 1 HP; heal amounts are computed from that.
            var current = trigger == HealTrigger.Revival ? 1 : PlayerFields.Health(health);
            var max = PlayerFields.MaxHealth(health);
            var amount = Amount(trigger, s, player, current, max, playerCount);

            if (amount <= 0)
            {
                Plugin.Log.LogDebug($"Player '{name}' was not healed because the heal value is zero");
                return;
            }

            health.HealOther(amount, true);
            Plugin.Log.LogDebug($"Player '{name}' with '{current}' HP received '{amount}' HP. So player should have '{Math.Min(max, current + amount)}' HP");
        }

        private static int Amount(HealTrigger trigger, HealSettings s, PlayerAvatar player, int current, int max, int playerCount)
        {
            var steamId = PlayerFields.SteamId(player);

            if (trigger == HealTrigger.Start
                && Settings.HealRequirementMode.Value.HasFlag(HealRequirementMode.Survive)
                && !SurvivorTracker.Survived(steamId))
            {
                Plugin.Log.LogDebug($"The player '{PlayerFields.Name(player)}' did not survive and will not be healed");
                return 0;
            }

            if (HealExpressions.IsSet(trigger))
            {
                try
                {
                    return HealExpressions.Calculate(trigger, new HealContext(PlayerFields.Name(player), max, PlayerFields.Health(player.playerHealth), steamId, playerCount));
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError("An error occurred while calculating the heal value. Please check the expression: " + ex.Message);
                    return 0;
                }
            }

            var packs = s.HealthPackValues;
            if (packs.Length > 0) return packs[Rng.Next(packs.Length)];
            if (s.ExactValue.Value > 0) return s.ExactValue.Value;
            if (s.ByMaxHealthPercentage.Value > 0m) return decimal.ToInt32(max * (s.ByMaxHealthPercentage.Value / 100m));
            return Math.Max(0, decimal.ToInt32(max * (s.ToMaxHealthPercentage.Value / 100m)) - current);
        }
    }
}
