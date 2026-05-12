using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace MuseumGambling;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log = null!;

    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<int> WinChancePercent = null!;
    internal static ConfigEntry<int> PayoutValue = null!;

    private void Awake()
    {
        Log = Logger;

        Enabled = Config.Bind(
            "General",
            "Enabled",
            true,
            "Global kill-switch. When false, the Museum Head behaves vanilla (no gambling).");

        WinChancePercent = Config.Bind(
            "General",
            "WinChancePercent",
            5,
            new ConfigDescription(
                "Percent chance (whole number, 0–100) that a click pays out instead of dealing damage.",
                new AcceptableValueRange<int>(0, 100)));

        PayoutValue = Config.Bind(
            "General",
            "PayoutValue",
            50000,
            new ConfigDescription(
                "Dollar value stamped on the spawned money-bag valuable on a win.",
                new AcceptableValueRange<int>(0, 1_000_000)));

        WinBroadcast.Register();

        var harmony = new Harmony("darkharasho.MuseumGambling");
        harmony.PatchAll();
        Log.LogInfo($"MuseumGambling v{PluginInfo.PLUGIN_VERSION} loaded.");
    }
}
