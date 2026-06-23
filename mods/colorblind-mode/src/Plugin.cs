using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ColorblindMode
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;
        internal static ConfigEntry<ColorblindType> Type = null!;
        internal static ConfigEntry<float> Intensity = null!;

        private void Awake()
        {
            Log = Logger;

            Type = Config.Bind("General", "Type", ColorblindType.Off,
                "Colorblindness type to correct for. Off restores the game's default colors.");
            Intensity = Config.Bind("General", "Intensity", 1.0f,
                new ConfigDescription(
                    "Strength of the correction. 0 = no change, 1 = full correction.",
                    new AcceptableValueRange<float>(0f, 1f)));

            new Harmony(PluginInfo.PLUGIN_GUID).PatchAll();
            // gameObject.AddComponent<ColorblindController>();
            Log.LogInfo($"ColorblindMode v{PluginInfo.PLUGIN_VERSION} loaded.");
        }
    }
}
