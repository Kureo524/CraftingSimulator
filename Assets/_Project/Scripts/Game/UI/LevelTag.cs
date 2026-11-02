using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Étiquette « Étage N » à côté de l'aperçu de pose : on sait toujours où l'objet va se poser.</summary>
    public sealed class LevelTag
    {
        private readonly GameContext ctx;
        private readonly Camera cam;
        private readonly VisualElement layer;
        private readonly Label tag;

        public LevelTag(GameContext ctx, VisualElement root, Camera cam)
        {
            this.ctx = ctx;
            this.cam = cam;
            layer = root.Q("overlay");
            tag = Ui.Text("", "level-tag hidden", layer);
            tag.pickingMode = PickingMode.Ignore;
        }

        public void Update()
        {
            var cell = ctx.PlacementCell;
            bool show = cell != null && layer.panel != null && (ctx.BuildItem != null || ctx.MovingMachine != null);
            if (show)
            {
                var c = cell.Value;
                var p3 = GridMath.CellCenter(c, 1.05f);
                if (cam.WorldToViewportPoint(p3).z <= 0) show = false;
                else
                {
                    var p = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, p3, cam);
                    tag.style.left = p.x;
                    tag.style.top = p.y;
                    tag.text = c.z == 0 ? "Étage 1 · sol" : $"Étage {c.z + 1} · au-dessus";
                    tag.EnableInClassList("level-tag--high", c.z > 0);
                }
            }
            Ui.SetVisible(tag, show);
        }
    }
}
