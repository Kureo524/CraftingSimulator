using System.Collections.Generic;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Indicateurs au-dessus des machines : pastille d'état en cas de problème, barre de progression en marche.</summary>
    public sealed class WorldOverlay
    {
        private sealed class Marker
        {
            public VisualElement Root, Badge, Fill, Gauge;
            public MachineStatus Shown = (MachineStatus)(-1);
        }

        private readonly VisualElement layer;
        private readonly World world;
        private readonly Camera cam;
        private readonly Dictionary<Machine, Marker> markers = new();

        public WorldOverlay(VisualElement layer, World world, Camera cam)
        {
            this.layer = layer;
            this.world = world;
            this.cam = cam;
            foreach (var m in world.Machines) Add(m);
            world.EntityPlaced += e => { if (e is Machine m) Add(m); };
            world.EntityRemoved += e =>
            {
                if (e is Machine m && markers.TryGetValue(m, out var mk)) { mk.Root.RemoveFromHierarchy(); markers.Remove(m); }
            };
        }

        private void Add(Machine m)
        {
            if (m is VaultMachine or WorkbenchMachine) return;
            var mk = new Marker { Root = Ui.El("overlay-marker", layer) };
            mk.Root.pickingMode = PickingMode.Ignore;
            mk.Badge = Ui.Badge("", null, mk.Root);
            mk.Gauge = Ui.Gauge(mk.Root, out mk.Fill);
            mk.Gauge.AddToClassList("overlay-gauge");
            markers[m] = mk;
        }

        public void Update()
        {
            if (layer.panel == null) return;
            foreach (var (m, mk) in markers)
            {
                var status = m.Status;
                bool problem = status.IsProblem();
                bool working = status == MachineStatus.Working;
                if (status != mk.Shown)
                {
                    mk.Shown = status;
                    Ui.SetBadge(mk.Badge, status.DisplayName(), Ui.StatusState(status));
                }
                Ui.SetVisible(mk.Badge, problem);
                Ui.SetVisible(mk.Gauge, working && m.Progress > 0f && m is not BatteryMachine);
                Ui.SetVisible(mk.Root, problem || working);
                if (working) mk.Fill.style.width = Length.Percent(m.Progress * 100f);

                var world3 = GridMath.CellCenter(m.Position, 1.15f);
                Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, world3, cam);
                bool onScreen = cam.WorldToViewportPoint(world3).z > 0;
                mk.Root.style.display = onScreen && (problem || working) ? DisplayStyle.Flex : DisplayStyle.None;
                mk.Root.style.left = p.x;
                mk.Root.style.top = p.y;
            }
        }
    }
}
