using System.Linq;
using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Rejette les liquides et gaz reçus. Utile pour se débarrasser d'un sous-produit, mais coûte de l'énergie.</summary>
    public sealed class VentMachine : Machine
    {
        public VentMachine(MachineDef def, World world, Vector3Int pos) : base(def, world, pos, SideMode.Input)
        {
            Inputs = new ResourceStorage(new[] { ResourceKind.Fluid, ResourceKind.Gas }, CapacityFor);
        }

        public override bool ConsumesEnergy => MachineDef.Vent.energyPerTick > 0;
        public int EnergyCostPerTick => Mathf.RoundToInt(MachineDef.Vent.energyPerTick * Multiplier(MachineStat.EnergyUsage));
        public int LastVented { get; private set; }

        protected override int Accept(ResourceDef r, int amount, bool simulate)
        {
            if (r != null && (r.Kind == ResourceKind.Item)) return 0;
            return base.Accept(r, amount, simulate);
        }

        protected override void OnTick()
        {
            LastVented = 0;
            var content = Inputs.Contents.FirstOrDefault();
            if (content.Key == null) { SetStatus(MachineStatus.Idle, "Rien à rejeter."); return; }

            int cost = EnergyCostPerTick;
            if (cost > 0 && ConsumeEnergy(cost) < cost) { SetStatus(MachineStatus.NoPower, $"Besoin de {cost} J/tick pour rejeter."); return; }

            int budget = Mathf.RoundToInt(MachineDef.Vent.ventPerTick * Multiplier(MachineStat.Speed));
            foreach (var kv in Inputs.Contents.ToList())
            {
                if (budget <= 0) break;
                int n = Inputs.Extract(kv.Key, budget);
                budget -= n;
                LastVented += n;
            }
            SetStatus(MachineStatus.Working, $"Rejette {LastVented} mB/tick.");
        }
    }
}
