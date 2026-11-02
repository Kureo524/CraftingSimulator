using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>L'établi n'a pas de logique automatique : le joueur y fabrique ses objets (voir WorkbenchService).</summary>
    public sealed class WorkbenchMachine : Machine
    {
        public WorkbenchMachine(MachineDef def, World world, Vector2Int pos) : base(def, world, pos, SideMode.None) { }

        protected override void OnTick() => SetStatus(MachineStatus.Idle, "");
        protected override int Accept(ResourceDef r, int amount, bool simulate) => 0;
    }
}
