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
