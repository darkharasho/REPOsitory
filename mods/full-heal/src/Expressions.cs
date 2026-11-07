using System;
using System.Collections.Generic;
using System.Linq;
using Expressive;
using Expressive.Expressions;
using Expressive.Functions;

namespace FullHeal
{
    /// <summary>Per-player values an Expression can read, e.g. [PlayerMaxHealth].</summary>
    public readonly struct HealContext
    {
        public readonly string PlayerName;
        public readonly int MaxHealth;
        public readonly int Health;
        public readonly string SteamId;
        public readonly int PlayerCount;

        public HealContext(string playerName, int maxHealth, int health, string steamId, int playerCount)
        {
            PlayerName = playerName;
            MaxHealth = maxHealth;
            Health = health;
            SteamId = steamId;
            PlayerCount = playerCount;
        }
    }

    public static class HealExpressions
    {
        private static readonly Dictionary<HealTrigger, Expression?> Compiled = new Dictionary<HealTrigger, Expression?>
        {
            { HealTrigger.Start, null },
            { HealTrigger.Revival, null },
        };

        public static void Update(HealTrigger trigger, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Compiled[trigger] = null;
                return;
            }
            var expression = new Expression(text);
            expression.RegisterFunction(new RandomListFunction());
            Compiled[trigger] = expression;
        }

        public static bool IsSet(HealTrigger trigger) => Compiled[trigger] != null;

        public static int Calculate(HealTrigger trigger, HealContext ctx)
        {
            var expression = Compiled[trigger] ?? throw new InvalidOperationException("No expression set");
            return Convert.ToInt32(expression.Evaluate(new Variables(ctx)));
        }

        /// <summary>Evaluates a configured expression once at load so typos show up in the log immediately.</summary>
        public static void SelfTest(HealTrigger trigger, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                var result = Calculate(trigger, new HealContext("TestPlayer", 100, 30, Guid.NewGuid().ToString(), 4));
                Plugin.Log.LogInfo($"Calculation test of the healing completed. Variables: PlayerMaxHealth='100', PlayerHealth='30', PlayerCount='4'. Result: '{result}'");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("An error occurred while calculating the heal value. Please check the expression: " + ex.Message);
            }
        }

        private sealed class Variables : IVariableProvider
        {
            private readonly HealContext _ctx;
            public Variables(HealContext ctx) => _ctx = ctx;

            public bool TryGetValue(string variableName, out object value)
            {
                switch (variableName)
                {
                    case "PlayerName": value = _ctx.PlayerName; return true;
                    case "PlayerMaxHealth": value = _ctx.MaxHealth; return true;
                    case "PlayerHealth": value = _ctx.Health; return true;
                    case "PlayerSurvived": value = SurvivorTracker.Survived(_ctx.SteamId); return true;
                    case "PlayerCount": value = _ctx.PlayerCount; return true;
                    case "PlayerUpgradeHealth": value = Upgrade(StatsManager.instance?.playerUpgradeHealth); return true;
                    case "PlayerUpgradeStamina": value = Upgrade(StatsManager.instance?.playerUpgradeStamina); return true;
                    case "PlayerUpgradeExtraJump": value = Upgrade(StatsManager.instance?.playerUpgradeExtraJump); return true;
                    case "PlayerUpgradeLaunch": value = Upgrade(StatsManager.instance?.playerUpgradeLaunch); return true;
                    case "PlayerUpgradeMapPlayerCount": value = Upgrade(StatsManager.instance?.playerUpgradeMapPlayerCount); return true;
                    case "PlayerUpgradeSpeed": value = Upgrade(StatsManager.instance?.playerUpgradeSpeed); return true;
                    case "PlayerUpgradeStrength": value = Upgrade(StatsManager.instance?.playerUpgradeStrength); return true;
                    case "PlayerUpgradeThrow": value = Upgrade(StatsManager.instance?.playerUpgradeThrow); return true;
                    case "PlayerUpgradeRange": value = Upgrade(StatsManager.instance?.playerUpgradeRange); return true;
                    default: value = null!; return false;
                }
            }

            private int Upgrade(Dictionary<string, int>? levels) =>
                levels != null && _ctx.SteamId != null && levels.TryGetValue(_ctx.SteamId, out var level) ? level : 0;
        }

        /// <summary>RandomList(x1, x2, ...) - returns one of its arguments at random.</summary>
        private sealed class RandomListFunction : FunctionBase
        {
            private static readonly Random Rng = new Random();
            public override string Name => "RandomList";

            public override object Evaluate(IExpression[] parameters, Context context)
            {
                if (parameters == null || parameters.Length == 0)
                    throw new Exception($"{Name}() expects at least 1 argument(s)");
                var values = parameters.Select(p => p.Evaluate(Variables)).ToList();
                return values[Rng.Next(values.Count)];
            }
        }
    }
}
