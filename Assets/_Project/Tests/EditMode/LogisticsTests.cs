using Nucleaire.Logistics;
using Nucleaire.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Nucleaire.Tests
{
    public class LogisticsTests : SimTestBase
    {
        [Test]
        public void Solar_PowersFurnace_ThroughCables()
        {
            Place<ProcessorMachine>("solar_panel", 1, 1);          // toutes faces en sortie par défaut
            Line("cable", 2, 4, 1);
            var furnace = Place<ProcessorMachine>("electric_furnace", 5, 1); // toutes faces en entrée par défaut
            furnace.InsertManual(R("sand"), 1);
            World.Step(80);
            Assert.AreEqual(1, furnace.Outputs.Amount(R("glass")));
        }

        [Test]
        public void DirectAdjacency_PushesIntoNeighbour()
        {
            var crusher = Place<ProcessorMachine>("crusher", 1, 1);
            var furnace = Place<ProcessorMachine>("electric_furnace", 2, 1);
            crusher.Sides.SetMode(Direction.East, SideMode.Output);
            Power(crusher, 1000);
            Power(furnace, 2000);
            crusher.InsertManual(R("iron_ore"), 1);
            World.Step(70);
            Assert.AreEqual(2, furnace.Outputs.Amount(R("iron_ingot")), "1 minerai broyé = 2 poudres = 2 lingots");
        }

        [Test]
        public void OutputSide_FacingInputOnly_RespectsTargetSides()
        {
            var crusher = Place<ProcessorMachine>("crusher", 1, 1);
            var furnace = Place<ProcessorMachine>("electric_furnace", 2, 1);
            crusher.Sides.SetMode(Direction.East, SideMode.Output);
            furnace.Sides.SetMode(Direction.West, SideMode.None);
            Power(crusher, 1000);
            crusher.InsertManual(R("iron_ore"), 1);
            World.Step(30);
            Assert.AreEqual(2, crusher.Outputs.Amount(R("iron_dust")), "Face fermée : rien ne passe");
        }

        [Test]
        public void Vault_FilteredOutput_FeedsConveyor()
        {
            var vault = World.FirstMachine<VaultMachine>();
            var p = vault.Position;
            vault.Sides.Set(Direction.North, SideMode.Output, R("iron_ore"));
            World.Place(T("conveyor"), p + new Vector3Int(0, 1, 0));
            var crusher = (ProcessorMachine)World.Place(M("crusher"), p + new Vector3Int(0, 2, 0));
            World.Step(5);
            Assert.Greater(crusher.Inputs.Amount(R("iron_ore")), 0);
            Assert.AreEqual(0, crusher.Inputs.Amount(R("coal")));
        }

        [Test]
        public void Vault_UnfilteredOutput_PushesNothing()
        {
            var vault = World.FirstMachine<VaultMachine>();
            var p = vault.Position;
            vault.Sides.Set(Direction.North, SideMode.Output);
            World.Place(T("conveyor"), p + new Vector3Int(0, 1, 0));
            World.Step(5);
            Assert.AreEqual(0, World.Logistics.NetworkAt(p + new Vector3Int(0, 1, 0)).Stored);
        }

        [Test]
        public void Networks_MergeAndSplit()
        {
            Line("pipe", 1, 3, 1);
            Assert.AreEqual(1, World.Logistics.Networks.Count);
            var net = World.Logistics.NetworkAt(new Vector3Int(1, 1, 0));
            Assert.AreEqual(3, net.Segments.Count);
            net.Insert(R("water"), 300);

            World.Remove(new Vector3Int(2, 1, 0));
            Assert.AreEqual(2, World.Logistics.Networks.Count);
            Assert.AreEqual(100, World.Logistics.NetworkAt(new Vector3Int(1, 1, 0)).Stored);
            Assert.AreEqual(100, World.Logistics.NetworkAt(new Vector3Int(3, 1, 0)).Stored);

            World.Place(T("pipe"), new Vector3Int(2, 1, 0));
            Assert.AreEqual(1, World.Logistics.Networks.Count);
            Assert.AreEqual(200, World.Logistics.NetworkAt(new Vector3Int(2, 1, 0)).Stored);
        }

        [Test]
        public void DifferentTransmitters_DoNotConnect()
        {
            World.Place(T("pipe"), new Vector3Int(1, 1, 0));
            World.Place(T("pressurized_tube"), new Vector3Int(2, 1, 0));
            Assert.AreEqual(2, World.Logistics.Networks.Count);
        }

        [Test]
        public void FluidNetwork_CarriesOneFluidAtATime()
        {
            Line("pressurized_tube", 1, 2, 1);
            var net = World.Logistics.NetworkAt(new Vector3Int(1, 1, 0));
            Assert.Greater(net.Insert(R("hydrogen"), 100), 0);
            Assert.AreEqual(0, net.Insert(R("oxygen"), 100));
            Assert.AreEqual(0, net.Insert(R("water"), 100), "Un tube à gaz ne transporte pas de liquide");
        }

        [Test]
        public void ItemNetwork_AllowsMixing()
        {
            Line("conveyor", 1, 3, 1);
            var net = World.Logistics.NetworkAt(new Vector3Int(1, 1, 0));
            Assert.AreEqual(1, net.Insert(R("iron_ore"), 1));
            Assert.AreEqual(1, net.Insert(R("coal"), 1));
        }

        [Test]
        public void Network_ThroughputIsLimited()
        {
            var furnace = Place<ProcessorMachine>("electric_furnace", 4, 1);
            Line("conveyor", 1, 3, 1);
            var net = World.Logistics.NetworkAt(new Vector3Int(3, 1, 0));
            net.Insert(R("sand"), 4);
            World.Step(1);
            Assert.AreEqual(4 - T("conveyor").ThroughputPerTick, net.Stored, "Le réseau ne livre que son débit par tick");
        }
    }
}
