using System.Linq;
using Nucleaire.Data;
using Nucleaire.EditorTools;
using NUnit.Framework;

namespace Nucleaire.Tests
{
    /// <summary>Vérifie le contenu réel du jeu : données cohérentes et arbre complet jusqu'à l'antimatière.</summary>
    public class RecipeDatabaseTests
    {
        private RecipeDatabase db;

        [SetUp]
        public void SetUp()
        {
            db = RecipeDatabaseSync.FindDatabase();
            Assert.IsNotNull(db, "RecipeDatabase introuvable dans Assets/_Project/Data");
        }

        [Test]
        public void Content_IsValid()
        {
            var report = RecipeDatabaseValidator.Validate(db);
            Assert.IsTrue(report.IsValid, report.ToString());
        }

        [Test]
        public void Content_HasNoUnreachableResource()
        {
            var report = RecipeDatabaseValidator.Validate(db);
            Assert.IsEmpty(report.Warnings, report.ToString());
        }

        [Test]
        public void Objectives_AreThreeWithIncreasingOrder()
        {
            Assert.GreaterOrEqual(db.Objectives.Count, 3);
            var orders = db.Objectives.Select(o => o.Order).ToList();
            CollectionAssert.AreEqual(orders.OrderBy(o => o).ToList(), orders);
        }

        [Test]
        public void Antimatter_IsReachable()
        {
            var pellet = db.Resources.First(r => r.Id == "antimatter_pellet");
            Assert.IsTrue(RecipeDatabaseValidator.Validate(db).Reachable.Contains(pellet));
        }

        [Test]
        public void RecipeBook_FindsProducersAndUsages()
        {
            var steam = db.Resources.First(r => r.Id == "steam");
            Assert.IsTrue(db.RecipesProducing(steam).Any(e => e.Source == RecipeSource.Reactor), "La vapeur doit venir du réacteur");
            Assert.IsTrue(db.RecipesUsing(steam).Any(e => e.Machine != null && e.Machine.Id == "turbine"), "La vapeur doit alimenter la turbine");

            var ore = db.Resources.First(r => r.Id == "iron_ore");
            Assert.IsTrue(db.RecipesProducing(ore).Any(e => e.Source == RecipeSource.Vault));
        }

        [Test]
        public void ChemicalInfuser_IsReusedForSeveralProcesses()
        {
            var infuser = db.Machines.First(m => m.Id == "chemical_infuser");
            Assert.GreaterOrEqual(db.ProcessesFor(infuser).Count, 2);
        }

        [Test]
        public void EveryPlaceableAndUpgrade_HasCraftingRecipeOrIsPrePlaced()
        {
            var crafted = db.CraftingRecipes.Select(c => c.Result.Resource).ToHashSet();
            foreach (var p in db.Machines.Cast<PlaceableDef>().Concat(db.Transmitters))
                if (!db.PrePlacedMachines.Contains(p as MachineDef))
                    Assert.IsTrue(crafted.Contains(p.Item), $"{p.Id} n'a pas de recette d'établi");
            foreach (var u in db.Upgrades)
                Assert.IsTrue(crafted.Contains(u.Item), $"{u.Id} n'a pas de recette d'établi");
        }
    }
}
