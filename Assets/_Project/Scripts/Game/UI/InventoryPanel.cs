using System.Linq;
using Nucleaire.Crafting;
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

            // Ce qu'on lâche sur l'inventaire : depuis une machine, il est récupéré ; depuis la barre rapide, il y est retiré.
            ItemTransfer.RegisterTarget(panel, p => p.From != null, p =>
            {
                if (!ctx.Require(PlayerPermission.Take)) return 0;
                int n = ctx.Session.Actions.Take(p.From, p.Resource, p.Amount);
                if (n == 0) ctx.Notify($"Impossible de récupérer {p.Resource.DisplayName.ToLowerInvariant()}.");
                return n;
            });

            ctx.InventoryRequested += Toggle;
            ctx.Session.Inventory.Changed += () => { if (IsOpen) Rebuild(); };

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


        public void Rebuild()
        {
            layer.EnableInClassList("inventory-layer--machine", MachineMode);
            hint.text = OpenMachine != null && OpenMachine is not VaultMachine
                ? "[Glisse une pile sur la machine, ou clique-la puis clique la machine (clic droit sur la machine : une unité). Clic droit sur une pile : la moitié. Maj + clic : tout déposer.]"
                : "[Comme dans Minecraft : glisse ou clique une pile pour la déplacer, clic droit pour en prendre la moitié, Maj + clic pour l'envoyer dans la barre rapide. Survol + 1–9 : échanger avec la barre. R / U : recette / usages.]";

            var inv = ctx.Session.Inventory;
            grid.Clear();
            int f = filter.Selected;
            int shown = 0;
            for (int i = PlayerInventory.HotbarSize; i < inv.Slots.Count; i++)
            {
                var slot = inv.Slots[i];
                // Avec un filtre : seulement les piles de cette catégorie.
                if (f != 0 && (slot.IsEmpty || CategoryOf(slot.Resource) != f)) continue;
                InventorySlotView.Build(grid, ctx, i, false);
                shown++;
            }
            hotbarRow.Clear();
            for (int i = 0; i < PlayerInventory.HotbarSize; i++) InventorySlotView.Build(hotbarRow, ctx, i, false);
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
            int n = p.Slot >= 0 ? ctx.Session.Actions.DepositFromSlot(m, p.Slot, p.Amount) : ctx.Session.Actions.Deposit(m, p.Resource, p.Amount);
            if (n == 0) ctx.Notify($"{m.MachineDef.DisplayName} : pas d'emplacement libre pour {p.Resource.DisplayName.ToLowerInvariant()}.");
            return n;
        }
    }
}
