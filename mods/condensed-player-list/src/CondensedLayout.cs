namespace CondensedPlayerList
{
    /// <summary>
    /// Pure layout math for the condensed single-column player list.
    /// No Unity dependency so it can be unit-tested in isolation.
    /// </summary>
    public static class CondensedLayout
    {
        // Vanilla per-row spacing (from MenuPlayerListed.Update): lobby 32, esc 22.
        // Condensed targets — tighter gaps so more rows fit on screen. Tuned in-game.
        public const float LobbySpacing = 22f;
        public const float EscSpacing = 15f;

        // X positions preserved exactly from vanilla per context.
        public const float LobbyX = 0f;
        public const float EscX = -23f;

        /// <summary>
        /// Returns the condensed local position (x, y) for a list entry.
        /// </summary>
        public static (float X, float Y) CondensedPosition(int listSpot, bool isLobby)
        {
            float x = isLobby ? LobbyX : EscX;
            float spacing = isLobby ? LobbySpacing : EscSpacing;
            return (x, -listSpot * spacing);
        }
    }
}
