using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Racine de l'interface : instancie les panneaux sur le document UI Toolkit.</summary>
    public sealed class GameUI
    {
        private readonly GameContext ctx;
        private readonly UIDocument document;
        private readonly HudPanel hud;
        private readonly InspectorPanel inspector;
        private readonly WorldOverlay overlay;
        private readonly TutorialPanel tutorial;
        private readonly ContextMenuPanel contextMenu;
        private readonly InventoryPanel inventory;
        private readonly WorkbenchPanel workbench;
        private readonly OptionsPanel options;

        public GameUI(GameContext ctx, UIDocument document, Camera cam, WorldView view)
        {
            this.ctx = ctx;
            this.document = document;
            var root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            foreach (var child in root.Children()) child.pickingMode = PickingMode.Ignore;

            new Tooltips(root.Q("tooltip-layer"));
            new GlassLight(root);
            overlay = new WorldOverlay(root.Q("overlay"), ctx.World, cam, view);
            hud = new HudPanel(ctx, root);
            new HotbarPanel(ctx, root);
            inspector = new InspectorPanel(ctx, root.Q("inspector"));
            contextMenu = new ContextMenuPanel(ctx, root, cam);
            inventory = new InventoryPanel(ctx, root);
            workbench = new WorkbenchPanel(ctx, root);
            options = new OptionsPanel(ctx, root);
            Book = new RecipeBookPanel(ctx, root);
            tutorial = new TutorialPanel(ctx, ctx.Session.Tutorial, root);
        }

        public RecipeBookPanel Book { get; }

        /// <summary>Un panneau utilise lui-même certaines touches (ex. R/U dans l'inventaire).</summary>
        public bool CapturesKeys => inventory.IsOpen || options.IsOpen || ctx.Input.IsRebinding;
        public bool IsTyping => Book.IsTyping || inventory.IsTyping;

        public void ToggleInventory() => inventory.Toggle();
        public void ToggleWorkbench() => workbench.Toggle();
        public void ToggleOptions() => options.Toggle();

        /// <summary>Ferme le panneau du dessus. Renvoie faux s'il n'y avait rien à fermer.</summary>
        public bool CloseTopPanel()
        {
            if (options.IsOpen) { if (!options.JustRebound) options.Close(); return true; }
            if (Book.IsOpen) { Book.Close(); return true; }
            if (workbench.IsOpen) { workbench.Close(); return true; }
            if (inventory.IsOpen) { inventory.Close(); return true; }
            return false;
        }

        public void CloseAll()
        {
            options.Close();
            Book.Close();
            workbench.Close();
            inventory.Close();
            ctx.ContextTarget = null;
        }

        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            var panel = document.rootVisualElement?.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return panel.Pick(p) != null;
        }

        public void Update(float cameraYaw, bool cutaway)
        {
            hud.Update(cameraYaw, cutaway);
            inspector.Update();
            overlay.Update();
            tutorial.Update();
            contextMenu.Update();
            inventory.Update(ctx.Input);
        }
    }
}
