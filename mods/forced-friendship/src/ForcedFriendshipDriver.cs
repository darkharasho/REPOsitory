using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace ForcedFriendship
{
    /// <summary>
    /// Runs only on the Photon host during level gameplay. Every TickInterval seconds it
    /// snapshots all players, asks DamageCalculator who should bleed, and applies the
    /// damage via PlayerHealth.HurtOther (which RPCs to each owning client).
    /// </summary>
    internal class ForcedFriendshipDriver : MonoBehaviour
    {
        private float _accum;
        private readonly List<PlayerState> _states = new List<PlayerState>();
        private readonly List<PlayerAvatar> _avatars = new List<PlayerAvatar>();
        private readonly List<PhysGrabCart> _carts = new List<PhysGrabCart>();
        private readonly List<Vec3> _cartPositions = new List<Vec3>();

        private void Update()
        {
            // Damage uses the Active* rule (host config; on the host Active* == local config).
            if (!Plugin.ActiveEnabled) return;

            // Only the host computes and applies damage. (IsInGameplay also requires a room.)
            if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient) return;
            if (!Plugin.IsInGameplay()) return;

            _accum += Time.deltaTime;
            if (_accum < Plugin.ActiveTickInterval) return;
            _accum = 0f;

            var list = GameDirector.instance?.PlayerList;
            if (list == null) return;

            _states.Clear();
            _avatars.Clear();
            foreach (var pa in list)
            {
                if (pa == null) continue;
                Vector3 pos = pa.transform.position;
                bool alive = PlayerLiveness.IsAlive(pa);
                bool inTruck = PlayerLiveness.IsInTruck(pa);
                _states.Add(new PlayerState(pos.x, pos.y, pos.z, alive, inTruck));
                _avatars.Add(pa);
            }

            var settings = new DamageSettings(
                enabled: true,
                safeDistance: Plugin.ActiveSafeDistance,
                bandWidth: Plugin.ActiveBandWidth,
                damagePerBand: Plugin.ActiveDamagePerBand);

            _cartPositions.Clear();
            if (Plugin.ActiveMode == AnchorMode.Cart)
            {
                CartLocator.FindMainCarts(_carts);
                foreach (PhysGrabCart c in _carts)
                {
                    if (c == null) continue;
                    Vector3 cp = c.transform.position;
                    _cartPositions.Add(new Vec3(cp.x, cp.y, cp.z));
                }
            }

            AnchorResult[] anchors =
                DamageCalculator.ResolveAnchors(_states, Plugin.ActiveMode, _cartPositions, Plugin.ActiveIncludeHeight);
            int[] damage = DamageCalculator.EvaluateDamage(anchors, settings);
            for (int i = 0; i < damage.Length; i++)
            {
                if (damage[i] <= 0) continue;
                PlayerAvatar pa = _avatars[i];
                if (pa.playerHealth == null) continue;
                // Position-independent DoT: pass Vector3.zero so HurtOtherRPC's <2f proximity
                // guard never drops a tick for a fast-moving remote player whose host-side
                // position lags. (The game uses Vector3.zero for non-positional damage too.)
                pa.playerHealth.HurtOther(damage[i], Vector3.zero, savingGrace: false);
            }
        }
    }
}
