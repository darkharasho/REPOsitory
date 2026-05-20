using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace Overstaffed
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal const int MaxAllowedPlayers = 20;
        internal const int MinAllowedPlayers = 1;
        internal const int DefaultPlayers = 10;

        internal static ManualLogSource Log = null!;
        internal static ConfigEntry<int> ConfigMaxPlayers = null!;

        private void Awake()
        {
            Log = Logger;

            ConfigMaxPlayers = Config.Bind(
                "General",
                "MaxPlayers",
                DefaultPlayers,
                new ConfigDescription(
                    "The maximum number of players allowed in a server.",
                    new AcceptableValueRange<int>(MinAllowedPlayers, MaxAllowedPlayers)));

            Log.LogInfo($"Overstaffed v{PluginInfo.PLUGIN_VERSION} loading — MaxPlayers={ConfigMaxPlayers.Value}");

            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            TryPatch(harmony, typeof(Patches.GameManagerAwakePatch));
            TryPatch(harmony, typeof(Patches.GameManagerSetMaxPlayersPatch));
            TryPatch(harmony, typeof(Patches.NetworkConnectOnConnectedToMasterPatch));

            Log.LogInfo("Overstaffed loaded.");
        }

        private static void TryPatch(Harmony harmony, System.Type patchType)
        {
            try
            {
                harmony.CreateClassProcessor(patchType).Patch();
            }
            catch (System.Exception e)
            {
                Log.LogWarning($"[Overstaffed] Failed to apply {patchType.Name}: {e.GetType().Name}: {e.Message}");
            }
        }
    }
}
