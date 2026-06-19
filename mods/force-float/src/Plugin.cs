using BepInEx;
using BepInEx.Logging;

namespace ForceFloat
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"ForceFloat v{PluginInfo.PLUGIN_VERSION} loaded.");
        }
    }
}
