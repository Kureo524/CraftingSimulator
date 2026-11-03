using System.Linq;
using Nucleaire.Crafting;
using Nucleaire.Simulation;
using NUnit.Framework;

namespace Nucleaire.Tests
{
    /// <summary>Inventaire à emplacements : piles, barre rapide, scinder / fusionner / échanger, cas limites.</summary>
    public class InventorySlotTests : SimTestBase
    {
        private PlayerInventory inv;
        private const int Cap = 4000;

        [SetUp]
        public void SetUp()
        {
            inv = new PlayerInventory(Cap);
            inv.PrefersHotbar = r => Db.PlaceableForItem(r) != null;
        }

        private int[] Amounts(Nucleaire.Data.ResourceDef r) =>
            inv.Slots.Where(s => s.Resource == r).Select(s => s.Amount).ToArray();

        private int IndexOf(Nucleaire.Data.ResourceDef r) =>
            inv.Slots.Select((s, i) => (s, i)).First(x => x.s.Resource == r).i;

        // ---------------- Ajout ----------------

        [Test]
        public void Add_SplitsItemsIntoStacksOf64()
        {
            Assert.AreEqual(150, inv.Add(R("iron_ore"), 150));
            CollectionAssert.AreEqual(new[] { 64, 64, 22 }, Amounts(R("iron_ore")));
            Assert.AreEqual(150, inv.Count(R("iron_ore")));
        }

        [Test]
        public void Add_CompletesExistingStackBeforeOpeningANewOne()
        {
            inv.Add(R("iron_ore"), 60);
            inv.Add(R("iron_ore"), 10);
            CollectionAssert.AreEqual(new[] { 64, 6 }, Amounts(R("iron_ore")));
        }

        [Test]
        public void Add_PlaceablesGoToHotbar_OthersToGrid()
        {
            var machineItem = M("crusher").Item;
            inv.Add(machineItem, 1);
            inv.Add(R("iron_ore"), 1);
            Assert.Less(IndexOf(machineItem), PlayerInventory.HotbarSize);
            Assert.GreaterOrEqual(IndexOf(R("iron_ore")), PlayerInventory.HotbarSize);
        }

        [Test]
        public void Add_GridGrowsWhenFull()
        {
            int initial = inv.Slots.Count;
            inv.Add(R("iron_ore"), PlayerInventory.ItemStack * (initial + 3));
            Assert.AreEqual(initial + 3, inv.Slots.Count);
            Assert.AreEqual(PlayerInventory.ItemStack * (initial + 3), inv.Count(R("iron_ore")));
        }

        [Test]
        public void Add_InvalidInputs_AreIgnored()
        {
            Assert.AreEqual(0, inv.Add(null, 5));
            Assert.AreEqual(0, inv.Add(R("iron_ore"), 0));
            Assert.AreEqual(0, inv.Add(R("iron_ore"), -3));
            Assert.AreEqual(0, inv.Add(Db.Energy, 10));
            Assert.IsTrue(inv.Slots.All(s => s.IsEmpty));
        }

        // ---------------- Retrait ----------------

        [Test]
        public void Remove_TakesGridBeforeHotbar_SmallestFirst()
        {
            inv.Add(R("iron_ore"), 64 + 10); // grille : 64 puis 10
            int big = IndexOf(R("iron_ore"));
            inv.Move(big, 0, 64);            // la pile de 64 passe dans la barre rapide
            inv.Remove(R("iron_ore"), 5);
            Assert.AreEqual(64, inv.Slots[0].Amount, "La barre rapide n'est entamée qu'en dernier");
            Assert.AreEqual(69, inv.Count(R("iron_ore")));
        }

        [Test]
        public void Remove_MoreThanOwned_RemovesOnlyWhatExists()
        {
            inv.Add(R("coal"), 3);
            Assert.AreEqual(3, inv.Remove(R("coal"), 50));
            Assert.AreEqual(0, inv.Count(R("coal")));
            Assert.IsTrue(inv.Slots.All(s => s.IsEmpty));
        }

        [Test]
        public void RemoveAll_IsAtomic_WhenSomethingIsMissing()
        {
            inv.Add(R("iron_ore"), 10);
            bool ok = inv.RemoveAll(new[] { new Nucleaire.Data.ResourceStack(R("iron_ore"), 5), new Nucleaire.Data.ResourceStack(R("coal"), 1) });
            Assert.IsFalse(ok);
            Assert.AreEqual(10, inv.Count(R("iron_ore")), "Rien n'est retiré si un ingrédient manque");
        }

        [Test]
        public void RemoveFromSlot_ClearsSlotWhenEmptied_AndRejectsBadIndex()
        {
            inv.Add(R("coal"), 4);
            int i = IndexOf(R("coal"));
            Assert.AreEqual(4, inv.RemoveFromSlot(i, 99));
            Assert.IsTrue(inv.Slots[i].IsEmpty);
            Assert.IsNull(inv.Slots[i].Resource);
            Assert.AreEqual(0, inv.RemoveFromSlot(-1, 1));
            Assert.AreEqual(0, inv.RemoveFromSlot(inv.Slots.Count, 1));
        }

        [Test]
        public void Clear_ShrinksGridBackToMinimum()
        {
            inv.Add(R("iron_ore"), PlayerInventory.ItemStack * 60);
            inv.Clear();
            Assert.AreEqual(PlayerInventory.HotbarSize + PlayerInventory.MinGridSlots, inv.Slots.Count);
            Assert.IsTrue(inv.Slots.All(s => s.IsEmpty));
        }

        // ---------------- Déplacements ----------------

        [Test]
        public void Move_ToEmptySlot_Splits()
        {
            inv.Add(R("iron_ore"), 40);
            int from = IndexOf(R("iron_ore"));
            Assert.AreEqual(15, inv.Move(from, 0, 15));
            Assert.AreEqual(25, inv.Slots[from].Amount);
            Assert.AreEqual(15, inv.Slots[0].Amount);
            Assert.AreEqual(R("iron_ore"), inv.Slots[0].Resource);
        }

        [Test]
        public void Move_OntoSameResource_MergesWithinStackLimit()
        {
            inv.Add(R("iron_ore"), 64 + 30); // 64 + 30
            var idx = inv.Slots.Select((s, i) => (s, i)).Where(x => x.s.Resource == R("iron_ore")).Select(x => x.i).ToArray();
            inv.Move(idx[0], 0, 64);   // 64 dans la barre rapide
            int moved = inv.Move(idx[1], 0, 30);
            Assert.AreEqual(0, moved, "Pile pleine : rien ne bouge");
            Assert.AreEqual(30, inv.Slots[idx[1]].Amount);

            inv.RemoveFromSlot(0, 50); // 14 restent
            moved = inv.Move(idx[1], 0, 30);
            Assert.AreEqual(30, moved);
            Assert.AreEqual(44, inv.Slots[0].Amount);
            Assert.IsTrue(inv.Slots[idx[1]].IsEmpty);
        }

        [Test]
        public void Move_PartialMerge_LeavesRemainderInSource()
        {
            inv.Add(R("iron_ore"), 60);
            int a = IndexOf(R("iron_ore"));
            inv.Move(a, 0, 50);      // barre : 50, source : 10
            inv.Add(R("iron_ore"), 20); // complète d'abord la barre (64) puis la source (16)
            Assert.AreEqual(64, inv.Slots[0].Amount);
            inv.RemoveFromSlot(0, 4); // barre : 60
            Assert.AreEqual(4, inv.Move(a, 0, 16));
            Assert.AreEqual(64, inv.Slots[0].Amount);
            Assert.AreEqual(12, inv.Slots[a].Amount);
        }

        [Test]
        public void Move_OntoOtherResource_SwapsWholeStacksOnly()
        {
            inv.Add(R("iron_ore"), 20);
            inv.Add(R("coal"), 7);
            int ore = IndexOf(R("iron_ore")), coal = IndexOf(R("coal"));

            Assert.AreEqual(0, inv.Move(ore, coal, 10), "Une demi-pile ne s'échange pas");
            Assert.AreEqual(20, inv.Slots[ore].Amount);

            Assert.AreEqual(20, inv.Move(ore, coal, 20));
            Assert.AreEqual(R("coal"), inv.Slots[ore].Resource);
            Assert.AreEqual(7, inv.Slots[ore].Amount);
            Assert.AreEqual(R("iron_ore"), inv.Slots[coal].Resource);
            Assert.AreEqual(20, inv.Slots[coal].Amount);
        }

        [Test]
        public void Move_InvalidCases_DoNothing()
        {
            inv.Add(R("iron_ore"), 5);
            int i = IndexOf(R("iron_ore"));
            int changes = 0;
            inv.Changed += () => changes++;
            Assert.AreEqual(0, inv.Move(i, i, 5));
            Assert.AreEqual(0, inv.Move(-1, 0, 5));
            Assert.AreEqual(0, inv.Move(i, inv.Slots.Count, 5));
            Assert.AreEqual(0, inv.Move(i, 0, 0));
            Assert.AreEqual(0, inv.Move(1, 2, 5), "Emplacement source vide");
            Assert.AreEqual(0, changes);
            Assert.AreEqual(5, inv.Count(R("iron_ore")));
        }

        [Test]
        public void Move_MoreThanStack_IsClampedToSource()
        {
            inv.Add(R("iron_ore"), 5);
            int i = IndexOf(R("iron_ore"));
            Assert.AreEqual(5, inv.Move(i, 0, 999));
            Assert.IsTrue(inv.Slots[i].IsEmpty);
        }

        [Test]
        public void Moves_NeverCreateOrLoseResources()
        {
            inv.Add(R("iron_ore"), 200);
            inv.Add(R("coal"), 90);
            inv.Add(R("water"), Cap + 1500);
            var rng = new System.Random(42);
            for (int k = 0; k < 500; k++)
                inv.Move(rng.Next(inv.Slots.Count), rng.Next(inv.Slots.Count), rng.Next(1, 100));
            Assert.AreEqual(200, inv.Count(R("iron_ore")));
            Assert.AreEqual(90, inv.Count(R("coal")));
            Assert.AreEqual(Cap + 1500, inv.Count(R("water")));
            foreach (var s in inv.Slots.Where(s => !s.IsEmpty))
                Assert.LessOrEqual(s.Amount, inv.StackSize(s.Resource));
            Assert.IsTrue(inv.Slots.All(s => s.Resource != null || s.Amount == 0));
        }

        // ---------------- Avec les machines ----------------

        [Test]
        public void DepositFromSlot_TakesOnlyFromThatSlot()
        {
            var crusher = Place<ProcessorMachine>("crusher", 2, 2);
            var player = Session.Inventory;
            player.Clear();
            player.Add(R("iron_ore"), 64 + 10);
            var idx = player.Slots.Select((s, i) => (s, i)).Where(x => x.s.Resource == R("iron_ore")).Select(x => x.i).ToArray();
            int n = Session.Actions.DepositFromSlot(crusher, idx[1], 10);
            Assert.Greater(n, 0);
            Assert.AreEqual(64, player.Slots[idx[0]].Amount, "L'autre pile n'est pas touchée");
            Assert.AreEqual(10 - n, player.Slots[idx[1]].Amount);
            Assert.AreEqual(0, Session.Actions.DepositFromSlot(crusher, 999, 5));
        }
    }
}
