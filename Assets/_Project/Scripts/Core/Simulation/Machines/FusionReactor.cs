using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Réacteur à fusion multi-bloc (deutérium-tritium). Il faut d'abord lui fournir une énorme charge d'allumage
    /// (une fission ne sera pas de trop) ; une fois le plasma allumé, chaque bobine produit beaucoup plus qu'elle ne coûte,
    /// tant qu'il y a du combustible D-T. Sans combustible ou sans entretien, le plasma s'éteint.</summary>
    public sealed class FusionReactor : Machine, IMultiblockController
    {
        private float fuelDebt;

        public FusionReactor(MachineDef def, World world, Vector3Int pos) : base(def, world, pos, SideMode.None)
        {
            Structure = new Multiblock(this, MultiblockKind.Fusion);
            var fuel = def.Multiblock.fusionFuel;
            Inputs = fuel != null ? new ResourceStorage(new[] { fuel.Kind }, CapacityFor, new[] { fuel }) : new ResourceStorage(CapacityFor);
        }

        public Multiblock Structure { get; }
        private MultiblockSettings M => MachineDef.Multiblock;
        public int Coils => Structure.Formed ? Structure.Count(StructureRole.FusionCoil) : 0;
        public bool Ignited { get; private set; }
        public int IgnitionEnergy => Mathf.Max(1, Coils) * M.ignitionPerCoil;
        public int GrossOutput => Coils * M.energyPerCoil;
        public int Sustain => Coils * M.sustainPerCoil;
        public int NetOutput => GrossOutput - Sustain;
        public int LastProduced { get; private set; }

        public override bool ConsumesEnergy => !Ignited;
        public override bool ProducesEnergy => true;
        public override int EnergyCapacity => IgnitionEnergy + Mathf.Max(1, Coils) * M.bufferPerCoil;
        /// <summary>Avant l'allumage, la charge ne ressort pas.</summary>
        public override int ExportableEnergy => Ignited ? Energy : 0;
        public override float Progress => Ignited ? 1f : Mathf.Clamp01((float)Energy / IgnitionEnergy);

        protected override void OnTick()
        {
            Structure.Refresh();
            LastProduced = 0;
            if (!Structure.Formed)
            {
                Ignited = false;
                SetStatus(MachineStatus.Idle, "Structure incomplète : " + Structure.Problem);
                return;
            }
            var fuel = M.fusionFuel;
            if (!Ignited)
            {
                if (Energy < IgnitionEnergy)
                {
                    SetStatus(MachineStatus.NoPower, $"Charge d'allumage : {Energy / 1000} / {IgnitionEnergy / 1000} kJ. Envoie de l'énergie par un port d'entrée.");
                    return;
                }
                if (fuel == null || Inputs.Amount(fuel) <= 0)
                {
                    SetStatus(MachineStatus.MissingInput, $"Prêt à l'allumage : il manque du {fuel?.DisplayName.ToLowerInvariant() ?? "combustible"}.");
                    return;
                }
                ConsumeEnergy(IgnitionEnergy);
                Ignited = true;
                World.Log(EventSeverity.Success, $"{MachineDef.DisplayName} : plasma allumé.", this);
            }

            fuelDebt += Coils * M.fuelPerCoilPer10Ticks / 10f;
            int burn = Mathf.FloorToInt(fuelDebt);
            if (burn > 0)
            {
                if (Inputs.Amount(fuel) < burn) { Extinguish($"Plus de {fuel.DisplayName.ToLowerInvariant()}."); return; }
                Inputs.Extract(fuel, burn);
                fuelDebt -= burn;
            }
            LastProduced = AddEnergy(NetOutput);
            if (LastProduced > 0) World.RecordProduction(World.Db.Energy, LastProduced);
            SetStatus(LastProduced < NetOutput ? MachineStatus.OutputFull : MachineStatus.Working,
                LastProduced < NetOutput ? "Réserve pleine : évacue l'énergie par un port de sortie." : $"Plasma allumé : +{NetOutput} J/tick.");
        }

        private void Extinguish(string why)
        {
            Ignited = false;
            fuelDebt = 0f;
            World.Log(EventSeverity.Warning, $"{MachineDef.DisplayName} : plasma éteint. {why}", this);
            SetStatus(MachineStatus.MissingInput, $"Plasma éteint : {why} Il faudra une nouvelle charge d'allumage.");
        }
    }
}
