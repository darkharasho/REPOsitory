using ColorblindMode;
using Xunit;

public class ColorMatricesTests
{
    private static void AssertClose(float[] expected, float[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
            Assert.True(System.Math.Abs(expected[i] - actual[i]) < 1e-3f,
                $"index {i}: expected {expected[i]}, got {actual[i]}");
    }

    [Fact]
    public void Off_IsIdentityCorrection()
    {
        AssertClose(ColorMatrices.Identity, ColorMatrices.Correction(ColorblindType.Off));
    }

    [Fact]
    public void Deuteranopia_CorrectionMatchesHandComputedValue()
    {
        // Correction = I + Err*(I - Sim_deuteranopia), hand-computed in the spec.
        float[] expected =
        {
            1f,       0f,       0f,
            -0.4375f, 1.4375f,  0f,
            0.2625f,  -0.5625f, 1.3f,
        };
        AssertClose(expected, ColorMatrices.Correction(ColorblindType.Deuteranopia));
    }

    [Fact]
    public void ToMixerPercent_IntensityZero_IsIdentityPercent()
    {
        float[] expected = { 100, 0, 0, 0, 100, 0, 0, 0, 100 };
        AssertClose(expected, ColorMatrices.ToMixerPercent(ColorblindType.Deuteranopia, 0f));
    }

    [Fact]
    public void ToMixerPercent_Off_IsIdentityPercentRegardlessOfIntensity()
    {
        float[] expected = { 100, 0, 0, 0, 100, 0, 0, 0, 100 };
        AssertClose(expected, ColorMatrices.ToMixerPercent(ColorblindType.Off, 1f));
    }

    [Fact]
    public void ToMixerPercent_IntensityOne_IsCorrectionTimes100()
    {
        float[] expected =
        {
            100f,    0f,      0f,
            -43.75f, 143.75f, 0f,
            26.25f,  -56.25f, 130f,
        };
        AssertClose(expected, ColorMatrices.ToMixerPercent(ColorblindType.Deuteranopia, 1f));
    }

    [Fact]
    public void ToMixerPercent_ClampsToEngineRange()
    {
        foreach (ColorblindType t in new[]
                 { ColorblindType.Deuteranopia, ColorblindType.Protanopia, ColorblindType.Tritanopia })
        foreach (float v in ColorMatrices.ToMixerPercent(t, 1f))
            Assert.InRange(v, -200f, 200f);
    }
}
