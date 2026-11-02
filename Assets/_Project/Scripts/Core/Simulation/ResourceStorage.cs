using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;

namespace Nucleaire.Simulation
{
    /// <summary>Réservoir multi-ressources : une capacité par ressource, déterminée par sa nature.</summary>
    public sealed class ResourceStorage
    {
        private readonly Dictionary<ResourceDef, int> amounts = new();
        private readonly Func<ResourceKind, int> capacity;
        private readonly HashSet<ResourceDef> allowed;

        /// <param name="capacity">Capacité par ressource selon sa nature.</param>
        /// <param name="allowed">Ressources acceptées (null = toutes).</param>
        public ResourceStorage(Func<ResourceKind, int> capacity, IEnumerable<ResourceDef> allowed = null)
        {
            this.capacity = capacity;
            this.allowed = allowed != null ? new HashSet<ResourceDef>(allowed) : null;
        }

        public IEnumerable<KeyValuePair<ResourceDef, int>> Contents => amounts.Where(kv => kv.Value > 0);
        public IReadOnlyCollection<ResourceDef> Allowed => allowed;
        public bool IsEmpty => !amounts.Any(kv => kv.Value > 0);

        public bool Accepts(ResourceDef r) => r != null && (allowed == null || allowed.Contains(r));
        public int Capacity(ResourceDef r) => r == null ? 0 : capacity(r.Kind);
        public int Amount(ResourceDef r) => r != null && amounts.TryGetValue(r, out var a) ? a : 0;
        public int Space(ResourceDef r) => Accepts(r) ? Math.Max(0, Capacity(r) - Amount(r)) : 0;
        public bool Has(ResourceStack s) => Amount(s.Resource) >= s.Amount;

        public int Insert(ResourceDef r, int amount, bool simulate = false)
        {
            if (amount <= 0) return 0;
            int n = Math.Min(amount, Space(r));
            if (n > 0 && !simulate) amounts[r] = Amount(r) + n;
            return n;
        }

        public int Extract(ResourceDef r, int amount, bool simulate = false)
        {
            if (amount <= 0) return 0;
            int n = Math.Min(amount, Amount(r));
            if (n > 0 && !simulate) amounts[r] = Amount(r) - n;
            return n;
        }

        /// <summary>Toutes les piles tiennent-elles (en tenant compte des doublons) ?</summary>
        public bool CanFitAll(IEnumerable<ResourceStack> stacks)
        {
            foreach (var g in stacks.Where(s => s.IsValid).GroupBy(s => s.Resource))
                if (Space(g.Key) < g.Sum(s => s.Amount)) return false;
            return true;
        }

        public bool HasAll(IEnumerable<ResourceStack> stacks)
        {
            foreach (var g in stacks.Where(s => s.IsValid).GroupBy(s => s.Resource))
                if (Amount(g.Key) < g.Sum(s => s.Amount)) return false;
            return true;
        }

        public void Clear() => amounts.Clear();
    }
}
