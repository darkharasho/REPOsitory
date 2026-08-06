using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace FaerieFlight
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;

        internal static ConfigEntry<bool> Enabled = null!;
        internal static ConfigEntry<bool> Flashlight = null!;
        internal static ConfigEntry<bool> RestoreGrabUpgrades = null!;
        internal static ConfigEntry<float> StrengthSoftening = null!;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Master on/off switch for permanent floating. Best set on the host; the host drives " +
                "the float for every player. (All players should run the mod.)");
            Flashlight = Config.Bind("General", "Flashlight", true,
                "Keep your flashlight on while floating. The game normally turns it off during tumble; " +
                "since it's a dark game, this keeps it lit.");
            RestoreGrabUpgrades = Config.Bind("General", "RestoreGrabUpgrades", true,
                "The game ignores Strength upgrades while a player is tumbling, so permanent float " +
                "would leave them permanently dead. Restores them (softened) while floating. " +
                "Host-side setting; grabbing enemies keeps vanilla behavior either way.");
            StrengthSoftening = Config.Bind("General", "StrengthSoftening", 0.65f,
                new ConfigDescription(
                    "How much to soften restored Strength upgrades while floating: 0 = full upgrade " +
                    "strength, 1 = square-root curve. Floating grabbers take the grab's recoil, so " +
                    "full strength can fling you.",
                    new AcceptableValueRange<float>(0f, 1f)));

            new Harmony(PluginInfo.PLUGIN_GUID).PatchAll();
            gameObject.AddComponent<FloatDriver>();
            Log.LogInfo($"FaerieFlight v{PluginInfo.PLUGIN_VERSION} loaded.");
        }
    }
}
