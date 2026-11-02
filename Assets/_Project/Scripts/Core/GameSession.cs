using System.Linq;
using Nucleaire.Crafting;
using Nucleaire.Data;
using Nucleaire.Objectives;
using Nucleaire.Simulation;
using UnityEngine;

namespace Nucleaire
{
    /// <summary>Une partie : l'atelier, l'inventaire du joueur et les services, initialisés depuis les données.</summary>
    public sealed class GameSession
    {
        public GameSession(RecipeDatabase db, SimulationSettings settings, TutorialDef tutorial = null)
        {
            World = new World(db, settings);
            Inventory = new PlayerInventory();
            Permissions = new PermissionGate();
            Actions = new PlayerActions(World, Inventory, Permissions);
            Workbench = new WorkbenchService(World, Inventory, Permissions);
            Objectives = new ObjectiveTracker(World);

            // Machines fixes posées côte à côte au centre de l'atelier.
            var center = settings.gridSize / 2;
            var placed = db.PrePlacedMachines.Where(m => m != null).ToList();
            for (int i = 0; i < placed.Count; i++)
                World.Place(placed[i], center + new Vector2Int(i * 2 - placed.Count + 1, -2));

            foreach (var s in db.StartingInventory.Where(s => s.IsValid)) Inventory.Add(s.Resource, s.Amount);

            var finalGoal = Objectives.Objectives.LastOrDefault()?.Target.Resource;
            if (finalGoal != null) Goal = new GoalProgress(World, Inventory, finalGoal);
            Tutorial = new TutorialTracker(this, tutorial);
            World.Log(EventSeverity.Info, "Bienvenue dans l'atelier. Ouvre le livre de recettes pour voir comment fabriquer l'antimatière.");
        }

        public World World { get; }
        public PlayerInventory Inventory { get; }
        /// <summary>Actions permises au joueur (restreintes pendant le tutoriel).</summary>
        public PermissionGate Permissions { get; }
        /// <summary>Tutoriel guidé (inactif si aucune donnée de tutoriel).</summary>
        public TutorialTracker Tutorial { get; }
        public PlayerActions Actions { get; }
        public WorkbenchService Workbench { get; }
        public ObjectiveTracker Objectives { get; }
        /// <summary>Progression dans l'arbre de l'objectif final (le dernier objectif).</summary>
        public GoalProgress Goal { get; }
    }
}
