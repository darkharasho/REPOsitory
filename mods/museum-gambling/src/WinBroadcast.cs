using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace MuseumGambling;

internal static class WinBroadcast
{
    // Photon reserves event codes 200+ for engine use; user codes are 0..199.
    internal const byte EventCode = 199;

    // Two separate one-shot stores so the damage suppression (consumed at
    // HurtCollider.PlayerHurt during State.Closed) and the visual effects
    // (consumed at State.Opening, when the mouth re-opens) don't race.
    private static readonly Dictionary<int, bool> _pendingDamage = new();
    private static readonly HashSet<int> _pendingEffects = new();

    private static bool _registered;

    internal static void Register()
    {
        if (_registered) return;
        PhotonNetwork.NetworkingClient.EventReceived += OnEventReceived;
        _registered = true;
    }

    internal static void Send(int viewId, bool win)
    {
        Apply(viewId, win);

        if (!PhotonNetwork.InRoom) return;

        var payload = new object[] { viewId, win };
        var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(EventCode, payload, options, SendOptions.SendReliable);
    }

    internal static bool ConsumePendingResult(int viewId)
    {
        if (_pendingDamage.TryGetValue(viewId, out var win))
        {
            _pendingDamage.Remove(viewId);
            return win;
        }
        return false;
    }

    internal static bool ConsumePendingEffect(int viewId) => _pendingEffects.Remove(viewId);

    private static void OnEventReceived(EventData ev)
    {
        if (ev.Code != EventCode) return;
        if (ev.CustomData is not object[] data || data.Length < 2) return;

        int viewId = (int)data[0];
        bool win = (bool)data[1];
        Apply(viewId, win);
    }

    private static void Apply(int viewId, bool win)
    {
        _pendingDamage[viewId] = win;
        if (win) _pendingEffects.Add(viewId);
    }
}
