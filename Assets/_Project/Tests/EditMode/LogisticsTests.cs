using System.Linq;
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
        public void Sorter_OnVaultFace_PushesItsListIntoConveyor()
        {
            var vault = World.FirstMachine<VaultMachine>();
            var p = vault.Position;
            var sorter = vault.AttachSorter(Direction.North);
            sorter.AddFilter(R("iron_ore"));
            sorter.AddFilter(R("coal"));
            World.Place(T("conveyor"), p + new Vector3Int(0, 1, 0));
            var furnace = (ProcessorMachine)World.Place(M("electric_furnace"), p + new Vector3Int(0, 2, 0));
            World.Step(8);
            Assert.Greater(furnace.Inputs.Amount(R("iron_ore")) + furnace.Inputs.Amount(R("coal")), 0);
            Assert.AreEqual(0, furnace.Inputs.Amount(R("copper_ore")), "Le cuivre n'est pas dans la liste");
            Assert.IsNull(World.At(p + new Vector3Int(0, 0, 0)) as Logistics.TransmitterSegment, "Le trieur ne prend pas de case");
        }

        [Test]
        public void Sorter_TakesOnlyItsList_FromAMachine()
        {
            var crusher = Place<ProcessorMachine>("crusher", 2, 2);
            crusher.Outputs.Insert(R("iron_dust"), 4);
            var sorter = crusher.AttachSorter(Direction.East);
            sorter.AddFilter(R("copper_dust"));
            World.Place(T("conveyor"), new Vector3Int(3, 2, 0));
            World.Step(3);
            Assert.AreEqual(4, crusher.Outputs.Amount(R("iron_dust")), "Le fer n'est pas dans la liste");
            sorter.AddFilter(R("iron_dust"));
            World.Step(1);
            Assert.Less(crusher.Outputs.Amount(R("iron_dust")), 4);
        }

        [Test]
        public void SideSlots_InputOnlyIntoChosenSlot()
        {
            var furnace = Place<ProcessorMachine>("electric_furnace", 2, 2);
            Assert.Greater(furnace.Inputs.Slots.Count, 1);
            furnace.Sides.Set(Direction.West, new SideSetting(SideMode.Input, null, 1));
            Assert.AreEqual(5, furnace.Receive(R("coal"), 5, Direction.West));
            Assert.AreEqual(R("coal"), furnace.Inputs.Slots[1].Resource);
            Assert.IsTrue(furnace.Inputs.Slots[0].IsEmpty);
            var options = furnace.SideOptions();
            Assert.IsTrue(options.Any(o => o.Mode == SideMode.Input && o.Slot == 1));
            Assert.AreEqual("Entrée 2 · charbon", furnace.SideLabel(Direction.West));
        }

        [Test]
        public void CableSides_PullPushAndCut()
        {
            var crusher = Place<ProcessorMachine>("crusher", 1, 1);
            foreach (var d in DirectionExtensions.All) crusher.Sides.SetMode(d, SideMode.None);
            crusher.Outputs.Insert(R("iron_dust"), 6);
            Line("conveyor", 2, 4, 1);
            var first = (Logistics.TransmitterSegment)World.At(new Vector3Int(2, 1, 0));
            Assert.AreEqual(1, World.Logistics.Networks.Count);

            World.Logistics.CycleCableSide(first, Direction.West);       // tirer depuis le broyeur
            Assert.AreEqual(Logistics.CableSide.Pull, first[Direction.West]);
            World.Step(2);
            Assert.Less(crusher.Outputs.Amount(R("iron_dust")), 6, "Le câble tire même si la face est neutre");

            World.Logistics.CycleCableSide(first, Direction.East);       // entre deux câbles : couper
            Assert.AreEqual(2, World.Logistics.Networks.Count);
            var second = (Logistics.TransmitterSegment)World.At(new Vector3Int(3, 1, 0));
            World.Logistics.CycleCableSide(second, Direction.West);      // depuis l'autre côté : relier
            Assert.AreEqual(1, World.Logistics.Networks.Count);
        }

        [Test]
        public void VaultInput_StoresIntoSharedInventory()
        {
            var vault = World.FirstMachine<VaultMachine>();
            var p = vault.Position;
            vault.Sides.SetMode(Direction.East, SideMode.Input);
            var crusher = (ProcessorMachine)World.Place(M("crusher"), p + new Vector3Int(1, 0, 0));
            foreach (var d in DirectionExtensions.All) crusher.Sides.SetMode(d, SideMode.None);
            crusher.Sides.SetMode(Direction.West, SideMode.Output);
            crusher.Outputs.Insert(R("iron_dust"), 3);
            World.Step(3);
            Assert.AreEqual(3, Session.Inventory.Count(R("iron_dust")), "Le vault range dans l'inventaire commun");
        }

        [Test]
        public void SeveralVaults_ShareStorage_AndTheLastOneStays()
        {
            var first = World.FirstMachine<VaultMachine>();
            Assert.IsFalse(Session.Actions.TryPickUp(first.Position, out var err), "Le dernier vault ne se retire pas");
            StringAssert.Contains("au moins un vault", err);
            var second = (VaultMachine)World.Place(M("vault"), first.Position + new Vector3Int(3, 0, 0));
            Session.Inventory.Add(R("iron_ingot"), 5);
            Assert.IsTrue(second.Pullable().Any(x => x.resource == R("iron_ingot") && x.amount == 5));
            Assert.IsTrue(Session.Actions.TryPickUp(first.Position, out err), err);
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
