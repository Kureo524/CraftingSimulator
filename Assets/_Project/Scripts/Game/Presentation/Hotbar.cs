using System;
using Nucleaire.Data;

namespace Nucleaire.Game
{
    /// <summary>Barre rapide : les 9 premiers emplacements de l'inventaire, comme dans Minecraft.
    /// Seul l'emplacement « équipé » (touches 1 à 9 ou clic) se pose dans l'atelier ; le rééquiper vide les mains.</summary>
    public sealed class Hotbar
    {
        public const int Size = Crafting.PlayerInventory.HotbarSize;
        private readonly GameContext ctx;
        private bool syncing;

        public Hotbar(GameContext ctx)
        {
            this.ctx = ctx;
            ctx.Session.Inventory.Changed += () =>
            {
                // La pile équipée a changé (épuisée, déplacée) : on rééquipe ce qu'il y a maintenant dans l'emplacement.
                if (Selected >= 0 && this[Selected] != ctx.BuildItem) Equip(Selected);
                Changed?.Invoke();
            };
            ctx.Session.BoardReset += () => { Selected = -1; Changed?.Invoke(); };
            ctx.BuildItemChanged += item =>
            {
                if (syncing) return;
                // Pose annulée (clic droit, Échap) : plus rien d'équipé.
                if (item == null || Selected < 0 || this[Selected] != item) { Selected = -1; Changed?.Invoke(); }
            };
        }

        public event Action Changed;
        /// <summary>Emplacement équipé, ou -1 (mains vides : on sélectionne les machines).</summary>
        public int Selected { get; private set; } = -1;

        public ResourceDef this[int i]
        {
            get
            {
                var s = ctx.Session.Inventory.Slots[i];
                return s.IsEmpty ? null : s.Resource;
            }
        }

        public int IndexOf(ResourceDef r) => ctx.Session.Inventory.HotbarIndexOf(r);

        /// <summary>Équipe un emplacement ; le rééquiper le range (mains vides).</summary>
        public void Select(int index)
        {
            if (ctx.ToolModeActive) return;
            if (index == Selected) index = -1;
            if (index >= 0 && this[index] != null && !ctx.Require(PlayerPermission.Place)) return;
            Equip(index);
            Changed?.Invoke();
        }

        private void Equip(int index)
        {
            Selected = index;
            var r = index >= 0 ? this[index] : null;
            bool placeable = r != null && (ctx.Db.PlaceableForItem(r) != null || r == ctx.Db.SorterItem);
            syncing = true;
            ctx.BuildItem = placeable ? r : null;
            syncing = false;
        }
    }
}
