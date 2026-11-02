using System.Linq;
using Nucleaire.Data;
using Nucleaire.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Nucleaire.Tests
{
    public class GameplayTests : SimTestBase
    {
        [Test]
        public void Session_StartsWithFixedMachinesAndKit()
        {
            Assert.IsNotNull(World.FirstMachine<VaultMachine>());
            foreach (var s in Db.StartingInventory) Assert.AreEqual(s.Amount, Session.Inventory.Count(s.Resource));
        }

        [Test]
        public void Workbench_CraftsAndExplainsMissing()
        {
            var circuit = Db.CraftingRecipes.First(c => c.Result.Resource.Id == "circuit");
            Assert.IsFalse(Session.Workbench.CanCraft(circuit, out var reason));
            StringAssert.Contains("Lingot de cuivre", reason);

            Session.Inventory.Add(R("copper_ingot"), 4);
            Session.Inventory.Add(R("iron_ingot"), 1);
            Assert.AreEqual(1, Session.Workbench.Craft(circuit, 5), "Une seule fabrication possible avec 1 lingot de fer");
            Assert.AreEqual(1, Session.Inventory.Count(R("circuit")));
            Assert.AreEqual(2, Session.Inventory.Count(R("copper_ingot")));
        }

        [Test]
        public void Place_ConsumesItem_PickUp_ReturnsItemAndContents()
        {
            var item = R("item_crusher");
            Assert.IsTrue(Session.Actions.TryPlace(item, new Vector3Int(1, 1, 0), out _));
            Assert.AreEqual(0, Session.Inventory.Count(item));
            Assert.IsFalse(Session.Actions.TryPlace(item, new Vector3Int(2, 1, 0), out var err));
            Assert.IsNotNull(err);

            var crusher = (Machine)World.At(new Vector3Int(1, 1, 0));
            crusher.InsertManual(R("iron_ore"), 3);
            Assert.IsTrue(Session.Actions.TryPickUp(new Vector3Int(1, 1, 0), out _));
            Assert.AreEqual(1, Session.Inventory.Count(item));
            Assert.AreEqual(3, Session.Inventory.Count(R("iron_ore")));
            Assert.IsNull(World.At(new Vector3Int(1, 1, 0)));
        }

        [Test]
        public void Vault_CanBeMovedAndKeepsItsSides()
        {
            var vault = World.FirstMachine<VaultMachine>();
            vault.Sides.Set(Direction.East, SideMode.Output, R("iron_ore"));
            var target = vault.Position + new Vector3Int(3, 0, 0);
            Assert.IsTrue(Session.Actions.TryMove(vault.Position, target, out var err), err);
            Assert.AreSame(vault, World.At(target));
            Assert.AreEqual(R("iron_ore"), vault.Sides[Direction.East].Filter);
        }

        [Test]
        public void Move_ToOccupiedOrOutOfBounds_IsRefused()
        {
            var furnace = Place<ProcessorMachine>("electric_furnace", 1, 1);
            Place<ProcessorMachine>("crusher", 2, 1);
            Assert.IsFalse(Session.Actions.TryMove(furnace.Position, new Vector3Int(2, 1, 0), out _));
            Assert.IsFalse(Session.Actions.TryMove(furnace.Position, new Vector3Int(1, 1, Settings.levels), out _));
            Assert.IsTrue(Session.Actions.TryMove(furnace.Position, new Vector3Int(1, 1, 1), out _), "On peut empiler en hauteur");
        }

        [Test]
        public void VerticalSides_TransferBetweenLevels()
        {
            var crusher = Place<ProcessorMachine>("crusher", 1, 1);
            var furnace = (ProcessorMachine)World.Place(M("electric_furnace"), new Vector3Int(1, 1, 1));
            foreach (var d in DirectionExtensions.All) crusher.Sides.SetMode(d, SideMode.None);
            crusher.Sides.SetMode(Direction.Up, SideMode.Output);
            Power(crusher, 1000);
            Power(furnace, 2000);
            crusher.InsertManual(R("iron_ore"), 1);
            World.Step(70);
            Assert.AreEqual(2, furnace.Outputs.Amount(R("iron_ingot")), "Le broyeur pousse vers le four au-dessus de lui");
        }

        [Test]
        public void Slots_OneResourcePerSlot_LikeAFurnace()
        {
            var furnace = Place<ProcessorMachine>("electric_furnace", 1, 1);
            Assert.AreEqual(2, furnace.Inputs.Slots.Count, "Le four a deux emplacements d'objet (acier : fer + charbon)");
            Assert.AreEqual(10, furnace.InsertManual(R("iron_ore"), 10));
            Assert.AreEqual(5, furnace.InsertManual(R("sand"), 5));
            Assert.AreEqual(0, furnace.InsertManual(R("coal"), 1), "Plus d'emplacement libre pour un troisième objet");
            var electrolyser = Place<ProcessorMachine>("electrolyser", 3, 1);
            Assert.AreEqual(ResourceKind.Fluid, electrolyser.Inputs.Slots.Single().Kind);
            Assert.AreEqual(2, electrolyser.Outputs.Slots.Count(s => s.Kind == ResourceKind.Gas));
        }

        [Test]
        public void Place_OutOfBoundsOrOccupied_IsRefused()
        {
            Assert.IsFalse(Session.Actions.TryPlace(R("item_universal_cable"), new Vector3Int(-1, 0, 0), out _));
            var vault = World.FirstMachine<VaultMachine>();
            Assert.IsFalse(Session.Actions.TryPlace(R("item_universal_cable"), vault.Position, out _));
            Assert.AreEqual(32, Session.Inventory.Count(R("item_universal_cable")));
        }

        [Test]
        public void Vault_ManualTake_IsUnlimitedForBaseOnly()
        {
            var vault = World.FirstMachine<VaultMachine>();
            Assert.AreEqual(500, Session.Actions.TakeFromVault(vault, R("iron_ore"), 500));
            Assert.AreEqual(0, Session.Actions.TakeFromVault(vault, R("iron_ingot"), 1));
        }

        [Test]
        public void Upgrades_SpeedUpAndCostMore_AndRespectSlots()
        {
            var furnace = Place<ProcessorMachine>("electric_furnace", 1, 1);
            var smelt = furnace.Recipes.First(r => r.Id == "smelt_glass");
            int baseCost = furnace.EnergyCostPerTick(smelt);

            Session.Inventory.Add(R("item_upgrade_speed"), 3);
            Assert.IsTrue(Session.Actions.TryInstallUpgrade(furnace, R("item_upgrade_speed"), out _));
            Assert.Greater(furnace.SpeedMultiplier, 1f);
            Assert.Greater(furnace.EnergyCostPerTick(smelt), baseCost);

            Assert.IsTrue(Session.Actions.TryInstallUpgrade(furnace, R("item_upgrade_speed"), out _));
            Assert.IsFalse(Session.Actions.TryInstallUpgrade(furnace, R("item_upgrade_speed"), out var err));
            Assert.IsNotNull(err);
            Assert.AreEqual(1, Session.Inventory.Count(R("item_upgrade_speed")));

            Assert.IsTrue(Session.Actions.RemoveUpgrade(furnace, U("upgrade_speed")));
            Assert.AreEqual(2, Session.Inventory.Count(R("item_upgrade_speed")));
        }

        [Test]
        public void Upgrade_Incompatible_IsRefused()
        {
            var furnace = Place<ProcessorMachine>("electric_furnace", 1, 1);
            Session.Inventory.Add(R("item_upgrade_heat_exchanger"), 1);
            Assert.IsFalse(Session.Actions.TryInstallUpgrade(furnace, R("item_upgrade_heat_exchanger"), out var err));
            StringAssert.Contains("compatible", err);
        }

        [Test]
        public void Objective_CompletesWhenProduced()
        {
            var steelObjective = Session.Objectives.Objectives[0];
            bool completed = false;
            Session.Objectives.Completed += o => completed |= o == steelObjective;

            var furnace = Place<ProcessorMachine>("electric_furnace", 1, 1);
            furnace.InsertManual(R("iron_ingot"), 10);
            furnace.InsertManual(R("coal"), 10);
            for (int i = 0; i < 400 && !completed; i++) { Power(furnace, 1000); World.Step(); }
            Assert.IsTrue(completed);
            Assert.IsTrue(Session.Objectives.IsCompleted(steelObjective));
            Assert.AreEqual(Session.Objectives.Objectives[1], Session.Objectives.Current);
        }

        [Test]
        public void Vent_DestroysGas_WithEnergy()
        {
            var vent = Place<VentMachine>("vent", 1, 1);
            vent.InsertManual(R("oxygen"), 1000);
            Assert.AreEqual(0, vent.InsertManual(R("iron_ore"), 1), "Un évent ne prend pas de solides");
            World.Step(5);
            Assert.AreEqual(MachineStatus.NoPower, vent.Status);
            Power(vent, 1000);
            World.Step(5);
            Assert.Less(vent.Inputs.Amount(R("oxygen")), 1000);
        }

        [Test]
        public void Battery_StoresAndReleases()
        {
            var battery = Place<BatteryMachine>("battery", 2, 1);
            Place<ProcessorMachine>("solar_panel", 1, 1);
            World.Step(50);
            Assert.Greater(battery.Energy, 0);

            battery.Sides.SetMode(Direction.East, SideMode.Output);
            var furnace = Place<ProcessorMachine>("electric_furnace", 3, 1);
            World.Step(1);
            Assert.Greater(furnace.Energy, 0);
        }

        [Test]
        public void Machine_KeepsChosenFacing_WhenMoved()
        {
            Session.Inventory.Add(R("item_electric_furnace"), 1);
            Assert.IsTrue(Session.Actions.TryPlace(R("item_electric_furnace"), new Vector3Int(2, 2, 0), out var err, null, Simulation.Direction.East), err);
            var m = (Simulation.Machine)World.At(new Vector3Int(2, 2, 0));
            Assert.AreEqual(Simulation.Direction.East, m.Facing);
            Assert.IsTrue(Session.Actions.TryMove(m.Position, new Vector3Int(4, 2, 0), out err), err);
            Assert.AreEqual(Simulation.Direction.East, m.Facing);
        }
    }
}