using System.Collections.Generic;

namespace Nucleaire.Data
{
    public enum RecipeSource { Process, Workbench, Reactor, Vault }

    /// <summary>Vue unifiée d'une façon d'obtenir des ressources (procédé, établi, réacteur, vault).
    /// Utilisée par le livre de recettes et par la validation de l'arbre.</summary>
    public sealed class RecipeEntry
    {
        public RecipeSource Source { get; }
        /// <summary>Machine qui réalise la recette (null pour l'établi tant qu'il n'est pas défini).</summary>
        public MachineDef Machine { get; }
        public IReadOnlyList<ResourceStack> Inputs { get; }
        public IReadOnlyList<ResourceStack> Outputs { get; }
        public int DurationTicks { get; }
        public int EnergyPerTick { get; }
        public string Note { get; }
        /// <summary>Asset d'origine (ProcessRecipe, CraftingRecipe, MachineDef…).</summary>
        public UnityEngine.Object Origin { get; }

        public RecipeEntry(RecipeSource source, MachineDef machine, IReadOnlyList<ResourceStack> inputs,
            IReadOnlyList<ResourceStack> outputs, int durationTicks, int energyPerTick, string note, UnityEngine.Object origin)
        {
            Source = source; Machine = machine; Inputs = inputs; Outputs = outputs;
            DurationTicks = durationTicks; EnergyPerTick = energyPerTick; Note = note; Origin = origin;
        }
    }
}
