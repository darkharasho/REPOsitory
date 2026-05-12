namespace MuseumGambling;

internal static class Outcome
{
    internal static bool ShouldWin(int roll, int winChancePercent)
        => winChancePercent > 0 && roll <= winChancePercent;
}
