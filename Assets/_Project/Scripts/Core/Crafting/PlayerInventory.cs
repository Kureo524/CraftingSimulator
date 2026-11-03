using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;

namespace Nucleaire.Crafting
{
    /// <summary>Un emplacement d'inventaire : une pile d'une seule ressource.</summary>
    public sealed class InventorySlot
    {
        public ResourceDef Resource { get; internal set; }
        public int Amount { get; internal set; }
        public bool IsEmpty => Resource == null || Amount <= 0;

        internal void Clear() { Resource = null; Amount = 0; }
    }

    /// <summary>Inventaire du joueur, à emplacements comme dans Minecraft : les 9 premiers forment la barre rapide,
    /// les suivants la grille (elle s'agrandit au besoin). Piles de 64 objets ; une bonbonne (liquide ou gaz) par emplacement.
    /// Le reste du jeu (établi, machines, vault) ne voit que les totaux par ressource.</summary>
    public sealed class PlayerInventory
    {
        public const int HotbarSize = 9;
        public const int MinGridSlots = 27;
        public const int ItemStack = 64;

        private readonly List<InventorySlot> slots = new();

        public PlayerInventory(int canisterCapacity = 4000)
        {
            CanisterCapacity = Math.Max(1, canisterCapacity);
            for (int i = 0; i < HotbarSize + MinGridSlots; i++) slots.Add(new InventorySlot());
        }

        public event Action Changed;

        /// <summary>Contenance d'une bonbonne (liquide ou gaz), en mB.</summary>
        public int CanisterCapacity { get; }
        public IReadOnlyList<InventorySlot> Slots => slots;
        /// <summary>Taille maximale d'une pile de cette ressource.</summary>
        public int StackSize(ResourceDef r) => r == null ? 0 : r.Kind == ResourceKind.Item ? ItemStack : CanisterCapacity;

        /// <summary>Objets posables : ils vont d'abord dans la barre rapide.</summary>
        public Func<ResourceDef, bool> PrefersHotbar { get; set; } = _ => false;

        // ---------------- Totaux (vue d'ensemble pour le reste du jeu) ----------------

        public IEnumerable<KeyValuePair<ResourceDef, int>> Items =>
            slots.Where(s => !s.IsEmpty).GroupBy(s => s.Resource)
                 .Select(g => new KeyValuePair<ResourceDef, int>(g.Key, g.Sum(s => s.Amount)))
                 .OrderBy(kv => kv.Key.DisplayName);

        public int Count(ResourceDef r) => r == null ? 0 : slots.Where(s => s.Resource == r).Sum(s => s.Amount);
        public bool Has(ResourceStack s) => Count(s.Resource) >= s.Amount;
        public bool HasAll(IEnumerable<ResourceStack> stacks) =>
            stacks.Where(s => s.IsValid).GroupBy(s => s.Resource).All(g => Count(g.Key) >= g.Sum(s => s.Amount));

        /// <summary>Contenu des bonbonnes d'un fluide (un emplacement = une bonbonne).</summary>
        public IEnumerable<int> Canisters(ResourceDef r) => slots.Where(s => s.Resource == r && s.Amount > 0).Select(s => s.Amount);

        public static bool CanHold(ResourceDef r) => r != null && r.Kind != ResourceKind.Energy;

        // ---------------- Ajout / retrait ----------------

        public int Add(ResourceDef r, int amount)
        {
            if (!CanHold(r) || amount <= 0) return 0;
            int left = amount, cap = StackSize(r);
            // 1) compléter les piles existantes (barre rapide d'abord)
            foreach (var s in slots.Where(s => s.Resource == r && s.Amount < cap))
            {
                int n = Math.Min(left, cap - s.Amount);
                s.Amount += n;
                left -= n;
                if (left == 0) break;
            }
            // 2) nouvelles piles : barre rapide pour les objets posables, sinon la grille ; la grille s'agrandit
            while (left > 0)
            {
                var s = EmptySlot(PrefersHotbar(r));
                int n = Math.Min(left, cap);
                s.Resource = r;
                s.Amount = n;
                left -= n;
            }
            Changed?.Invoke();
            return amount;
        }

        private InventorySlot EmptySlot(bool hotbarFirst)
        {
            if (hotbarFirst)
                for (int i = 0; i < HotbarSize; i++) if (slots[i].IsEmpty) return slots[i];
            for (int i = HotbarSize; i < slots.Count; i++) if (slots[i].IsEmpty) return slots[i];
            for (int i = 0; i < HotbarSize; i++) if (slots[i].IsEmpty) return slots[i];
            var added = new InventorySlot();
            slots.Add(added);
            return added;
        }

        /// <summary>Retire une quantité au total (la grille d'abord, puis la barre rapide ; piles les plus petites d'abord).</summary>
        public int Remove(ResourceDef r, int amount)
        {
            int n = Math.Min(amount, Count(r));
            if (n <= 0) return 0;
            int left = n;
            var order = slots.Select((s, i) => (s, i)).Where(x => x.s.Resource == r && x.s.Amount > 0)
                .OrderBy(x => x.i < HotbarSize ? 1 : 0).ThenBy(x => x.s.Amount).Select(x => x.s).ToList();
            foreach (var s in order)
            {
                int k = Math.Min(left, s.Amount);
                s.Amount -= k;
                if (s.Amount == 0) s.Clear();
                left -= k;
                if (left == 0) break;
            }
            Changed?.Invoke();
            return n;
        }

        /// <summary>Retire d'un emplacement précis (dépôt dans une machine depuis cette pile).</summary>
        public int RemoveFromSlot(int index, int amount)
        {
            if (index < 0 || index >= slots.Count || amount <= 0) return 0;
            var s = slots[index];
            int n = Math.Min(amount, s.Amount);
            if (n <= 0) return 0;
            s.Amount -= n;
            if (s.Amount == 0) s.Clear();
            Changed?.Invoke();
            return n;
        }

        public bool RemoveAll(IEnumerable<ResourceStack> stacks)
        {
            var list = stacks.Where(s => s.IsValid).ToList();
            if (!HasAll(list)) return false;
            foreach (var s in list) Remove(s.Resource, s.Amount);
            return true;
        }

        public void Clear()
        {
            foreach (var s in slots) s.Clear();
            while (slots.Count > HotbarSize + MinGridSlots && slots[^1].IsEmpty) slots.RemoveAt(slots.Count - 1);
            Changed?.Invoke();
        }

        // ---------------- Déplacements entre emplacements ----------------

        /// <summary>Déplace <paramref name="amount"/> de l'emplacement <paramref name="from"/> vers <paramref name="to"/> :
        /// vers un emplacement vide (scinder), sur la même ressource (fusionner, dans la limite de la pile),
        /// ou sur une autre ressource (échange, seulement si on déplace toute la pile). Renvoie la quantité déplacée.</summary>
        public int Move(int from, int to, int amount)
        {
            if (from == to || from < 0 || to < 0 || from >= slots.Count || to >= slots.Count) return 0;
            var a = slots[from];
            var b = slots[to];
            if (a.IsEmpty) return 0;
            amount = Math.Min(amount, a.Amount);
            if (amount <= 0) return 0;
            int moved;
            if (b.IsEmpty)
            {
                b.Resource = a.Resource;
                b.Amount = amount;
                a.Amount -= amount;
                moved = amount;
            }
            else if (b.Resource == a.Resource)
            {
                moved = Math.Min(amount, StackSize(b.Resource) - b.Amount);
                b.Amount += moved;
                a.Amount -= moved;
            }
            else
            {
                if (amount < a.Amount) return 0; // on n'échange que des piles entières
                (a.Resource, b.Resource) = (b.Resource, a.Resource);
                (a.Amount, b.Amount) = (b.Amount, a.Amount);
                moved = amount;
            }
            if (a.Amount <= 0) a.Clear();
            if (moved > 0) Changed?.Invoke();
            return moved;
        }

        /// <summary>Premier emplacement de la barre rapide qui contient cette ressource (-1 sinon).</summary>
        public int HotbarIndexOf(ResourceDef r)
        {
            for (int i = 0; i < HotbarSize; i++) if (slots[i].Resource == r && slots[i].Amount > 0) return i;
            return -1;
        }
    }
}
