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
            private int nextX = 1;

            public long Energy, Ticks;
            public readonly Dictionary<string, int> Runs = new();

            public ChainRunner(BalanceTests t) { this.t = t; }

            private Machine For(MachineDef def)
            {
                if (machines.TryGetValue(def, out var m)) return m;
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

            private void RunReactor(ReactorMachine reactor, int fuel)
            {
                var s = reactor.Settings;
                Take(s.fuel, fuel);
                reactor.InsertManual(s.fuel, fuel);
                reactor.SetBurnRate(5);
                for (int tick = 0; reactor.Inputs.Amount(s.fuel) > 0; tick++)
                {
                    Assert.Less(tick, 10000, "Le réacteur ne brûle pas son combustible");
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

        // ---------------- L'énigme de l'énergie ----------------

        [Test]
        public void Shifter_RequiresFullPower()
        {
            var shifter = Place<ProcessorMachine>("supercritical_shifter", 1, 1);
            shifter.InsertManual(R("polonium"), 50);
            Power(shifter, 300); // la moitié de ce qu'il faut pour un tick
            World.Step(20);
            Assert.AreEqual(MachineStatus.NoPower, shifter.Status);
            Assert.AreEqual(0f, shifter.Progress, 1e-4f, "Sans pleine puissance, aucune progression");
            Assert.AreEqual(300, shifter.Energy, "L'énergie n'est pas gaspillée");
        }

        [Test]
        public void SolarAlone_CannotRunTheShifter()
        {
            var shifter = Place<ProcessorMachine>("supercritical_shifter", 6, 1);
            shifter.InsertManual(R("polonium"), 50);
            for (int x = 1; x <= 4; x++) Place<ProcessorMachine>("solar_panel", x, 2);
            Line("cable", 1, 5, 1);
            World.Step(300);
            Assert.AreEqual(0, World.Produced(R("antimatter")), "4 panneaux solaires ne suffisent pas au déphaseur");
        }

        /// <summary>Solution de l'énigme : pompes → réacteur → vapeur → turbines → bus d'énergie → déphaseur (et pompes).</summary>
        [Test]
        public void ReactorTurbineLoop_PowersTheShifter()
        {
            //  y=5 : tuyau d'eau x=3..6 | réacteur x=7 | tube de vapeur x=8..12
            //  y=4 : pompes x=3..6       |              | turbines x=8..11, déphaseur x=13
            //  y=3 : bus de câbles x=2..14
            Line("cable", 2, 14, 3);
            for (int x = 3; x <= 6; x++)
            {
                var pump = Place<ProcessorMachine>("electric_pump", x, 4);
                foreach (var d in DirectionExtensions.All) pump.Sides.SetMode(d, SideMode.None);
                pump.Sides.SetMode(Direction.South, SideMode.Input);
                pump.Sides.SetMode(Direction.North, SideMode.Output);
                Power(pump, 2000); // amorçage : les pompes doivent tourner avant le réacteur
            }
            Line("pipe", 3, 6, 5);

            var reactor = Place<ReactorMachine>("fission_reactor", 7, 5);
            foreach (var d in DirectionExtensions.All) reactor.Sides.SetMode(d, SideMode.None);
            reactor.Sides.SetMode(Direction.West, SideMode.Input);
            reactor.Sides.Set(Direction.East, SideMode.Output, R("steam"));
            reactor.InsertManual(R("fissile_fuel"), 8000);
            Line("pressurized_tube", 8, 12, 5);

            for (int x = 8; x <= 11; x++)
            {
                var turbine = Place<ProcessorMachine>("turbine", x, 4);
                foreach (var d in DirectionExtensions.All) turbine.Sides.SetMode(d, SideMode.None);
                turbine.Sides.SetMode(Direction.North, SideMode.Input);
                turbine.Sides.SetMode(Direction.South, SideMode.Output);
            }

            var shifter = Place<ProcessorMachine>("supercritical_shifter", 13, 4);
            foreach (var d in DirectionExtensions.All) shifter.Sides.SetMode(d, SideMode.None);
            shifter.Sides.SetMode(Direction.South, SideMode.Input);
            shifter.InsertManual(R("polonium"), 200);

            World.Step(20);                  // l'eau arrive
            reactor.SetBurnRate(7);
            World.Step(600);

            TestContext.WriteLine($"Réacteur : {reactor.Status} {reactor.Heat:0}°, refroidi à {reactor.LastCooling:P0}. " +
                                  $"Déphaseur : {shifter.Status} {shifter.StatusDetail}. Antimatière : {World.Produced(R("antimatter"))}");
            Assert.IsFalse(reactor.IsScrammed, "Le réacteur doit rester refroidi par les pompes");
            Assert.Less(reactor.Heat, reactor.Settings.meltdownHeat * Settings.reactorWarningRatio);
            Assert.GreaterOrEqual(World.Produced(R("antimatter")), 20, "La boucle réacteur/turbines doit alimenter le déphaseur");
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
