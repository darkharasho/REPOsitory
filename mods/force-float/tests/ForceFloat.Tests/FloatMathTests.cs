using ForceFloat;
using Xunit;

namespace ForceFloat.Tests;

public class FloatMathTests
{
    [Fact]
    public void Zero_input_gives_zero_direction()
    {
        var d = FloatMath.DriftDirection(
            camForward: new V3(0f, 0f, 1f), camRight: new V3(1f, 0f, 0f),
            inputX: 0f, inputZ: 0f);
        Assert.Equal(0f, d.SqrMagnitude, precision: 4);
    }

    [Fact]
    public void Forward_input_follows_camera_forward()
    {
        var d = FloatMath.DriftDirection(
            camForward: new V3(0f, 0f, 1f), camRight: new V3(1f, 0f, 0f),
            inputX: 0f, inputZ: 1f);
        Assert.Equal(0f, d.X, precision: 4);
        Assert.Equal(0f, d.Y, precision: 4);
        Assert.Equal(1f, d.Z, precision: 4);
    }

    [Fact]
    public void Combined_diagonal_input_is_normalized_to_unit_length()
    {
        // forward + right both full -> raw length sqrt(2) > 1 -> normalized to length 1
        var d = FloatMath.DriftDirection(
            camForward: new V3(0f, 0f, 1f), camRight: new V3(1f, 0f, 0f),
            inputX: 1f, inputZ: 1f);
        Assert.Equal(1f, d.SqrMagnitude, precision: 3);
    }

    [Fact]
    public void Small_input_is_left_unnormalized()
    {
        // half forward only -> length 0.5, below 1 -> left as-is
        var d = FloatMath.DriftDirection(
            camForward: new V3(0f, 0f, 1f), camRight: new V3(1f, 0f, 0f),
            inputX: 0f, inputZ: 0.5f);
        Assert.Equal(0.5f, d.Z, precision: 4);
        Assert.Equal(0.25f, d.SqrMagnitude, precision: 4);
    }
}
