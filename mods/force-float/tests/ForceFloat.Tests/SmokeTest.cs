using ForceFloat;
using Xunit;

namespace ForceFloat.Tests;

public class SmokeTest
{
    [Fact]
    public void DriftDirection_is_callable()
    {
        var d = FloatMath.DriftDirection(new V3(0, 0, 1), new V3(1, 0, 0), 0f, 0f);
        Assert.Equal(0f, d.SqrMagnitude, precision: 4);
    }
}
