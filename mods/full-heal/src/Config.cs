using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace FullHeal
{
    // Config keys, defaults and enum values match FullHealth 1.9.0 exactly, so an existing
    // FullHealth.cfg can be copied over darkharasho.FullHeal.cfg unchanged.

    [Flags]
    public enum HealMode : byte { Self = 1, Others = 2, All = Self | Others }

    [Flags]
    public enum HealthPackMode : byte { None = 0, Small = 1, Medium = 2, Large = 4, All = Small | Medium | Large }

    [Flags]
    public enum PhaseMode : byte { Level = 1, Shop = 2, Lobby = 4, Arena = 8, All = Level | Shop | Lobby | Arena }

    [Flags]
    public enum HealRequirementMode : byte { None = 0, Survive = 1 }

    // Not [Flags] in the original either, but the values still work as bit flags (All = 3).
    public enum WorkMode : byte { Host = 1, Client = 2, All = 3 }

    public enum HealTrigger { Start, Revival }

    public sealed class HealSettings
    {
        public ConfigEntry<bool> Enabled = null!;
        public ConfigEntry<decimal> ToMaxHealthPercentage = null!;
        public ConfigEntry<decimal> ByMaxHealthPercentage = null!;
        public ConfigEntry<int> ExactValue = null!;
        public ConfigEntry<HealthPackMode> HealthPackMode = null!;
        public ConfigEntry<string> Expression = null!;
        public ConfigEntry<WorkMode> WorkMode = null!;
        public ConfigEntry<HealMode> HealMode = null!;
        public ConfigEntry<PhaseMode> PhaseMode = null!;
        public int[] HealthPackValues = Array.Empty<int>();
    }

    public static class Settings
    {
        public static readonly HealSettings Start = new HealSettings();
        public static readonly HealSettings Revival = new HealSettings();
        public static ConfigEntry<HealRequirementMode> HealRequirementMode = null!;

        public static HealSettings For(HealTrigger trigger) => trigger == HealTrigger.Start ? Start : Revival;

        private const string PhaseWarning =
            "Attention! You must set the `PhaseMode` parameter to only one of `Level` or `Lobby`, not both. Otherwise, the healing will be done twice.";

        public static void Bind(ConfigFile config)
        {
            BindItem(config, HealTrigger.Start, "General", enabledDefault: true);
            HealRequirementMode = config.Bind("General", "HealRequirementMode", FullHeal.HealRequirementMode.None,
                "Sets requirements for performing heal.\n" +
                "`None` - no requirements.\n" +
                "`Survive` - the player must return to the truck at the end of the round and survive.");
            BindItem(config, HealTrigger.Revival, "Revival", enabledDefault: false);
        }

        private static void BindItem(ConfigFile config, HealTrigger trigger, string section, bool enabledDefault)
        {
            var s = For(trigger);

            s.Enabled = config.Bind(section, "Enabled", enabledDefault, "Indicates whether the heal is enabled.");

            s.ToMaxHealthPercentage = config.Bind(section, "ToMaxHealthPercentage", 100m,
                "The percentage of the player's maximum health to which he will be healed (Min: 0, Max: 100).\n" + PhaseWarning);
            ClampPercent(s.ToMaxHealthPercentage);

            s.ByMaxHealthPercentage = config.Bind(section, "ByMaxHealthPercentage", 0m,
                "The percentage of the player's maximum health that the player will be healed by (Min: 0, Max: 100).\n" + PhaseWarning + "\n" +
                "It has priority over the `ToMaxHealthPercentage` parameter.");
            ClampPercent(s.ByMaxHealthPercentage);

            s.ExactValue = config.Bind(section, "ExactValue", 0,
                "Sets the exact value to heal.\n" + PhaseWarning + "\n" +
                "If the value is `0`, then it is disabled.\n" +
                "It has priority over the `ToMaxHealthPercentage` and `ByMaxHealthPercentage` parameters.\n" +
                "For example, if you set the value to `15`, you will always receive 15 HP.");
            if (s.ExactValue.Value < 0) s.ExactValue.Value = 0;

            s.HealthPackMode = config.Bind(section, "HealthPackMode", FullHeal.HealthPackMode.None,
                "Sets healing value options to the same as in health packs, one of which will be applied randomly.\n" + PhaseWarning + "\n" +
                "If the value is `None`, then it is disabled.\n" +
                "It has priority over the `ToMaxHealthPercentage` and `ByMaxHealthPercentage` and `ExactValue` parameters.\n" +
                "For example, if you set the value to `Medium, Large`, you will receive a random healing of 50HP or 100HP.\n" +
                "`Small` - heal 25 HP.\n`Medium` - heal 50 HP.\n`Large` - heal 100 HP.");
            UpdateHealthPackValues(s);
            s.HealthPackMode.SettingChanged += (_, _) => UpdateHealthPackValues(s);

            s.Expression = config.Bind<string>(section, "Expression", "",
                "The expression used to calculate the healing value.\n" + PhaseWarning + "\n" +
                "If the value is null or empty or whitespace, then it is disabled.\n" +
                "It has priority over the `ToMaxHealthPercentage` and `ByMaxHealthPercentage` and `ExactValue` and `HealthPackMode` parameters.\n" +
                "The library used to parse expression is https://github.com/bijington/expressive. You can find detailed information in the documentation.\n" +
                "Variables:\n" +
                "`[PlayerName]` - player's name.\n" +
                "`[PlayerMaxHealth]` - player's maximum health.\n" +
                "`[PlayerHealth]` - player's health.\n" +
                "`[PlayerSurvived]` - whether the player survived the last round.\n" +
                "`[PlayerCount]` - number of players.\n" +
                "`[PlayerUpgradeHealth]`, `[PlayerUpgradeStamina]`, `[PlayerUpgradeExtraJump]`, `[PlayerUpgradeLaunch]`, " +
                "`[PlayerUpgradeMapPlayerCount]`, `[PlayerUpgradeSpeed]`, `[PlayerUpgradeStrength]`, `[PlayerUpgradeThrow]`, " +
                "`[PlayerUpgradeRange]` - player's upgrade levels.\n" +
                "Custom functions (Standard functions can be found here https://github.com/bijington/expressive/wiki/Functions):\n" +
                "`RandomList(x1, x2, x3...)` - selects a random number from a list. For example, `RandomList(10, 20)` will randomly return `10` or `20`\n" +
                "Examples (quotation marks should be removed):\n" +
                "`Max((([PlayerMaxHealth]*0.8)-[PlayerHealth]),0)` - heal to 80% of maximum health.\n" +
                "`[PlayerMaxHealth]*0.3` - heal by 30% of maximum health.\n" +
                "`40` - heal 40 HP.\n" +
                "`RandomList(25, 50, 100)` - heal 25, 50 or 100 HP at random.");
            HealExpressions.Update(trigger, s.Expression.Value);
            s.Expression.SettingChanged += (_, _) => HealExpressions.Update(trigger, s.Expression.Value);
            HealExpressions.SelfTest(trigger, s.Expression.Value);

            s.WorkMode = config.Bind(section, "WorkMode", FullHeal.WorkMode.Host,
                "Configures the work mode.\n" +
                "Warning: You are using `All` or `Client` values for this parameter at your own risk.\n" +
                "If another lobby players do not agree with this parameter, do not use it.\n" +
                "Also, the incomplete heal may not work correctly with this parameter, since the host or other clients may also perform the heal.\n" +
                "`Host` - works only on host.\n`Client` - works only on client.\n`All` - works on host and client.");

            s.HealMode = config.Bind(section, "HealMode", FullHeal.HealMode.All,
                "Configures the heal mode.\n`Self` - heal self.\n`Others` - heal others.\n`All` - heal all.");

            s.PhaseMode = config.Bind(section, "PhaseMode", FullHeal.PhaseMode.All,
                "Configures in which phases of the game health restoration will be triggered.\n" +
                "`Level` - the level.\n" +
                "`Shop` - in the shop. Attention! Healing in the shop does not affect the game.\n" +
                "`Lobby` - in the truck (not menu) before starting the next level.\n" +
                "`Arena` - in the arena.\n" +
                "`All` - in all phases.");
        }

        private static void ClampPercent(ConfigEntry<decimal> entry)
        {
            var clamped = Math.Min(100m, Math.Max(0m, entry.Value));
            if (clamped != entry.Value) entry.Value = clamped;
        }

        private static readonly (HealthPackMode Flag, int Hp)[] PackValues =
        {
            (FullHeal.HealthPackMode.Small, 25),
            (FullHeal.HealthPackMode.Medium, 50),
            (FullHeal.HealthPackMode.Large, 100),
        };

        private static void UpdateHealthPackValues(HealSettings s) =>
            s.HealthPackValues = PackValues.Where(p => s.HealthPackMode.Value.HasFlag(p.Flag)).Select(p => p.Hp).ToArray();
    }
}
