using Xunit;

namespace MuseumGambling.Tests;

public class OutcomeTests
{
    [Theory]
    [InlineData(1, 0, false)]      // never-win, lowest roll → lose
    [InlineData(100, 0, false)]    // never-win, highest roll → lose
    [InlineData(1, 100, true)]     // always-win, lowest roll → win
    [InlineData(100, 100, true)]   // always-win, highest roll → win
    [InlineData(5, 5, true)]       // boundary: roll equals chance → win
    [InlineData(6, 5, false)]      // boundary: roll just above chance → lose
    [InlineData(50, -1, false)]    // defensive: negative chance → never wins
    [InlineData(50, 101, true)]    // defensive: >100 chance → always wins (rolls capped 1..100)
    public void ShouldWin_truth_table(int roll, int winChancePercent, bool expected)
        => Assert.Equal(expected, Outcome.ShouldWin(roll, winChancePercent));
}
