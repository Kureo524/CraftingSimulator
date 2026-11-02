using System.Linq;
using Nucleaire.Data;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Inventaire du joueur (E) avec, à droite, le catalogue de tout ce qui existe dans le jeu
    /// (objets, liquides, gaz, énergie, machines). Survol + R : comment l'obtenir ; survol + U : à quoi il sert.</summary>
    public sealed class InventoryPanel
    {
        private static readonly string[] InvCategories = { "Tout", "Machines", "Transport", "Matériaux", "Améliorations" };
        private static readonly string[] CatCategories = { "Tout", "Objets", "Liquides", "Gaz", "Machines" };

        private readonly GameContext ctx;
        private readonly VisualElement layer, invGrid, catGrid;
        private readonly SegmentedControl invFilter, catFilter;
        private readonly TextField search;
        private readonly Label hint;
        private ResourceDef hovered;

        public InventoryPanel(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            layer = root.Q("inventory-layer");
            var panel = root.Q("inventory");
            layer.RegisterCallback<PointerDownEvent>(evt => { if (evt.target == layer) Close(); });

            // --- Inventaire ---
            var left = Ui.El("glass inventory__left", panel);
            var head = Ui.El("row", left);
            Ui.Text("Inventaire", "sheet-title grow", head);
            Ui.Button("×", Close, "btn btn--ghost btn--icon", head);
            invFilter = new SegmentedControl(InvCategories);
            invFilter.AddToClassList("inventory__filter");
            invFilter.Changed += _ => RebuildInventory();
            left.Add(invFilter);
            var invScroll = new ScrollView(ScrollViewMode.Vertical);
            invScroll.AddToClassList("grow");
            left.Add(invScroll);
            invGrid = invScroll.contentContainer;
            invGrid.AddToClassList("islot-grid");
            hint = Ui.Text("", "system inventory__hint", left);

            // --- Catalogue ---
            var right = Ui.El("glass glass--light inventory__catalog", panel);
            Ui.Text("Catalogue", "title", right);
            Ui.Text("Tout ce qui existe dans l'atelier. Survole un élément puis appuie sur R (recette) ou U (usages).", "meta wrap", right);
            search = new TextField();
            search.textEdition.placeholder = "Rechercher…";
            search.AddToClassList("book__search");
            search.RegisterValueChangedCallback(_ => RebuildCatalog());
            right.Add(search);
            catFilter = new SegmentedControl(CatCategories);
            catFilter.AddToClassList("book__filter");
            catFilter.Changed += _ => RebuildCatalog();
            right.Add(catFilter);
            var catScroll = new ScrollView(ScrollViewMode.Vertical);
            catScroll.AddToClassList("grow");
            right.Add(catScroll);
            catGrid = catScroll.contentContainer;
            catGrid.AddToClassList("catalog-grid");

            ctx.InventoryRequested += Toggle;
            ctx.Session.Inventory.Changed += () => { if (IsOpen) RebuildInventory(); };
            Ui.SetVisible(layer, false);
        }

        public bool IsOpen => !layer.ClassListContains("hidden");
        public bool IsTyping => search.focusController?.focusedElement == search;

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        public void Open()
        {
            Ui.SetVisible(layer, true);
            RebuildInventory();
            RebuildCatalog();
            UpdateHint();
        }

        public void Close()
        {
            Ui.SetVisible(layer, false);
            search.Blur();
            hovered = null;
        }

        /// <summary>R / U sur l'élément survolé.</summary>
        public void Update(GameInput input)
        {
            if (!IsOpen || hovered == null || IsTyping) return;
            if (input.Pressed(GameInput.ShowRecipe)) ctx.OpenRecipeBook(hovered, 0);
            else if (input.Pressed(GameInput.ShowUsage)) ctx.OpenRecipeBook(hovered, 1);
        }

        private void Hover(VisualElement e, ResourceDef r)
        {
            e.RegisterCallback<PointerEnterEvent>(_ => { hovered = r; UpdateHint(); });
            e.RegisterCallback<PointerLeaveEvent>(_ => { if (hovered == r) { hovered = null; UpdateHint(); } });
        }

        private void UpdateHint() => hint.text = hovered == null
            ? $"[Survole un objet : {ctx.Input.Display(GameInput.ShowRecipe)} = recette, {ctx.Input.Display(GameInput.ShowUsage)} = usages. Clic sur une machine = la poser.]"
            : $"[{hovered.DisplayName} — {ctx.Input.Display(GameInput.ShowRecipe)} : comment l'obtenir · {ctx.Input.Display(GameInput.ShowUsage)} : à quoi il sert]";

        private int InvCategory(ResourceDef r)
        {
            var p = ctx.Db.PlaceableForItem(r);
            if (p is MachineDef) return 1;
            if (p is TransmitterDef) return 2;
            if (ctx.Db.UpgradeForItem(r) != null) return 4;
            return 3;
        }

        private void RebuildInventory()
        {
            invGrid.Clear();
            int f = invFilter.Selected;
            var items = ctx.Session.Inventory.Items.Where(kv => f == 0 || InvCategory(kv.Key) == f)
                .OrderBy(kv => InvCategory(kv.Key)).ThenBy(kv => kv.Key.DisplayName).ToList();
            if (items.Count == 0) { Ui.System(f == 0 ? "Inventaire vide. Prends des ressources dans le vault." : "Rien dans cette catégorie.", invGrid); return; }
            foreach (var (r, count) in items)
            {
                var cell = Ui.El("vault-cell", invGrid);
                var box = Ui.El("islot", cell);
                Ui.El("islot__fill", box).style.backgroundColor = Palette.WithAlpha(Palette.Muted(r.Color), 0.55f);
                Ui.Text(count.ToString(), "number--small islot__count", box);
                Ui.Text(r.DisplayName, "meta islot__name", cell);
                var placeable = ctx.Db.PlaceableForItem(r);
                box.RegisterCallback<ClickEvent>(_ =>
                {
                    if (placeable == null || !ctx.Require(PlayerPermission.Place)) return;
                    ctx.BuildItem = r;
                    Close();
                });
                Hover(cell, r);
            }
        }

        private int CatCategory(ResourceDef r)
        {
            if (ctx.Db.PlaceableForItem(r) != null) return 4;
            return r.Kind switch { ResourceKind.Fluid => 2, ResourceKind.Gas => 3, _ => 1 };
        }

        private void RebuildCatalog()
        {
            catGrid.Clear();
            string q = search.value?.Trim().ToLowerInvariant() ?? "";
            int f = catFilter.Selected;
            foreach (var r in ctx.Db.Resources.Where(r => r != null && (f == 0 || CatCategory(r) == f))
                         .Where(r => q.Length == 0 || r.DisplayName.ToLowerInvariant().Contains(q))
                         .OrderBy(CatCategory).ThenBy(r => r.DisplayName))
            {
                var cell = Ui.El("catalog-cell " + KindClass(r.Kind), catGrid);
                Ui.Swatch(Palette.Muted(r.Color), cell);
                Ui.Text(r.DisplayName, "catalog-cell__name", cell);
                cell.RegisterCallback<ClickEvent>(_ => ctx.OpenRecipeBook(r));
                Hover(cell, r);
                Tooltips.Register(cell, () => (r.DisplayName, $"{r.Kind.DisplayName()}{(r.IsBaseResource ? " · vault" : "")}\nR : recette · U : usages · clic : livre"));
            }
            if (catGrid.childCount == 0) Ui.System("Aucun résultat.", catGrid);
        }

        private static string KindClass(ResourceKind k) => k switch
        {
            ResourceKind.Fluid => "catalog-cell--fluid",
            ResourceKind.Gas => "catalog-cell--gas",
            ResourceKind.Energy => "catalog-cell--energy",
            _ => "catalog-cell--item",
        };
    }
}
