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
            Inventory = new PlayerInventory(settings.canisterCapacity);
            World.SharedStorage = Inventory;
            Inventory.PrefersHotbar = r => db.PlaceableForItem(r) != null || r == db.SorterItem;
            Permissions = new PermissionGate();
            Actions = new PlayerActions(World, Inventory, Permissions);
            Workbench = new WorkbenchService(World, Inventory, Permissions);
            Objectives = new ObjectiveTracker(World);

            SetupBoard();

            var finalGoal = Objectives.Objectives.LastOrDefault()?.Target.Resource;
            if (finalGoal != null) Goal = new GoalProgress(World, Inventory, finalGoal);
            Tutorial = new TutorialTracker(this, tutorial);
            // Le tutoriel se joue sur un plateau d'essai : à sa fin (ou s'il est passé), on repart de zéro pour de bon.
            Tutorial.Advanced += () => { if (Tutorial.Finished && !BoardWasReset) ResetBoard(); };
            World.Log(EventSeverity.Info, "Bienvenue dans l'atelier. Ouvre le livre de recettes pour voir comment fabriquer l'antimatière.");
        }

        /// <summary>Machines de départ posées au centre de l'atelier, et inventaire de départ.</summary>
        private void SetupBoard()
        {
            var db = World.Db;
            var center = (Vector3Int)(World.Settings.gridSize / 2);
            var placed = db.PrePlacedMachines.Where(m => m != null).ToList();
            for (int i = 0; i < placed.Count; i++)
                World.Place(placed[i], center + new Vector3Int(i * 2 - placed.Count + 1, -2, 0));
            foreach (var s in db.StartingInventory.Where(s => s.IsValid)) Inventory.Add(s.Resource, s.Amount);
        }

        /// <summary>Vide l'atelier, l'inventaire et les statistiques, puis remet le plateau de départ.</summary>
        public void ResetBoard()
        {
            BoardWasReset = true;
            World.Clear();
            World.ResetArea();
            World.ResetProduction();
            Inventory.Clear();
            Objectives.Reset();
            SetupBoard();
            Goal?.Reset();
            World.Log(EventSeverity.Info, "Nouvelle partie : l'atelier est vide, à toi de jouer jusqu'à l'antimatière.");
            BoardReset?.Invoke();
        }

        public bool BoardWasReset { get; private set; }
        public event System.Action BoardReset;

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
