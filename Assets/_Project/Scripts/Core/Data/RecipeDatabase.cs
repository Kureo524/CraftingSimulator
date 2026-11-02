using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nucleaire.Data
{
    /// <summary>Point d'entrée unique de toutes les données de crafting.
    /// Les listes sont synchronisées automatiquement dans l'éditeur (voir RecipeDatabaseSync) :
    /// créer un asset dans Data/ suffit pour qu'il soit pris en compte.</summary>
    [CreateAssetMenu(menuName = "Nucleaire/Base de recettes", fileName = "RecipeDatabase")]
    public sealed class RecipeDatabase : ScriptableObject
    {
        [Header("Contenu (synchronisé automatiquement)")]
        [SerializeField] private List<ResourceDef> resources = new();
        [SerializeField] private List<MachineDef> machines = new();
        [SerializeField] private List<TransmitterDef> transmitters = new();
        [SerializeField] private List<ProcessRecipe> processes = new();
        [SerializeField] private List<CraftingRecipe> craftingRecipes = new();
        [SerializeField] private List<UpgradeDef> upgrades = new();
        [SerializeField] private List<ObjectiveDef> objectives = new();

        [Header("Partie")]
        [Tooltip("Ressource « énergie » produite par les générateurs.")]
        [SerializeField] private ResourceDef energy;
        [Tooltip("Machines déjà posées dans l'atelier au lancement.")]
        [SerializeField] private List<MachineDef> prePlacedMachines = new();
        [Tooltip("Inventaire du joueur au lancement.")]
        [SerializeField] private List<ResourceStack> startingInventory = new();

        public IReadOnlyList<ResourceDef> Resources => resources;
        public IReadOnlyList<MachineDef> Machines => machines;
        public IReadOnlyList<TransmitterDef> Transmitters => transmitters;
        public IReadOnlyList<ProcessRecipe> Processes => processes;
        public IReadOnlyList<CraftingRecipe> CraftingRecipes => craftingRecipes;
        public IReadOnlyList<UpgradeDef> Upgrades => upgrades;
        public IReadOnlyList<ObjectiveDef> Objectives => objectives;
        public ResourceDef Energy => energy;
        public IReadOnlyList<MachineDef> PrePlacedMachines => prePlacedMachines;
        public IReadOnlyList<ResourceStack> StartingInventory => startingInventory;

        // ---- Index construits à la demande ----
        private List<RecipeEntry> entries;
        private Dictionary<ResourceDef, List<RecipeEntry>> producing;
        private Dictionary<ResourceDef, List<RecipeEntry>> using_;
        private Dictionary<MachineDef, List<ProcessRecipe>> processesByMachine;
        private Dictionary<ResourceDef, PlaceableDef> placeableByItem;
        private Dictionary<ResourceDef, UpgradeDef> upgradeByItem;

        public IReadOnlyList<RecipeEntry> AllEntries { get { EnsureIndex(); return entries; } }

        /// <summary>Toutes les façons d'obtenir cette ressource (« Comment le fabriquer »).</summary>
        public IReadOnlyList<RecipeEntry> RecipesProducing(ResourceDef r)
        {
            EnsureIndex();
            return r != null && producing.TryGetValue(r, out var l) ? l : (IReadOnlyList<RecipeEntry>)System.Array.Empty<RecipeEntry>();
        }

        /// <summary>Toutes les recettes qui consomment cette ressource (« Utilisé dans »).</summary>
        public IReadOnlyList<RecipeEntry> RecipesUsing(ResourceDef r)
        {
            EnsureIndex();
            return r != null && using_.TryGetValue(r, out var l) ? l : (IReadOnlyList<RecipeEntry>)System.Array.Empty<RecipeEntry>();
        }

        public IReadOnlyList<ProcessRecipe> ProcessesFor(MachineDef machine)
        {
            EnsureIndex();
            return machine != null && processesByMachine.TryGetValue(machine, out var l) ? l : (IReadOnlyList<ProcessRecipe>)System.Array.Empty<ProcessRecipe>();
        }

        /// <summary>Transmetteurs posables avec cet objet (le câble universel en pose plusieurs, un par type transporté).</summary>
        public IReadOnlyList<TransmitterDef> TransmittersForItem(ResourceDef item) =>
            item == null ? System.Array.Empty<TransmitterDef>() : Transmitters.Where(t => t != null && t.Item == item).ToList();

        public PlaceableDef PlaceableForItem(ResourceDef item)
        {
            EnsureIndex();
            return item != null && placeableByItem.TryGetValue(item, out var p) ? p : null;
        }

        public UpgradeDef UpgradeForItem(ResourceDef item)
        {
            EnsureIndex();
            return item != null && upgradeByItem.TryGetValue(item, out var u) ? u : null;
        }

        public IEnumerable<ResourceDef> BaseResources => resources.Where(r => r != null && r.IsBaseResource);

        public void InvalidateIndex() => entries = null;

        private void OnEnable() => entries = null;
        private void OnValidate() => entries = null;

        private void EnsureIndex()
        {
            if (entries != null) return;
            entries = new List<RecipeEntry>();
            producing = new Dictionary<ResourceDef, List<RecipeEntry>>();
            using_ = new Dictionary<ResourceDef, List<RecipeEntry>>();
            processesByMachine = new Dictionary<MachineDef, List<ProcessRecipe>>();
            placeableByItem = new Dictionary<ResourceDef, PlaceableDef>();
            upgradeByItem = new Dictionary<ResourceDef, UpgradeDef>();


            foreach (var p in processes.Where(p => p != null))
            {
                if (p.Machine != null)
                {
                    if (!processesByMachine.TryGetValue(p.Machine, out var list)) processesByMachine[p.Machine] = list = new List<ProcessRecipe>();
                    list.Add(p);
                }
                entries.Add(new RecipeEntry(RecipeSource.Process, p.Machine, p.Inputs, p.Outputs, p.DurationTicks, p.EnergyPerTick, null, p));
            }

            foreach (var c in craftingRecipes.Where(c => c != null))
                entries.Add(new RecipeEntry(RecipeSource.Workbench, null, c.Ingredients, new[] { c.Result }, 0, 0, "Craft manuel", c));

            foreach (var m in machines.Where(m => m != null))
            {
                if (m.Behaviour == MachineBehaviour.Reactor && m.Reactor.fuel != null)
                {
                    var r = m.Reactor;
                    var ins = new List<ResourceStack> { new(r.fuel, 1) };
                    if (r.coolant != null && r.coolantPerFuel > 0) ins.Add(new ResourceStack(r.coolant, r.coolantPerFuel));
                    var outs = new List<ResourceStack>();
                    if (r.waste != null && r.wastePerFuel > 0) outs.Add(new ResourceStack(r.waste, r.wastePerFuel));
                    if (r.heatedCoolant != null && r.heatedCoolantPerFuel > 0) outs.Add(new ResourceStack(r.heatedCoolant, r.heatedCoolantPerFuel));
                    entries.Add(new RecipeEntry(RecipeSource.Reactor, m, ins, outs, 1, 0,
                        $"Par mB de combustible brûlé (jusqu'à {r.maxBurnRate} mB/tick). Sans eau, la chaleur monte.", m));
                }
                else if (m.Behaviour == MachineBehaviour.Vault)
                {
                    foreach (var b in BaseResources)
                        entries.Add(new RecipeEntry(RecipeSource.Vault, m, System.Array.Empty<ResourceStack>(),
                            new[] { new ResourceStack(b, 1) }, 0, 0, "Ressource de base, illimitée", m));
                }
            }

            foreach (var e in entries)
            {
                foreach (var o in e.Outputs.Where(o => o.Resource != null)) Add(producing, o.Resource, e);
                foreach (var i in e.Inputs.Where(i => i.Resource != null)) Add(using_, i.Resource, e);
            }

            foreach (var p in machines.Cast<PlaceableDef>().Concat(transmitters))
                if (p != null && p.Item != null && !placeableByItem.ContainsKey(p.Item)) placeableByItem[p.Item] = p;
            foreach (var u in upgrades)
                if (u != null && u.Item != null) upgradeByItem[u.Item] = u;
        }

        private static void Add(Dictionary<ResourceDef, List<RecipeEntry>> dict, ResourceDef key, RecipeEntry e)
        {
            if (!dict.TryGetValue(key, out var list)) dict[key] = list = new List<RecipeEntry>();
            if (!list.Contains(e)) list.Add(e);
        }

#if UNITY_EDITOR
        public void EditorSetContent(List<ResourceDef> res, List<MachineDef> mach, List<TransmitterDef> trans,
            List<ProcessRecipe> proc, List<CraftingRecipe> craft, List<UpgradeDef> upg, List<ObjectiveDef> obj)
        {
            resources = res; machines = mach; transmitters = trans; processes = proc;
            craftingRecipes = craft; upgrades = upg; objectives = obj;
            entries = null;
        }

        public void EditorSetGame(ResourceDef energyDef, List<MachineDef> prePlaced, List<ResourceStack> inventory)
        {
            energy = energyDef; prePlacedMachines = prePlaced; startingInventory = inventory;
            entries = null;
        }
#endif
    }
}
