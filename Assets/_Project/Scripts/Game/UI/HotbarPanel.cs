using System.Linq;
using Nucleaire.Data;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Inventaire du joueur en bas de l'écran. Cliquer un élément posable passe en mode pose.</summary>
    public sealed class HotbarPanel
    {
        private readonly GameContext ctx;
        private readonly VisualElement hotbar;
        private readonly ScrollView scroll;
        private readonly SegmentedControl filter;

        private static readonly string[] Categories = { "Tout", "Machines", "Transport", "Matériaux", "Améliorations" };

        public HotbarPanel(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            hotbar = root.Q("hotbar");
            filter = new SegmentedControl(Categories);
            filter.AddToClassList("hotbar__filter");
            filter.Changed += _ => Rebuild();
            hotbar.Add(filter);
            scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.AddToClassList("hotbar__scroll");
            hotbar.Add(scroll);

            ctx.Session.Inventory.Changed += Rebuild;
            ctx.BuildItemChanged += _ => Rebuild();
            Rebuild();
        }

        private int CategoryOf(ResourceDef r)
        {
            var p = ctx.Db.PlaceableForItem(r);
            if (p is MachineDef) return 1;
            if (p is TransmitterDef) return 2;
            if (ctx.Db.UpgradeForItem(r) != null) return 4;
            return 3;
        }

        private void Rebuild()
        {
            scroll.Clear();
            int cat = filter.Selected;
            var items = ctx.Session.Inventory.Items
                .Where(kv => cat == 0 || CategoryOf(kv.Key) == cat)
                .OrderBy(kv => CategoryOf(kv.Key)).ThenBy(kv => kv.Key.DisplayName)
                .ToList();

            if (items.Count == 0)
            {
                Ui.Text(cat == 0 ? "[Inventaire vide. Prends des ressources dans le vault.]" : "[Rien dans cette catégorie.]", "system hotbar__empty", scroll);
                return;
            }

            foreach (var (r, count) in items)
            {
                var placeable = ctx.Db.PlaceableForItem(r);
                var slot = Ui.El("slot", scroll);
                slot.EnableInClassList("slot--active", ctx.BuildItem == r);
                slot.EnableInClassList("slot--placeable", placeable != null);
                Ui.Swatch(Palette.Muted(r.Color), slot).AddToClassList("slot__swatch");
                Ui.Text(count.ToString(), "number--small slot__count", slot);
                Ui.Text(r.DisplayName, "meta slot__name", slot);

                slot.RegisterCallback<ClickEvent>(_ =>
                {
                    if (placeable != null)
                    {
                        if (ctx.BuildItem == r) ctx.BuildItem = null;
                        else if (ctx.Require(PlayerPermission.Place)) ctx.BuildItem = r;
                    }
                    else ctx.OpenRecipeBook(r);
                });
                Tooltips.Register(slot, () => (r.DisplayName,
                    (string.IsNullOrEmpty(r.Description) ? "" : r.Description + "\n") +
                    (placeable != null ? "Clic : poser dans l'atelier." : ctx.Db.UpgradeForItem(r) != null
                        ? "S'installe depuis le panneau d'une machine. Clic : livre de recettes."
                        : "Clic : livre de recettes.")));
            }
        }
    }
}
