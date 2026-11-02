using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;

namespace Nucleaire.Crafting
{
    /// <summary>Inventaire du joueur : uniquement des solides (les fluides restent dans les machines et tuyaux).</summary>
    public sealed class PlayerInventory
    {
        private readonly Dictionary<ResourceDef, int> items = new();

        public event Action Changed;

        public IEnumerable<KeyValuePair<ResourceDef, int>> Items => items.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key.DisplayName);
        public int Count(ResourceDef r) => r != null && items.TryGetValue(r, out var n) ? n : 0;
        public bool Has(ResourceStack s) => Count(s.Resource) >= s.Amount;
        public bool HasAll(IEnumerable<ResourceStack> stacks) =>
            stacks.Where(s => s.IsValid).GroupBy(s => s.Resource).All(g => Count(g.Key) >= g.Sum(s => s.Amount));

        public static bool CanHold(ResourceDef r) => r != null && r.Kind == ResourceKind.Item;

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
