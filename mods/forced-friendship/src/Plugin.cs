using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;

namespace ForcedFriendship
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;

        internal static ConfigEntry<bool> Enabled = null!;
        internal static ConfigEntry<float> SafeDistance = null!;
        internal static ConfigEntry<float> BandWidth = null!;
        internal static ConfigEntry<int> DamagePerBand = null!;
        internal static ConfigEntry<float> TickInterval = null!;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Master on/off switch for Forced Friendship.");
            SafeDistance = Config.Bind("General", "SafeDistance", 15f,
                new ConfigDescription(
                    "Units within which the nearest living player keeps you safe.",
                    new AcceptableValueRange<float>(0.1f, 1000f)));
            BandWidth = Config.Bind("General", "BandWidth", 8f,
                new ConfigDescription(
                    "Units per additional damage band beyond the safe radius.",
                    new AcceptableValueRange<float>(0.1f, 1000f)));
            DamagePerBand = Config.Bind("General", "DamagePerBand", 5,
                new ConfigDescription(
                    "HP per tick, multiplied by the band number.",
                    new AcceptableValueRange<int>(1, 100)));
            TickInterval = Config.Bind("General", "TickInterval", 2f,
                new ConfigDescription(
                    "Seconds between damage evaluations.",
                    new AcceptableValueRange<float>(0.1f, 60f)));

            var harmony = new Harmony("darkharasho.ForcedFriendship");
            harmony.PatchAll();
            Log.LogInfo($"ForcedFriendship v{PluginInfo.PLUGIN_VERSION} loaded.");
            gameObject.AddComponent<ForcedFriendshipDriver>();
        }

        /// <summary>
        /// True only when the local client is in active level gameplay (in a Photon room,
        /// on a non-lobby, non-shop level). Mirrors the proven check from mini-eepo; the
        /// shop is always excluded for Forced Friendship since it is a natural cluster zone.
        /// </summary>
        internal static bool IsInGameplay()
        {
            // Require being in a Photon room first — at startup RunManager.levelCurrent has a
            // non-lobby default that would otherwise wrongly read as "in gameplay".
            if (!PhotonNetwork.InRoom) return false;
            var rm = RunManager.instance;
            if (rm == null) return false;
            var current = rm.levelCurrent;
            if (current == null) return false;
            if (current == rm.levelLobby || current == rm.levelLobbyMenu) return false;
            if (SemiFunc.IsLevelShop(current)) return false;
            return true;
        }
    }
}
