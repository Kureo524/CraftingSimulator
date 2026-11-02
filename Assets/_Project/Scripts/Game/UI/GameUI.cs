using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Racine de l'interface : instancie les panneaux sur le document UI Toolkit.</summary>
    public sealed class GameUI
    {
        private readonly UIDocument document;
        private readonly HudPanel hud;
        private readonly InspectorPanel inspector;
        private readonly WorldOverlay overlay;
        private readonly TutorialPanel tutorial;

        public GameUI(GameContext ctx, UIDocument document, Camera cam)
        {
            this.document = document;
            var root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            foreach (var child in root.Children()) child.pickingMode = PickingMode.Ignore;

            new Tooltips(root.Q("tooltip-layer"));
            new GlassLight(root);
            overlay = new WorldOverlay(root.Q("overlay"), ctx.World, cam);
            hud = new HudPanel(ctx, root);
            new HotbarPanel(ctx, root);
            inspector = new InspectorPanel(ctx, root.Q("inspector"));
            Book = new RecipeBookPanel(ctx, root);
            tutorial = new TutorialPanel(ctx, ctx.Session.Tutorial, root);
        }

        public RecipeBookPanel Book { get; }
        public InspectorPanel Inspector => inspector;

        /// <summary>Le pointeur est-il au-dessus d'un élément d'interface cliquable ?</summary>
        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            var panel = document.rootVisualElement?.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return panel.Pick(p) != null;
        }

        public bool IsTyping => Book.IsTyping;

        public void Update(float cameraYaw)
        {
            hud.Update(cameraYaw);
            inspector.Update();
            overlay.Update();
            tutorial.Update();
        }
    }
}
