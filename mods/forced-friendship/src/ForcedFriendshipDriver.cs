using System.Collections.Generic;
using HarmonyLib;
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
        // PlayerAvatar.deadSet / .isDisabled are 'internal' in Assembly-CSharp and this mod
        // builds against the un-publicized game DLL, so they are read via cached field-ref delegates.
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> DeadSetRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("deadSet");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> IsDisabledRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isDisabled");

        private float _accum;
        private readonly List<PlayerState> _states = new List<PlayerState>();
        private readonly List<PlayerAvatar> _avatars = new List<PlayerAvatar>();

        private void Update()
        {
            if (!Plugin.Enabled.Value) return;

            // Only the host computes and applies damage. (IsInGameplay also requires a room.)
            if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient) return;
            if (!Plugin.IsInGameplay()) return;

            _accum += Time.deltaTime;
            if (_accum < Plugin.TickInterval.Value) return;
            _accum = 0f;

            var list = GameDirector.instance?.PlayerList;
            if (list == null) return;

            _states.Clear();
            _avatars.Clear();
            foreach (var pa in list)
            {
                if (pa == null) continue;
                Vector3 pos = pa.transform.position;
                bool alive = !DeadSetRef(pa) && !IsDisabledRef(pa);
                _states.Add(new PlayerState(pos.x, pos.y, pos.z, alive));
                _avatars.Add(pa);
            }

            var settings = new DamageSettings(
                enabled: true,
                safeDistance: Plugin.SafeDistance.Value,
                bandWidth: Plugin.BandWidth.Value,
                damagePerBand: Plugin.DamagePerBand.Value);

            int[] damage = DamageCalculator.Evaluate(_states, settings);
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
