using HarmonyLib;

namespace UnchainedIndestructibles.Patches
{
    // Scoped cap override: temporarily raise the drone Item's maxAmount so
    // ItemManager.GetPurchasedItems (decompile line 153 -- Mathf.Clamp(value, 0,
    // item.maxAmount)) clamps to our configured value instead of the vanilla 1.
    // Restored in the finalizer.
    [HarmonyPatch(typeof(ItemManager), "GetPurchasedItems")]
    internal static class ItemManager_GetPurchasedItems_Patch
    {
        private static Item? _drone;
        private static int _originalMaxAmount;
        private static bool _restorePending;

        public static void Prefix()
        {
            _restorePending = false;
            _drone = DroneItem.Resolve();
            if (_drone == null) return;

            _originalMaxAmount = _drone.maxAmount;
            _drone.maxAmount = Config.MaxAmount.Value;
            _restorePending = true;
        }

        public static void Finalizer()
        {
            if (!_restorePending || _drone == null) return;
            _drone.maxAmount = _originalMaxAmount;
            _restorePending = false;
            _drone = null;
        }
    }
}
