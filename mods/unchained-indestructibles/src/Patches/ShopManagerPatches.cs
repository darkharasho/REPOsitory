using HarmonyLib;

namespace UnchainedIndestructibles.Patches
{
    // Scoped cap override: temporarily raise the drone Item's maxAmountInShop
    // so ShopManager.GetAllItemsFromStatsManager (which reads the field inline
    // at decompile line 233) treats the drone as having a higher cap, then
    // restore it in the finalizer. Method is host-only in vanilla (early-returns
    // on SemiFunc.IsNotMasterClient at line 203), so this only runs on the host.
    [HarmonyPatch(typeof(ShopManager), "GetAllItemsFromStatsManager")]
    internal static class ShopManager_GetAllItemsFromStatsManager_Patch
    {
        // Per-call snapshot. The patched method is not reentrant (it iterates
        // itemDictionary serially), so a single static slot is safe.
        private static Item? _drone;
        private static int _originalMaxAmountInShop;
        private static bool _restorePending;

        public static void Prefix()
        {
            _restorePending = false;
            _drone = DroneItem.Resolve();
            if (_drone == null) return;

            _originalMaxAmountInShop = _drone.maxAmountInShop;
            _drone.maxAmountInShop = Config.MaxAmountInShop.Value;
            _restorePending = true;
        }

        public static void Finalizer()
        {
            if (!_restorePending || _drone == null) return;
            _drone.maxAmountInShop = _originalMaxAmountInShop;
            _restorePending = false;
            _drone = null;
        }
    }
}
