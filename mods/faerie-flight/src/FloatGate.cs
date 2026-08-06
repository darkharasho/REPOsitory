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

        /// <summary>
        /// May ANY float work run this frame? Above all this fences the prefab scan: every
        /// PrefabRef.Prefab access is a synchronous bundle load, and one fired during a scene
        /// transition — while REPOLib re-registers items and level bundles are in flight —
        /// can block the main thread indefinitely (black screen). Nothing floats during a
        /// loading screen anyway, so deferring until the level-load animation completes
        /// costs nothing.
        /// </summary>
        public static bool ShouldRunFloatWork(bool modEnabled, bool inFloatableLevel, bool levelFullyLoaded)
        {
            return modEnabled && inFloatableLevel && levelFullyLoaded;
        }

        /// <summary>
        /// Grab strength to force onto the grabbed object while a floating player holds it,
        /// or null to leave vanilla physics alone. The game clamps a tumbling grabber to base
        /// strength (1f), killing Strength upgrades under permanent float; the object-level
        /// override is applied AFTER that clamp, so it restores them. Softened toward the
        /// square root because a tumbling grabber also takes the grab's recoil — full
        /// upgraded strength flings the floater. Enemies keep the vanilla clamp: restored
        /// strength would let floaters drag them harder than vanilla ever allows.
        /// </summary>
        public static float? CompensatedGrabStrength(
            bool effectActive,
            bool grabbedIsEnemy,
            float upgradedStrength,
            float softening)
        {
            if (!effectActive || grabbedIsEnemy) return null;
            if (upgradedStrength <= 1f) return null; // override would equal the vanilla clamp
            float t = softening < 0f ? 0f : (softening > 1f ? 1f : softening);
            float sqrt = (float)System.Math.Sqrt(upgradedStrength);
            return upgradedStrength + (sqrt - upgradedStrength) * t;
        }
    }
}
