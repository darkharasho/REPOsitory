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
        /// <summary>True when the player is standing in the extraction truck — a safe zone.</summary>
        public readonly bool InTruck;

        public PlayerState(float x, float y, float z, bool alive, bool inTruck = false)
        {
            X = x;
            Y = y;
            Z = z;
            Alive = alive;
            InTruck = inTruck;
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

    /// <summary>Where each player measures its distance from.</summary>
    public enum AnchorMode
    {
        /// <summary>Nearest living other player (the original rule).</summary>
        Buddy,
        /// <summary>The main hauling cart.</summary>
        Cart,
    }

    /// <summary>Beam color band for the tether visual.</summary>
    public enum BeamZone
    {
        Safe,   // green
        Warn,   // yellow
        Danger, // red — taking damage
    }

    /// <summary>A Unity-free 3D point (e.g. a cart position).</summary>
    public readonly struct Vec3
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>One player's resolved anchor position and distance to it.</summary>
    public readonly struct AnchorResult
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly float Distance;
        public readonly bool HasAnchor;
        /// <summary>True when the player is in a safe zone (e.g. the truck): no damage, beam stays green.</summary>
        public readonly bool Safe;

        public AnchorResult(float x, float y, float z, float distance, bool hasAnchor, bool safe = false)
        {
            X = x;
            Y = y;
            Z = z;
            Distance = distance;
            HasAnchor = hasAnchor;
            Safe = safe;
        }

        public static AnchorResult None => new AnchorResult(0f, 0f, 0f, 0f, false);
    }

    /// <summary>Pure banded damage-over-time math. No Unity/Photon/BepInEx dependencies.</summary>
    public static class DamageCalculator
    {
        public static float Distance(in PlayerState a, in PlayerState b) => Distance(a, b, includeHeight: true);

        /// <summary>
        /// Distance between two players. When <paramref name="includeHeight"/> is false the
        /// vertical (Y) axis is ignored, so players on different floors of the same tall room
        /// still count as close.
        /// </summary>
        public static float Distance(in PlayerState a, in PlayerState b, bool includeHeight)
        {
            float dx = a.X - b.X;
            float dy = includeHeight ? a.Y - b.Y : 0f;
            float dz = a.Z - b.Z;
            return (float)Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        }

        private static float Distance(in PlayerState a, in Vec3 b, bool includeHeight)
        {
            float dx = a.X - b.X;
            float dy = includeHeight ? a.Y - b.Y : 0f;
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
        /// Resolves each player's anchor position and distance. In Buddy mode the anchor is the
        /// nearest living OTHER player. In Cart mode every living player anchors on the NEAREST
        /// cart in <paramref name="carts"/> (supporting multiple medium carts); if the list is
        /// empty, Cart mode falls back to Buddy. Dead players get <see cref="AnchorResult.None"/>.
        /// </summary>
        public static AnchorResult[] ResolveAnchors(
            IReadOnlyList<PlayerState> players, AnchorMode mode, IReadOnlyList<Vec3> carts,
            bool includeHeight = true)
        {
            var result = new AnchorResult[players.Count];
            bool useCart = mode == AnchorMode.Cart && carts != null && carts.Count > 0;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState self = players[i];
                if (!self.Alive) { result[i] = AnchorResult.None; continue; }

                if (useCart)
                {
                    float best = float.PositiveInfinity;
                    int bestIdx = -1;
                    for (int c = 0; c < carts!.Count; c++)
                    {
                        float d = Distance(self, carts[c], includeHeight);
                        if (d < best) { best = d; bestIdx = c; }
                    }
                    Vec3 cart = carts[bestIdx];
                    result[i] = new AnchorResult(cart.X, cart.Y, cart.Z, best, true, self.InTruck);
                    continue;
                }

                float nearest = float.PositiveInfinity;
                int nearestIdx = -1;
                for (int j = 0; j < players.Count; j++)
                {
                    if (j == i) continue;
                    PlayerState other = players[j];
                    if (!other.Alive) continue;

                    float d = Distance(self, other, includeHeight);
                    if (d < nearest) { nearest = d; nearestIdx = j; }
                }

                if (nearestIdx < 0) { result[i] = AnchorResult.None; continue; }
                PlayerState n = players[nearestIdx];
                result[i] = new AnchorResult(n.X, n.Y, n.Z, nearest, true, self.InTruck);
            }

            return result;
        }

        /// <summary>
        /// Classifies a distance into a beam color band. Past the safe radius is Danger
        /// (taking damage). Within the last <paramref name="warnPercent"/> fraction of the
        /// safe radius (and up to/including it) is Warn. Otherwise Safe. A warnPercent of 0
        /// (or less) removes the yellow zone; values are clamped to [0, 1].
        /// </summary>
        public static BeamZone Classify(float distance, float safeDistance, float warnPercent)
        {
            if (distance > safeDistance) return BeamZone.Danger;
            if (warnPercent <= 0f) return BeamZone.Safe;
            float p = warnPercent > 1f ? 1f : warnPercent;
            float warnStart = safeDistance * (1f - p);
            return distance >= warnStart ? BeamZone.Warn : BeamZone.Safe;
        }

        /// <summary>
        /// HP to apply per player this tick from precomputed anchors. A player with no
        /// anchor — or one flagged <see cref="AnchorResult.Safe"/> (e.g. in the truck) —
        /// takes none; otherwise damage = band(distance) * DamagePerBand.
        /// </summary>
        public static int[] EvaluateDamage(IReadOnlyList<AnchorResult> anchors, in DamageSettings s)
        {
            var result = new int[anchors.Count];
            if (!s.Enabled) return result;

            for (int i = 0; i < anchors.Count; i++)
            {
                AnchorResult a = anchors[i];
                if (!a.HasAnchor || a.Safe) continue;
                int band = Band(a.Distance, s.SafeDistance, s.BandWidth);
                result[i] = band * s.DamagePerBand;
            }

            return result;
        }

        /// <summary>
        /// Beam color for one anchor: forced <see cref="BeamZone.Safe"/> when the player is in
        /// a safe zone, otherwise the distance-based <see cref="Classify"/> band.
        /// </summary>
        public static BeamZone ZoneForAnchor(in AnchorResult a, float safeDistance, float warnPercent)
            => a.Safe ? BeamZone.Safe : Classify(a.Distance, safeDistance, warnPercent);

        /// <summary>
        /// Whether a tether beam should be drawn at all. Requires an anchor. In Buddy mode a
        /// Safe (green) beam is hidden — split groups that are each internally fine show no
        /// lines; the tether only appears once someone is in the warn/danger zone. In Cart mode
        /// the tether to the cart is always shown as a guide.
        /// </summary>
        public static bool ShouldDrawBeam(AnchorMode mode, BeamZone zone, bool hasAnchor)
            => hasAnchor && (mode == AnchorMode.Cart || zone != BeamZone.Safe);

        /// <summary>
        /// Buddy-mode convenience: each living player is damaged by the band of the distance
        /// to its nearest living other player. Equivalent to ResolveAnchors(Buddy) + EvaluateDamage.
        /// </summary>
        public static int[] Evaluate(IReadOnlyList<PlayerState> players, in DamageSettings s)
        {
            AnchorResult[] anchors = ResolveAnchors(players, AnchorMode.Buddy, Array.Empty<Vec3>());
            return EvaluateDamage(anchors, s);
        }
    }
}
