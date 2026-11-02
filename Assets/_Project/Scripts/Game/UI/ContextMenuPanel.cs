using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Petit menu au-dessus d'une machine (clic droit) : déplacer, supprimer, fermer.
    /// Se ferme avec la croix, Échap, ou un clic n'importe où ailleurs.</summary>
    public sealed class ContextMenuPanel
    {
        private readonly GameContext ctx;
        private readonly VisualElement layer, menu;
        private readonly Camera cam;
        private IGridEntity target;

        public ContextMenuPanel(GameContext ctx, VisualElement root, Camera cam)
        {
            this.ctx = ctx;
            this.cam = cam;
            layer = root.Q("context-layer");
            menu = Ui.El("glass glass--opaque context-menu hidden", layer);
            ctx.ContextTargetChanged += Show;
            // Un clic dans l'interface en dehors du menu le ferme.
            root.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (target != null && evt.target is VisualElement v && !menu.Contains(v)) ctx.ContextTarget = null;
            }, TrickleDown.TrickleDown);
            ctx.World.EntityRemoved += e => { if (e == target) ctx.ContextTarget = null; };
        }

        public bool IsOpen => target != null;

        private void Show(IGridEntity e)
        {
            target = e;
            menu.Clear();
            Ui.SetVisible(menu, e != null);
            if (e == null) return;

            var head = Ui.El("row context-menu__head", menu);
            if (e.Def.Item != null) IconSlot.Create(e.Def.Item, head, classes: "rslot--small");
            else Ui.Swatch(Palette.Muted(e.Def.Color), head);
            Ui.Text(e.Def.DisplayName, "bold grow", head);
            Ui.Button("×", () => ctx.ContextTarget = null, "btn btn--ghost btn--icon context-menu__close", head);

            if (e is Machine m)
                Ui.Button("Déplacer la machine", () =>
                {
                    ctx.ContextTarget = null;
                    if (!ctx.Require(PlayerPermission.Move)) return;
                    ctx.Selection = null;
                    ctx.MovingMachine = m;
                }, "btn context-menu__item", menu);

            var del = Ui.Button(e is Machine ? "Supprimer la machine" : "Supprimer", () =>
            {
                ctx.ContextTarget = null;
                if (ctx.Session.Actions.TryPickUp(e.Position, out var err))
                    ctx.Notify($"{e.Def.DisplayName} rangé dans l'inventaire.", EventSeverity.Info);
                else ctx.Notify(err);
            }, "btn btn--danger context-menu__item", menu);
            Tooltips.Register(del, "Supprimer", "L'objet revient dans ton inventaire, avec les solides et améliorations qu'il contenait. Les liquides et gaz sont perdus.");
            Update();
        }

        public void Update()
        {
            if (target == null || layer.panel == null) return;
            var p = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, GridMath.CellCenter(target.Position, 1.1f), cam);
            menu.style.left = p.x;
            menu.style.top = p.y;
        }
    }
}
