using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ChillShopKeeper;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static Plugin Instance = null!;
    internal static ManualLogSource Log = null!;

    internal static ConfigEntry<bool> DisableGlobally = null!;
    internal static PlayerRegistry Players = null!;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        DisableGlobally = Config.Bind(
            "General",
            "DisableGlobally",
            false,
            "When true, the ShopKeeper ignores ruckus from everyone. Overrides per-player toggles.");

        Players = new PlayerRegistry(Config);

        var harmony = new Harmony("darkharasho.ChillShopKeeper");
        harmony.PatchAll();
        Log.LogInfo($"ChillShopKeeper v{PluginInfo.PLUGIN_VERSION} loaded.");
    }
}
