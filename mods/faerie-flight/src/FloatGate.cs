namespace FaerieFlight
{
    /// <summary>Pure spawn-decision rules for the float effect (unit-testable, no Unity types).</summary>
    public static class FloatGate
    {
        /// <summary>
        /// May a zero-gravity effect be spawned for this player right now?
        /// Every link must be live: <c>SemiAffect.Setup</c> dereferences
        /// <c>playerAvatar.tumble.physGrabObject</c>, so spawning before PlayerTumble is
        /// linked (level-start race) throws mid-Photon-dispatch and leaks a
        /// half-initialized effect. Skipping is always safe — the master rebroadcasts
        /// the roster within 4 seconds.
        /// </summary>
        public static bool CanSpawnEffect(
            bool modEnabled,
            bool inFloatableLevel,
            bool avatarAlive,
            bool tumbleLinked,
            bool physGrabObjectLinked,
            bool effectAlreadyActive)
        {
            return modEnabled
                && inFloatableLevel
                && avatarAlive
                && tumbleLinked
                && physGrabObjectLinked
                && !effectAlreadyActive;
        }

        /// <summary>
        /// May the prefab scan run right now? Each scan walks the whole item dictionary and
        /// every <c>PrefabRef.Prefab</c> access is a synchronous bundle load, so an
        /// unresolvable prefab (item removed by another mod, custom-level bundle mid-unload)
        /// retried every frame grinds the game to an apparent freeze. Failed scans wait out
        /// <paramref name="nextScanAllowedAt"/>; a resolved prefab never rescans.
        /// </summary>
        public static bool ShouldScanPrefab(bool prefabCached, float now, float nextScanAllowedAt)
        {
            return !prefabCached && now >= nextScanAllowedAt;
        }
    }
}
