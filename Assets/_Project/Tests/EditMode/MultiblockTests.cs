using System.Linq;
using Nucleaire.Data;
using Nucleaire.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Nucleaire.Tests
{
    /// <summary>Multi-blocs : forme, ports, et ce que la taille rapporte et coûte.</summary>
    public class MultiblockTests : SimTestBase
    {
        private static readonly Vector3Int O = new(2, 2, 0);

        [Test]
        public void ControllerAlone_ShowsATemplateToBuild()
        {
            var c = (FissionReactor)World.Place(M("fission_reactor"), new Vector3Int(5, 5, 0), Direction.South);
            World.Step();
            Assert.IsFalse(c.Structure.Formed);
            Assert.AreEqual(26 - 1, c.Structure.Missing.Count, "Gabarit 3×3×3 : 26 cases de paroi, moins le contrôleur");
        }

        [Test]
        public void CompleteBox_IsFormed_AndLinksItsPorts()
        {
            var port = O + new Vector3Int(2, 1, 1);
            var c = BuildMultiblock<FissionReactor>("fission_reactor", O, new Vector3Int(3, 3, 3), "fuel_assembly", 1, (port, SideMode.Input, -1));
            Assert.IsTrue(c.Structure.Formed, c.Structure.Problem);
            Assert.AreEqual(1, c.Structure.Count(StructureRole.FuelAssembly));
            Assert.AreSame(c, ((PortMachine)World.At(port)).Controller);
            Assert.AreEqual(2, c.MaxBurnRate);
        }

        [Test]
        public void MissingWall_BreaksTheStructure_AndUnlinksPorts()
        {
            var port = O + new Vector3Int(2, 1, 1);
            var c = BuildMultiblock<FissionReactor>("fission_reactor", O, new Vector3Int(3, 3, 3), "fuel_assembly", 1, (port, SideMode.Input, -1));
            World.Remove(O + new Vector3Int(1, 2, 2)); // un bloc du toit
            World.Step();
            Assert.IsFalse(c.Structure.Formed);
            CollectionAssert.Contains(c.Structure.Missing, O + new Vector3Int(1, 2, 2));
            Assert.IsNull(((PortMachine)World.At(port)).Controller);
        }

        [Test]
        public void Corners_MustBeFrames()
        {
            var c = BuildMultiblock<FissionReactor>("fission_reactor", O, new Vector3Int(3, 3, 3), "fuel_assembly", 1);
            World.Remove(O);
            World.Place(S("structural_glass"), O);
            World.Step();
            Assert.IsFalse(c.Structure.Formed);
            StringAssert.Contains("coins", c.Structure.Problem);
        }

        [Test]
        public void WrongInterior_IsRefused()
        {
            var c = BuildMultiblock<FissionReactor>("fission_reactor", O, new Vector3Int(3, 3, 3), "sps_coil", 1);
            Assert.IsFalse(c.Structure.Formed);
            StringAssert.Contains("intérieur", c.Structure.Problem);
        }

        [Test]
        public void Port_OutputSlot_LetsOnlyThatProductOut()
        {
            var steamPort = O + new Vector3Int(4, 2, 1);
            var c = BuildMultiblock<FissionReactor>("fission_reactor", O, new Vector3Int(5, 5, 3), "fuel_assembly", 9, (steamPort, SideMode.Output, 1));
            Assert.IsTrue(c.Structure.Formed, c.Structure.Problem);
            c.Outputs.Insert(R("steam"), 500);
            c.Outputs.Insert(R("nuclear_waste"), 50);
            var port = (PortMachine)World.At(steamPort);
            var offered = port.Extractable(Direction.East).Select(x => x.resource).ToList();
            CollectionAssert.Contains(offered, R("steam"));
            CollectionAssert.DoesNotContain(offered, R("nuclear_waste"));
        }

        [Test]
        public void Sps_RequiresAllCoilsPower_EveryTick()
        {
            var sps = BuildMultiblock<SpsMachine>("supercritical_shifter", O, new Vector3Int(4, 3, 3), "sps_coil", 2);
            Assert.IsTrue(sps.Structure.Formed, sps.Structure.Problem);
            Assert.AreEqual(2 * sps.Recipe.EnergyPerTick, sps.PowerDemand, "Chaque bobine ajoute sa demande");
            sps.InsertManual(R("polonium"), 50);
            Power(sps, sps.PowerDemand - 1);
            World.Step(5);
            Assert.AreEqual(MachineStatus.NoPower, sps.Status);
            Assert.AreEqual(0f, sps.Progress, 1e-4f);

            for (int i = 0; i < sps.Recipe.DurationTicks; i++) { Power(sps, sps.EnergyCapacity); World.Step(); }
            Assert.Greater(sps.Outputs.Amount(R("antimatter")), 0, "Deux bobines : deux fois plus vite");
        }

        [Test]
        public void Fusion_NeedsAHugeIgnition_ThenProducesFarMoreThanFission()
        {
            var fusion = BuildMultiblock<FusionReactor>("fusion_reactor", O, new Vector3Int(3, 3, 3), "fusion_coil", 1);
            Assert.IsTrue(fusion.Structure.Formed, fusion.Structure.Problem);
            fusion.InsertManual(R("dt_fuel"), 1000);
            Power(fusion, fusion.IgnitionEnergy / 2);
            World.Step(3);
            Assert.IsFalse(fusion.Ignited, "Charge insuffisante");

            Power(fusion, fusion.IgnitionEnergy);
            World.Step();
            Assert.IsTrue(fusion.Ignited);
            int before = fusion.Energy;
            World.Step(10);
            Assert.Greater(fusion.Energy - before, 10 * 4000, "Une bobine rapporte des milliers de J/tick");

            // Une bobine de fusion vaut bien plus qu'un assemblage de fission (≈ 2 mB/tick → 20 vapeur → 200 J/tick).
            var m = M("fusion_reactor").Multiblock;
            Assert.Greater(m.energyPerCoil - m.sustainPerCoil, 10 * 200);
            Assert.Greater(fusion.IgnitionEnergy, 100000, "L'allumage demande une vraie source d'énergie");
        }

        [Test]
        public void Fusion_WithoutFuel_PlasmaGoesOut()
        {
            var fusion = BuildMultiblock<FusionReactor>("fusion_reactor", O, new Vector3Int(3, 3, 3), "fusion_coil", 1);
            fusion.InsertManual(R("dt_fuel"), 1);
            Power(fusion, fusion.EnergyCapacity);
            World.Step(30);
            Assert.IsFalse(fusion.Ignited, "Sans combustible D-T, le plasma s'éteint");
            Assert.AreEqual(MachineStatus.MissingInput, fusion.Status);
        }

        [Test]
        public void InductionMatrix_StoresAndReleases_AtItsRate()
        {
            var inPort = O + new Vector3Int(0, 1, 1);
            var outPort = O + new Vector3Int(2, 1, 1);
            var m = BuildMultiblock<InductionMatrix>("battery", O, new Vector3Int(3, 3, 3), "induction_cell", 1,
                (inPort, SideMode.Input, -1), (outPort, SideMode.Output, -1));
            Assert.IsTrue(m.Structure.Formed, m.Structure.Problem);
            var port = (Machine)World.At(inPort);
            int accepted = port.InsertManual(Db.Energy, 100000);
            Assert.AreEqual(m.Rate, accepted, "Le débit limite l'entrée");
            var furnace = (ProcessorMachine)World.Place(M("electric_furnace"), outPort + new Vector3Int(1, 0, 0));
            furnace.Sides.SetMode(Direction.West, SideMode.Input);
            World.Step(2);
            Assert.Greater(furnace.Energy, 0, "Le port de sortie alimente la machine voisine");
        }

        /// <summary>La boucle de l'énigme : fission → vapeur (port « Sortie 2 ») → turbines → bus → déphaseur à pleine puissance.</summary>
        [Test]
        public void FissionAndTurbines_CanRunASmallSps()
        {
            // Réacteur 5×5×4 : 18 assemblages. Port vapeur (est, étage 2), port énergie (est, étage 1).
            var steamPort = new Vector3Int(5, 3, 1);
            var powerPort = new Vector3Int(5, 2, 0);
            var reactor = BuildMultiblock<FissionReactor>("fission_reactor", new Vector3Int(1, 1, 0), new Vector3Int(5, 5, 4), "fuel_assembly", 18,
                (steamPort, SideMode.Output, 1), (powerPort, SideMode.Input, -1));
            Assert.IsTrue(reactor.Structure.Formed, reactor.Structure.Problem);

            // Vapeur : tube à l'étage 2, turbines dessous qui poussent sur un bus de câbles.
            for (int x = 6; x <= 11; x++) World.Place(T("pressurized_tube"), new Vector3Int(x, 3, 1));
            for (int x = 6; x <= 11; x++)
            {
                var tb = (ProcessorMachine)World.Place(M("turbine"), new Vector3Int(x, 3, 0));
                foreach (var d in DirectionExtensions.All) tb.Sides.SetMode(d, SideMode.None);
                tb.Sides.SetMode(Direction.Up, SideMode.Input);
                tb.Sides.SetMode(Direction.South, SideMode.Output);
            }
            for (int x = 6; x <= 13; x++) World.Place(T("cable"), new Vector3Int(x, 2, 0));

            // Déphaseur 3×3×3, une bobine, port d'énergie au nord-ouest relié au bus.
            var spsPort = new Vector3Int(13, 4, 0);
            World.Place(T("cable"), new Vector3Int(13, 3, 0));
            var sps = BuildMultiblock<SpsMachine>("supercritical_shifter", new Vector3Int(12, 4, 0), new Vector3Int(3, 3, 3), "sps_coil", 1,
                (spsPort, SideMode.Input, -1));
            Assert.IsTrue(sps.Structure.Formed, sps.Structure.Problem);
            sps.InsertManual(R("polonium"), 200);

            reactor.InsertManual(R("fissile_fuel"), 20000);
            Power(reactor, reactor.EnergyCapacity); // amorçage du contrôle
            reactor.SetBurnRate(28);
            for (int i = 0; i < 800; i++)
            {
                reactor.InsertManual(R("water"), reactor.BurnRate * reactor.CoolantPerFuel);
                World.Step();
            }
            TestContext.WriteLine($"Réacteur {reactor.Status} {reactor.StatusDetail} · déphaseur {sps.Status} {sps.StatusDetail} · antimatière {World.Produced(R("antimatter"))}");
            Assert.IsFalse(reactor.IsScrammed);
            Assert.GreaterOrEqual(World.Produced(R("antimatter")), 2, "La boucle fission/turbines tient une bobine de déphaseur");
        }
    }
}
