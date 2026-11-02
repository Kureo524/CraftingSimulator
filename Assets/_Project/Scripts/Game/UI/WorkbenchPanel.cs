using System.Linq;
using Nucleaire.Data;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>L'établi : un panneau ouvrable à tout moment (touche C), plus un objet de l'atelier.</summary>
    public sealed class WorkbenchPanel
    {
        private static readonly string[] Categories = { "Machines", "Transport", "Composants", "Améliorations" };

        private readonly GameContext ctx;
        private readonly VisualElement layer, panel, list;
        private readonly SegmentedControl filter;
        private readonly Toggle craftableOnly;
        private int category;

        public WorkbenchPanel(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            layer = root.Q("workbench-layer");
            panel = root.Q("workbench");
            layer.RegisterCallback<PointerDownEvent>(evt => { if (evt.target == layer) Close(); });

            var head = Ui.El("row", panel);
            Ui.Text("Établi", "sheet-title grow", head);
            Ui.Button("×", Close, "btn btn--ghost btn--icon", head);
            Ui.Text("Fabrique ici machines, transmetteurs, composants et améliorations avec les objets de ton inventaire.", "text-2 wrap workbench__desc", panel);
            var bar = Ui.El("row workbench__bar", panel);
            filter = new SegmentedControl(Categories);
            filter.Changed += i => { category = i; Rebuild(); };
            bar.Add(filter);
            Ui.El("spacer", bar);
            craftableOnly = new Toggle("Fabricables seulement") { value = false };
            craftableOnly.AddToClassList("workbench__toggle");
            craftableOnly.RegisterValueChangedCallback(_ => Rebuild());
            bar.Add(craftableOnly);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            panel.Add(scroll);
            list = scroll.contentContainer;
            list.AddToClassList("workbench__grid");

            ctx.WorkbenchRequested += Open;
            ctx.Session.Inventory.Changed += () => { if (IsOpen) Rebuild(); };
            Ui.SetVisible(layer, false);
        }

        public bool IsOpen => !layer.ClassListContains("hidden");
        public void Open() { Ui.SetVisible(layer, true); Rebuild(); }
        public void Close() => Ui.SetVisible(layer, false);
        public void Toggle() { if (IsOpen) Close(); else ctx.OpenWorkbench(); }

        private int CategoryOf(CraftingRecipe c)
        {
            var r = c.Result.Resource;
            var p = ctx.Db.PlaceableForItem(r);
            if (p is MachineDef) return 0;
            if (p is TransmitterDef) return 1;
            if (ctx.Db.UpgradeForItem(r) != null) return 3;
            return 2;
        }

        private void Rebuild()
        {
            list.Clear();
            var wb = ctx.Session.Workbench;
            var recipes = wb.Recipes.Where(c => CategoryOf(c) == category)
                .Where(c => !craftableOnly.value || wb.CanCraft(c, out _))
                .OrderByDescending(c => wb.CanCraft(c, out _)).ThenBy(c => c.Result.Resource.DisplayName).ToList();
            if (recipes.Count == 0) Ui.System("Rien à fabriquer ici pour l'instant.", list);

            foreach (var c in recipes)
            {
                bool can = wb.CanCraft(c, out var reason);
                var card = Ui.El("glass card craft-card", list);
                card.EnableInClassList("craft-card--disabled", !can);
                var head = Ui.El("row", card);
                IconSlot.Create(c.Result.Resource, head, c.Result.Amount > 1 ? c.Result.Amount.ToString() : null, "rslot--small");
                var title = Ui.Text(c.Result.Resource.DisplayName, "bold grow", head);
                title.RegisterCallback<ClickEvent>(_ => ctx.OpenRecipeBook(c.Result.Resource));
                Ui.Text($"×{c.Result.Amount}", "meta", head);
                if (!string.IsNullOrEmpty(c.Result.Resource.Description)) Ui.Text(c.Result.Resource.Description, "meta wrap", card);

                var flow = Ui.El("row recipe-flow", card);
                foreach (var i in c.Ingredients)
                {
                    int have = ctx.Session.Inventory.Count(i.Resource);
                    var cell = Ui.El("recipe-ingredient", flow);
                    var s = IconSlot.Create(i.Resource, cell, i.Amount.ToString(), have >= i.Amount ? null : "rslot--missing",
                        () => $"Tu en as {have}, il en faut {i.Amount}.");
                    s.RegisterCallback<ClickEvent>(_ => ctx.OpenRecipeBook(i.Resource));
                    Ui.Text($"{have}/{i.Amount}", "mono recipe-ingredient__amount" + (have >= i.Amount ? "" : " recipe-ingredient__amount--missing"), cell);
                }

                var actions = Ui.El("row craft-actions", card);
                Ui.El("spacer", actions);
                var one = Ui.Button("Fabriquer", () => { if (wb.Craft(c) == 0 && !wb.CanCraft(c, out var r)) ctx.Notify(r); }, can ? "btn btn--primary btn--small" : "btn btn--small", actions);
                one.SetEnabled(can);
                var five = Ui.Button("×5", () => wb.Craft(c, 5), "btn btn--small", actions);
                five.style.marginLeft = 4;
                five.SetEnabled(can);
                if (!can) Tooltips.Register(card, "Pas encore", reason);
            }
        }
    }
}
