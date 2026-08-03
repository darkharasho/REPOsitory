using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace ModdedPurchaseKeeper
{
    // Fixes: modded (REPOLib-registered) shop items are bought and charged, but never appear
    // in the level.
    //
    // Cause: on level transitions REPOLib re-registers its items, and StatsManager.AddItem
    // resets itemsPurchased[name] = 0 for each — wiping the purchase count of a modded item
    // you just bought, before PunManager.TruckPopulateItemVolumes (via ItemManager.
    // GetPurchasedItems) can respawn it. Vanilla items never go through that path, so only
    // modded items vanish. (Verified against EMLGunPlus coil guns, REPO 0.4.4 / REPOLib 4.2.0.)
    //
    // Fix: rather than chase REPOLib's re-registration trigger, we keep our own ledger of what
    // the player actually bought/used (ItemPurchase / SetItemPurchase) and re-assert those
    // counts in a prefix on GetPurchasedItems — the exact moment the truck reads them. Whatever
    // zeroed the value in between, it's correct when it matters. The ledger is cleared on a
    // genuine new-run reset (ResetAllStats), so purchases never leak across runs.
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("REPOLib", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;

        // Authoritative purchase counts keyed by Item.name (== the itemsPurchased key).
        internal static readonly Dictionary<string, int> Ledger = new Dictionary<string, int>();

        private void Awake()
        {
            Log = Logger;
            new Harmony(PluginInfo.PLUGIN_GUID).PatchAll();
            Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded — protecting modded purchases.");
        }
    }

    // Record the cumulative count right after a purchase is committed.
    [HarmonyPatch(typeof(StatsManager), "ItemPurchase")]
    internal static class Track_ItemPurchase
    {
        private static void Postfix(string itemName)
        {
            var sm = StatsManager.instance;
            if (sm == null) return;
            LedgerLogic.RecordCount(Plugin.Ledger, itemName, sm.itemsPurchased.TryGetValue(itemName, out var v) ? v : 0);
        }
    }

    // Track explicit sets too (the charging station's crystal path goes through here).
    [HarmonyPatch(typeof(StatsManager), "SetItemPurchase")]
    internal static class Track_SetItemPurchase
    {
        private static void Postfix(Item _item, int value)
        {
            if (_item != null) LedgerLogic.RecordCount(Plugin.Ledger, ((UnityEngine.Object)_item).name, value);
        }
    }

    // Consumption does NOT go through SetItemPurchase: using a power-up
    // (ItemUpgrade.PlayerUpgrade) and spending a consumable (StatsManager.ItemRemove —
    // grenades, health packs, mines, the revive item) decrement/remove itemsPurchased by
    // DIRECT dictionary writes. Without mirroring those, the ledger stays at the bought
    // count and the re-assert below resurrects spent items — a free duplicate power-up in
    // the truck after every consumption. The prefix/postfix pair only syncs when THIS call
    // actually changed the count, so a REPOLib registration wipe can never be mistaken for
    // a consumption.
    [HarmonyPatch(typeof(ItemUpgrade), "PlayerUpgrade")]
    internal static class Sync_PlayerUpgradeConsumption
    {
        private static readonly AccessTools.FieldRef<ItemUpgrade, ItemAttributes> ItemAttributesRef =
            AccessTools.FieldRefAccess<ItemUpgrade, ItemAttributes>("itemAttributes");

        private static string? KeyOf(ItemUpgrade upgrade)
        {
            var attributes = ItemAttributesRef(upgrade);
            var item = attributes != null ? attributes.item : null;
            return item != null ? ((UnityEngine.Object)item).name : null;
        }

        private static int? Count(string key)
        {
            var sm = StatsManager.instance;
            if (sm == null) return null;
            return sm.itemsPurchased.TryGetValue(key, out var v) ? v : (int?)null;
        }

        private static void Prefix(ItemUpgrade __instance, ref int? __state)
        {
            var key = KeyOf(__instance);
            __state = key != null ? Count(key) : null;
        }

        private static void Postfix(ItemUpgrade __instance, int? __state)
        {
            var key = KeyOf(__instance);
            if (key == null || StatsManager.instance == null) return;
            int? after = Count(key);
            if (after != __state) LedgerLogic.SyncConsumption(Plugin.Ledger, key, after);
        }
    }

    [HarmonyPatch(typeof(StatsManager), "ItemRemove")]
    internal static class Sync_ItemRemoveConsumption
    {
        private static int? Count(StatsManager sm, string key)
            => sm.itemsPurchased.TryGetValue(key, out var v) ? v : (int?)null;

        private static void Prefix(StatsManager __instance, string instanceName, ref int? __state)
        {
            __state = Count(__instance, LedgerLogic.PurchaseKeyFromInstanceName(instanceName));
        }

        private static void Postfix(StatsManager __instance, string instanceName, int? __state)
        {
            string key = LedgerLogic.PurchaseKeyFromInstanceName(instanceName);
            int? after = Count(__instance, key);
            if (after != __state) LedgerLogic.SyncConsumption(Plugin.Ledger, key, after);
        }
    }

    // Genuine new run — drop the ledger so old purchases don't carry over.
    [HarmonyPatch(typeof(StatsManager), "ResetAllStats")]
    internal static class Clear_OnReset
    {
        private static void Postfix()
        {
            if (Plugin.Ledger.Count > 0) Plugin.Ledger.Clear();
        }
    }

    // Re-assert correct counts immediately before the truck respawns purchased items.
    [HarmonyPatch(typeof(ItemManager), "GetPurchasedItems")]
    internal static class Reassert_BeforeTruck
    {
        private static void Prefix()
        {
            var sm = StatsManager.instance;
            if (sm == null || Plugin.Ledger.Count == 0) return;

            foreach (var kv in Plugin.Ledger)
            {
                int cur = sm.itemsPurchased.TryGetValue(kv.Key, out var c) ? c : 0;
                var reassert = LedgerLogic.ReassertValue(kv.Value, sm.itemDictionary.ContainsKey(kv.Key), cur);
                if (reassert is int count)
                    sm.itemsPurchased[kv.Key] = count;
            }
        }
    }
}
