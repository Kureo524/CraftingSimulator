using System.Linq;
using Nucleaire.Data;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Catalogue de tout ce qui existe dans le jeu, collé au bord droit de l'écran (comme la liste de JEI/NEI).
    /// Visible avec l'inventaire et le livre de recettes. Icônes seules ; nom au survol ; R / U / clic.</summary>
    public sealed class CatalogPanel
    {
        private static readonly string[] Categories = { "Tout", "Objets", "Liquides", "Gaz", "Machines" };

        private readonly GameContext ctx;
        private readonly VisualElement panel, grid;
        private readonly TextField search;
        private readonly SegmentedControl filter;
        private readonly Label count;

        public CatalogPanel(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            panel = root.Q("catalog");
            var head = Ui.El("row", panel);
            Ui.Text("Catalogue", "title grow", head);
            count = Ui.Text("", "meta", head);
            search = new TextField();
            search.textEdition.placeholder = "Rechercher…";
            search.AddToClassList("book__search");
            search.RegisterValueChangedCallback(_ => Rebuild());
            panel.Add(search);
            filter = new SegmentedControl(Categories);
            filter.AddToClassList("book__filter");
            filter.Changed += _ => Rebuild();
            panel.Add(filter);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            panel.Add(scroll);
            grid = scroll.contentContainer;
            grid.AddToClassList("catalog-grid");
            var legend = Ui.El("catalog__legend", panel);
            Legend(legend, "water", "Liquide : remplit la case");
            Legend(legend, "hydrogen", "Gaz : translucide, en volutes");
            keys = Ui.Text("", "system catalog__keys", panel);
            Ui.SetVisible(panel, false);
            Rebuild();
        }

        private readonly Label keys;

        private void Legend(VisualElement parent, string id, string text)
        {
            var r = ctx.Db.Resources.FirstOrDefault(x => x != null && x.Id == id);
            if (r == null) return;
            var row = Ui.El("row catalog__legend-row", parent);
            row.Add(new ResourceIcon(r));
            Ui.Text(text, "meta", row);
        }

        public bool IsVisible => !panel.ClassListContains("hidden");
        public bool IsTyping => search.focusController?.focusedElement == search;

        public void SetVisible(bool visible)
        {
            if (visible == IsVisible) return;
            Ui.SetVisible(panel, visible);
            if (!visible) search.Blur();
            else keys.text = $"[Survole une icône : {ctx.Input.Display(GameInput.ShowRecipe)} = recette, {ctx.Input.Display(GameInput.ShowUsage)} = usages. Clic = recette.]";
        }

        private int CategoryOf(ResourceDef r)
        {
            if (ctx.Db.PlaceableForItem(r) != null) return 4;
            return r.Kind switch { ResourceKind.Fluid => 2, ResourceKind.Gas => 3, _ => 1 };
        }

        private void Rebuild()
        {
            grid.Clear();
            string q = search.value?.Trim().ToLowerInvariant() ?? "";
            int f = filter.Selected;
            int n = 0;
            foreach (var r in ctx.Db.Resources.Where(r => r != null && (f == 0 || CategoryOf(r) == f))
                         .Where(r => q.Length == 0 || r.DisplayName.ToLowerInvariant().Contains(q))
                         .OrderBy(CategoryOf).ThenBy(r => r.Kind).ThenBy(r => r.DisplayName))
            {
                var slot = IconSlot.Create(r, grid, classes: "rslot--catalog", extra: () => "Clic : comment l'obtenir.");
                slot.RegisterCallback<ClickEvent>(_ => ctx.OpenRecipeBook(r, 0));
                n++;
            }
            count.text = n.ToString();
            if (n == 0) Ui.System("Aucun résultat.", grid);
        }
    }
}
