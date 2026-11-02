using System;
using Nucleaire.Data;
using Nucleaire.Simulation;

namespace Nucleaire.Game
{
    /// <summary>État partagé entre le monde 3D et l'interface : partie, sélection, objet en cours de pose.</summary>
    public sealed class GameContext
    {
        private IGridEntity selection;
        private ResourceDef buildItem;

        public GameContext(GameSession session, SimulationRunner runner)
        {
            Session = session;
            Runner = runner;
        }

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
            set { if (buildItem == value) return; buildItem = value; BuildItemChanged?.Invoke(value); }
        }

        public event Action<IGridEntity> SelectionChanged;
        public event Action<ResourceDef> BuildItemChanged;
        /// <summary>Demande d'ouverture du livre de recettes sur une ressource.</summary>
        public event Action<ResourceDef> RecipeBookRequested;
        /// <summary>Message court affiché au joueur (erreur d'action, aide).</summary>
        public event Action<string, EventSeverity> Toast;

        public void OpenRecipeBook(ResourceDef r = null)
        {
            if (Require(PlayerPermission.RecipeBook)) RecipeBookRequested?.Invoke(r);
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
