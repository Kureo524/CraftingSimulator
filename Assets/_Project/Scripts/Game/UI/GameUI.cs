using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

using Nucleaire.Data;

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
        private readonly CatalogPanel catalog;
        private readonly TutorialGuide guide;
        private readonly ItemTransfer transfer;
        private readonly ViewFilterPanel viewFilter;
        private readonly CablePicker cablePicker;
        private readonly ConfigureOverlay configure;
        private readonly LevelTag levelTag;

        public GameUI(GameContext ctx, UIDocument document, Camera cam, WorldView view)
        {
            this.ctx = ctx;
            this.document = document;
            var root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            foreach (var child in root.Children()) child.pickingMode = PickingMode.Ignore;

            new Tooltips(root.Q("tooltip-layer"));
            GlassDropdown.Init(root.Q("popup-layer"));
            new GlassLight(root);
            overlay = new WorldOverlay(root.Q("overlay"), ctx.World, cam, view);
            hud = new HudPanel(ctx, root);
            new HotbarPanel(ctx, root);
            inspector = new InspectorPanel(ctx, root.Q("inspector"));
            contextMenu = new ContextMenuPanel(ctx, root, cam);
            viewFilter = new ViewFilterPanel(ctx, root, view);
            cablePicker = new CablePicker(ctx, root);
            new ModePanels(ctx, root);
            new CreativeToggle(ctx, root);
            configure = new ConfigureOverlay(ctx, root, cam);
            levelTag = new LevelTag(ctx, root, cam);
            ctx.Deletion.Changed += () => { if (ctx.Deletion.Active) CloseAll(); };
            ctx.Configure.Changed += () => { if (ctx.Configure.Active) CloseAll(); };
            inventory = new InventoryPanel(ctx, root);
            transfer = new ItemTransfer(ctx, root);
            // Ouvrir une machine ouvre l'écran machine + inventaire ; le fermer abandonne la sélection.
            inventory.Closed += () => { transfer.Cancel(); ctx.Selection = null; };
            ctx.SelectionChanged += e =>
            {
                if (e != null) inventory.Open();
                else if (inventory.IsOpen) inventory.Rebuild();
            };
            workbench = new WorkbenchPanel(ctx, root);
            options = new OptionsPanel(ctx, root);
            Book = new RecipeBookPanel(ctx, root);
            catalog = new CatalogPanel(ctx, root);
            IconSlot.KeyHint = () => $"{ctx.Input.Display(GameInput.ShowRecipe)} : recette · {ctx.Input.Display(GameInput.ShowUsage)} : usages";
            tutorial = new TutorialPanel(ctx, ctx.Session.Tutorial, root);
            guide = new TutorialGuide(ctx, ctx.Session.Tutorial, tutorial, root, cam, view);
        }

        public RecipeBookPanel Book { get; }

        /// <summary>Un panneau utilise lui-même certaines touches (R / U sur une icône, inventaire, livre, options).</summary>
        public bool CapturesKeys => inventory.IsOpen || Book.IsOpen || options.IsOpen || ctx.Input.IsRebinding || HoveredResource() != null;
        public bool IsTyping
        {
            get
            {
                var focused = document.rootVisualElement?.panel?.focusController?.focusedElement as VisualElement;
                for (var e = focused; e != null; e = e.parent)
                    if (e.ClassListContains("unity-base-text-field")) return true;
                return false;
            }
        }

        /// <summary>Annule l'objet en main ou le glisser en cours. Renvoie vrai s'il y en avait un.</summary>
        public bool CancelTransfer() => transfer.Cancel();

        /// <summary>Ressource dont l'icône est sous le curseur (n'importe quel panneau), ou null.</summary>
        public ResourceDef HoveredResource()
        {
            var panel = document.rootVisualElement?.panel;
            var mouse = Mouse.current;
            if (panel == null || mouse == null) return null;
            var pos = mouse.position.ReadValue();
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(pos.x, Screen.height - pos.y));
            return IconSlot.ResourceUnder(panel.Pick(p));
        }

        public void ToggleInventory() => inventory.Toggle();
        public void ToggleWorkbench() => workbench.Toggle();
        public void ToggleOptions() => options.Toggle();

        /// <summary>Ferme le panneau du dessus. Renvoie faux s'il n'y avait rien à fermer.</summary>
        public bool CloseTopPanel()
        {
            if (GlassDropdown.CloseOpen()) return true;
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
            catalog.SetVisible(false);
            ctx.ContextTarget = null;
        }

        /// <summary>Touches 1 à 9 : équiper un emplacement ; inventaire ouvert et objet survolé : l'y ranger.</summary>
        private void UpdateHotbarKeys()
        {
            if (IsTyping || options.IsOpen || ctx.Input.IsRebinding || ctx.ToolModeActive) return;
            for (int i = 0; i < Hotbar.Size; i++)
            {
                if (!ctx.Input.Pressed(GameInput.HotbarSlot(i))) continue;
                if (inventory.IsOpen)
                {
                    // Survol d'une pile + chiffre : l'échanger avec cet emplacement de la barre rapide.
                    var panel = document.rootVisualElement?.panel;
                    var mouse = UnityEngine.InputSystem.Mouse.current;
                    if (panel != null && mouse != null)
                    {
                        var pos = mouse.position.ReadValue();
                        int from = InventorySlotView.IndexUnder(panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, new Vector2(pos.x, Screen.height - pos.y))));
                        if (from >= 0 && from != i) ctx.Session.Inventory.Move(from, i, ctx.Session.Inventory.Slots[from].Amount);
                    }
                }
                else if (!workbench.IsOpen && !Book.IsOpen) ctx.Hotbar.Select(i);
                return;
            }
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
            tutorial.Compact = inventory.IsOpen || workbench.IsOpen || Book.IsOpen || options.IsOpen;
            tutorial.Update();
            transfer.Update();
            viewFilter.Update();
            cablePicker.Update();
            configure.Update();
            levelTag.Update();
            UpdateHotbarKeys();
            guide.Update();
            contextMenu.Update();
            catalog.SetVisible(inventory.IsOpen || Book.IsOpen);

            // R / U sur n'importe quelle icône survolée (inventaire, catalogue, livre, machine, vault…).
            if (!IsTyping && !options.IsOpen && !ctx.Input.IsRebinding && !ctx.ToolModeActive)
            {
                bool recipe = ctx.Input.Pressed(GameInput.ShowRecipe), usage = ctx.Input.Pressed(GameInput.ShowUsage);
                if (recipe || usage)
                {
                    var r = HoveredResource();
                    if (r != null) ctx.OpenRecipeBook(r, usage ? 1 : 0);
                }
            }
        }
    }
}
