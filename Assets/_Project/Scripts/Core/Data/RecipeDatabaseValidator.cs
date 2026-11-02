using System.Collections.Generic;
using System.Linq;

namespace Nucleaire.Data
{
    /// <summary>Vérifie la cohérence des données : références manquantes, quantités invalides,
    /// et surtout que chaque objectif est atteignable depuis le départ (vault + inventaire initial).</summary>
    public static class RecipeDatabaseValidator
    {
        public sealed class Report
        {
            public readonly List<string> Errors = new();
            public readonly List<string> Warnings = new();
            public readonly HashSet<ResourceDef> Reachable = new();
            public readonly HashSet<MachineDef> AvailableMachines = new();
            public bool IsValid => Errors.Count == 0;
            public override string ToString() =>
                string.Join("\n", Errors.Select(e => "ERREUR : " + e).Concat(Warnings.Select(w => "Attention : " + w)));
        }

        public static Report Validate(RecipeDatabase db)
        {
            var report = new Report();
            if (db == null) { report.Errors.Add("Base de recettes absente."); return report; }

            CheckStructure(db, report);
            ComputeReachability(db, report);

            foreach (var o in db.Objectives.Where(o => o != null))
                if (o.Target.Resource != null && !report.Reachable.Contains(o.Target.Resource))
                    report.Errors.Add($"Objectif « {o.Title} » inatteignable : {o.Target.Resource.DisplayName} ne peut pas être produit.");

            var prePlacedItems = new HashSet<ResourceDef>(db.PrePlacedMachines.Where(m => m != null).Select(m => m.Item));
            foreach (var r in db.Resources.Where(r => r != null && !report.Reachable.Contains(r) && !prePlacedItems.Contains(r)))
                report.Warnings.Add($"Ressource jamais obtenable : {r.DisplayName} ({r.Id}).");

            return report;
        }

        private static void CheckStructure(RecipeDatabase db, Report report)
        {
            if (db.Energy == null) report.Errors.Add("Ressource « énergie » non définie.");
            if (!db.Machines.Any(m => m != null && m.Behaviour == MachineBehaviour.Vault)) report.Errors.Add("Aucun vault défini.");

            var ids = new HashSet<string>();
            foreach (var r in db.Resources)
            {
                if (r == null) { report.Errors.Add("Ressource nulle dans la base."); continue; }
                if (!ids.Add(r.Id)) report.Errors.Add($"Identifiant de ressource en double : {r.Id}.");
            }

            foreach (var p in db.Machines.Cast<PlaceableDef>().Concat(db.Transmitters).Concat(db.Structures))
            {
                if (p == null) { report.Errors.Add("Placeable nul dans la base."); continue; }
                if (p is MachineDef { Behaviour: MachineBehaviour.Workbench }) report.Errors.Add($"{p.Id} : l'établi n'est plus une machine posable.");
                if (p.Item == null) report.Errors.Add($"{p.Id} n'a pas d'objet d'inventaire associé.");
            }

            foreach (var p in db.Processes)
            {
                if (p == null) { report.Errors.Add("Procédé nul dans la base."); continue; }
                if (p.Machine == null) report.Errors.Add($"Procédé {p.Id} sans machine.");
                else if (p.Machine.Behaviour is not (MachineBehaviour.Processor or MachineBehaviour.Sps))
                    report.Errors.Add($"Procédé {p.Id} attribué à {p.Machine.Id}, qui n'exécute pas de procédés.");
                if (p.Outputs.Count == 0) report.Errors.Add($"Procédé {p.Id} sans sortie.");
                CheckStacks(p.Id, p.Inputs, report, allowEnergy: false);
                CheckStacks(p.Id, p.Outputs, report, allowEnergy: true);
                if (p.Machine != null)
                    foreach (var s in p.Inputs.Concat(p.Outputs).Where(s => s.IsValid && s.Resource.Kind != ResourceKind.Energy))
                        if (s.Amount > p.Machine.CapacityFor(s.Resource.Kind))
                            report.Errors.Add($"Procédé {p.Id} : {s} dépasse la capacité de {p.Machine.Id}.");
            }

            foreach (var c in db.CraftingRecipes)
            {
                if (c == null) { report.Errors.Add("Recette d'établi nulle."); continue; }
                if (!c.Result.IsValid) report.Errors.Add($"Recette d'établi {c.Id} sans résultat.");
                CheckStacks(c.Id, c.Ingredients, report, allowEnergy: false);
                foreach (var s in c.Ingredients.Append(c.Result).Where(s => s.IsValid && s.Resource.Kind != ResourceKind.Item))
                    report.Errors.Add($"Recette d'établi {c.Id} : {s.Resource.DisplayName} n'est pas un solide.");
            }

            foreach (var u in db.Upgrades)
            {
                if (u == null) { report.Errors.Add("Amélioration nulle."); continue; }
                if (u.Item == null) report.Errors.Add($"Amélioration {u.Id} sans objet associé.");
                if (u.Modifiers.Count == 0) report.Warnings.Add($"Amélioration {u.Id} sans effet.");
            }

            foreach (var m in db.Machines.Where(m => m != null && m.Behaviour == MachineBehaviour.Reactor))
            {
                var r = m.Reactor;
                if (r.fuel == null || r.coolant == null || r.waste == null || r.heatedCoolant == null)
                    report.Errors.Add($"Réacteur {m.Id} : ressources (combustible/eau/déchets/vapeur) incomplètes.");
                if (r.restartHeat >= r.meltdownHeat) report.Errors.Add($"Réacteur {m.Id} : température de relance ≥ surchauffe.");
            }

            foreach (var o in db.Objectives)
                if (o == null || !o.Target.IsValid) report.Errors.Add($"Objectif {(o != null ? o.Id : "nul")} sans cible valide.");
        }

        private static void CheckStacks(string owner, IReadOnlyList<ResourceStack> stacks, Report report, bool allowEnergy)
        {
            foreach (var s in stacks)
            {
                if (s.Resource == null) report.Errors.Add($"{owner} : ressource manquante.");
                else if (s.Amount <= 0) report.Errors.Add($"{owner} : quantité invalide pour {s.Resource.DisplayName}.");
                else if (!allowEnergy && s.Resource.Kind == ResourceKind.Energy)
                    report.Errors.Add($"{owner} : l'énergie se consomme via « énergie par tick », pas comme ingrédient.");
            }
        }

        /// <summary>Point fixe : on part de l'inventaire initial et des machines déjà posées,
        /// et on applique toutes les recettes réalisables jusqu'à ce que plus rien ne change.</summary>
        private static void ComputeReachability(RecipeDatabase db, Report report)
        {
            var have = report.Reachable;
            var machines = report.AvailableMachines;

            foreach (var s in db.StartingInventory.Where(s => s.IsValid)) have.Add(s.Resource);
            foreach (var m in db.PrePlacedMachines.Where(m => m != null)) machines.Add(m);

            bool changed = true;
            while (changed)
            {
                changed = false;

                foreach (var m in db.Machines.Where(m => m != null && m.Item != null && have.Contains(m.Item)))
                    changed |= machines.Add(m);

                if (machines.Any(m => m.Behaviour == MachineBehaviour.Vault))
                    foreach (var b in db.BaseResources) changed |= have.Add(b);

                bool hasEnergy = db.Energy != null && have.Contains(db.Energy);

                foreach (var p in db.Processes.Where(p => p != null && p.Machine != null && machines.Contains(p.Machine)))
                {
                    if (p.EnergyPerTick > 0 && !hasEnergy) continue;
                    if (!p.Inputs.All(i => i.Resource != null && have.Contains(i.Resource))) continue;
                    foreach (var o in p.Outputs.Where(o => o.Resource != null)) changed |= have.Add(o.Resource);
                }

                // L'établi est un panneau toujours accessible.
                foreach (var c in db.CraftingRecipes.Where(c => c != null && c.Result.IsValid))
                        if (c.Ingredients.All(i => i.Resource != null && have.Contains(i.Resource)))
                            changed |= have.Add(c.Result.Resource);

                foreach (var m in machines.Where(m => m.Behaviour == MachineBehaviour.Reactor).ToList())
                {
                    var r = m.Reactor;
                    if (r.fuel == null || !have.Contains(r.fuel) || r.coolant == null || !have.Contains(r.coolant)) continue;
                    if (r.waste != null) changed |= have.Add(r.waste);
                    if (r.heatedCoolant != null) changed |= have.Add(r.heatedCoolant);
                }
            }
        }
    }
}
