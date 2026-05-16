using BepInEx.Configuration;

namespace UnchainedIndestructibles
{
    internal static class Config
    {
        internal static ConfigEntry<int> MaxAmount = null!;
        internal static ConfigEntry<int> MaxAmountInShop = null!;

        internal static void Bind(ConfigFile config)
        {
            var range = new AcceptableValueRange<int>(1, 99);

            MaxAmount = config.Bind(
                "IndestructibleDrone", "MaxAmount", 10,
                new ConfigDescription(
                    "Maximum number of Indestructible Drones the host can carry into a run from purchased inventory. " +
                    "Vanilla is 1.",
                    range));

            MaxAmountInShop = config.Bind(
                "IndestructibleDrone", "MaxAmountInShop", 3,
                new ConfigDescription(
                    "Maximum number of Indestructible Drones the host's shop can spawn per refresh. Vanilla is 1.",
                    range));
        }
    }
}
