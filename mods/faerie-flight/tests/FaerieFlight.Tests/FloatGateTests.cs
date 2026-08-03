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
}
