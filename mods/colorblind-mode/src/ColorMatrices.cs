namespace ColorblindMode
{
    public enum ColorblindType { Off, Deuteranopia, Protanopia, Tritanopia }

    /// <summary>
    /// Daltonization color math, deliberately free of any UnityEngine dependency so it can be
    /// unit-tested standalone. Matrices are row-major length-9 arrays: index = row*3 + col, where
    /// row is the OUTPUT channel (R,G,B) and col is the INPUT channel (R,G,B).
    ///
    /// Correction is the standard daltonize map: C = I + E * (I - S), where S is a dichromat
    /// SIMULATION matrix (Viénot/Brettel 1999, widely reproduced) and E is the Fidaner et al.
    /// error-redistribution matrix. Both are linear, so the composite is a single 3x3 the PPv2
    /// channel mixer can apply. (Mixer runs in PPv2's LDR working space, so this is a good
    /// accessibility approximation rather than a colorimetrically exact transform.)
    /// </summary>
    public static class ColorMatrices
    {
        public static readonly float[] Identity = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };

        // Fidaner et al. error-redistribution (shifts lost red/green error into other channels).
        private static readonly float[] ErrShift = { 0, 0, 0, 0.7f, 1, 0, 0.7f, 0, 1 };

        // Viénot 1999 dichromat simulation matrices (sRGB).
        private static readonly float[] SimDeuteranopia =
            { 0.625f, 0.375f, 0f, 0.70f, 0.30f, 0f, 0f, 0.30f, 0.70f };
        private static readonly float[] SimProtanopia =
            { 0.56667f, 0.43333f, 0f, 0.55833f, 0.44167f, 0f, 0f, 0.24167f, 0.75833f };
        private static readonly float[] SimTritanopia =
            { 0.95f, 0.05f, 0f, 0f, 0.43333f, 0.56667f, 0f, 0.475f, 0.525f };

        public static float[] Correction(ColorblindType type)
        {
            float[]? sim = Sim(type);
            if (sim == null) return (float[])Identity.Clone();
            return Add(Identity, Multiply(ErrShift, Sub(Identity, sim)));
        }

        public static float[] ToMixerPercent(ColorblindType type, float intensity)
        {
            float t = intensity < 0f ? 0f : (intensity > 1f ? 1f : intensity);
            float[] m = Lerp(Identity, Correction(type), t);
            var pct = new float[9];
            for (int i = 0; i < 9; i++)
            {
                float v = m[i] * 100f;
                pct[i] = v < -200f ? -200f : (v > 200f ? 200f : v);
            }
            return pct;
        }

        private static float[]? Sim(ColorblindType type) => type switch
        {
            ColorblindType.Deuteranopia => SimDeuteranopia,
            ColorblindType.Protanopia => SimProtanopia,
            ColorblindType.Tritanopia => SimTritanopia,
            _ => null,
        };

        private static float[] Multiply(float[] a, float[] b)
        {
            var r = new float[9];
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    r[row * 3 + col] =
                        a[row * 3 + 0] * b[0 * 3 + col] +
                        a[row * 3 + 1] * b[1 * 3 + col] +
                        a[row * 3 + 2] * b[2 * 3 + col];
            return r;
        }

        private static float[] Add(float[] a, float[] b)
        {
            var r = new float[9];
            for (int i = 0; i < 9; i++) r[i] = a[i] + b[i];
            return r;
        }

        private static float[] Sub(float[] a, float[] b)
        {
            var r = new float[9];
            for (int i = 0; i < 9; i++) r[i] = a[i] - b[i];
            return r;
        }

        private static float[] Lerp(float[] a, float[] b, float t)
        {
            var r = new float[9];
            for (int i = 0; i < 9; i++) r[i] = a[i] + (b[i] - a[i]) * t;
            return r;
        }
    }
}
