using System.Collections.Generic;
using Nucleaire.Data;
using NUnit.Framework;
using UnityEngine;

namespace Nucleaire.Tests
{
    /// <summary>Teste le validateur sur des bases construites en mémoire (cas limites).</summary>
    public class RecipeValidatorTests
    {
        private readonly List<Object> created = new();

        private T New<T>(string id) where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            o.name = id;
            created.Add(o);
            return o;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        private ResourceDef Res(string id, ResourceKind k = ResourceKind.Item, bool isBase = false)
        {
            var r = New<ResourceDef>(id);
            r.EditorInit(id, k, Color.white, isBase, "");
            return r;
        }

        private MachineDef Machine(string id, MachineBehaviour b, ResourceDef item)
        {
            var m = New<MachineDef>(id);
            m.EditorInitPlaceable(id, "", item, Color.gray);
            m.EditorInitMachine(b, 1000, 64, 4000, 2);
            return m;
        }

        private RecipeDatabase MiniDatabase(out ResourceDef ore, out ResourceDef ingot, out MachineDef furnace, out ResourceDef energy,
            out List<ProcessRecipe> processes, out List<MachineDef> machines, out List<ResourceDef> resources)
        {
            energy = Res("energy", ResourceKind.Energy);
            ore = Res("ore", ResourceKind.Item, isBase: true);
            ingot = Res("ingot");
            var vaultItem = Res("item_vault"); var furnaceItem = Res("item_furnace"); var solarItem = Res("item_solar");
            var vault = Machine("vault", MachineBehaviour.Vault, vaultItem);
            furnace = Machine("furnace", MachineBehaviour.Processor, furnaceItem);
            var solar = Machine("solar", MachineBehaviour.Processor, solarItem);

            var solarGen = New<ProcessRecipe>("solar_gen");
            solarGen.EditorInit("Solaire", solar, new List<ResourceStack>(), new List<ResourceStack> { new(energy, 5) }, 1, 0);
            var smelt = New<ProcessRecipe>("smelt");
            smelt.EditorInit("Fonte", furnace, new List<ResourceStack> { new(ore, 1) }, new List<ResourceStack> { new(ingot, 1) }, 10, 5);

            processes = new List<ProcessRecipe> { solarGen, smelt };
            machines = new List<MachineDef> { vault, furnace, solar };
            resources = new List<ResourceDef> { energy, ore, ingot, vaultItem, furnaceItem, solarItem };

            var obj = New<ObjectiveDef>("obj");
            obj.EditorInit("Lingot", "", new ResourceStack(ingot, 1), 1, "");

            var db = New<RecipeDatabase>("db");
            db.EditorSetContent(resources, machines, new List<TransmitterDef>(), processes, new List<CraftingRecipe>(),
                new List<UpgradeDef>(), new List<ObjectiveDef> { obj });
            db.EditorSetGame(energy, new List<MachineDef> { vault },
                new List<ResourceStack> { new(furnaceItem, 1), new(solarItem, 1) });
            return db;
        }

        [Test]
        public void ValidTree_ObjectiveReachable()
        {
            var db = MiniDatabase(out _, out var ingot, out _, out _, out _, out _, out _);
            var report = RecipeDatabaseValidator.Validate(db);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.IsTrue(report.Reachable.Contains(ingot));
        }

        [Test]
        public void MissingMachine_MakesObjectiveUnreachable()
        {
            var db = MiniDatabase(out _, out _, out _, out var energy, out _, out var machines, out _);
            // Plus de four dans l'inventaire de départ et aucune recette pour en fabriquer un.
            db.EditorSetGame(energy, new List<MachineDef> { machines[0] }, new List<ResourceStack>());
            var report = RecipeDatabaseValidator.Validate(db);
            Assert.IsFalse(report.IsValid);
            StringAssert.Contains("inatteignable", report.ToString());
        }

        [Test]
        public void NoEnergySource_BlocksPoweredProcess()
        {
            var db = MiniDatabase(out _, out var ingot, out _, out var energy, out _, out var machines, out var resources);
            var furnaceItem = resources[4];
            db.EditorSetGame(energy, new List<MachineDef> { machines[0] }, new List<ResourceStack> { new(furnaceItem, 1) });
            var report = RecipeDatabaseValidator.Validate(db);
            Assert.IsFalse(report.Reachable.Contains(ingot));
        }

        [Test]
        public void EnergyAsIngredient_IsRejected()
        {
            var db = MiniDatabase(out var ore, out var ingot, out var furnace, out var energy, out var processes, out _, out _);
            var bad = New<ProcessRecipe>("bad");
            bad.EditorInit("Mauvais", furnace, new List<ResourceStack> { new(energy, 10) }, new List<ResourceStack> { new(ingot, 1) }, 1, 0);
            processes.Add(bad);
            db.InvalidateIndex();
            var report = RecipeDatabaseValidator.Validate(db);
            Assert.IsFalse(report.IsValid);
        }

        [Test]
        public void ProcessWithoutMachine_IsRejected()
        {
            var db = MiniDatabase(out var ore, out var ingot, out _, out _, out var processes, out _, out _);
            var orphan = New<ProcessRecipe>("orphan");
            orphan.EditorInit("Orphelin", null, new List<ResourceStack> { new(ore, 1) }, new List<ResourceStack> { new(ingot, 1) }, 1, 0);
            processes.Add(orphan);
            db.InvalidateIndex();
            Assert.IsFalse(RecipeDatabaseValidator.Validate(db).IsValid);
        }

        [Test]
        public void RecipeBook_IndexesVaultAndProcesses()
        {
            var db = MiniDatabase(out var ore, out var ingot, out _, out _, out _, out _, out _);
            Assert.AreEqual(RecipeSource.Vault, db.RecipesProducing(ore)[0].Source);
            Assert.AreEqual(1, db.RecipesProducing(ingot).Count);
            Assert.AreEqual(1, db.RecipesUsing(ore).Count);
        }
    }
}
