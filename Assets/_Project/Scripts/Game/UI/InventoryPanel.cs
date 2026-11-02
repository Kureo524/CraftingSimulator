using System.Linq;
using Nucleaire.Data;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Inventaire du joueur (E) : une grille d'emplacements au centre de l'écran, icônes et quantités seulement.
    /// Le catalogue de tout le jeu s'affiche en même temps, collé au bord droit (voir <see cref="CatalogPanel"/>).</summary>
    public sealed class InventoryPanel
    {
        private const int Columns = 9, MinRows = 4;
        private static readonly string[] Categories = { "Tout", "Machines", "Transport", "Matériaux", "Améliorations" };

        private readonly GameContext ctx;
        private readonly VisualElement layer, grid;
        private readonly SegmentedControl filter;

        public InventoryPanel(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            layer = root.Q("inventory-layer");
            var panel = root.Q("inventory");
            layer.RegisterCallback<PointerDownEvent>(evt => { if (evt.target == layer) Close(); });

            var head = Ui.El("row", panel);
            Ui.Text("Inventaire", "sheet-title grow", head);
            Ui.Button("×", Close, "btn btn--ghost btn--icon", head);
            filter = new SegmentedControl(Categories);
            filter.AddToClassList("inventory__filter");
            filter.Changed += _ => Rebuild();
            panel.Add(filter);
            grid = Ui.El("inventory__grid", panel);
            Ui.Text("[Clic sur une machine ou un tuyau : le poser. Survol : nom, R / U : recette / usages.]", "system inventory__hint", panel);

            ctx.InventoryRequested += Toggle;
            ctx.Session.Inventory.Changed += () => { if (IsOpen) Rebuild(); };
            Ui.SetVisible(layer, false);
        }

        public bool IsOpen => !layer.ClassListContains("hidden");

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        public void Open()
        {
            Ui.SetVisible(layer, true);
            Rebuild();
        }

        public void Close() => Ui.SetVisible(layer, false);

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
            grid.Clear();
            int f = filter.Selected;
            var items = ctx.Session.Inventory.Items.Where(kv => f == 0 || CategoryOf(kv.Key) == f)
                .OrderBy(kv => CategoryOf(kv.Key)).ThenBy(kv => kv.Key.Kind).ThenBy(kv => kv.Key.DisplayName).ToList();

            foreach (var (r, count) in items)
            {
                var placeable = ctx.Db.PlaceableForItem(r);
                var slot = IconSlot.Create(r, grid, count.ToString(), "rslot--inv",
                    () => $"×{count}" + (placeable != null ? " · clic : poser dans l'atelier" : ""));
                slot.RegisterCallback<ClickEvent>(_ =>
                {
                    if (placeable == null) { ctx.OpenRecipeBook(r, 1); return; }
                    if (!ctx.Require(PlayerPermission.Place)) return;
                    ctx.BuildItem = r;
                    Close();
                });
            }

            int total = System.Math.Max(MinRows * Columns, (items.Count + Columns - 1) / Columns * Columns);
            for (int i = items.Count; i < total; i++) Ui.El("rslot rslot--inv rslot--empty", grid);
        }
    }
}
