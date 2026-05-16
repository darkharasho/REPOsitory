namespace UnchainedIndestructibles
{
    internal static class DroneItem
    {
        private static Item? _cached;
        private static StatsManager? _cachedStats;
        private static bool _missingWarned;

        // Returns the Indestructible Drone Item from StatsManager.itemDictionary,
        // or null if StatsManager isn't initialized yet or the item is missing
        // (e.g. some other mod removed it). Caches by StatsManager.instance ref;
        // re-resolves automatically if the singleton is rebuilt (it shouldn't,
        // but we guard cheaply).
        internal static Item? Resolve()
        {
            var stats = StatsManager.instance;
            if (stats == null) return null;

            if (!ReferenceEquals(stats, _cachedStats))
            {
                _cached = null;
                _cachedStats = stats;
                _missingWarned = false;
            }
            if (_cached != null) return _cached;

            foreach (var item in stats.itemDictionary.Values)
            {
                if (item != null && item.emojiIcon == SemiFunc.emojiIcon.drone_indestructible)
                {
                    _cached = item;
                    break;
                }
            }

            if (_cached == null && !_missingWarned)
            {
                Plugin.Log.LogWarning(
                    "Indestructible Drone item not found in StatsManager.itemDictionary — mod is inert.");
                _missingWarned = true;
            }
            return _cached;
        }
    }
}
