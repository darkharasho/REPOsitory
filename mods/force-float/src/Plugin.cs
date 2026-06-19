using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ForceFloat
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;

        internal static ConfigEntry<bool> Enabled = null!;
        internal static ConfigEntry<bool> Flashlight = null!;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Master on/off switch for permanent floating. Best set on the host; the host drives " +
                "the float for every player. (All players should run the mod.)");
            Flashlight = Config.Bind("General", "Flashlight", true,
                "Keep your flashlight on while floating. The game normally turns it off during tumble; " +
                "since it's a dark game, this keeps it lit.");

            new Harmony(PluginInfo.PLUGIN_GUID).PatchAll();
            gameObject.AddComponent<FloatDriver>();
            Log.LogInfo($"ForceFloat v{PluginInfo.PLUGIN_VERSION} loaded.");
        }
    }
}
