using HarmonyLib;

namespace ForcedFriendship
{
    /// <summary>
    /// Shared liveness check. PlayerAvatar.deadSet / .isDisabled are 'internal' in
    /// Assembly-CSharp and this mod builds against the un-publicized game DLL, so they
    /// are read via cached field-ref delegates.
    /// </summary>
    internal static class PlayerLiveness
    {
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> DeadSetRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("deadSet");
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> IsDisabledRef =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>("isDisabled");
        // RoomVolumeCheck.inTruck is also 'internal' — read via a cached field-ref delegate.
        private static readonly AccessTools.FieldRef<RoomVolumeCheck, bool> InTruckRef =
            AccessTools.FieldRefAccess<RoomVolumeCheck, bool>("inTruck");

        internal static bool IsAlive(PlayerAvatar pa) => !DeadSetRef(pa) && !IsDisabledRef(pa);

        /// <summary>True when the player is currently inside the extraction truck (a safe zone).</summary>
        internal static bool IsInTruck(PlayerAvatar pa)
        {
            RoomVolumeCheck rvc = pa.RoomVolumeCheck;
            return rvc != null && InTruckRef(rvc);
        }
    }
}
