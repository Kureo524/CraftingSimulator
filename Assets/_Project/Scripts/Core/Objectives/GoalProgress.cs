using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Crafting;
using Nucleaire.Data;
using Nucleaire.Simulation;

namespace Nucleaire.Objectives
{
    /// <summary>Progression vers l'objectif final : part des étapes de l'arbre de fabrication déjà obtenues.
    /// L'arbre est calculé depuis les données : pour chaque ressource, la recette la plus directe,
    /// ses ingrédients et la machine qui la réalise. Les ressources de base (vault) et l'énergie sont exclues.</summary>
    public sealed class GoalProgress
    {
        private readonly RecipeDatabase db;
        private readonly HashSet<ResourceDef> obtained = new();
        private readonly List<ResourceDef> steps;
        private readonly Dictionary<ResourceDef, RecipeEntry> chosen = new();

        public GoalProgress(World world, PlayerInventory inventory, ResourceDef goal)
        {
            db = world.Db;
            Goal = goal;
            steps = BuildSteps(goal);

            world.ResourceProduced += (r, _) => Mark(r);
            world.EntityPlaced += e => Mark(e.Def.Item);
            inventory.Changed += () => { foreach (var kv in inventory.Items) Mark(kv.Key); };
            foreach (var e in world.Entities) Mark(e.Def.Item);
            foreach (var kv in inventory.Items) Mark(kv.Key);
        }

        public ResourceDef Goal { get; }
        /// <summary>Étapes de l'arbre, des feuilles vers l'objectif.</summary>
        public IReadOnlyList<ResourceDef> Steps => steps;
        public int Done => steps.Count(obtained.Contains);
        public float Ratio => steps.Count == 0 ? 1f : (float)Done / steps.Count;
        public bool IsObtained(ResourceDef r) => obtained.Contains(r);
        public event Action Changed;

        /// <summary>Prochaines étapes réalisables : pas encore obtenues, mais dont tous les prérequis le sont.</summary>
        public IEnumerable<ResourceDef> Frontier => steps.Where(s => !obtained.Contains(s) && Prerequisites(s).All(IsSatisfied));

        public IEnumerable<ResourceDef> Prerequisites(ResourceDef r)
        {
            if (!chosen.TryGetValue(r, out var e)) return Enumerable.Empty<ResourceDef>();
            var list = e.Inputs.Select(i => i.Resource).Where(x => x != null).ToList();
            if (e.Machine?.Item != null && e.Source != RecipeSource.Vault) list.Add(e.Machine.Item);
            return list.Where(x => x != r);
        }

        private bool IsSatisfied(ResourceDef r) => r.IsBaseResource || r == db.Energy || obtained.Contains(r) || !steps.Contains(r);

        private void Mark(ResourceDef r)
        {
            if (r == null || !obtained.Add(r)) return;
            if (steps.Contains(r)) Changed?.Invoke();
        }

        private List<ResourceDef> BuildSteps(ResourceDef goal)
        {
            var order = new List<ResourceDef>();
            var visiting = new HashSet<ResourceDef>();
            var done = new HashSet<ResourceDef>();

            void Visit(ResourceDef r)
            {
                if (r == null || r.IsBaseResource || r == db.Energy || done.Contains(r) || !visiting.Add(r)) return;
                var entry = Choose(r, visiting);
                if (entry != null)
                {
                    chosen[r] = entry;
                    foreach (var i in entry.Inputs) Visit(i.Resource);
                    if (entry.Machine?.Item != null && !db.PrePlacedMachines.Contains(entry.Machine)) Visit(entry.Machine.Item);
                }
                visiting.Remove(r);
                done.Add(r);
                order.Add(r);
            }

            Visit(goal);
            return order;
        }

        /// <summary>Recette la plus directe : le moins d'ingrédients non élémentaires, sans boucle.</summary>
        private RecipeEntry Choose(ResourceDef r, HashSet<ResourceDef> visiting) =>
            db.RecipesProducing(r)
                .Where(e => e.Source != RecipeSource.Vault)
                .Where(e => !e.Inputs.Any(i => visiting.Contains(i.Resource)))
                .OrderBy(e => e.Inputs.Count(i => i.Resource != null && !i.Resource.IsBaseResource))
                .ThenBy(e => e.Inputs.Count)
                .FirstOrDefault();
    }
}
