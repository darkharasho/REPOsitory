using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace UnchainedIndestructibles
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;

        private void Awake()
        {
            Log = Logger;
            var harmony = new Harmony("darkharasho.UnchainedIndestructibles");
            harmony.PatchAll();
            Log.LogInfo($"UnchainedIndestructibles v{PluginInfo.PLUGIN_VERSION} loaded.");
        }
    }
}
