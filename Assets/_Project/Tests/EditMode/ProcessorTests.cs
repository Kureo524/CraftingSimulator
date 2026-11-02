using Nucleaire.Simulation;
using NUnit.Framework;

namespace Nucleaire.Tests
{
    public class ProcessorTests : SimTestBase
    {
        [Test]
        public void Furnace_WithPower_SmeltsOre()
        {
            var furnace = Place<ProcessorMachine>("electric_furnace", 1, 1);
            Power(furnace, 2000);
            furnace.InsertManual(R("iron_ore"), 1);
            World.Step(31);
            Assert.AreEqual(1, furnace.Outputs.Amount(R("iron_ingot")));
            Assert.AreEqual(1, World.Produced(R("iron_ingot")));
        }

        [Test]
        public void NoPower_BlocksAndExplains()
        {
            var furnace = Place<ProcessorMachine>("electric_furnace", 1, 1);
            furnace.InsertManual(R("iron_ore"), 1);
            World.Step(50);
            Assert.AreEqual(MachineStatus.NoPower, furnace.Status);
            Assert.AreEqual(0, furnace.Outputs.Amount(R("iron_ingot")));
            StringAssert.Contains("J/tick", furnace.StatusDetail);
        }

        [Test]
        public void LowPower_SlowsDown()
        {
            var furnace = Place<ProcessorMachine>("electric_furnace", 1, 1);
            furnace.InsertManual(R("iron_dust"), 1);
            Power(furnace, 6 * 20); // la moitié de 12 J/tick pendant 20 ticks
            World.Step(1);
            Power(furnace, 0);
            Assert.AreEqual(MachineStatus.Working, furnace.Status);
            World.Step(19);
            Assert.AreEqual(0, furnace.Outputs.Amount(R("iron_ingot")), "Sans assez d'énergie, la fonte n'est pas finie à temps");
        }

        [Test]
        public void MissingInput_ListsWhatIsMissing()
        {
            var furnace = Place<ProcessorMachine>("electric_furnace", 1, 1);
            Power(furnace, 1000);
            furnace.InsertManual(R("iron_ingot"), 1); // acier : il manque le charbon
            World.Step(1);
            Assert.AreEqual(MachineStatus.MissingInput, furnace.Status);
            StringAssert.Contains("Charbon", furnace.StatusDetail);
        }

        [Test]
        public void ByProduct_FillsUpAndBlocks()
        {
            var electrolyser = Place<ProcessorMachine>("electrolyser", 1, 1);
            for (int i = 0; i < 2000 && electrolyser.Status != MachineStatus.OutputFull; i++)
            {
                electrolyser.InsertManual(R("water"), 1000);
                Power(electrolyser, 1000);
                World.Step();
                electrolyser.Outputs.Extract(R("hydrogen"), 10000); // on évacue l'hydrogène, pas l'oxygène
            }
            Assert.AreEqual(MachineStatus.OutputFull, electrolyser.Status, "L'oxygène non évacué doit bloquer l'électrolyseur");
            Assert.AreEqual(electrolyser.Outputs.Capacity(R("oxygen")), electrolyser.Outputs.Amount(R("oxygen")));
        }

        [Test]
        public void LockedRecipe_IsRespected()
        {
            var furnace = Place<ProcessorMachine>("electric_furnace", 1, 1);
            Power(furnace, 5000);
            furnace.InsertManual(R("iron_ore"), 1);
            furnace.InsertManual(R("sand"), 1);
            furnace.LockRecipe(furnace.Recipes[0].Id == "smelt_glass" ? furnace.Recipes[0] : System.Linq.Enumerable.First(furnace.Recipes, r => r.Id == "smelt_glass"));
            World.Step(25);
            Assert.AreEqual(1, furnace.Outputs.Amount(R("glass")));
            Assert.AreEqual(0, furnace.Outputs.Amount(R("iron_ingot")));
        }

        [Test]
        public void Solar_StopsWhenBufferFull()
        {
            var solar = Place<ProcessorMachine>("solar_panel", 1, 1);
            World.Step(500);
            Assert.AreEqual(solar.EnergyCapacity, solar.Energy);
            Assert.AreEqual(MachineStatus.OutputFull, solar.Status);
        }

        [Test]
        public void Disabled_DoesNothing()
        {
            var furnace = Place<ProcessorMachine>("electric_furnace", 1, 1);
            Power(furnace, 2000);
            furnace.InsertManual(R("iron_ore"), 1);
            furnace.SetEnabled(false);
            World.Step(40);
            Assert.AreEqual(MachineStatus.Disabled, furnace.Status);
            Assert.AreEqual(0, furnace.Outputs.Amount(R("iron_ingot")));
        }
    }
}
