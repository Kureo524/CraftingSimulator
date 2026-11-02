using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;

namespace Nucleaire.Crafting
{
    /// <summary>Inventaire du joueur : solides (à l'unité), liquides et gaz (en mB, rangés en bonbonnes).
    /// L'énergie ne se transporte pas.</summary>
    public sealed class PlayerInventory
    {
        private readonly Dictionary<ResourceDef, int> items = new();

        public PlayerInventory(int canisterCapacity = 4000) => CanisterCapacity = Math.Max(1, canisterCapacity);

        /// <summary>Contenance d'une bonbonne (liquide ou gaz), en mB.</summary>
        public int CanisterCapacity { get; }

        /// <summary>Contenu des bonbonnes d'un fluide : des bonbonnes pleines, puis une partiellement remplie.</summary>
        public IEnumerable<int> Canisters(ResourceDef r)
        {
            int left = Count(r);
            while (left > 0) { int n = Math.Min(left, CanisterCapacity); yield return n; left -= n; }
        }

        public event Action Changed;

        public IEnumerable<KeyValuePair<ResourceDef, int>> Items => items.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key.DisplayName);
        public int Count(ResourceDef r) => r != null && items.TryGetValue(r, out var n) ? n : 0;
        public bool Has(ResourceStack s) => Count(s.Resource) >= s.Amount;
        public bool HasAll(IEnumerable<ResourceStack> stacks) =>
            stacks.Where(s => s.IsValid).GroupBy(s => s.Resource).All(g => Count(g.Key) >= g.Sum(s => s.Amount));

        public static bool CanHold(ResourceDef r) => r != null && r.Kind != ResourceKind.Energy;

        public void Clear()
        {
            items.Clear();
            Changed?.Invoke();
        }

        public int Add(ResourceDef r, int amount)
        {
            if (!CanHold(r) || amount <= 0) return 0;
            items[r] = Count(r) + amount;
            Changed?.Invoke();
            return amount;
        }

        public int Remove(ResourceDef r, int amount)
        {
            int n = Math.Min(amount, Count(r));
            if (n <= 0) return 0;
            items[r] = Count(r) - n;
            if (items[r] == 0) items.Remove(r);
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
    }
}
