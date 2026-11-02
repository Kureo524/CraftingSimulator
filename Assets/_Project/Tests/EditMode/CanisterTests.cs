using System.Linq;
using Nucleaire.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Nucleaire.Tests
{
    /// <summary>Liquides et gaz dans l'inventaire, rangés en bonbonnes.</summary>
    public class CanisterTests : SimTestBase
    {
        [Test]
        public void Water_IsSplitIntoCanisters()
        {
            int cap = Session.Inventory.CanisterCapacity;
            Assert.AreEqual(cap * 2 + 500, Session.Inventory.Add(R("water"), cap * 2 + 500));
            CollectionAssert.AreEqual(new[] { cap, cap, 500 }, Session.Inventory.Canisters(R("water")).ToArray());
        }

        [Test]
        public void Canister_PoursIntoMachine_AndTankFillsCanisters()
        {
            var electrolyser = Place<ProcessorMachine>("electrolyser", 2, 2);
            Session.Inventory.Add(R("water"), 3000);
            int poured = Session.Actions.Deposit(electrolyser, R("water"), 3000);
            Assert.Greater(poured, 0);
            Assert.AreEqual(3000 - poured, Session.Inventory.Count(R("water")));

            int back = Session.Actions.Take(electrolyser, R("water"), int.MaxValue);
            Assert.AreEqual(poured, back, "Le contenu de la cuve revient en bonbonnes");
        }

        [Test]
        public void Energy_CannotBeCarried()
        {
            Assert.AreEqual(0, Session.Inventory.Add(Db.Energy, 100));
        }
    }
}
