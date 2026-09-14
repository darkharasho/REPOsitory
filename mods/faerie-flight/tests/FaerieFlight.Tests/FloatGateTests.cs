using Xunit;

namespace FaerieFlight.Tests
{
    public class FloatGateTests
    {
        // All links live, gate open, no effect yet -> spawn.
        [Fact]
        public void AllConditionsMet_Spawns()
        {
            Assert.True(FloatGate.CanSpawnEffect(
                modEnabled: true,
                inFloatableLevel: true,
                avatarAlive: true,
                tumbleLinked: true,
                physGrabObjectLinked: true,
                effectAlreadyActive: false));
        }

        // The level-start race that leaked zombie effects: PlayerTumble not linked yet.
        [Fact]
        public void TumbleNotLinked_Blocks()
        {
            Assert.False(FloatGate.CanSpawnEffect(true, true, true, tumbleLinked: false, physGrabObjectLinked: false, effectAlreadyActive: false));
        }

        // Tumble linked but its PhysGrabObject still missing -> Setup would still NRE.
        [Fact]
        public void PhysGrabObjectNotLinked_Blocks()
        {
            Assert.False(FloatGate.CanSpawnEffect(true, true, true, tumbleLinked: true, physGrabObjectLinked: false, effectAlreadyActive: false));
        }

        // Local kill switch: a roster from the host must not spawn on a client that
        // turned the mod off (Update() would just destroy it next frame anyway).
        [Fact]
        public void ModDisabled_Blocks()
        {
            Assert.False(FloatGate.CanSpawnEffect(modEnabled: false, inFloatableLevel: true, avatarAlive: true, tumbleLinked: true, physGrabObjectLinked: true, effectAlreadyActive: false));
        }

        // Roster arriving outside a floatable level (menu/truck/shop/arena) is stale.
        [Fact]
        public void OutsideFloatableLevel_Blocks()
        {
            Assert.False(FloatGate.CanSpawnEffect(true, inFloatableLevel: false, avatarAlive: true, tumbleLinked: true, physGrabObjectLinked: true, effectAlreadyActive: false));
        }

        // Dead/disabled players must never be re-floated (fights the death-head physics).
        [Fact]
        public void DeadAvatar_Blocks()
        {
            Assert.False(FloatGate.CanSpawnEffect(true, true, avatarAlive: false, tumbleLinked: true, physGrabObjectLinked: true, effectAlreadyActive: false));
        }

        // One live effect per player — re-broadcasts top up the existing one instead.
        [Fact]
        public void EffectAlreadyActive_Blocks()
        {
            Assert.False(FloatGate.CanSpawnEffect(true, true, true, true, true, effectAlreadyActive: true));
        }
    }

    public class PrefabScanGateTests
    {
        // Prefab already resolved -> no scan, ever.
        [Fact]
        public void PrefabCached_NeverScans()
        {
            Assert.False(FloatGate.ShouldScanPrefab(prefabCached: true, now: 100f, nextScanAllowedAt: 0f));
        }

        // Fresh scene (nextScanAllowedAt reset to 0) -> first scan runs immediately.
        [Fact]
        public void FirstScanAfterSceneReset_Runs()
        {
            Assert.True(FloatGate.ShouldScanPrefab(prefabCached: false, now: 0f, nextScanAllowedAt: 0f));
        }

        // The freeze bug: a failed scan must NOT retry on the next frame.
        [Fact]
        public void WithinCooldown_Blocks()
        {
            Assert.False(FloatGate.ShouldScanPrefab(prefabCached: false, now: 10.016f, nextScanAllowedAt: 12f));
        }

        // Once the cooldown elapses the scan may retry (staff can appear late — REPOLib
        // re-registration / custom-level bundles).
        [Fact]
        public void CooldownElapsed_Runs()
        {
            Assert.True(FloatGate.ShouldScanPrefab(prefabCached: false, now: 12f, nextScanAllowedAt: 12f));
        }
    }

    // The freeze family's real fix: NO float work (above all no item-DB prefab scans, which
    // are synchronous bundle loads) until the level has fully finished loading. A scan fired
    // during a scene transition — while REPOLib re-registers items and custom-level bundles
    // are in flight — can block the main thread forever (black screen).
    public class FloatWorkGateTests
    {
        [Fact]
        public void LevelStillLoading_BlocksAllWork()
        {
            Assert.False(FloatGate.ShouldRunFloatWork(modEnabled: true, inFloatableLevel: true, levelFullyLoaded: false));
        }

        [Fact]
        public void LevelLoaded_Runs()
        {
            Assert.True(FloatGate.ShouldRunFloatWork(modEnabled: true, inFloatableLevel: true, levelFullyLoaded: true));
        }

        [Fact]
        public void ModDisabled_Blocks()
        {
            Assert.False(FloatGate.ShouldRunFloatWork(modEnabled: false, inFloatableLevel: true, levelFullyLoaded: true));
        }

        [Fact]
        public void OutsideFloatableLevel_Blocks()
        {
            Assert.False(FloatGate.ShouldRunFloatWork(modEnabled: true, inFloatableLevel: false, levelFullyLoaded: true));
        }
    }

    // Touching PhotonNetwork at plugin boot force-runs PUN's static init long before the
    // game configures Photon, wedging lobby creation in a 100%-CPU connect spin (bisected
    // live, 2026-08-05). The callback target may therefore only exist while we're actively
    // floating in multiplayer — by then the game itself initialized Photon long ago.
    public class PhotonCallbackGateTests
    {
        [Fact]
        public void ActiveMultiplayer_WantsCallback()
        {
            Assert.True(FloatGate.PhotonCallbackDesired(floatWorkActive: true, isMultiplayer: true));
        }

        // Menus, loading screens, disabled — never registered, so boot/menu frames can
        // never be the first PhotonNetwork touch.
        [Fact]
        public void Inactive_NoCallback()
        {
            Assert.False(FloatGate.PhotonCallbackDesired(floatWorkActive: false, isMultiplayer: true));
        }

        // Singleplayer never needs the roster listener.
        [Fact]
        public void Singleplayer_NoCallback()
        {
            Assert.False(FloatGate.PhotonCallbackDesired(floatWorkActive: true, isMultiplayer: false));
        }
    }

    // The Tumble Climb upgrade turns any grab that lands on static geometry into a climb —
    // and under permanent float everyone is always tumbling, so every wall grab hijacks the
    // floater: the climb spring (strength 14+, scaling with upgrade level) overpowers the
    // 8-force zero-grav drift and yanks them to the grab point ("randomly dragged down to
    // the ground" — group-confirmed trigger, 2026-08-06). While the local player's effect is
    // live we refresh the game's own climb kill-switch
    // (PhysGrabber.OverrideDisableSpecialGrabPowers, the timer enemies use), which gates
    // climb entry AND detaches an in-progress climb. Vanilla never hits this because tumble
    // is brief.
    public class ClimbSuppressionTests
    {
        [Fact]
        public void FloatingWithConfigOn_Suppresses()
        {
            Assert.True(FloatGate.ShouldSuppressClimb(disableClimbWhileFloating: true, localEffectActive: true));
        }

        // No live effect on the local player (dead, between levels, mod disabled) -> the
        // upgrade must work exactly as vanilla.
        [Fact]
        public void NotFloating_LeavesClimbAlone()
        {
            Assert.False(FloatGate.ShouldSuppressClimb(disableClimbWhileFloating: true, localEffectActive: false));
        }

        // Players who want wall-climb chaos while floating can keep it via config.
        [Fact]
        public void ConfigOff_LeavesClimbAlone()
        {
            Assert.False(FloatGate.ShouldSuppressClimb(disableClimbWhileFloating: false, localEffectActive: true));
        }
    }

    // Revived players spawn at their death head's position (ReviveRPC teleports the body
    // there) — under permanent float that's wherever the floating reviver carried the head,
    // i.e. mid-air — and then waited out the remainder of the 4s roster interval with
    // vanilla gravity, plummeting to the floor (log-verified 2026-08-06: every
    // alive-without-effect window followed a ReviveRPC burst, incl. a 6-player mass revive).
    // The master now pulls the next broadcast forward as soon as an eligible player is
    // missing an effect; a short debounce coalesces mass revives and lets the revive
    // teleport settle.
    public class RebroadcastTests
    {
        [Fact]
        public void NobodyMissing_KeepsSchedule()
        {
            Assert.Equal(13.5f, FloatGate.NextBroadcastTime(scheduledAt: 13.5f, now: 10f, eligiblePlayerMissingEffect: false, missingEffectDelay: 0.25f));
        }

        [Fact]
        public void MissingEffect_PullsBroadcastForward()
        {
            Assert.Equal(10.25f, FloatGate.NextBroadcastTime(scheduledAt: 13.5f, now: 10f, eligiblePlayerMissingEffect: true, missingEffectDelay: 0.25f));
        }

        // A broadcast already due sooner than the debounce must not be pushed BACK.
        [Fact]
        public void MissingButBroadcastImminent_KeepsEarlierSchedule()
        {
            Assert.Equal(10.1f, FloatGate.NextBroadcastTime(scheduledAt: 10.1f, now: 10f, eligiblePlayerMissingEffect: true, missingEffectDelay: 0.25f));
        }

        // Overdue schedule (broadcast fires this frame) stays overdue.
        [Fact]
        public void MissingAndOverdue_FiresNow()
        {
            Assert.Equal(9.9f, FloatGate.NextBroadcastTime(scheduledAt: 9.9f, now: 10f, eligiblePlayerMissingEffect: true, missingEffectDelay: 0.25f));
        }
    }

    // The game clamps grab strength to base (1f) while the grabber is tumbling, so under
    // permanent float Strength upgrades would be permanently dead. The compensation forces
    // a softened upgraded value back in via PhysGrabObject.OverrideGrabStrength — which the
    // game applies AFTER its tumble clamp. Null means "leave vanilla alone".
    public class GrabCompensationTests
    {
        // Not floating this player -> vanilla physics untouched.
        [Fact]
        public void NotFloating_LeavesVanilla()
        {
            Assert.Null(FloatGate.CompensatedGrabStrength(effectActive: false, grabbedIsEnemy: false, upgradedStrength: 3f, softening: 0.65f));
        }

        // Grabbing an enemy: keep the vanilla tumble clamp (base strength) — restoring
        // upgrades would let floaters drag enemies far harder than vanilla ever allows.
        [Fact]
        public void GrabbingEnemy_LeavesVanilla()
        {
            Assert.Null(FloatGate.CompensatedGrabStrength(effectActive: true, grabbedIsEnemy: true, upgradedStrength: 3f, softening: 0.65f));
        }

        // No Strength upgrades: the override would equal the clamp — skip the call.
        [Fact]
        public void NoUpgrades_LeavesVanilla()
        {
            Assert.Null(FloatGate.CompensatedGrabStrength(effectActive: true, grabbedIsEnemy: false, upgradedStrength: 1f, softening: 0.65f));
        }

        // Softening 0 restores the full upgraded strength.
        [Fact]
        public void SofteningZero_RestoresFullStrength()
        {
            Assert.Equal(3f, FloatGate.CompensatedGrabStrength(true, false, upgradedStrength: 3f, softening: 0f)!.Value, 4);
        }

        // Softening 1 is the full square-root curve.
        [Fact]
        public void SofteningOne_SqrtCurve()
        {
            Assert.Equal(2f, FloatGate.CompensatedGrabStrength(true, false, upgradedStrength: 4f, softening: 1f)!.Value, 4);
        }

        // Default-ish midpoint: lerp(S, sqrt(S), softening).
        [Fact]
        public void Softening_LerpsTowardSqrt()
        {
            // S=3: 3 + (sqrt(3)-3)*0.65 = 2.1758...
            Assert.Equal(2.1758f, FloatGate.CompensatedGrabStrength(true, false, upgradedStrength: 3f, softening: 0.65f)!.Value, 3);
        }

        // Out-of-range softening is clamped to [0,1], not extrapolated.
        [Fact]
        public void Softening_Clamped()
        {
            Assert.Equal(2f, FloatGate.CompensatedGrabStrength(true, false, upgradedStrength: 4f, softening: 1.5f)!.Value, 4);
            Assert.Equal(4f, FloatGate.CompensatedGrabStrength(true, false, upgradedStrength: 4f, softening: -0.5f)!.Value, 4);
        }
    }
}
