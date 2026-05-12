using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace MuseumGambling;

internal static class WinBroadcast
{
    // Photon reserves event codes 200+ for engine use; user codes are 0..199.
    internal const byte EventCode = 199;

    private static readonly Dictionary<int, bool> _pending = new();
    private static bool _registered;

    internal static void Register()
    {
        if (_registered) return;
        PhotonNetwork.NetworkingClient.EventReceived += OnEventReceived;
        _registered = true;
    }

    internal static void Send(int viewId, bool win)
    {
        // Always apply locally first so singleplayer (no room) and master itself see the result.
        _pending[viewId] = win;

        if (!PhotonNetwork.InRoom) return;

        var payload = new object[] { viewId, win };
        var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(EventCode, payload, options, SendOptions.SendReliable);
    }

    internal static bool ConsumePendingResult(int viewId)
    {
        if (_pending.TryGetValue(viewId, out var win))
        {
            _pending.Remove(viewId);
            return win;
        }
        return false;
    }

    private static void OnEventReceived(EventData ev)
    {
        if (ev.Code != EventCode) return;
        if (ev.CustomData is not object[] data || data.Length < 2) return;

        int viewId = (int)data[0];
        bool win = (bool)data[1];
        _pending[viewId] = win;
    }
}
