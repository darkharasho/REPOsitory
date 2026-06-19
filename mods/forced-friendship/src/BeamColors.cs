using UnityEngine;

namespace ForcedFriendship
{
    /// <summary>Shared zone → color mapping for the tether beam and the on-screen status indicator.</summary>
    internal static class BeamColors
    {
        // Muted (not full-saturation) so even at high opacity it reads as a soft tint. The
        // colorblind palette swaps green for blue (green/red is the common red-green confusion);
        // blue / yellow / red are well separated for deuteran & protan vision.
        internal static Color For(BeamZone zone, bool colorblind)
        {
            if (colorblind)
            {
                switch (zone)
                {
                    case BeamZone.Danger: return new Color(0.84f, 0.15f, 0.20f); // red
                    case BeamZone.Warn: return new Color(0.95f, 0.85f, 0.15f);   // yellow
                    default: return new Color(0.15f, 0.50f, 0.85f);             // blue (safe)
                }
            }
            switch (zone)
            {
                case BeamZone.Danger: return new Color(0.85f, 0.20f, 0.16f);
                case BeamZone.Warn: return new Color(0.85f, 0.70f, 0.15f);
                default: return new Color(0.25f, 0.75f, 0.30f);
            }
        }
    }
}
