using System.Collections.Generic;
using System.Reflection;
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
        // builds against the un-publicized game DLL, so they are read via cached reflection.
        private static readonly FieldInfo DeadSetField =
            typeof(PlayerAvatar).GetField("deadSet", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo IsDisabledField =
            typeof(PlayerAvatar).GetField("isDisabled", BindingFlags.Instance | BindingFlags.NonPublic);

        private float _accum;
        private readonly List<PlayerState> _states = new List<PlayerState>();
        private readonly List<PlayerAvatar> _avatars = new List<PlayerAvatar>();

        private static bool GetBool(FieldInfo field, PlayerAvatar pa) =>
            field != null && (bool)field.GetValue(pa);

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
                bool alive = !GetBool(DeadSetField, pa) && !GetBool(IsDisabledField, pa);
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
                pa.playerHealth.HurtOther(damage[i], pa.transform.position, savingGrace: false);
            }
        }
    }
}
