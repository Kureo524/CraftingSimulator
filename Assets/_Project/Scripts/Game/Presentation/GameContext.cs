using System;
using Nucleaire.Data;
using Nucleaire.Simulation;

namespace Nucleaire.Game
{
    /// <summary>État partagé entre le monde 3D et l'interface : partie, sélection, objet en cours de pose.</summary>
    public sealed class GameContext
    {
        private IGridEntity selection, contextTarget;
        private ResourceDef buildItem;
        private Machine movingMachine;

        public GameContext(GameSession session, SimulationRunner runner, GameInput input)
        {
            Session = session;
            Runner = runner;
            Input = input;
        }

        public GameInput Input { get; }

        /// <summary>Entité dont le menu contextuel (clic droit) est ouvert.</summary>
        public IGridEntity ContextTarget
        {
            get => contextTarget;
            set { if (contextTarget == value) return; contextTarget = value; ContextTargetChanged?.Invoke(value); }
        }

        /// <summary>Machine en cours de déplacement (suit le curseur jusqu'au clic).</summary>
        public Machine MovingMachine
        {
            get => movingMachine;
            set
            {
                if (movingMachine == value) return;
                movingMachine = value;
                if (value != null) BuildItem = null;
                MovingChanged?.Invoke(value);
            }
        }

        public event Action<IGridEntity> ContextTargetChanged;
        public event Action<Machine> MovingChanged;
        public event Action WorkbenchRequested;
        public event Action InventoryRequested;

        public void OpenWorkbench() { if (Require(PlayerPermission.Craft)) WorkbenchRequested?.Invoke(); }
        public void OpenInventory() => InventoryRequested?.Invoke();

        public GameSession Session { get; }
        public SimulationRunner Runner { get; }
        public World World => Session.World;
        public RecipeDatabase Db => Session.World.Db;

        public IGridEntity Selection
        {
            get => selection;
            set { if (selection == value) return; selection = value; SelectionChanged?.Invoke(value); }
        }

        /// <summary>Objet sélectionné dans l'inventaire pour être posé (null = mode sélection).</summary>
        public ResourceDef BuildItem
        {
            get => buildItem;
            set
            {
                if (buildItem == value) return;
                buildItem = value;
                if (value != null) MovingMachine = null;
                BuildItemChanged?.Invoke(value);
            }
        }

        public event Action<IGridEntity> SelectionChanged;
        public event Action<ResourceDef> BuildItemChanged;
        /// <summary>Demande d'ouverture du livre de recettes sur une ressource (onglet 0 : obtenir, 1 : utilisé dans).</summary>
        public event Action<ResourceDef, int> RecipeBookRequested;
        /// <summary>Message court affiché au joueur (erreur d'action, aide).</summary>
        public event Action<string, EventSeverity> Toast;

        public void OpenRecipeBook(ResourceDef r = null, int tab = 0)
        {
            if (Require(PlayerPermission.RecipeBook)) RecipeBookRequested?.Invoke(r, tab);
        }

        /// <summary>Vérifie qu'une action est permise (tutoriel) ; sinon prévient le joueur.</summary>
        public bool Require(PlayerPermission p)
        {
            if (Session.Permissions.Check(p, out var error)) return true;
            Notify(error, EventSeverity.Info);
            return false;
        }

        public bool IsAllowed(PlayerPermission p) => Session.Permissions.IsAllowed(p);
        public void Notify(string message, EventSeverity severity = EventSeverity.Warning) => Toast?.Invoke(message, severity);
    }
}
