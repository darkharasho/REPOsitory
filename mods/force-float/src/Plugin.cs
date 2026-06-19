using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace ForceFloat
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;

        internal static ConfigEntry<bool> Enabled = null!;

        private void Awake()
        {
            Log = Logger;

            // Only the host spawns the float effect (tumble physics is master-authoritative),
            // so the HOST's Enabled value governs the whole lobby — clients float without the mod.
            Enabled = Config.Bind("General", "Enabled", true,
                "Master on/off switch for permanent floating. Only the host's value matters: the " +
                "host drives the float for every player.");

            gameObject.AddComponent<FloatDriver>();
            Log.LogInfo($"ForceFloat v{PluginInfo.PLUGIN_VERSION} loaded.");
        }
    }
}
