using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace ForceFloat
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;

        internal static ConfigEntry<bool> EnableDrift = null!;
        internal static ConfigEntry<bool> Wings = null!;

        private void Awake()
        {
            Log = Logger;

            EnableDrift = Config.Bind("General", "EnableDrift", true,
                "Steer through the air with your movement keys (look where you want to go). " +
                "Off = pure ragdoll drift with no steering.");
            Wings = Config.Bind("General", "Wings", true,
                "Show the tumble wing visuals while floating.");

            gameObject.AddComponent<FloatDriver>();
            Log.LogInfo($"ForceFloat v{PluginInfo.PLUGIN_VERSION} loaded.");
        }
    }
}
