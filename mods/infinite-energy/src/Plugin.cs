using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace InfiniteEnergy
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;
        internal static ConfigEntry<bool> Enabled = null!;

        private static readonly AccessTools.FieldRef<RoundDirector, bool> DebugInfiniteBattery =
            AccessTools.FieldRefAccess<RoundDirector, bool>("debugInfiniteBattery");

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "Keep every item battery (guns, melee durability, drones, tools) full. The host's setting is what counts.");
            Enabled.SettingChanged += (_, _) => Apply();

            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            Log.LogInfo($"InfiniteEnergy v{PluginInfo.PLUGIN_VERSION} loaded.");
        }

        // The game ships a developer flag that refills every ItemBattery each frame and
        // stops the revolver from spending chambers; RoundDirector is recreated per level.
        internal static void Apply()
        {
            if (RoundDirector.instance != null)
                DebugInfiniteBattery(RoundDirector.instance) = Enabled.Value;
        }
    }

    [HarmonyPatch(typeof(RoundDirector), "Awake")]
    internal static class RoundDirectorAwakePatch
    {
        private static void Postfix() => Plugin.Apply();
    }

    // Without this, each gun shot drops a bar (drain sound + RPC) and the next frame's
    // refill raises it again (charge sound), so every shot plays two sounds.
    [HarmonyPatch(typeof(ItemBattery), nameof(ItemBattery.RemoveFullBar))]
    internal static class RemoveFullBarPatch
    {
        private static bool Prefix() => !Plugin.Enabled.Value;
    }
}
