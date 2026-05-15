using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ChillShopKeeper;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log = null!;

    internal static ConfigEntry<bool> DisableGlobally = null!;
    internal static ConfigEntry<bool> HostOnlyOnOffSwitch = null!;
    internal static PlayerRegistry Players = null!;

    private void Awake()
    {
        Log = Logger;

        DisableGlobally = Config.Bind(
            "General",
            "DisableGlobally",
            false,
            "When true, the ShopKeeper ignores ruckus from everyone. Overrides per-player toggles.");

        HostOnlyOnOffSwitch = Config.Bind(
            "General",
            "HostOnlyOnOffSwitch",
            false,
            "When true, only the host (Photon master client) can press the ShopKeeper's on/off switch. Non-host players' presses are blocked locally before any animation, cooldown, or RPC. Default false preserves vanilla button feel.");

        Players = new PlayerRegistry(Config);

        var harmony = new Harmony("darkharasho.ChillShopKeeper");
        harmony.PatchAll();
        Log.LogInfo($"ChillShopKeeper v{PluginInfo.PLUGIN_VERSION} loaded.");
    }
}
