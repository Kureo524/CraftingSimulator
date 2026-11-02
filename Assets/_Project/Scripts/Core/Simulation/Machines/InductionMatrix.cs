using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Matrice à induction multi-bloc : les cellules stockent l'énergie, les fournisseurs augmentent le débit
    /// d'entrée et de sortie. Plus elle est grande, plus elle fuit un peu.</summary>
    public sealed class InductionMatrix : Machine, IMultiblockController
    {
        private int inBudget, outBudget;

        public InductionMatrix(MachineDef def, World world, Vector3Int pos) : base(def, world, pos, SideMode.None)
        {
            Structure = new Multiblock(this, MultiblockKind.Induction);
        }

        public Multiblock Structure { get; }
        private MultiblockSettings M => MachineDef.Multiblock;
        public int Cells => Structure.Formed ? Structure.Count(StructureRole.InductionCell) : 0;
        public int Providers => Structure.Formed ? Structure.Count(StructureRole.InductionProvider) : 0;
        public int Rate => M.baseRate + Providers * M.ratePerProvider;
        public int Leak => Structure.Formed ? Structure.InteriorVolume * M.leakPerInterior : 0;

        public override bool ConsumesEnergy => Structure.Formed;
        public override bool ProducesEnergy => Structure.Formed;
        public override int EnergyCapacity => Cells * M.capacityPerCell;
        public override int ExportableEnergy => Mathf.Min(Energy, outBudget);
        public override float Progress => EnergyCapacity == 0 ? 0f : (float)Energy / EnergyCapacity;

        protected override int Accept(ResourceDef r, int amount, bool simulate)
        {
            if (r != EnergyDef) return 0;
            int n = base.Accept(r, Mathf.Min(amount, inBudget), simulate);
            if (!simulate) inBudget -= n;
            return n;
        }

        public override int Extract(ResourceDef r, int amount)
        {
            if (r != EnergyDef) return 0;
            int n = base.Extract(r, Mathf.Min(amount, outBudget));
            outBudget -= n;
            return n;
        }

        protected override void OnTick()
        {
            Structure.Refresh();
            inBudget = outBudget = Structure.Formed ? Rate : 0;
            if (!Structure.Formed) { SetStatus(MachineStatus.Idle, "Structure incomplète : " + Structure.Problem); return; }
            if (Energy > EnergyCapacity) ConsumeEnergy(Energy - EnergyCapacity);
            ConsumeEnergy(Mathf.Min(Energy, Leak));
            SetStatus(Energy > 0 ? MachineStatus.Working : MachineStatus.Idle, $"{Energy / 1000} / {EnergyCapacity / 1000} kJ · débit {Rate} J/tick.");
        }
    }
}
