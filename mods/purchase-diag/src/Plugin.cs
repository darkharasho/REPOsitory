using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PurchaseDiag
{
    // Diagnostic-only plugin. Instruments the shop-purchase -> next-level-respawn
    // chain and logs the state at every boundary, so we can see exactly which link
    // drops a purchased item (esp. modded items like the EML coil guns).
    //
    // Chain being traced (all in Assembly-CSharp):
    //   StatsManager.ItemPurchase(name)          -> records into itemsPurchased
    //   ItemManager.GetPurchasedItems()          -> builds purchasedItems from itemsPurchased+itemDictionary
    //   PunManager.TruckPopulateItemVolumes()    -> host-only; matches items to truck ItemVolumes
    //   PunManager.SpawnItem(Item, ItemVolume)   -> PhotonNetwork.InstantiateRoomObject(prefab.ResourcePath)
    //   PhotonNetwork.InstantiateRoomObject(...) -> actual network spawn
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;
        private const string TAG = "[PDIAG]";

        private void Awake()
        {
            Log = Logger;
            new Harmony(PluginInfo.PLUGIN_GUID).PatchAll();
            Log.LogInfo($"{TAG} PurchaseDiag v{PluginInfo.PLUGIN_VERSION} loaded — instrumenting purchased-item respawn chain.");
        }

        // --- helpers -------------------------------------------------------

        // Read PrefabRef.ResourcePath / IsValid() reflectively — the field type is a
        // generic PrefabRef<T> and we don't want a hard compile dependency on its shape.
        internal static (string path, string valid) InspectPrefab(object? prefabRef)
        {
            if (prefabRef == null) return ("<null prefab>", "n/a");
            try
            {
                var t = prefabRef.GetType();
                var path = t.GetProperty("ResourcePath")?.GetValue(prefabRef) as string ?? "<no ResourcePath>";
                object? v = t.GetMethod("IsValid", Type.EmptyTypes)?.Invoke(prefabRef, null);
                return (path, v?.ToString() ?? "n/a");
            }
            catch (Exception e) { return ($"<err {e.GetType().Name}>", "err"); }
        }

        internal static object? GetItemPrefab(Item item)
        {
            try { return typeof(Item).GetField("prefab")?.GetValue(item); }
            catch { return null; }
        }
    }

    // 1) Purchase is recorded ------------------------------------------------
    [HarmonyPatch(typeof(StatsManager), "ItemPurchase")]
    internal static class ItemPurchase_Patch
    {
        private static void Postfix(string itemName)
        {
            try
            {
                var sm = StatsManager.instance;
                int count = sm != null && sm.itemsPurchased.TryGetValue(itemName, out var c) ? c : -1;
                bool inDict = sm != null && sm.itemDictionary.ContainsKey(itemName);
                Plugin.Log.LogInfo($"[PDIAG] ItemPurchase('{itemName}') -> itemsPurchased={count}, inItemDictionary={inDict}");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[PDIAG] ItemPurchase postfix err: {e}"); }
        }
    }

    // 2) purchasedItems list is built from itemsPurchased -------------------
    [HarmonyPatch(typeof(ItemManager), "GetPurchasedItems")]
    internal static class GetPurchasedItems_Patch
    {
        private static void Postfix(ItemManager __instance)
        {
            try
            {
                var sm = StatsManager.instance;
                var sb = new StringBuilder();
                sb.Append("[PDIAG] GetPurchasedItems -> itemsPurchased(>0): ");
                if (sm != null)
                    foreach (var kv in sm.itemsPurchased)
                        if (kv.Value > 0) sb.Append($"{kv.Key}={kv.Value}  ");
                Plugin.Log.LogInfo(sb.ToString());

                var sb2 = new StringBuilder();
                sb2.Append($"[PDIAG] GetPurchasedItems -> purchasedItems[{__instance.purchasedItems.Count}]: ");
                foreach (var it in __instance.purchasedItems)
                    sb2.Append(it == null ? "<null> " : $"{it.itemName}(vol={it.itemVolume}) ");
                Plugin.Log.LogInfo(sb2.ToString());
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[PDIAG] GetPurchasedItems postfix err: {e}"); }
        }
    }

    // 3) truck population (host-only) --------------------------------------
    [HarmonyPatch(typeof(PunManager), "TruckPopulateItemVolumes")]
    internal static class TruckPopulate_Patch
    {
        private static void Prefix()
        {
            try
            {
                bool notMaster = SemiFunc.IsNotMasterClient();
                var im = ItemManager.instance;
                Plugin.Log.LogInfo($"[PDIAG] TruckPopulateItemVolumes ENTER  isNotMaster={notMaster}  " +
                                   $"purchasedItems={im?.purchasedItems.Count}  itemVolumes={im?.itemVolumes?.Count}");

                var sizes = new Dictionary<string, int>();
                if (im?.itemVolumes != null)
                    foreach (var v in im.itemVolumes)
                        if (v != null)
                        {
                            var key = v.itemVolume.ToString();
                            sizes[key] = sizes.TryGetValue(key, out var n) ? n + 1 : 1;
                        }
                var sb = new StringBuilder("[PDIAG]   available truck ItemVolume sizes: ");
                foreach (var kv in sizes) sb.Append($"{kv.Key}={kv.Value}  ");
                Plugin.Log.LogInfo(sb.ToString());
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[PDIAG] TruckPopulate prefix err: {e}"); }
        }

        private static void Postfix()
        {
            try
            {
                Plugin.Log.LogInfo($"[PDIAG] TruckPopulateItemVolumes EXIT  spawnedItems={ItemManager.instance?.spawnedItems.Count}");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[PDIAG] TruckPopulate postfix err: {e}"); }
        }
    }

    // 4) per-item spawn attempt -------------------------------------------
    [HarmonyPatch(typeof(PunManager), "SpawnItem")]
    internal static class SpawnItem_Patch
    {
        private static void Prefix(Item item, ItemVolume volume)
        {
            try
            {
                if (item == null) { Plugin.Log.LogInfo("[PDIAG] SpawnItem(item=null)"); return; }
                var (path, valid) = Plugin.InspectPrefab(Plugin.GetItemPrefab(item));
                Plugin.Log.LogInfo($"[PDIAG] SpawnItem '{item.itemName}' vol={item.itemVolume} -> " +
                                   $"volumeSlot={(volume == null ? "<null>" : volume.itemVolume.ToString())}  " +
                                   $"prefab.ResourcePath='{path}'  prefab.IsValid={valid}");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[PDIAG] SpawnItem prefix err: {e}"); }
        }
    }

    // NOTE: an earlier version also patched PhotonNetwork.InstantiateRoomObject to log the
    // spawn's return value, but patching that Photon method froze this pack at lobby creation
    // (it collides with Photon's network-callback init). Removed. SpawnItem's prefab.IsValid
    // log + whether SpawnItem is even reached is enough to localize the break.
}
