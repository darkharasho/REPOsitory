using System.Collections.Generic;
using UnityEngine;

namespace MuseumGambling;

// Per-MuseumPropMoneyHead memory of where the clicking player was standing
// when they were grabbed. Used so the payout spawns where the player was
// (and the head's mouth points), instead of derived from the head's
// transform.forward which on this mesh points sideways.
internal static class GrabSpot
{
    private static readonly Dictionary<int, Vector3> _spots = new();

    internal static void Record(int viewId, Vector3 position) => _spots[viewId] = position;

    internal static Vector3? Consume(int viewId)
    {
        if (_spots.TryGetValue(viewId, out var p))
        {
            _spots.Remove(viewId);
            return p;
        }
        return null;
    }
}
