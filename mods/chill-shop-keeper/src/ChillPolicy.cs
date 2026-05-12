namespace ChillShopKeeper;

public static class ChillPolicy
{
    public static bool ShouldSkip(bool disableGlobally, bool playerExempt)
        => disableGlobally || playerExempt;
}
