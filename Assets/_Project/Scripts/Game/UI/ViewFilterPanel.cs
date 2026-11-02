using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using Nucleaire.Logistics;
using Nucleaire.Simulation;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Filtre d'affichage (en bas à gauche) : un bouton qui se déplie vers la droite, avec une liste
    /// déroulante qui s'ouvre vers le haut (quoi afficher) et le choix des étages visibles.</summary>
    public sealed class ViewFilterPanel
    {
        private readonly GameContext ctx;
        private readonly WorldView view;
        private readonly VisualElement panel, body;
        private readonly Button toggle;
        private readonly Label count;
        private readonly List<(string label, ResourceDef icon, Func<IGridEntity, bool> keep)> filters = new();
        private int filterIndex, maxLevel = -1;
        private bool expanded;

        public ViewFilterPanel(GameContext ctx, VisualElement root, WorldView view)
        {
            this.ctx = ctx;
            this.view = view;
            panel = root.Q("view-filter");

            toggle = Ui.Button("Affichage ›", Toggle, "btn view-filter__toggle", panel);
            Tooltips.Register(toggle, "Filtrer l'affichage", "Ne montrer qu'un type de machine ou de transmetteur, ou seulement les étages du bas.");
            body = Ui.El("row view-filter__body hidden", panel);

            BuildFilters();
            Ui.Text("Afficher", "meta view-filter__label", body);
            var dd = new GlassDropdown(filters.Select(f => new GlassDropdown.Option(f.label, f.icon)), 0, openUp: true);
            dd.AddToClassList("view-filter__dropdown");
            dd.Changed += i => { filterIndex = i; Apply(); };
            body.Add(dd);

            Ui.Text("Étages", "meta view-filter__label", body);
            int levels = ctx.World.Settings.levels;
            var levelLabels = new List<string> { "Tous" };
            for (int i = 1; i < levels; i++) levelLabels.Add(i == 1 ? "1" : $"1–{i}");
            var lv = new SegmentedControl(levelLabels);
            lv.Changed += i => { maxLevel = i == 0 ? -1 : i - 1; Apply(); };
            body.Add(lv);
            Tooltips.Register(lv, "Étages visibles", "Masque les étages au-dessus du choix, pour voir à l'intérieur d'une construction en hauteur.");

            count = Ui.Text("", "mono view-filter__count", body);
            var reset = Ui.Button("×", () => { dd.Select(0); lv.Select(0); }, "btn btn--ghost btn--icon", body);
            Tooltips.Register(reset, "Tout afficher");
            Apply();
        }

        private void BuildFilters()
        {
            filters.Add(("Tout", null, _ => true));
            filters.Add(("Machines", null, e => e is Machine));
            filters.Add(("Transmetteurs", null, e => e is TransmitterSegment));
            filters.Add(("Énergie : câbles et générateurs", ctx.Db.Energy, e =>
                e is TransmitterSegment { TransmitterDef: { Carries: ResourceKind.Energy } } || e is Machine { ProducesEnergy: true }));
            filters.Add(("Liquides : tuyaux", ctx.Db.Resources.FirstOrDefault(r => r.Id == "water"), e => e is TransmitterSegment { TransmitterDef: { Carries: ResourceKind.Fluid } }));
            filters.Add(("Gaz : tubes pressurisés", ctx.Db.Resources.FirstOrDefault(r => r.Id == "hydrogen"), e => e is TransmitterSegment { TransmitterDef: { Carries: ResourceKind.Gas } }));
            filters.Add(("Objets : convoyeurs", ctx.Db.Resources.FirstOrDefault(r => r.Id == "iron_ingot"), e => e is TransmitterSegment { TransmitterDef: { Carries: ResourceKind.Item } }));
            foreach (var m in ctx.Db.Machines.Where(m => m != null).OrderBy(m => m.DisplayName))
            {
                var def = m;
                filters.Add((def.DisplayName, def.Item, e => e is Machine mm && mm.MachineDef == def));
            }
        }

        private void Toggle()
        {
            expanded = !expanded;
            Ui.SetVisible(body, expanded);
            toggle.text = expanded ? "Affichage ‹" : "Affichage ›";
        }

        private void Apply()
        {
            var keep = filters[filterIndex].keep;
            int lvl = maxLevel;
            bool active = filterIndex != 0 || lvl >= 0;
            view.Filter = active ? e => keep(e) && (lvl < 0 || e.Position.z <= lvl) : null;
            panel.EnableInClassList("view-filter--active", active);
        }

        public void Update()
        {
            if (expanded) count.text = $"{view.VisibleCount} / {view.TotalCount}";
        }
    }
}
