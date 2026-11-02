using Nucleaire.Simulation;
using NUnit.Framework;

namespace Nucleaire.Tests
{
    public class ReactorTests : SimTestBase
    {
        /// <summary>Avance la simulation en alimentant le contrôle du réacteur.</summary>
        private void Run(int ticks = 1)
        {
            for (int i = 0; i < ticks; i++) { Power(reactor, reactor.EnergyCapacity); World.Step(); }
        }

        private ReactorMachine reactor;

        [SetUp]
        public void SetUp()
        {
            // 5×5×3 : 9 assemblages de combustible → combustion max 18 mB/tick.
            reactor = BuildMultiblock<FissionReactor>("fission_reactor", new UnityEngine.Vector3Int(1, 1, 0), new UnityEngine.Vector3Int(5, 5, 3), "fuel_assembly", 9);
            Assert.IsTrue(((FissionReactor)reactor).Structure.Formed, ((FissionReactor)reactor).Structure.Problem);
            reactor.InsertManual(R("fissile_fuel"), 4000);
        }

        [Test]
        public void WithEnoughWater_StaysCoolAndMakesSteam()
        {
            reactor.SetBurnRate(5);
            for (int i = 0; i < 100; i++)
            {
                reactor.InsertManual(R("water"), 100);
                Run(1);
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
            reactor.SetBurnRate(reactor.MaxBurnRate);
            Run(3);
            Assert.AreEqual(MachineStatus.Overheating, reactor.Status);
            StringAssert.Contains("Manque", reactor.StatusDetail);

            Run(500);
            Assert.IsTrue(reactor.IsScrammed);
            Assert.AreEqual(MachineStatus.Meltdown, reactor.Status);
            Assert.AreEqual(0, reactor.Inputs.Amount(R("fissile_fuel")), "Le combustible est perdu à la surchauffe");
        }

        [Test]
        public void AfterMeltdown_RestartOnlyWhenCooled()
        {
            reactor.SetBurnRate(reactor.MaxBurnRate);
            for (int i = 0; i < 1000 && !reactor.IsScrammed; i++) Run(1);
            Assert.IsTrue(reactor.IsScrammed);
            Assert.IsFalse(reactor.CanRestart);
            Assert.IsFalse(reactor.Restart(), "Relance impossible tant que le réacteur est chaud");

            for (int i = 0; i < 2000 && !reactor.CanRestart; i++) Run(1);
            Assert.IsTrue(reactor.CanRestart);
            Assert.IsTrue(reactor.Restart());
            Assert.IsFalse(reactor.IsScrammed);
        }

        [Test]
        public void SteamNotEvacuated_CausesOverheating()
        {
            reactor.SetBurnRate(reactor.MaxBurnRate);
            for (int i = 0; i < 400 && reactor.Status != MachineStatus.Overheating; i++)
            {
                reactor.InsertManual(R("water"), 1000);
                Run(1);
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
        public void WithoutControlPower_ItDoesNotBurn()
        {
            reactor.SetBurnRate(4);
            World.Step(3);
            Assert.AreEqual(MachineStatus.NoPower, reactor.Status);
            Assert.AreEqual(4000, reactor.Inputs.Amount(R("fissile_fuel")));
        }

        [Test]
        public void MoreAssemblies_MoreBurn()
        {
            Assert.AreEqual(9 * 2, reactor.MaxBurnRate);
        }

        [Test]
        public void BurnRate_IsClamped()
        {
            reactor.SetBurnRate(9999);
            Assert.AreEqual(reactor.MaxBurnRate, reactor.BurnRate);
            reactor.SetBurnRate(-3);
            Assert.AreEqual(0, reactor.BurnRate);
        }
    }
}

