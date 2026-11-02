using System.Linq;
using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Déphaseur supercritique multi-bloc : chaque bobine supraconductrice ajoute de la vitesse,
    /// mais il exige à chaque tick l'énergie de toutes ses bobines ; sinon il ne progresse pas du tout.</summary>
    public sealed class SpsMachine : Machine, IMultiblockController
    {
        private readonly ProcessRecipe recipe;
        private float progressTicks;

        public SpsMachine(MachineDef def, World world, Vector3Int pos) : base(def, world, pos, SideMode.None)
        {
            Structure = new Multiblock(this, MultiblockKind.Sps);
            recipe = world.Db.ProcessesFor(def).FirstOrDefault();
            if (recipe != null)
            {
                Inputs = new ResourceStorage(recipe.Inputs.Select(i => i.Resource.Kind), CapacityFor, recipe.Inputs.Select(i => i.Resource));
                Outputs = new ResourceStorage(recipe.Outputs.Select(o => o.Resource.Kind), CapacityFor, recipe.Outputs.Select(o => o.Resource));
            }
        }

        public Multiblock Structure { get; }
        public ProcessRecipe Recipe => recipe;
        public int Coils => Structure.Formed ? Structure.Count(StructureRole.SpsCoil) : 0;
        /// <summary>Énergie exigée à chaque tick (toutes les bobines).</summary>
        public int PowerDemand => recipe == null ? 0 : Coils * recipe.EnergyPerTick;
        public override bool ConsumesEnergy => true;
        public override int EnergyCapacity => Mathf.Max(2000, PowerDemand * MachineDef.Multiblock.spsBufferTicks);
        public override float Progress => recipe == null ? 0f : Mathf.Clamp01(progressTicks / recipe.DurationTicks);

        protected override void OnTick()
        {
            Structure.Refresh();
            if (recipe == null) { SetStatus(MachineStatus.Idle, "Aucun procédé."); return; }
            if (!Structure.Formed) { SetStatus(MachineStatus.Idle, "Structure incomplète : " + Structure.Problem); return; }
            if (!Inputs.HasAll(recipe.Inputs)) { SetStatus(MachineStatus.MissingInput, $"Il faut {Describe(recipe.Inputs)}."); return; }
            if (!Outputs.CanFitAll(recipe.Outputs)) { SetStatus(MachineStatus.OutputFull, "Sortie pleine : évacue l'antimatière."); return; }
            int need = PowerDemand;
            if (Energy < need)
            {
                SetStatus(MachineStatus.NoPower, $"Il exige {need} J/tick à chaque tick ({Coils} bobine(s)) : sans cela, rien ne progresse.");
                return;
            }
            ConsumeEnergy(need);
            progressTicks += Coils;
            while (progressTicks >= recipe.DurationTicks && Inputs.HasAll(recipe.Inputs) && Outputs.CanFitAll(recipe.Outputs))
            {
                progressTicks -= recipe.DurationTicks;
                foreach (var i in recipe.Inputs) Inputs.Extract(i.Resource, i.Amount);
                foreach (var o in recipe.Outputs)
                {
                    Outputs.Insert(o.Resource, o.Amount);
                    World.RecordProduction(o.Resource, o.Amount);
                }
            }
            SetStatus(MachineStatus.Working, $"{Coils} bobine(s) · {need} J/tick.");
        }

        private static string Describe(System.Collections.Generic.IEnumerable<ResourceStack> s) =>
            string.Join(" + ", s.Select(x => $"{x.Amount}{x.Resource.Kind.Unit()} {x.Resource.DisplayName.ToLowerInvariant()}"));
    }
}
