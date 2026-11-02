using System.Linq;
using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Inventaire du joueur (E) : une grille d'emplacements au centre de l'écran, icônes et quantités seulement.
    /// Les liquides et gaz sont rangés en bonbonnes, remplies selon leur contenu.
    /// Quand une machine est ouverte, son interface s'affiche au-dessus de l'inventaire (écran « machine ») :
    /// on y glisse les objets, ou on les clique puis on clique la machine.
    /// Le catalogue de tout le jeu s'affiche en même temps, collé au bord droit (voir <see cref="CatalogPanel"/>).</summary>
    public sealed class InventoryPanel
    {
        private const int Columns = 9;
        private static readonly string[] Categories = { "Tout", "Machines", "Transport", "Matériaux", "Fluides", "Améliorations" };

        private readonly GameContext ctx;
        private readonly VisualElement layer, panel, grid;
        private readonly SegmentedControl filter;
        private readonly Label hint;
        private readonly VisualElement hotbarRow;

        public InventoryPanel(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            layer = root.Q("inventory-layer");
            panel = root.Q("inventory");
            layer.RegisterCallback<PointerDownEvent>(evt => { if (evt.target == layer || evt.target == root.Q("inventory-column")) Close(); });

            var head = Ui.El("row", panel);
            Ui.Text("Inventaire", "sheet-title grow", head);
            Ui.Button("×", Close, "btn btn--ghost btn--icon", head).name = "inventory-close";
            filter = new SegmentedControl(Categories);
            filter.AddToClassList("inventory__filter");
            filter.Changed += _ => Rebuild();
            panel.Add(filter);
            grid = Ui.El("inventory__grid", panel);
            Ui.Text("Barre rapide", "meta inventory__bar-title", panel);
            hotbarRow = Ui.El("inventory__grid inventory__hotbar", panel);
            hint = Ui.Text("", "system inventory__hint", panel);

            // Ce qu'on lâche sur l'inventaire depuis une machine y est récupéré.
            ItemTransfer.RegisterTarget(panel, p => p.From != null, p =>
            {
                if (!ctx.Require(PlayerPermission.Take)) return 0;
                int n = ctx.Session.Actions.Take(p.From, p.Resource, p.Amount);
                if (n == 0) ctx.Notify($"Impossible de récupérer {p.Resource.DisplayName.ToLowerInvariant()}.");
                return n;
            });

            ctx.InventoryRequested += Toggle;
            ctx.Session.Inventory.Changed += () => { if (IsOpen) Rebuild(); };
            ctx.Hotbar.Changed += () => { if (IsOpen) Rebuild(); };
            Ui.SetVisible(layer, false);
        }

        public bool IsOpen => !layer.ClassListContains("hidden");
        /// <summary>Machine ouverte au-dessus de l'inventaire (null = inventaire seul).</summary>
        private Machine OpenMachine => ctx.Selection as Machine;
        private bool MachineMode => ctx.Selection != null;

        /// <summary>Fermeture de l'écran (la sélection de machine est aussi abandonnée).</summary>
        public event System.Action Closed;

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        public void Open()
        {
            Ui.SetVisible(layer, true);
            Rebuild();
        }

        public void Close()
        {
            if (!IsOpen) return;
            Ui.SetVisible(layer, false);
            Closed?.Invoke();
        }

        private int CategoryOf(ResourceDef r)
        {
            if (r.Kind is ResourceKind.Fluid or ResourceKind.Gas) return 4;
            var p = ctx.Db.PlaceableForItem(r);
            if (p is MachineDef) return 1;
            if (p is TransmitterDef) return 2;
            if (ctx.Db.UpgradeForItem(r) != null) return 5;
            return 3;
        }

        public void Rebuild()
        {
            layer.EnableInClassList("inventory-layer--machine", MachineMode);
            hint.text = OpenMachine != null && OpenMachine is not VaultMachine
                ? "[Glisse un objet sur la machine, ou clique-le puis clique la machine. Clic droit sur une case : la moitié du stack ; clic droit sur la machine : une unité. Maj + clic : tout déposer.]"
                : "[Range les machines et câbles dans la barre rapide : glisse-les dessus (Maj + clic : place libre). Survol + touche 1–9 : ranger à cet emplacement. R / U : recette / usages.]";

            grid.Clear();
            int f = filter.Selected;
            var inv = ctx.Session.Inventory;
            // Ce qui est rangé dans la barre rapide n'apparaît pas aussi dans la grille (comme dans Minecraft).
            var items = inv.Items.Where(kv => ctx.Hotbar.IndexOf(kv.Key) < 0)
                .Where(kv => f == 0 || CategoryOf(kv.Key) == f)
                .OrderBy(kv => CategoryOf(kv.Key)).ThenBy(kv => kv.Key.Kind).ThenBy(kv => kv.Key.DisplayName).ToList();

            int used = 0;
            foreach (var (r, count) in items)
            {
                if (r.Kind == ResourceKind.Item) { ItemSlot(r, count); used++; continue; }
                foreach (int amount in inv.Canisters(r)) { CanisterSlot(r, amount); used++; }
            }

            hotbarRow.Clear();
            for (int i = 0; i < Hotbar.Size; i++) HotbarPanel.BuildSlot(hotbarRow, ctx, i, true);

            int minRows = MachineMode ? 2 : 3;
            int total = System.Math.Max(minRows * Columns, (used + Columns - 1) / Columns * Columns);
            for (int i = used; i < total; i++) Ui.El("rslot rslot--inv rslot--empty", grid);
        }

        private void ItemSlot(ResourceDef r, int count)
        {
            var placeable = ctx.Db.PlaceableForItem(r);
            var slot = IconSlot.Create(r, grid, count.ToString(), "rslot--inv",
                () => $"×{count}" + (MachineMode ? " · glisse-le sur la machine" : placeable != null ? " · glisse-le dans la barre rapide" : ""));
            ItemTransfer.RegisterSource(slot, () => new Payload(r, ctx.Session.Inventory.Count(r)), shift => Click(r, placeable, shift));
        }

        private void CanisterSlot(ResourceDef r, int amount)
        {
            int cap = ctx.Session.Inventory.CanisterCapacity;
            var slot = Ui.El("rslot rslot--inv rslot--canister", grid);
            var icon = new ResourceIcon(r) { Canister = true, Level = (float)amount / cap };
            slot.Add(icon);
            IconSlot.Bind(slot, () => r, () => $"{amount} / {cap} mB" + (r.Kind == ResourceKind.Gas ? " · bonbonne de gaz" : " · jerrican"));
            ItemTransfer.RegisterSource(slot, () => new Payload(r, amount), shift => Click(r, null, shift, amount));
        }

        /// <summary>Clic simple sur une case. Renvoie faux pour « prendre en main » l'objet (écran machine).</summary>
        private bool Click(ResourceDef r, PlaceableDef placeable, bool shift, int amount = int.MaxValue)
        {
            var m = OpenMachine;
            if (m != null && m is not VaultMachine)
            {
                if (!shift) return false;
                MachineDrop.Deposit(ctx, m, new Payload(r, System.Math.Min(amount, ctx.Session.Inventory.Count(r))));
                return true;
            }
            if (placeable != null)
            {
                // Objet posable : on le prend en main pour le ranger dans la barre rapide (Maj + clic : premier emplacement libre).
                if (!shift) return false;
                var bar = ctx.Hotbar;
                int free = -1;
                for (int i = 0; i < Hotbar.Size && free < 0; i++) if (bar[i] == null) free = i;
                if (bar.IndexOf(r) >= 0) ctx.Notify($"{r.DisplayName} est déjà dans la barre rapide.", EventSeverity.Info);
                else if (free < 0) ctx.Notify("Barre rapide pleine : glisse l'objet sur un emplacement pour le remplacer.", EventSeverity.Info);
                else bar.Assign(free, r);
                return true;
            }
            ctx.OpenRecipeBook(r, 1);
            return true;
        }
    }

    /// <summary>Dépôt dans une machine : amélioration installée, sinon ressource insérée dans un emplacement compatible.</summary>
    public static class MachineDrop
    {
        public static bool Accepts(Machine m, Payload p) => m != null && m is not VaultMachine && p.From == null;

        public static int Deposit(GameContext ctx, Machine m, Payload p)
        {
            if (ctx.Db.UpgradeForItem(p.Resource) != null && m.MachineDef.UpgradeSlots > 0)
            {
                if (ctx.Session.Actions.TryInstallUpgrade(m, p.Resource, out var err)) return 1;
                ctx.Notify(err);
                return 0;
            }
            if (!ctx.Require(PlayerPermission.Deposit)) return 0;
            int n = ctx.Session.Actions.Deposit(m, p.Resource, p.Amount);
            if (n == 0) ctx.Notify($"{m.MachineDef.DisplayName} : pas d'emplacement libre pour {p.Resource.DisplayName.ToLowerInvariant()}.");
            return n;
        }
    }
}
