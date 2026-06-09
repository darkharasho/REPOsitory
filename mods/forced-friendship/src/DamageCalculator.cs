using System;
using System.Collections.Generic;

namespace ForcedFriendship
{
    /// <summary>A Unity-free snapshot of one player for damage evaluation.</summary>
    public readonly struct PlayerState
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly bool Alive;

        public PlayerState(float x, float y, float z, bool alive)
        {
            X = x;
            Y = y;
            Z = z;
            Alive = alive;
        }
    }

    /// <summary>The effective rule values for one damage tick.</summary>
    public readonly struct DamageSettings
    {
        public readonly bool Enabled;
        public readonly float SafeDistance;
        public readonly float BandWidth;
        public readonly int DamagePerBand;

        public DamageSettings(bool enabled, float safeDistance, float bandWidth, int damagePerBand)
        {
            Enabled = enabled;
            SafeDistance = safeDistance;
            BandWidth = bandWidth;
            DamagePerBand = damagePerBand;
        }
    }

    /// <summary>Pure banded damage-over-time math. No Unity/Photon/BepInEx dependencies.</summary>
    public static class DamageCalculator
    {
        public static float Distance(in PlayerState a, in PlayerState b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return (float)Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        }

        /// <summary>
        /// 0 when within (or exactly at) the safe radius. Otherwise the band number,
        /// where each <paramref name="bandWidth"/> units past the safe radius is one
        /// more band. A distance landing exactly on a band edge counts as the higher band.
        /// </summary>
        public static int Band(float distance, float safeDistance, float bandWidth)
        {
            if (distance <= safeDistance) return 0;
            if (bandWidth <= 0f) return 1;
            return (int)Math.Floor((distance - safeDistance) / bandWidth) + 1;
        }

        /// <summary>
        /// Returns the HP to apply to each player this tick (same order/length as
        /// <paramref name="players"/>). A living player is damaged by the band of the
        /// distance to its nearest living OTHER player; dead players neither anchor
        /// others nor take damage; a player with no living other player takes none.
        /// </summary>
        public static int[] Evaluate(IReadOnlyList<PlayerState> players, in DamageSettings s)
        {
            var result = new int[players.Count];
            if (!s.Enabled) return result;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState self = players[i];
                if (!self.Alive) continue;

                float nearest = float.PositiveInfinity;
                for (int j = 0; j < players.Count; j++)
                {
                    if (j == i) continue;
                    PlayerState other = players[j];
                    if (!other.Alive) continue;

                    float d = Distance(self, other);
                    if (d < nearest) nearest = d;
                }

                if (float.IsPositiveInfinity(nearest)) continue; // no living other player

                int band = Band(nearest, s.SafeDistance, s.BandWidth);
                result[i] = band * s.DamagePerBand;
            }

            return result;
        }
    }
}
