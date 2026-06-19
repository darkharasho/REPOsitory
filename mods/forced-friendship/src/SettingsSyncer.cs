using Photon.Pun;
using UnityEngine;

namespace ForcedFriendship
{
    /// <summary>
    /// Syncs the gameplay rule from the host to every client via Photon room custom properties,
    /// so each client's beams reflect the same AnchorMode / SafeDistance the host uses to deal
    /// damage. Display preferences (beam enabled/show-all/warn/width) are NOT synced — they stay
    /// local to each player.
    ///
    /// Polling instead of MonoBehaviourPunCallbacks: the room-property/join callbacks proved
    /// unreliable when attached to the BepInEx plugin GameObject (same finding as mini-eepo),
    /// so we poll the cheap room-state booleans every frame and re-pull once a second.
    /// </summary>
    internal class SettingsSyncer : MonoBehaviour
    {
        private const string K_ENABLED = "FF_EN";
        private const string K_MODE    = "FF_MODE"; // AnchorMode as int
        private const string K_SAFE    = "FF_SAFE";
        private const string K_BAND    = "FF_BAND";
        private const string K_DMG     = "FF_DMG";
        private const string K_TICK    = "FF_TICK";
        private const string K_HEIGHT  = "FF_HGT";
        private const string K_GRACE   = "FF_GRC";

        // Static singleton — FindObjectOfType doesn't reliably find us on the BepInEx plugin
        // GameObject (it lives outside the normal scene hierarchy).
        internal static SettingsSyncer? Instance;

        private bool _wasInRoom;
        private float _pullPollDelay;

        private void Awake() => Instance = this;
        private void Start() => Plugin.Log.LogInfo("[Sync] SettingsSyncer ready (polling mode)");

        private void Update()
        {
            bool inRoom = PhotonNetwork.InRoom;
            bool master = inRoom && PhotonNetwork.IsMasterClient;

            if (master)
            {
                // Host (incl. singleplayer's offline room): keep Active* tracking live local config
                // every frame so config changes update instantly, and broadcast to clients only
                // when a value actually changed (PushHostSettings dedups + refreshes Active*).
                PushHostSettings();
            }
            else if (inRoom)
            {
                // Non-host client: pull the host rule from room properties — immediately on join,
                // then poll once a second in case the host pushed after we joined.
                if (!_wasInRoom)
                {
                    PullHostSettings();
                    _pullPollDelay = 1f;
                }
                else
                {
                    _pullPollDelay -= Time.unscaledDeltaTime;
                    if (_pullPollDelay <= 0f) { _pullPollDelay = 1f; PullHostSettings(); }
                }
            }
            else
            {
                // Not in a room (main menu): mirror local config so it's ready before we join.
                if (_wasInRoom) Plugin.Log.LogInfo("[Sync] Left room");
                Plugin.ResetToLocalConfig();
            }

            _wasInRoom = inRoom;
        }

        private void PullHostSettings()
        {
            var props = PhotonNetwork.CurrentRoom?.CustomProperties;
            if (props == null) return;
            bool changed = false;
            if (props.ContainsKey(K_ENABLED)) { var v = (bool)props[K_ENABLED];           if (Plugin.ActiveEnabled       != v) { Plugin.ActiveEnabled       = v; changed = true; } }
            if (props.ContainsKey(K_MODE))    { var v = (AnchorMode)(int)props[K_MODE];    if (Plugin.ActiveMode          != v) { Plugin.ActiveMode          = v; changed = true; } }
            if (props.ContainsKey(K_SAFE))    { var v = (int)props[K_SAFE];                if (Plugin.ActiveSafeDistance  != v) { Plugin.ActiveSafeDistance  = v; changed = true; } }
            if (props.ContainsKey(K_BAND))    { var v = (int)props[K_BAND];                if (Plugin.ActiveBandWidth     != v) { Plugin.ActiveBandWidth     = v; changed = true; } }
            if (props.ContainsKey(K_DMG))     { var v = (int)props[K_DMG];                 if (Plugin.ActiveDamagePerBand != v) { Plugin.ActiveDamagePerBand = v; changed = true; } }
            if (props.ContainsKey(K_TICK))    { var v = (int)props[K_TICK];                if (Plugin.ActiveTickInterval  != v) { Plugin.ActiveTickInterval  = v; changed = true; } }
            if (props.ContainsKey(K_HEIGHT))  { var v = (bool)props[K_HEIGHT];             if (Plugin.ActiveIncludeHeight != v) { Plugin.ActiveIncludeHeight = v; changed = true; } }
            if (props.ContainsKey(K_GRACE))   { var v = (int)props[K_GRACE];               if (Plugin.ActiveGracePeriod   != v) { Plugin.ActiveGracePeriod   = v; changed = true; } }
            if (changed)
                Plugin.Log.LogInfo($"[Sync] Pulled host rule — enabled={Plugin.ActiveEnabled} mode={Plugin.ActiveMode} safe={Plugin.ActiveSafeDistance} band={Plugin.ActiveBandWidth} dmg={Plugin.ActiveDamagePerBand} tick={Plugin.ActiveTickInterval}");
        }

        // Cache last-pushed values so we don't broadcast a room update when SettingChanged fires
        // with identical values (some config systems re-emit on autosave).
        private bool? _lastEnabled;
        private int _lastMode = int.MinValue;
        private int _lastSafe = int.MinValue, _lastBand = int.MinValue, _lastTick = int.MinValue;
        private int _lastDmg = int.MinValue;
        private bool? _lastHeight;
        private int _lastGrace = int.MinValue;

        private void PushHostSettings()
        {
            if (PhotonNetwork.CurrentRoom == null) return;
            bool en = Plugin.Enabled.Value;
            int mode = (int)Plugin.Mode.Value;
            int safe = Plugin.SafeDistance.Value, band = Plugin.BandWidth.Value, tick = Plugin.TickInterval.Value;
            int dmg = Plugin.DamagePerBand.Value;
            bool height = Plugin.IncludeHeight.Value;
            int grace = Plugin.GracePeriod.Value;

            if (en == _lastEnabled && mode == _lastMode && safe == _lastSafe &&
                band == _lastBand && dmg == _lastDmg && tick == _lastTick &&
                height == _lastHeight && grace == _lastGrace)
            {
                Plugin.ResetToLocalConfig(); // refresh Active mirrors, skip the broadcast
                return;
            }
            _lastEnabled = en; _lastMode = mode; _lastSafe = safe;
            _lastBand = band; _lastDmg = dmg; _lastTick = tick; _lastHeight = height; _lastGrace = grace;

            var props = new ExitGames.Client.Photon.Hashtable
            {
                [K_ENABLED] = en, [K_MODE] = mode, [K_SAFE] = safe,
                [K_BAND] = band, [K_DMG] = dmg, [K_TICK] = tick, [K_HEIGHT] = height, [K_GRACE] = grace,
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            Plugin.ResetToLocalConfig();
            Plugin.Log.LogInfo($"[Sync] Host pushed rule — enabled={en} mode={(AnchorMode)mode} safe={safe} band={band} dmg={dmg} tick={tick} height={height} grace={grace}");
        }
    }
}
