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
        public static VisualElement BuildSlot(VisualElement parent, GameContext ctx, int i, bool inInventory) =>
            InventorySlotView.Build(parent, ctx, i, !inInventory);
    }
}
