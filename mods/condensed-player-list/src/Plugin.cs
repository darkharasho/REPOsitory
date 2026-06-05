using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace CondensedPlayerList
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;

        private void Awake()
        {
            Log = Logger;
            var harmony = new Harmony("darkharasho.CondensedPlayerList");
            harmony.PatchAll();
            Log.LogInfo($"CondensedPlayerList v{PluginInfo.PLUGIN_VERSION} loaded.");
        }
    }
}
