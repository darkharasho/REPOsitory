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
        internal static ConfigEntry<int> SafeDistance = null!;
        internal static ConfigEntry<int> BandWidth = null!;
        internal static ConfigEntry<int> DamagePerBand = null!;
        internal static ConfigEntry<int> TickInterval = null!;
        internal static ConfigEntry<bool> IncludeHeight = null!;
        internal static ConfigEntry<AnchorMode> Mode = null!;
        internal static ConfigEntry<bool> BeamsEnabled = null!;
        internal static ConfigEntry<bool> BeamsShowAll = null!;
        internal static ConfigEntry<bool> BeamsAlwaysShow = null!;
        internal static ConfigEntry<int> BeamsWarnPercent = null!;
        internal static ConfigEntry<int> BeamsWidth = null!;
        internal static ConfigEntry<int> BeamsOpacity = null!;

        // Active rule values — the gameplay rule comes from the HOST so every client's beams
        // match the host-authoritative damage. They start from local config and are overwritten
        // by SettingsSyncer when in a room as a non-host. Beam display prefs (BeamsEnabled,
        // BeamsShowAll, BeamsWarnPercent, BeamsWidth) are intentionally NOT synced — each player
        // controls their own visuals.
        internal static bool ActiveEnabled;
        internal static AnchorMode ActiveMode;
        internal static int ActiveSafeDistance;
        internal static int ActiveBandWidth;
        internal static int ActiveDamagePerBand;
        internal static int ActiveTickInterval;
        internal static bool ActiveIncludeHeight;

        /// <summary>Beam thickness in world units (the int Width knob is a 1/100 scale).</summary>
        internal static float BeamWidthWorld => BeamsWidth.Value * 0.01f;
        /// <summary>Warn-zone fraction of SafeDistance (the int WarnPercent knob is 0–100).</summary>
        internal static float WarnFraction => BeamsWarnPercent.Value / 100f;
        /// <summary>Beam opacity 0–1 (the int Opacity knob is 0–100).</summary>
        internal static float BeamOpacity => BeamsOpacity.Value / 100f;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Master on/off switch for Forced Friendship.");
            SafeDistance = Config.Bind("General", "SafeDistance", 20,
                new ConfigDescription(
                    "Units within which your anchor (nearest player or cart) keeps you safe.",
                    new AcceptableValueRange<int>(1, 100)));
            BandWidth = Config.Bind("General", "BandWidth", 8,
                new ConfigDescription(
                    "Units per additional damage band beyond the safe radius.",
                    new AcceptableValueRange<int>(1, 100)));
            DamagePerBand = Config.Bind("General", "DamagePerBand", 5,
                new ConfigDescription(
                    "HP per tick, multiplied by the band number.",
                    new AcceptableValueRange<int>(1, 100)));
            TickInterval = Config.Bind("General", "TickInterval", 2,
                new ConfigDescription(
                    "Seconds between damage evaluations.",
                    new AcceptableValueRange<int>(1, 30)));
            IncludeHeight = Config.Bind("General", "IncludeHeight", false,
                "If true, vertical distance counts toward the safe radius. Default false so being " +
                "on a different floor of the same tall room doesn't trigger damage.");

            Mode = Config.Bind("General", "AnchorMode", AnchorMode.Buddy,
                "Buddy = stay near the nearest living player (default). " +
                "Cart = stay near the nearest hauling cart instead.");

            BeamsEnabled = Config.Bind("Beams", "Enabled", true,
                "Draw a tether beam from each player to their anchor.");
            BeamsShowAll = Config.Bind("Beams", "ShowAllPlayers", true,
                "Show beams for every living player. If false, only your own beam is drawn.");
            BeamsAlwaysShow = Config.Bind("Beams", "AlwaysShow", true,
                "Always draw the tether (color conveys safety). If false, the beam hides while " +
                "you're safe and only appears in the warn/danger zone.");
            BeamsWarnPercent = Config.Bind("Beams", "WarnPercent", 25,
                new ConfigDescription(
                    "Percent of SafeDistance, at the outer edge, where the beam turns yellow " +
                    "before it turns red. 0 disables the yellow zone.",
                    new AcceptableValueRange<int>(0, 100)));
            BeamsWidth = Config.Bind("Beams", "Width", 2,
                new ConfigDescription(
                    "Tether thickness (1 = thinnest). The default approximates the game's grab beam.",
                    new AcceptableValueRange<int>(1, 20)));
            BeamsOpacity = Config.Bind("Beams", "Opacity", 40,
                new ConfigDescription(
                    "Beam opacity percent (1 = faint, 100 = solid). Lower is more translucent.",
                    new AcceptableValueRange<int>(1, 100)));

            ResetToLocalConfig();

            var harmony = new Harmony("darkharasho.ForcedFriendship");
            harmony.PatchAll();
            Log.LogInfo($"ForcedFriendship v{PluginInfo.PLUGIN_VERSION} loaded.");
            gameObject.AddComponent<ForcedFriendshipDriver>();
            gameObject.AddComponent<BeamRenderer>();
            gameObject.AddComponent<SettingsSyncer>();
        }

        /// <summary>Copy the local config into the Active* mirrors (host values, or singleplayer).</summary>
        internal static void ResetToLocalConfig()
        {
            ActiveEnabled       = Enabled.Value;
            ActiveMode          = Mode.Value;
            ActiveSafeDistance  = SafeDistance.Value;
            ActiveBandWidth     = BandWidth.Value;
            ActiveDamagePerBand = DamagePerBand.Value;
            ActiveTickInterval  = TickInterval.Value;
            ActiveIncludeHeight = IncludeHeight.Value;
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
