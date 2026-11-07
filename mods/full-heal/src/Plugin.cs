using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace FullHeal
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    // Running both would heal twice and bring back the original's freeze.
    [BepInIncompatibility("FullHealth")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;
        internal static Plugin Instance = null!;

        private void Awake()
        {
            Log = Logger;
            Instance = this;
            Settings.Bind(Config);
            new Harmony(PluginInfo.PLUGIN_GUID).PatchAll();
            Log.LogInfo($"FullHeal v{PluginInfo.PLUGIN_VERSION} loaded.");
        }
    }
}
