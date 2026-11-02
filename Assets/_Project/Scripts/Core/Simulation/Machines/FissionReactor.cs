using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Réacteur à fission multi-bloc. Chaque assemblage de combustible ajoute de la combustion possible,
    /// mais demande de l'énergie de contrôle ; les cuves grandissent avec le volume intérieur.</summary>
    public sealed class FissionReactor : ReactorMachine, IMultiblockController
    {
        public FissionReactor(MachineDef def, World world, Vector3Int pos) : base(def, world, pos, SideMode.None)
        {
            Structure = new Multiblock(this, MultiblockKind.Fission);
        }

        public Multiblock Structure { get; }
        private MultiblockSettings M => MachineDef.Multiblock;
        public int Assemblies => Structure.Formed ? Structure.Count(StructureRole.FuelAssembly) : 0;
        public override int MaxBurnRate => Assemblies * M.burnPerAssembly;
        public int ControlUpkeep => Assemblies * M.upkeepPerAssembly;
        public override bool ConsumesEnergy => true;
        public override int EnergyCapacity => Mathf.Max(1000, ControlUpkeep * 200);

        protected override int CapacityFor(ResourceKind kind) => kind == ResourceKind.Item || !Structure.Formed
            ? base.CapacityFor(kind)
            : Mathf.Max(base.CapacityFor(kind), Structure.InteriorVolume * M.fluidCapacityPerInterior);

        protected override bool Operational(out MachineStatus status, out string why)
        {
            Structure.Refresh();
            status = MachineStatus.Idle;
            why = null;
            if (!Structure.Formed) { why = "Structure incomplète : " + Structure.Problem; return false; }
            if (BurnRate == 0) return true;
            if (Energy < ControlUpkeep)
            {
                status = MachineStatus.NoPower;
                why = $"Contrôle hors ligne : il faut {ControlUpkeep} J/tick ({Assemblies} assemblage(s)). Relie de l'énergie à un port.";
                return false;
            }
            ConsumeEnergy(ControlUpkeep);
            return true;
        }
    }
}
