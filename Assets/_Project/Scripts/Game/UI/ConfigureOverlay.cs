using Nucleaire.Logistics;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Mode modification : étiquettes sur les six faces de la machine (ou les côtés du câble) sous le curseur,
    /// et panneau en haut de l'écran avec le dernier réglage.</summary>
    public sealed class ConfigureOverlay
    {
        private readonly GameContext ctx;
        private readonly Camera cam;
        private readonly VisualElement layer, panel;
        private readonly Label[] labels = new Label[6];
        private Label hover, last;

        public ConfigureOverlay(GameContext ctx, VisualElement root, Camera cam)
        {
            this.ctx = ctx;
            this.cam = cam;
            layer = root.Q("overlay");
            panel = root.Q("configure-panel");
            for (int i = 0; i < 6; i++)
            {
                labels[i] = Ui.Text("", "face-tag hidden", layer);
                labels[i].pickingMode = PickingMode.Ignore;
            }
            BuildPanel();
            ctx.Configure.Changed += () => { Ui.SetVisible(panel, ctx.Configure.Active); last.text = ctx.Configure.LastChange == null ? "" : $"[{ctx.Configure.LastChange}]"; };
            ctx.Input.Changed += BuildPanel;
            Ui.SetVisible(panel, false);
        }

        private void BuildPanel()
        {
            panel.Clear();
            var head = Ui.El("row mode-panel__head", panel);
            Ui.Text("Mode modification", "bold grow mode-panel__accent", head);
            Ui.Text($"{ctx.Input.Display(GameInput.DeleteToggle)} : mode suivant", "meta", head);
            Ui.Text("[Machine : clic sur une face = réglage suivant, clic droit = précédent (neutre → entrée → entrée 1, 2… → sortie → sortie 1, 2…).]", "system mode-panel__hint", panel);
            Ui.Text("[Câble : côté collé à une machine = normal → tirer → pousser. Entre deux câbles : couper ou relier.]", "system mode-panel__hint", panel);
            hover = Ui.Text("", "mode-panel__line mode-panel__hover", panel);
            last = Ui.Text("", "system mode-panel__ok", panel);
            var actions = Ui.El("row mode-panel__actions", panel);
            Ui.El("spacer", actions);
            Ui.Button("Terminer", ctx.Configure.Exit, "btn btn--primary", actions);
        }

        public void Update()
        {
            bool active = ctx.Configure.Active && layer.panel != null;
            var (entity, face, _) = ctx.Hover;
            for (int i = 0; i < 6; i++)
            {
                var d = (Direction)i;
                string text = !active ? null : entity switch
                {
                    Machine m => m.SorterAt(d) != null ? "Trieur" : m.SideLabel(d),
                    TransmitterSegment seg => CableLabel(seg, d),
                    _ => null,
                };
                var l = labels[i];
                if (string.IsNullOrEmpty(text)) { Ui.SetVisible(l, false); continue; }
                var c = GridMath.CellCenter(entity.Position, entity is Machine ? 0.5f : 0.15f);
                var p3 = d.IsVertical() ? c + Vector3.up * (d == Direction.Up ? 0.62f : -0.4f) : c + GridMath.DirectionVector(d) * (entity is Machine ? 0.62f : 0.5f);
                if (cam.WorldToViewportPoint(p3).z <= 0) { Ui.SetVisible(l, false); continue; }
                var p = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, p3, cam);
                l.text = text;
                l.style.left = p.x;
                l.style.top = p.y;
                l.EnableInClassList("face-tag--current", face == d);
                l.EnableInClassList("face-tag--none", text is "Neutre" or "Normal");
                Ui.SetVisible(l, true);
            }
            if (active && hover != null)
                hover.text = entity == null || face == null ? "Vise une face de machine ou un côté de câble."
                    : entity is Machine mm ? $"{mm.MachineDef.DisplayName} · face {face.Value.DisplayName().ToLowerInvariant()} : {(mm.SorterAt(face.Value) != null ? "trieur" : mm.SideLabel(face.Value))}"
                    : entity is TransmitterSegment s ? $"{s.TransmitterDef.DisplayName} · côté {face.Value.DisplayName().ToLowerInvariant()} : {CableLabel(s, face.Value) ?? "rien à régler"}" : "";
        }

        private string CableLabel(TransmitterSegment seg, Direction d)
        {
            var n = ctx.World.At(seg.Position + d.Offset());
            return n switch
            {
                Machine => seg[d] switch { CableSide.Pull => "Tirer", CableSide.Push => "Pousser", _ => "Normal" },
                TransmitterSegment other when other.TransmitterDef == seg.TransmitterDef => seg.ConnectsTo(other) ? "Relié" : "Coupé",
                _ => null,
            };
        }
    }
}
