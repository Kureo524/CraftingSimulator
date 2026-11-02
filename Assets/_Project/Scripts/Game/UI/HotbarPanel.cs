using Nucleaire.Data;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Barre rapide en bas de l'écran, comme dans Minecraft : 9 emplacements numérotés.
    /// L'emplacement équipé (touche 1 à 9 ou clic) est celui qui se pose dans l'atelier ; le rééquiper vide les mains.
    /// Au-dessus : accès au livre, à l'inventaire (pour ranger la barre) et à l'établi.</summary>
    public sealed class HotbarPanel
    {
        private readonly GameContext ctx;
        private readonly VisualElement slotsRow;

        public HotbarPanel(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            var hotbar = root.Q("hotbar");
            var top = Ui.El("row hotbar__top", hotbar);
            var book = Ui.Button("Livre", () => ctx.OpenRecipeBook(), "btn btn--small", top);
            book.name = "hotbar-book";
            Tooltips.Register(book, () => ("Livre de recettes", $"Touche {ctx.Input.Display(GameInput.RecipeBook)}. Comment obtenir chaque ressource, et à quoi elle sert."));
            var inv = Ui.Button("Inventaire", ctx.OpenInventory, "btn btn--small", top);
            inv.name = "hotbar-inventory";
            inv.style.marginLeft = 4;
            Tooltips.Register(inv, () => ("Inventaire", $"Touche {ctx.Input.Display(GameInput.Inventory)}. Glisse un objet sur la barre rapide pour l'y ranger."));
            var wb = Ui.Button("Établi", () => ctx.OpenWorkbench(), "btn btn--small", top);
            wb.name = "hotbar-workbench";
            wb.style.marginLeft = 4;
            Tooltips.Register(wb, () => ("Établi", $"Touche {ctx.Input.Display(GameInput.Workbench)}. Fabrique machines, câbles et améliorations."));

            slotsRow = Ui.El("row hotbar__slots", hotbar);
            ctx.Hotbar.Changed += Rebuild;
            ctx.Session.Inventory.Changed += Rebuild;
            ctx.Input.Changed += Rebuild;
            Rebuild();
        }

        private void Rebuild()
        {
            slotsRow.Clear();
            for (int i = 0; i < Hotbar.Size; i++) BuildSlot(slotsRow, ctx, i, false);
        }

        /// <summary>Un emplacement de barre rapide (en bas de l'écran ou dans l'inventaire).</summary>
        public static VisualElement BuildSlot(VisualElement parent, GameContext ctx, int i, bool inInventory)
        {
            var bar = ctx.Hotbar;
            var r = bar[i];
            int count = r != null ? ctx.Session.Inventory.Count(r) : 0;
            var slot = Ui.El("slot hotbar-slot" + (inInventory ? " rslot rslot--inv" : ""), parent);
            slot.name = (inInventory ? "inv-hotbar-" : "hotbar-slot-") + i;
            slot.EnableInClassList("slot--active", !inInventory && bar.Selected == i);
            slot.EnableInClassList("hotbar-slot--empty", r == null);
            slot.EnableInClassList("hotbar-slot--out", r != null && count == 0);
            Ui.Text(ctx.Input.Display(GameInput.HotbarSlot(i)), "hotbar-slot__key", slot).pickingMode = PickingMode.Ignore;
            if (r != null)
            {
                slot.Add(new ResourceIcon(r));
                Ui.Text(count.ToString(), "rslot__count", slot).pickingMode = PickingMode.Ignore;
                IconSlot.Bind(slot, () => r, () => $"×{count} · " + (inInventory
                    ? "glisse un autre objet ici pour le remplacer"
                    : $"touche {ctx.Input.Display(GameInput.HotbarSlot(i))} ou clic : l'équiper pour le poser"));
            }
            else Tooltips.Register(slot, $"Emplacement {i + 1}", "Vide. Ouvre l'inventaire et glisses-y une machine ou un câble.");

            // Ranger : lâcher un objet posable de l'inventaire sur l'emplacement.
            ItemTransfer.RegisterTarget(slot, p => p.From == null && bar.CanHold(p.Resource), p =>
            {
                bar.Assign(i, p.Resource);
                return p.Amount; // l'objet n'est plus « en main »
            });
            if (!inInventory) slot.RegisterCallback<ClickEvent>(_ => bar.Select(i));
            else if (r != null) ItemTransfer.RegisterSource(slot, () => new Payload(r, ctx.Session.Inventory.Count(r)), _ => false);
            return slot;
        }
    }
}
