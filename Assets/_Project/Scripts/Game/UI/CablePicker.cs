using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Choix du type de câble, en haut de l'écran, quand le câble universel est équipé :
    /// électricité, liquide, gaz ou objets. Chaque câble posé ne transporte que son type.</summary>
    public sealed class CablePicker
    {
        private static readonly (ResourceKind kind, string label, string icon)[] Kinds =
        {
            (ResourceKind.Energy, "Électricité", "energy"),
            (ResourceKind.Fluid, "Liquide", "water"),
            (ResourceKind.Gas, "Gaz", "hydrogen"),
            (ResourceKind.Item, "Objets", "iron_ingot"),
        };

        private readonly GameContext ctx;
        private readonly VisualElement panel;
        private readonly List<(ResourceKind kind, VisualElement button)> buttons = new();
        private readonly Label hint;

        public CablePicker(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            panel = root.Q("cable-picker");
            Ui.Text("Câble universel", "bold cable-picker__title", panel);
            var row = Ui.El("row", panel);
            foreach (var (kind, label, iconId) in Kinds)
            {
                var k = kind;
                var b = Ui.El("row cable-picker__option", row);
                var res = ctx.Db.Resources.FirstOrDefault(r => r.Id == iconId);
                if (res != null)
                {
                    var icon = new ResourceIcon(res);
                    icon.AddToClassList("cable-picker__icon");
                    b.Add(icon);
                }
                Ui.Text(label, null, b);
                b.RegisterCallback<ClickEvent>(_ => ctx.CableKind = k);
                var t = ctx.Db.Transmitters.FirstOrDefault(x => x.Carries == k);
                if (t != null) Tooltips.Register(b, t.DisplayName, t.Description);
                buttons.Add((k, b));
            }
            hint = Ui.Text("", "system cable-picker__hint", panel);
            ctx.BuildItemChanged += _ => Refresh();
            ctx.CableKindChanged += _ => Refresh();
            ctx.Input.Changed += Refresh;
            Refresh();
        }

        private bool Active => ctx.BuildItem != null && ctx.Db.TransmittersForItem(ctx.BuildItem).Count > 1;

        public void Update()
        {
            if (!Active || !ctx.Input.Pressed(GameInput.CycleCable)) return;
            int i = System.Array.FindIndex(Kinds, k => k.kind == ctx.CableKind);
            ctx.CableKind = Kinds[(i + 1) % Kinds.Length].kind;
        }

        private void Refresh()
        {
            Ui.SetVisible(panel, Active);
            foreach (var (k, b) in buttons) b.EnableInClassList("cable-picker__option--active", k == ctx.CableKind);
            hint.text = $"[{ctx.Input.Display(GameInput.CycleCable)} : changer de type · clic droit : arrêter de poser]";
        }
    }
}
