using UnityEngine;
using Nucleaire.Data;

namespace Nucleaire.Simulation
{
    /// <summary>Stocke l'énergie : reçoit par ses faces d'entrée, restitue par ses faces de sortie.</summary>
    public sealed class BatteryMachine : Machine
    {
        public BatteryMachine(MachineDef def, World world, Vector2Int pos) : base(def, world, pos, SideMode.Input) { }

        public override bool ConsumesEnergy => true;
        public override bool ProducesEnergy => true;
        public override float Progress => EnergyCapacity == 0 ? 0f : (float)Energy / EnergyCapacity;

        protected override void OnTick() =>
            SetStatus(Energy > 0 ? MachineStatus.Working : MachineStatus.Idle, $"{Energy} / {EnergyCapacity} J");
    }
}
