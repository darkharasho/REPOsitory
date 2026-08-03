using System.Collections.Generic;
using Xunit;

namespace ModdedPurchaseKeeper.Tests
{
    public class LedgerLogicTests
    {
        private static Dictionary<string, int> Ledger(params (string key, int count)[] entries)
        {
            var d = new Dictionary<string, int>();
            foreach (var (key, count) in entries) d[key] = count;
            return d;
        }

        // -- RecordCount ---------------------------------------------------------

        [Fact]
        public void RecordCount_StoresGameCount()
        {
            var ledger = Ledger();
            LedgerLogic.RecordCount(ledger, "Item Upgrade Health", 2);
            Assert.Equal(2, ledger["Item Upgrade Health"]);
        }

        // -- SyncConsumption (the 0.1.0 free-duplicate bug) ------------------------

        [Fact]
        public void SyncConsumption_LastCopyConsumed_RemovesLedgerEntry()
        {
            var ledger = Ledger(("Item Upgrade Health", 1));
            LedgerLogic.SyncConsumption(ledger, "Item Upgrade Health", null);
            Assert.False(ledger.ContainsKey("Item Upgrade Health"));
        }

        [Fact]
        public void SyncConsumption_OneOfManyConsumed_KeepsSurvivingCount()
        {
            var ledger = Ledger(("Item Upgrade Health", 3));
            LedgerLogic.SyncConsumption(ledger, "Item Upgrade Health", 2);
            Assert.Equal(2, ledger["Item Upgrade Health"]);
        }

        [Fact]
        public void SyncConsumption_ZeroCount_RemovesLedgerEntry()
        {
            var ledger = Ledger(("Item Upgrade Health", 1));
            LedgerLogic.SyncConsumption(ledger, "Item Upgrade Health", 0);
            Assert.False(ledger.ContainsKey("Item Upgrade Health"));
        }

        [Fact]
        public void SyncConsumption_UntrackedItem_LeavesLedgerAlone()
        {
            var ledger = Ledger();
            LedgerLogic.SyncConsumption(ledger, "Item Grenade Explosive", 4);
            Assert.Empty(ledger);
        }

        // -- ReassertValue (raise-only restore, extracted from Reassert_BeforeTruck) --

        [Fact]
        public void ReassertValue_RestoresWipedCount()
        {
            Assert.Equal(1, LedgerLogic.ReassertValue(1, isRegistered: true, currentCount: 0));
        }

        [Fact]
        public void ReassertValue_LeavesMatchingCount()
        {
            Assert.Null(LedgerLogic.ReassertValue(1, isRegistered: true, currentCount: 1));
        }

        [Fact]
        public void ReassertValue_LeavesHigherCurrentAlone()
        {
            Assert.Null(LedgerLogic.ReassertValue(2, isRegistered: true, currentCount: 3));
        }

        [Fact]
        public void ReassertValue_SkipsUnregisteredItem()
        {
            Assert.Null(LedgerLogic.ReassertValue(1, isRegistered: false, currentCount: 0));
        }

        [Fact]
        public void ReassertValue_SkipsNonPositiveLedgerCount()
        {
            Assert.Null(LedgerLogic.ReassertValue(0, isRegistered: true, currentCount: 0));
        }

        // -- instance-name key -----------------------------------------------------

        [Fact]
        public void PurchaseKey_StripsInstanceSuffix()
        {
            Assert.Equal("Item Grenade Explosive", LedgerLogic.PurchaseKeyFromInstanceName("Item Grenade Explosive/2"));
        }

        [Fact]
        public void PurchaseKey_NoSuffix_ReturnsName()
        {
            Assert.Equal("Item Mine Stun", LedgerLogic.PurchaseKeyFromInstanceName("Item Mine Stun"));
        }

        // -- end-to-end: the reported bug -------------------------------------------

        [Fact]
        public void BoughtWipedRestored_ThenConsumed_IsNeverResurrected()
        {
            var ledger = Ledger();

            // Shop: buy one health upgrade -> game count 1, ledger records it.
            LedgerLogic.RecordCount(ledger, "Item Upgrade Health", 1);

            // Level load: REPOLib re-registration wipes the game count to 0.
            // The re-assert must restore it (this is the mod's whole purpose).
            Assert.Equal(1, LedgerLogic.ReassertValue(ledger["Item Upgrade Health"], isRegistered: true, currentCount: 0));

            // Player uses the upgrade: the game removes the itemsPurchased entry.
            LedgerLogic.SyncConsumption(ledger, "Item Upgrade Health", null);

            // Next scene load: nothing left to re-assert -> no free duplicate.
            Assert.False(ledger.ContainsKey("Item Upgrade Health"));
        }
    }
}
