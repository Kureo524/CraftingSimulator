using System.Linq;
using Nucleaire.Data;
using NUnit.Framework;
using UnityEngine;

namespace Nucleaire.Tests
{
    public sealed class WorkshopAreaTests
    {
        private RecipeDatabase db;
        private SimulationSettings settings;
        private GameSession session;

        [SetUp]
        public void SetUp()
        {
            db = EditorTools.RecipeDatabaseSync.FindDatabase();
            settings = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<SimulationSettings>("Assets/_Project/Data/SimulationSettings.asset"));
            session = new GameSession(db, settings);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        [Test]
        public void StartsAt15Centred_GrowsTo39_In6Steps()
        {
            var w = session.World;
            Assert.AreEqual(new RectInt(12, 12, 15, 15), w.Area);
            Assert.AreEqual(9, settings.levels);
            Assert.AreEqual(6, w.MaxExpansions);
            Assert.IsTrue(w.InBounds(new Vector3Int(12, 26, 8)));
            Assert.IsFalse(w.InBounds(new Vector3Int(11, 20, 0)));
            Assert.IsFalse(w.InBounds(new Vector3Int(20, 20, 9)));
            for (int i = 0; i < 6; i++) Assert.IsTrue(w.Expand());
            Assert.IsFalse(w.Expand());
            Assert.AreEqual(new RectInt(0, 0, 39, 39), w.Area);
        }

        [Test]
        public void Expanding_CostsMaterials_ScaledByTier()
        {
            var actions = session.Actions;
            session.Inventory.Clear();
            Assert.IsFalse(actions.TryExpandWorkshop(out var error));
            StringAssert.Contains("manque", error);
            foreach (var c in settings.expansionCost) session.Inventory.Add(c.Resource, c.Amount * 3);
            Assert.IsTrue(actions.TryExpandWorkshop(out _));   // coût ×1
            Assert.IsTrue(actions.TryExpandWorkshop(out _));   // coût ×2
            Assert.IsFalse(actions.TryExpandWorkshop(out _));  // coût ×3 : plus assez
            Assert.AreEqual(19 + 4, session.World.Area.width);
            foreach (var c in settings.expansionCost) Assert.AreEqual(0, session.Inventory.Count(c.Resource));
        }

        [Test]
        public void ResetBoard_ShrinksBackToStart()
        {
            session.World.Expand();
            session.ResetBoard();
            Assert.AreEqual(15, session.World.Area.width);
        }

        [Test]
        public void FissionReactor_CanBe7x7x9()
        {
            Assert.AreEqual(9, Simulation.Multiblock.MaxHeight);
            Assert.AreEqual(7, Simulation.Multiblock.MaxSize);
        }
    }
}
