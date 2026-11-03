using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using Nucleaire.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Nucleaire.Tests
{
    /// <summary>Équilibrage : un « joueur scripté » remonte l'arbre de chaque objectif en faisant réellement
    /// tourner les machines de la simulation, et mesure le temps et l'énergie nécessaires.</summary>
    public class BalanceTests : SimTestBase
    {
        private sealed class ChainRunner
        {
            private readonly BalanceTests t;
            private readonly Dictionary<MachineDef, Machine> machines = new();
            private readonly Dictionary<ResourceDef, long> stock = new();
            private int nextX = 1, nextMultiblockX = 1;

            public long Energy, Ticks;
            public readonly Dictionary<string, int> Runs = new();

            public ChainRunner(BalanceTests t) { this.t = t; }

            private Machine For(MachineDef def)
            {
                if (machines.TryGetValue(def, out var m)) return m;
                if (def.IsMultiblockController)
                {
                    // Multi-bloc minimal : 3×3×3 avec un composant interne.
                    string inside = def.Behaviour == MachineBehaviour.Reactor ? "fuel_assembly" : "sps_coil";
                    m = t.BuildMultiblock<Machine>(def.Id, new Vector3Int(nextMultiblockX, 12, 0), new Vector3Int(3, 3, 3), inside, 1);
                    nextMultiblockX += 4;
                    return machines[def] = m;
                }
                m = (Machine)t.World.Place(def, new Vector3Int(nextX, 1, 0));
                nextX += 2; // une case libre entre deux machines : aucun échange automatique
                foreach (var d in DirectionExtensions.All) m.Sides.SetMode(d, SideMode.None);
                return machines[def] = m;
            }

            private long Have(ResourceDef r) => stock.TryGetValue(r, out var n) ? n : 0;
            private void Add(ResourceDef r, long n) => stock[r] = Have(r) + n;

            public void Ensure(ResourceDef r, long amount)
            {
                int guard = 0;
                while (Have(r) < amount)
                {
                    Assert.Less(++guard, 10000, $"Boucle infinie pour {r.Id}");
                    if (r.IsBaseResource) { Add(r, amount - Have(r)); return; }
                    ProduceOnce(r);
                }
            }

            private void Take(ResourceDef r, long n) { Ensure(r, n); stock[r] -= n; }

            private void ProduceOnce(ResourceDef r)
            {
                var entry = t.Db.RecipesProducing(r)
                    .Where(e => e.Source != RecipeSource.Vault)
                    .OrderBy(e => e.Inputs.Count(i => !i.Resource.IsBaseResource)).ThenBy(e => e.Inputs.Count)
                    .FirstOrDefault();
                Assert.IsNotNull(entry, $"Aucune recette pour {r.Id}");
                string key = entry.Origin.name;
                Runs[key] = (Runs.TryGetValue(key, out var k) ? k : 0) + 1;

                switch (entry.Source)
                {
                    case RecipeSource.Workbench:
                        foreach (var i in entry.Inputs) Take(i.Resource, i.Amount);
                        foreach (var o in entry.Outputs) Add(o.Resource, o.Amount);
                        break;
                    case RecipeSource.Reactor:
                        RunReactor((ReactorMachine)For(entry.Machine), 100);
                        break;
                    default:
                        RunProcess((ProcessRecipe)entry.Origin);
                        break;
                }
            }

            private void RunProcess(ProcessRecipe p)
            {
                if (For(p.Machine) is SpsMachine sps) { RunSps(sps, p); return; }
                var m = (ProcessorMachine)For(p.Machine);
                foreach (var i in p.Inputs) Ensure(i.Resource, i.Amount); // peut réutiliser cette même machine
                m.LockRecipe(p);
                foreach (var i in p.Inputs) { Take(i.Resource, i.Amount); m.InsertManual(i.Resource, i.Amount); }
                var watch = p.Outputs.First(o => o.Resource != t.Db.Energy).Resource;
                long before = m.Outputs.Amount(watch);
                for (int tick = 0; m.Outputs.Amount(watch) == before; tick++)
                {
                    Assert.Less(tick, 100000, $"{p.Id} ne se termine pas : {m.Status} {m.StatusDetail}");
                    int top = m.EnergyCapacity - m.Energy;
                    Energy += m.InsertManual(t.Db.Energy, top);
                    t.World.Step();
                    Ticks++;
                }
                foreach (var (r, n) in m.Outputs.Contents.ToList()) { m.Outputs.Extract(r, n); Add(r, n); }
            }

            private void RunSps(SpsMachine sps, ProcessRecipe p)
            {
                foreach (var i in p.Inputs) Ensure(i.Resource, i.Amount);
                foreach (var i in p.Inputs) { Take(i.Resource, i.Amount); sps.InsertManual(i.Resource, i.Amount); }
                var watch = p.Outputs[0].Resource;
                long before = sps.Outputs.Amount(watch);
                for (int tick = 0; sps.Outputs.Amount(watch) == before; tick++)
                {
                    Assert.Less(tick, 100000, $"{p.Id} ne se termine pas : {sps.Status} {sps.StatusDetail}");
                    Energy += sps.InsertManual(t.Db.Energy, sps.EnergyCapacity - sps.Energy);
                    t.World.Step();
                    Ticks++;
                }
                foreach (var (r, n) in sps.Outputs.Contents.ToList()) { sps.Outputs.Extract(r, n); Add(r, n); }
            }

            private void RunReactor(ReactorMachine reactor, int fuel)
            {
                var s = reactor.Settings;
                Take(s.fuel, fuel);
                reactor.InsertManual(s.fuel, fuel);
                reactor.SetBurnRate(5);
                for (int tick = 0; reactor.Inputs.Amount(s.fuel) > 0; tick++)
                {
                    Assert.Less(tick, 10000, "Le réacteur ne brûle pas son combustible");
                    Energy += reactor.InsertManual(t.Db.Energy, reactor.EnergyCapacity - reactor.Energy);
                    int water = reactor.BurnRate * reactor.CoolantPerFuel;
                    Take(s.coolant, water);
                    reactor.InsertManual(s.coolant, water);
                    t.World.Step();
                    Ticks++;
                    Assert.IsFalse(reactor.IsScrammed, "Le réacteur ne doit pas surchauffer avec assez d'eau");
                }
                reactor.SetBurnRate(0);
                foreach (var (r, n) in reactor.Outputs.Contents.ToList()) { reactor.Outputs.Extract(r, n); Add(r, n); }
                Add(s.coolant, reactor.Inputs.Extract(s.coolant, reactor.Inputs.Amount(s.coolant)));
            }
        }

        internal T BuildMultiblock<T>(string id, Vector3Int min, Vector3Int size, string inside, int count) where T : Machine =>
            base.BuildMultiblock<T>(id, min, size, inside, count);

        private (long energy, long ticks, Dictionary<string, int> runs) Measure(ObjectiveDef o)
        {
            BaseSetUp(); // atelier neuf pour chaque mesure
            var runner = new ChainRunner(this);
            runner.Ensure(o.Target.Resource, o.Target.Amount);
            return (runner.Energy, runner.Ticks, runner.Runs);
        }

        [Test]
        public void EveryObjective_CanBeProduced_WithIncreasingCost()
        {
            var results = Db.Objectives.Select(o => (o, m: Measure(o))).ToList();
            foreach (var (o, m) in results)
                TestContext.WriteLine($"{o.Title} : {m.ticks} ticks ({m.ticks / 10f / 60f:0.#} min de machine), {m.energy / 1000f:0.#} kJ, " +
                                      $"{m.runs.Values.Sum()} opérations — " + string.Join(", ", m.runs.Select(kv => $"{kv.Key}×{kv.Value}")));

            for (int i = 1; i < results.Count; i++)
            {
                Assert.Greater(results[i].m.energy, results[i - 1].m.energy, "L'énergie nécessaire doit croître d'un objectif à l'autre");
                Assert.Greater(results[i].m.ticks, results[i - 1].m.ticks, "Le temps nécessaire doit croître d'un objectif à l'autre");
            }
        }

        // ---------------- Progression vers l'objectif ----------------

        [Test]
        public void GoalProgress_CoversTheWholeTree()
        {
            var goal = Session.Goal;
            Assert.IsNotNull(goal);
            var ids = goal.Steps.Select(s => s.Id).ToList();
            foreach (var expected in new[] { "fissile_fuel", "nuclear_waste", "polonium", "antimatter", "item_fission_reactor", "item_supercritical_shifter" })
                CollectionAssert.Contains(ids, expected);
            CollectionAssert.DoesNotContain(ids, "iron_ore", "Les ressources de base ne sont pas des étapes");
            Assert.AreEqual("antimatter_pellet", ids.Last(), "L'objectif est la dernière étape");
            Assert.Less(goal.Ratio, 0.2f);
            Assert.IsNotEmpty(goal.Frontier.ToList(), "Il y a toujours une prochaine étape suggérée");
        }

        [Test]
        public void GoalProgress_IncreasesWhenProducing()
        {
            var goal = Session.Goal;
            int before = goal.Done;
            World.RecordProduction(R("steel_ingot"), 1);
            Assert.AreEqual(before + 1, goal.Done);
            Assert.IsFalse(goal.Frontier.Contains(R("steel_ingot")));
        }
    }
}
