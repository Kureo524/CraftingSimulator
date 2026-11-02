using System;
using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Réacteur à fission. Le joueur règle la vitesse de combustion.
    /// Chaque mB brûlé exige de l'eau, qui devient de la vapeur. Sans eau (ou si la vapeur n'est pas évacuée),
    /// la chaleur monte ; au-delà du seuil : arrêt d'urgence, le combustible est perdu.</summary>
    public sealed class ReactorMachine : Machine
    {
        private readonly ReactorSettings s;

        public ReactorMachine(MachineDef def, World world, Vector3Int pos) : base(def, world, pos, SideMode.Input)
        {
            s = def.Reactor;
            Inputs = new ResourceStorage(new[] { s.fuel.Kind, s.coolant.Kind }, CapacityFor, new[] { s.fuel, s.coolant });
            Outputs = new ResourceStorage(new[] { s.waste.Kind, s.heatedCoolant.Kind }, CapacityFor, new[] { s.waste, s.heatedCoolant });
            Heat = s.ambientHeat;
        }

        public ReactorSettings Settings => s;
        public int BurnRate { get; private set; }
        public float Heat { get; private set; }
        public bool IsScrammed { get; private set; }
        public int LastBurn { get; private set; }
        /// <summary>Part de la chaleur évacuée par l'eau au dernier tick (0..1).</summary>
        public float LastCooling { get; private set; } = 1f;
        public float LastHeatDelta { get; private set; }
        public float HeatRatio => Mathf.Clamp01(Heat / s.meltdownHeat);
        public override float Progress => HeatRatio;

        public int CoolantPerFuel => Mathf.RoundToInt(s.coolantPerFuel * Multiplier(MachineStat.CoolantUsage));
        public int HeatedCoolantPerFuel => Mathf.RoundToInt(s.heatedCoolantPerFuel * Multiplier(MachineStat.HeatedCoolant));
        public bool CanRestart => IsScrammed && Heat <= s.restartHeat;

        public void SetBurnRate(int rate) => BurnRate = Math.Clamp(rate, 0, s.maxBurnRate);

        public bool Restart()
        {
            if (!CanRestart) return false;
            IsScrammed = false;
            World.Log(EventSeverity.Info, $"{MachineDef.DisplayName} relancé.", this);
            return true;
        }

        protected override void OnTick()
        {
            float before = Heat;
            LastBurn = 0;
            LastCooling = 1f;

            if (IsScrammed)
            {
                Cool();
                LastHeatDelta = Heat - before;
                SetStatus(MachineStatus.Meltdown, CanRestart
                    ? "Refroidi : prêt à être relancé."
                    : $"Refroidissement en cours ({Heat:0}°, relance possible sous {s.restartHeat:0}°).");
                return;
            }

            int fuel = Inputs.Amount(s.fuel);
            int wasteRoom = s.wastePerFuel > 0 ? Outputs.Space(s.waste) / s.wastePerFuel : int.MaxValue;
            int burn = Math.Min(BurnRate, Math.Min(fuel, wasteRoom));

            if (burn > 0)
            {
                Inputs.Extract(s.fuel, burn);
                int waste = burn * s.wastePerFuel;
                Outputs.Insert(s.waste, waste);
                World.RecordProduction(s.waste, waste);

                // Refroidissement : l'eau n'évacue la chaleur que si la vapeur produite a de la place.
                int coolantNeed = burn * CoolantPerFuel;
                if (coolantNeed > 0)
                {
                    int ratio = Math.Max(1, CoolantPerFuel);
                    int steamPerCoolant = HeatedCoolantPerFuel;
                    int maxBySteam = steamPerCoolant > 0 ? Outputs.Space(s.heatedCoolant) * ratio / Math.Max(1, steamPerCoolant) : int.MaxValue;
                    int coolant = Math.Min(coolantNeed, Math.Min(Inputs.Amount(s.coolant), maxBySteam));
                    Inputs.Extract(s.coolant, coolant);
                    int steam = coolant * steamPerCoolant / ratio;
                    if (steam > 0)
                    {
                        Outputs.Insert(s.heatedCoolant, steam);
                        World.RecordProduction(s.heatedCoolant, steam);
                    }
                    LastCooling = (float)coolant / coolantNeed;
                }
                Heat += burn * s.heatPerFuel * (1f - LastCooling);
                LastBurn = burn;
            }

            Cool();
            LastHeatDelta = Heat - before;

            if (Heat >= s.meltdownHeat) { Meltdown(); return; }
            UpdateStatus(fuel, wasteRoom);
        }

        private void Cool() => Heat = Mathf.Max(s.ambientHeat, Heat - s.passiveCooling);

        private void Meltdown()
        {
            int lost = Inputs.Extract(s.fuel, Inputs.Amount(s.fuel));
            IsScrammed = true;
            BurnRate = 0;
            SetStatus(MachineStatus.Meltdown, $"Surchauffe ! {lost} mB de combustible perdus. Laisse refroidir puis relance.");
        }

        private void UpdateStatus(int fuel, int wasteRoom)
        {
            bool hot = Heat >= s.meltdownHeat * World.Settings.reactorWarningRatio;
            if (BurnRate == 0) { SetStatus(hot ? MachineStatus.Overheating : MachineStatus.Idle, "Vitesse de combustion à 0."); return; }
            if (fuel == 0) { SetStatus(MachineStatus.MissingInput, $"Plus de {s.fuel.DisplayName}."); return; }
            if (wasteRoom == 0) { SetStatus(MachineStatus.OutputFull, $"{s.waste.DisplayName} plein : évacue les déchets."); return; }

            if (LastCooling < 1f)
            {
                string cause = Inputs.Amount(s.coolant) == 0 || Outputs.Space(s.heatedCoolant) > 0
                    ? $"Manque d'{s.coolant.DisplayName.ToLowerInvariant()}"
                    : $"{s.heatedCoolant.DisplayName} non évacuée";
                SetStatus(MachineStatus.Overheating, $"{cause} : chaleur {(LastHeatDelta >= 0 ? "+" : "")}{LastHeatDelta:0.#}/tick ({Heat:0}°/{s.meltdownHeat:0}°).");
                return;
            }
            if (hot) { SetStatus(MachineStatus.Overheating, $"Chaleur élevée : {Heat:0}°/{s.meltdownHeat:0}°."); return; }
            SetStatus(MachineStatus.Working, $"Combustion {LastBurn} mB/tick, refroidi.");
        }
    }
}
