using System;
using System.Linq;
using Nucleaire.Data;

namespace Nucleaire.Game
{
    /// <summary>Barre rapide à 9 emplacements, comme dans Minecraft : on y range les objets posables de l'inventaire,
    /// et seul l'emplacement « équipé » (touches 1 à 9 ou clic) se pose dans l'atelier.
    /// Les nouveaux objets posables s'y rangent tout seuls s'il reste de la place.</summary>
    public sealed class Hotbar
    {
        public const int Size = 9;
        private readonly GameContext ctx;
        private readonly ResourceDef[] slots = new ResourceDef[Size];
        private bool syncing;

        public Hotbar(GameContext ctx)
        {
            this.ctx = ctx;
            ctx.Session.Inventory.Changed += AutoFill;
            ctx.Session.BoardReset += () => { Array.Clear(slots, 0, Size); Selected = -1; AutoFill(); };
            ctx.BuildItemChanged += item =>
            {
                if (syncing) return;
                // Pose annulée (clic droit, Échap, plus d'objets) : plus rien d'équipé.
                if (item == null || Selected < 0 || slots[Selected] != item) { Selected = -1; Changed?.Invoke(); }
            };
            AutoFill();
        }

        public event Action Changed;
        /// <summary>Emplacement équipé, ou -1 (mains vides : on sélectionne les machines).</summary>
        public int Selected { get; private set; } = -1;
        public ResourceDef this[int i] => slots[i];
        public int IndexOf(ResourceDef r) => Array.IndexOf(slots, r);

        public bool CanHold(ResourceDef r) => r != null && ctx.Db.PlaceableForItem(r) != null;

        /// <summary>Range un objet dans un emplacement (il quitte son ancien emplacement ; échange si occupé).</summary>
        public bool Assign(int index, ResourceDef r)
        {
            if (index < 0 || index >= Size) return false;
            if (r != null && !CanHold(r)) return false;
            int old = r == null ? -1 : IndexOf(r);
            if (old >= 0) slots[old] = slots[index];
            slots[index] = r;
            if (Selected == index || Selected == old) Equip(Selected);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Équipe un emplacement ; le rééquiper le range (mains vides).</summary>
        public void Select(int index)
        {
            if (index == Selected) index = -1;
            if (index >= 0 && slots[index] != null && !ctx.Require(PlayerPermission.Place)) return;
            Equip(index);
            Changed?.Invoke();
        }

        private void Equip(int index)
        {
            Selected = index;
            var r = index >= 0 ? slots[index] : null;
            syncing = true;
            ctx.BuildItem = r != null && ctx.Session.Inventory.Count(r) > 0 ? r : null;
            syncing = false;
        }

        private void AutoFill()
        {
            bool changed = false;
            foreach (var (r, _) in ctx.Session.Inventory.Items.ToList())
            {
                if (!CanHold(r) || IndexOf(r) >= 0) continue;
                int free = Array.IndexOf(slots, null);
                if (free < 0) break;
                slots[free] = r;
                changed = true;
            }
            if (changed) Changed?.Invoke();
        }
    }
}
