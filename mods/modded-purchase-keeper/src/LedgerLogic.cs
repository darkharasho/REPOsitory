using System.Collections.Generic;

namespace ModdedPurchaseKeeper
{
    /// <summary>
    /// Pure ledger rules — no Unity or game types, so they're unit-testable.
    /// The ledger remembers what the player actually owns (keyed by Item.name, the
    /// itemsPurchased key) so the re-assert before truck population can restore counts
    /// REPOLib's re-registration wiped — without resurrecting counts the player spent.
    /// </summary>
    public static class LedgerLogic
    {
        /// <summary>A purchase/set the game committed: remember the resulting count.</summary>
        public static void RecordCount(Dictionary<string, int> ledger, string itemName, int count)
        {
            ledger[itemName] = count;
        }

        /// <summary>
        /// The game consumed a purchase (upgrade used, consumable spent) by writing
        /// itemsPurchased directly. Mirror the surviving count; <paramref name="gameCountAfter"/>
        /// null means the game removed the entry entirely.
        /// </summary>
        public static void SyncConsumption(Dictionary<string, int> ledger, string itemName, int? gameCountAfter)
        {
            if (!ledger.ContainsKey(itemName)) return; // never tracked -> nothing to correct
            if (gameCountAfter is int n && n > 0)
                ledger[itemName] = n;
            else
                ledger.Remove(itemName);
        }

        /// <summary>Count to force back into itemsPurchased before the truck reads it, or null to leave it.</summary>
        public static int? ReassertValue(int ledgerCount, bool isRegistered, int currentCount)
        {
            if (ledgerCount <= 0 || !isRegistered || currentCount >= ledgerCount) return null;
            return ledgerCount;
        }

        /// <summary>itemsPurchased key for a StatsManager.ItemRemove instance name ("Item X/2" → "Item X").</summary>
        public static string PurchaseKeyFromInstanceName(string instanceName)
        {
            return instanceName.Split('/')[0];
        }
    }
}
