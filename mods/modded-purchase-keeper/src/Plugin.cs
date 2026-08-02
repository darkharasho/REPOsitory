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
            Plugin.Ledger[itemName] = sm.itemsPurchased.TryGetValue(itemName, out var v) ? v : 0;
        }
    }

    // Track explicit sets too (e.g. consuming/using a purchased item decrements it).
    [HarmonyPatch(typeof(StatsManager), "SetItemPurchase")]
    internal static class Track_SetItemPurchase
    {
        private static void Postfix(Item _item, int value)
        {
            if (_item != null) Plugin.Ledger[((UnityEngine.Object)_item).name] = value;
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

            int fixedN = 0;
            foreach (var kv in Plugin.Ledger)
            {
                if (kv.Value <= 0) continue;
                if (!sm.itemDictionary.ContainsKey(kv.Key)) continue; // not registered -> can't spawn
                int cur = sm.itemsPurchased.TryGetValue(kv.Key, out var c) ? c : 0;
                if (cur < kv.Value)
                {
                    sm.itemsPurchased[kv.Key] = kv.Value;
                    fixedN++;
                }
            }
            if (fixedN > 0)
                Plugin.Log.LogInfo($"[ModdedPurchaseKeeper] Re-asserted {fixedN} purchase count(s) zeroed before truck spawn.");
        }
    }
}
