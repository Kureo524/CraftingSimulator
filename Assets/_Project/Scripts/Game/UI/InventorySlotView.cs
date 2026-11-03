using System.Runtime.CompilerServices;
using Nucleaire.Crafting;
using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Un emplacement d'inventaire à l'écran (grille, rangée de barre rapide ou barre du bas).
    /// Dans l'inventaire : glisser ou cliquer pour prendre la pile, clic droit pour en prendre la moitié,
    /// lâcher sur un autre emplacement pour déplacer / fusionner / échanger, Maj + clic pour l'envoyer
    /// dans la barre rapide (ou dans la grille), ou dans la machine ouverte.</summary>
    public static class InventorySlotView
    {
        private sealed class Index { public int Value; }
        private static readonly ConditionalWeakTable<VisualElement, Index> Indices = new();

        /// <summary>Emplacement d'inventaire sous un élément (en remontant ses parents), ou -1.</summary>
        public static int IndexUnder(VisualElement e)
        {
            for (; e != null; e = e.parent)
                if (Indices.TryGetValue(e, out var i)) return i.Value;
            return -1;
        }

        public static VisualElement Build(VisualElement parent, GameContext ctx, int index, bool hud)
        {
            var inv = ctx.Session.Inventory;
            var s = inv.Slots[index];
            var r = s.IsEmpty ? null : s.Resource;
            bool fluid = r != null && r.Kind is ResourceKind.Fluid or ResourceKind.Gas;
            var el = Ui.El(hud ? "slot hotbar-slot" : "rslot rslot--inv" + (index < PlayerInventory.HotbarSize ? " hotbar-slot" : ""), parent);
            el.EnableInClassList("rslot--empty", !hud && r == null);
            el.EnableInClassList("hotbar-slot--empty", r == null);
            el.EnableInClassList("rslot--canister", fluid);
            Indices.AddOrUpdate(el, new Index { Value = index });

            if (index < PlayerInventory.HotbarSize)
                Ui.Text(ctx.Input.Display(GameInput.HotbarSlot(index)), "hotbar-slot__key", el).pickingMode = PickingMode.Ignore;
            if (r != null)
            {
                el.Add(new ResourceIcon(r) { Canister = fluid, Level = fluid ? (float)s.Amount / inv.CanisterCapacity : 1f });
                string count = fluid ? "" : s.Amount > 1 ? s.Amount.ToString() : "";
                if (count.Length > 0) Ui.Text(count, "rslot__count", el).pickingMode = PickingMode.Ignore;
                int amount = s.Amount;
                IconSlot.Bind(el, () => r, () => (fluid ? $"{amount} / {inv.CanisterCapacity} mB" : $"×{amount}") +
                    (hud ? $" · touche {ctx.Input.Display(GameInput.HotbarSlot(index))} ou clic : l'équiper" : " · clic droit : la moitié"));
            }
            else if (index < PlayerInventory.HotbarSize) Tooltips.Register(el, $"Emplacement {index + 1}", "Vide. Ouvre l'inventaire et glisses-y un objet.");

            if (hud)
            {
                el.EnableInClassList("slot--active", ctx.Hotbar.Selected == index);
                el.RegisterCallback<ClickEvent>(_ => ctx.Hotbar.Select(index));
                return el;
            }

            // Prendre la pile (ou la moitié au clic droit, voir ItemTransfer).
            ItemTransfer.RegisterSource(el, () =>
            {
                var cur = inv.Slots[index];
                return cur.IsEmpty ? null : new Payload(cur.Resource, cur.Amount, null, index);
            }, shift => Click(ctx, index, shift));

            // Recevoir : une pile d'un autre emplacement (déplacer / fusionner / échanger) ou un produit d'une machine.
            ItemTransfer.RegisterTarget(el, p => p.From != null || (p.Slot >= 0 && p.Slot != index), p =>
            {
                if (p.From != null)
                {
                    if (!ctx.Require(PlayerPermission.Take)) return 0;
                    return ctx.Session.Actions.Take(p.From, p.Resource, p.Amount);
                }
                return inv.Move(p.Slot, index, p.Amount);
            });
            return el;
        }

        /// <summary>Clic simple. Renvoie faux pour « prendre en main » la pile.</summary>
        private static bool Click(GameContext ctx, int index, bool shift)
        {
            if (!shift) return false;
            var inv = ctx.Session.Inventory;
            var s = inv.Slots[index];
            if (s.IsEmpty) return true;
            // Machine ouverte : tout déposer dedans.
            if (ctx.Selection is Machine m && m is not VaultMachine)
            {
                MachineDrop.Deposit(ctx, m, new Payload(s.Resource, s.Amount, null, index));
                return true;
            }
            // Sinon : barre rapide ↔ grille (fusion d'abord, puis premier emplacement libre).
            bool toGrid = index < PlayerInventory.HotbarSize;
            int from = toGrid ? PlayerInventory.HotbarSize : 0, to = toGrid ? inv.Slots.Count : PlayerInventory.HotbarSize;
            for (int pass = 0; pass < 2 && !inv.Slots[index].IsEmpty; pass++)
                for (int i = from; i < to && !inv.Slots[index].IsEmpty; i++)
                {
                    var t = inv.Slots[i];
                    if (pass == 0 ? t.Resource == s.Resource : t.IsEmpty) inv.Move(index, i, inv.Slots[index].Amount);
                }
            return true;
        }
    }
}
