using CondensedPlayerList;
using Xunit;

namespace CondensedPlayerList.Tests;

public class CondensedLayoutTests
{
    [Theory]
    [InlineData(true,  0)]   // lobby, first row
    [InlineData(false, 0)]   // esc, first row
    public void FirstRow_isAtOrigin_y(bool isLobby, int listSpot)
    {
        var (_, y) = CondensedLayout.CondensedPosition(listSpot, isLobby);
        Assert.Equal(0f, y, 3);
    }

    [Fact]
    public void Lobby_usesCondensedSpacing_22()
    {
        var (x, y) = CondensedLayout.CondensedPosition(3, isLobby: true);
        Assert.Equal(0f, x, 3);         // lobby X preserved
        Assert.Equal(-66f, y, 3);       // -3 * 22
    }

    [Fact]
    public void Esc_usesCondensedSpacing_15()
    {
        var (x, y) = CondensedLayout.CondensedPosition(3, isLobby: false);
        Assert.Equal(-23f, x, 3);       // esc X preserved
        Assert.Equal(-45f, y, 3);       // -3 * 15
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Spacing_isTighterThanVanilla(bool isLobby)
    {
        float vanilla = isLobby ? CondensedLayout.VanillaLobbySpacing : CondensedLayout.VanillaEscSpacing;
        var (_, y1) = CondensedLayout.CondensedPosition(1, isLobby);
        Assert.True(-y1 < vanilla, $"row gap {-y1} should be < vanilla {vanilla}");
    }
}
