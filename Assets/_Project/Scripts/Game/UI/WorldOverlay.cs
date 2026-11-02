using System.Collections.Generic;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Pastille d'état au-dessus des machines en difficulté (orange : bloquée, rouge : critique).
    /// Le détail du problème s'affiche dans le panneau de la machine, pas dans le monde.</summary>
    public sealed class WorldOverlay
    {
        private readonly VisualElement layer;
        private readonly Camera cam;
        private readonly WorldView view;
        private readonly Dictionary<Machine, VisualElement> dots = new();

        public WorldOverlay(VisualElement layer, World world, Camera cam, WorldView view)
        {
            this.layer = layer;
            this.cam = cam;
            this.view = view;
            foreach (var m in world.Machines) Add(m);
            world.EntityPlaced += e => { if (e is Machine m) Add(m); };
            world.EntityRemoved += e =>
            {
                if (e is Machine m && dots.TryGetValue(m, out var d)) { d.RemoveFromHierarchy(); dots.Remove(m); }
            };
        }

        private void Add(Machine m)
        {
            var dot = Ui.El("status-dot", layer);
            dot.pickingMode = PickingMode.Ignore;
            dots[m] = dot;
        }

        public void Update()
        {
            if (layer.panel == null) return;
            foreach (var (m, dot) in dots)
            {
                var state = Ui.StatusState(m.Status);
                bool show = m.Status.IsProblem() && view.IsVisible(m);
                var p3 = GridMath.CellCenter(m.Position, 1.05f);
                if (show && cam.WorldToViewportPoint(p3).z <= 0) show = false;
                dot.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show) continue;
                dot.EnableInClassList("status-dot--danger", state == "danger");
                dot.EnableInClassList("status-dot--warning", state != "danger");
                var p = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, p3, cam);
                dot.style.left = p.x;
                dot.style.top = p.y;
            }
        }
    }
}
