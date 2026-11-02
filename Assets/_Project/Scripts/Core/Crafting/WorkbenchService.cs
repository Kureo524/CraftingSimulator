using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using Nucleaire.Simulation;

namespace Nucleaire.Crafting
{
    /// <summary>Fabrication manuelle à l'établi : consomme les ingrédients de l'inventaire, ajoute le résultat.</summary>
    public sealed class WorkbenchService
    {
        private readonly World world;
        private readonly PlayerInventory inventory;

        public WorkbenchService(World world, PlayerInventory inventory)
        {
            this.world = world;
            this.inventory = inventory;
        }

        public IReadOnlyList<CraftingRecipe> Recipes => world.Db.CraftingRecipes;

        public bool HasWorkbench => world.Machines.Any(m => m is WorkbenchMachine);

        public IEnumerable<ResourceStack> Missing(CraftingRecipe r) =>
            r.Ingredients.Where(s => s.IsValid).GroupBy(s => s.Resource)
                .Select(g => new ResourceStack(g.Key, g.Sum(s => s.Amount) - inventory.Count(g.Key)))
                .Where(s => s.Amount > 0);

        public bool CanCraft(CraftingRecipe r, out string reason)
        {
            reason = null;
            if (r == null) reason = "Recette inconnue.";
            else if (!HasWorkbench) reason = "Il faut un établi dans l'atelier.";
            else
            {
                var missing = Missing(r).ToList();
                if (missing.Count > 0) reason = "Il manque : " + string.Join(", ", missing.Select(m => m.ToString()));
            }
            return reason == null;
        }

        /// <summary>Fabrique jusqu'à <paramref name="times"/> fois. Retourne le nombre de fabrications réussies.</summary>
        public int Craft(CraftingRecipe r, int times = 1)
        {
            int done = 0;
            for (int i = 0; i < times && CanCraft(r, out _); i++)
            {
                inventory.RemoveAll(r.Ingredients);
                inventory.Add(r.Result.Resource, r.Result.Amount);
                world.RecordProduction(r.Result.Resource, r.Result.Amount);
                done++;
            }
            if (done > 0) world.Log(EventSeverity.Success, $"Fabriqué : {r.Result.Resource.DisplayName} ×{r.Result.Amount * done}");
            return done;
        }
    }
}
