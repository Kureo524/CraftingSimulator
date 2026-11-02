using System;
using System.Linq;
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
            session.World.EntityRemoved += e =>
            {
                if (selection == e) Selection = null;
                if (contextTarget == e) ContextTarget = null;
            };
            session.BoardReset += () => { Selection = null; ContextTarget = null; BuildItem = null; MovingMachine = null; };
            Hotbar = new Hotbar(this);
            Plan = new BuildPlan(this);
            Deletion = new DeleteMode(this);
            Configure = new ConfigureMode(this);
        }

        /// <summary>Mode modification (faces des machines, côtés des câbles).</summary>
        public ConfigureMode Configure { get; }

        /// <summary>Un mode outil (suppression ou modification) est actif : pas de panneaux.</summary>
        public bool ToolModeActive => Deletion.Active || Configure.Active;

        /// <summary>Touche A : Normal → Suppression → Modification → Normal.</summary>
        public void CycleToolMode()
        {
            if (Deletion.Active) { Deletion.Exit(); Configure.Enter(); return; }
            if (Configure.Active) { Configure.Exit(); return; }
            if (!Require(PlayerPermission.PickUp)) return;
            Deletion.Enter();
        }

        /// <summary>Ce qui est sous le curseur dans l'atelier (entité, face visée, trieur).</summary>
        public (IGridEntity entity, Direction? face, FaceSorter sorter) Hover { get; set; }

        /// <summary>Case où l'objet en main sera posé (null : aucune) ; sert au repère d'étage.</summary>
        public UnityEngine.Vector3Int? PlacementCell { get; set; }

        /// <summary>Face dont le trieur doit être montré à l'ouverture de la machine.</summary>
        public Direction? FocusSorterFace { get; set; }

        /// <summary>Pose multiple en attente de validation.</summary>
        public BuildPlan Plan { get; }
        /// <summary>Mode suppression (touche A).</summary>
        public DeleteMode Deletion { get; }

        private Direction buildFacing = Direction.North;

        /// <summary>Orientation des machines posées (R pendant la pose).</summary>
        public Direction BuildFacing
        {
            get => buildFacing;
            set { if (buildFacing == value) return; buildFacing = value; BuildFacingChanged?.Invoke(value); }
        }

        public event Action<Direction> BuildFacingChanged;

        /// <summary>Barre rapide : l'objet équipé est celui qui se pose.</summary>
        public Hotbar Hotbar { get; }

        private ResourceKind cableKind = ResourceKind.Energy;

        /// <summary>Type de câble posé avec le câble universel (électricité, liquide, gaz, objets).</summary>
        public ResourceKind CableKind
        {
            get => cableKind;
            set { if (cableKind == value) return; cableKind = value; CableKindChanged?.Invoke(value); }
        }

        public event Action<ResourceKind> CableKindChanged;

        /// <summary>Ce qui sera réellement posé avec l'objet équipé (le bon câble pour le câble universel).</summary>
        public PlaceableDef BuildPlaceable
        {
            get
            {
                if (buildItem == null) return null;
                var variants = Db.TransmittersForItem(buildItem);
                if (variants.Count > 1) return variants.FirstOrDefault(t => t.Carries == cableKind) ?? variants[0];
                return Db.PlaceableForItem(buildItem);
            }
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
        /// <summary>Ouverture de l'établi, éventuellement sur la recette d'une ressource.</summary>
        public event Action<ResourceDef> WorkbenchRequested;
        public event Action InventoryRequested;

        public void OpenWorkbench(ResourceDef focus = null) { if (!ToolModeActive && Require(PlayerPermission.Craft)) WorkbenchRequested?.Invoke(focus); }
        public void OpenInventory() { if (!ToolModeActive) InventoryRequested?.Invoke(); }

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
            if (!ToolModeActive && Require(PlayerPermission.RecipeBook)) RecipeBookRequested?.Invoke(r, tab);
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
