using Nucleaire.Simulation;
using NUnit.Framework;

namespace Nucleaire.Tests
{
    public class ReactorTests : SimTestBase
    {
        private ReactorMachine reactor;

        [SetUp]
        public void SetUp()
        {
            reactor = Place<ReactorMachine>("fission_reactor", 1, 1);
            reactor.InsertManual(R("fissile_fuel"), 4000);
        }

        [Test]
        public void WithEnoughWater_StaysCoolAndMakesSteam()
        {
            reactor.SetBurnRate(5);
            for (int i = 0; i < 100; i++)
            {
                reactor.InsertManual(R("water"), 100);
                World.Step();
                reactor.Extract(R("steam"), 10000);
            }
            Assert.AreEqual(MachineStatus.Working, reactor.Status);
            Assert.Less(reactor.Heat, 100f);
            Assert.Greater(World.Produced(R("steam")), 0);
            Assert.Greater(World.Produced(R("nuclear_waste")), 0);
        }

        [Test]
        public void WithoutWater_OverheatsThenMeltsDown()
        {
            reactor.SetBurnRate(reactor.Settings.maxBurnRate);
            World.Step(3);
            Assert.AreEqual(MachineStatus.Overheating, reactor.Status);
            StringAssert.Contains("Manque", reactor.StatusDetail);

            World.Step(500);
            Assert.IsTrue(reactor.IsScrammed);
            Assert.AreEqual(MachineStatus.Meltdown, reactor.Status);
            Assert.AreEqual(0, reactor.Inputs.Amount(R("fissile_fuel")), "Le combustible est perdu à la surchauffe");
        }

        [Test]
        public void AfterMeltdown_RestartOnlyWhenCooled()
        {
            reactor.SetBurnRate(reactor.Settings.maxBurnRate);
            for (int i = 0; i < 1000 && !reactor.IsScrammed; i++) World.Step();
            Assert.IsTrue(reactor.IsScrammed);
            Assert.IsFalse(reactor.CanRestart);
            Assert.IsFalse(reactor.Restart(), "Relance impossible tant que le réacteur est chaud");

            for (int i = 0; i < 2000 && !reactor.CanRestart; i++) World.Step();
            Assert.IsTrue(reactor.CanRestart);
            Assert.IsTrue(reactor.Restart());
            Assert.IsFalse(reactor.IsScrammed);
        }

        [Test]
        public void SteamNotEvacuated_CausesOverheating()
        {
            reactor.SetBurnRate(reactor.Settings.maxBurnRate);
            for (int i = 0; i < 400 && reactor.Status != MachineStatus.Overheating; i++)
            {
                reactor.InsertManual(R("water"), 1000);
                World.Step();
            }
            Assert.AreEqual(MachineStatus.Overheating, reactor.Status);
            StringAssert.Contains("Vapeur", reactor.StatusDetail);
        }

        [Test]
        public void HeatExchanger_ReducesWaterUse()
        {
            int baseUse = reactor.CoolantPerFuel;
            Assert.IsTrue(reactor.InstallUpgrade(U("upgrade_heat_exchanger")));
            Assert.Less(reactor.CoolantPerFuel, baseUse);
            Assert.Less(reactor.HeatedCoolantPerFuel, reactor.Settings.heatedCoolantPerFuel);
        }

        [Test]
        public void BurnRate_IsClamped()
        {
            reactor.SetBurnRate(9999);
            Assert.AreEqual(reactor.Settings.maxBurnRate, reactor.BurnRate);
            reactor.SetBurnRate(-3);
            Assert.AreEqual(0, reactor.BurnRate);
        }
    }
}
