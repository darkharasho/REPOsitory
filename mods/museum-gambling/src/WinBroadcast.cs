using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace MuseumGambling;

internal static class WinBroadcast
{
    // Photon reserves event codes 200+ for engine use; user codes are 0..199.
    // REPO itself uses 199 ("you were kicked"), 123 (kick), and 124 (ban) —
    // anything in that set will boot every client to the lobby on receipt.
    internal const byte EventCode = 173;

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

    // Peek (don't remove): the head's hurt collider stays active for the
    // full 3-second Closed window and can re-trigger PlayerHurt every
    // playerDamageCooldown (~0.25s). Consuming on first hit would leave
    // subsequent hits unsuppressed and kill the winner anyway. The flag is
    // cleared at the State.Opening transition instead.
    internal static bool PeekPendingResult(int viewId)
    {
        return _pendingDamage.TryGetValue(viewId, out var win) && win;
    }

    internal static void ClearPendingResult(int viewId) => _pendingDamage.Remove(viewId);

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
