using System.Reflection;
using Photon.Pun;
using UnityEngine;

namespace MuseumGambling;

internal static class Payout
{
    // ValuableObject.dollarValueOverride is internal to Assembly-CSharp; use reflection.
    private static readonly FieldInfo? s_dollarValueOverride =
        typeof(ValuableObject).GetField("dollarValueOverride",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    internal static void Spawn(Vector3 position, int value)
    {
        try
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
                return;

            GameObject prefab = AssetManager.instance.surplusValuableSmall;
            if (prefab == null)
            {
                Plugin.Log.LogError("[MuseumGambling] surplusValuableSmall prefab missing — cannot spawn payout.");
                return;
            }

            GameObject spawned = SemiFunc.IsMultiplayer()
                ? PhotonNetwork.InstantiateRoomObject("Valuables/" + prefab.name, position, Quaternion.identity, 0)
                : Object.Instantiate(prefab, position, Quaternion.identity);

            if (spawned == null)
            {
                Plugin.Log.LogError("[MuseumGambling] Spawn returned null.");
                return;
            }

            var valuable = spawned.GetComponent<ValuableObject>();
            if (valuable == null)
            {
                Plugin.Log.LogError("[MuseumGambling] Spawned object has no ValuableObject component.");
                return;
            }

            if (s_dollarValueOverride == null)
            {
                Plugin.Log.LogError("[MuseumGambling] dollarValueOverride field not found via reflection — value not stamped.");
            }
            else
            {
                s_dollarValueOverride.SetValue(valuable, value);
            }

            Plugin.Log.LogInfo($"[MuseumGambling] Spawned money bag worth {value} at {position}.");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"Payout.Spawn failed: {ex}");
        }
    }
}
