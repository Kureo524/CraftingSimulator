using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;

namespace Nucleaire.Simulation
{
    /// <summary>Un emplacement : un type de ressource (objet, liquide ou gaz) et une seule ressource à la fois.</summary>
    public sealed class StorageSlot
    {
        public StorageSlot(ResourceKind kind) { Kind = kind; }

        public ResourceKind Kind { get; }
        /// <summary>Ressource contenue (null si vide).</summary>
        public ResourceDef Resource { get; internal set; }
        public int Amount { get; internal set; }
        public bool IsEmpty => Resource == null || Amount <= 0;
    }

    /// <summary>Réservoir à emplacements, comme un four de Minecraft : chaque emplacement a un type
    /// (objet, liquide, gaz) et ne contient qu'une ressource. Une ressource n'occupe qu'un emplacement.
    /// Sans liste d'emplacements, le réservoir crée un emplacement par ressource (mode libre).</summary>
    public sealed class ResourceStorage
    {
        private readonly List<StorageSlot> slots = new();
        private readonly Func<ResourceKind, int> capacity;
        private readonly HashSet<ResourceDef> allowed;
        private readonly bool unbounded;

        /// <summary>Mode libre : un emplacement par ressource, en nombre illimité.</summary>
        public ResourceStorage(Func<ResourceKind, int> capacity, IEnumerable<ResourceDef> allowed = null)
        {
            this.capacity = capacity;
            this.allowed = allowed != null ? new HashSet<ResourceDef>(allowed) : null;
            unbounded = true;
        }

        /// <summary>Emplacements fixes, un par type indiqué.</summary>
        public ResourceStorage(IEnumerable<ResourceKind> slotKinds, Func<ResourceKind, int> capacity, IEnumerable<ResourceDef> allowed = null)
            : this(capacity, allowed)
        {
            unbounded = false;
            foreach (var k in slotKinds) slots.Add(new StorageSlot(k));
        }

        public IReadOnlyList<StorageSlot> Slots => slots;
        public IReadOnlyCollection<ResourceDef> Allowed => allowed;
        public bool IsEmpty => slots.All(s => s.IsEmpty);

        public IEnumerable<KeyValuePair<ResourceDef, int>> Contents =>
            slots.Where(s => !s.IsEmpty).Select(s => new KeyValuePair<ResourceDef, int>(s.Resource, s.Amount));

        public bool Accepts(ResourceDef r) => r != null && r.Kind != ResourceKind.Energy && (allowed == null || allowed.Contains(r))
                                              && (unbounded || slots.Any(s => s.Kind == r.Kind));

        public int Capacity(ResourceDef r) => r == null ? 0 : capacity(r.Kind);
        public int Amount(ResourceDef r) => r == null ? 0 : slots.Where(s => s.Resource == r).Sum(s => s.Amount);
        public bool Has(ResourceStack s) => Amount(s.Resource) >= s.Amount;

        private StorageSlot SlotOf(ResourceDef r) => slots.FirstOrDefault(s => s.Resource == r && s.Amount > 0);
        private StorageSlot EmptySlot(ResourceKind k) => slots.FirstOrDefault(s => s.Kind == k && s.IsEmpty);

        public int Space(ResourceDef r)
        {
            if (!Accepts(r)) return 0;
            var s = SlotOf(r);
            if (s != null) return Math.Max(0, Capacity(r) - s.Amount);
            return unbounded || EmptySlot(r.Kind) != null ? Capacity(r) : 0;
        }

        public int Insert(ResourceDef r, int amount, bool simulate = false)
        {
            if (amount <= 0) return 0;
            int n = Math.Min(amount, Space(r));
            if (n <= 0 || simulate) return Math.Max(0, n);
            var s = SlotOf(r) ?? EmptySlot(r.Kind);
            if (s == null) { s = new StorageSlot(r.Kind); slots.Add(s); }
            s.Resource = r;
            s.Amount += n;
            return n;
        }

        public int Extract(ResourceDef r, int amount, bool simulate = false)
        {
            if (amount <= 0) return 0;
            var s = SlotOf(r);
            if (s == null) return 0;
            int n = Math.Min(amount, s.Amount);
            if (!simulate)
            {
                s.Amount -= n;
                if (s.Amount == 0)
                {
                    s.Resource = null;
                    if (unbounded) slots.Remove(s);
                }
            }
            return n;
        }

        /// <summary>Toutes les piles tiennent-elles en même temps (chaque ressource dans son emplacement) ?</summary>
        public bool CanFitAll(IEnumerable<ResourceStack> stacks)
        {
            var emptiesUsed = new Dictionary<ResourceKind, int>();
            foreach (var g in stacks.Where(s => s.IsValid).GroupBy(s => s.Resource))
            {
                var r = g.Key;
                int need = g.Sum(s => s.Amount);
                if (!Accepts(r) || need > Capacity(r)) return false;
                var s = SlotOf(r);
                if (s != null) { if (Capacity(r) - s.Amount < need) return false; continue; }
                if (unbounded) continue;
                emptiesUsed.TryGetValue(r.Kind, out var used);
                if (slots.Count(x => x.Kind == r.Kind && x.IsEmpty) <= used) return false;
                emptiesUsed[r.Kind] = used + 1;
            }
            return true;
        }

        public bool HasAll(IEnumerable<ResourceStack> stacks)
        {
            foreach (var g in stacks.Where(s => s.IsValid).GroupBy(s => s.Resource))
                if (Amount(g.Key) < g.Sum(s => s.Amount)) return false;
            return true;
        }

        public void Clear()
        {
            foreach (var s in slots) { s.Resource = null; s.Amount = 0; }
            if (unbounded) slots.Clear();
        }

        /// <summary>Emplacements nécessaires pour une liste de recettes : pour chaque type, le maximum utilisé par une recette.</summary>
        public static List<ResourceKind> SlotsFor(IEnumerable<IReadOnlyList<ResourceStack>> recipes)
        {
            var max = new Dictionary<ResourceKind, int>();
            foreach (var stacks in recipes)
                foreach (var g in stacks.Where(s => s.IsValid && s.Resource.Kind != ResourceKind.Energy).GroupBy(s => s.Resource.Kind))
                    max[g.Key] = Math.Max(max.TryGetValue(g.Key, out var m) ? m : 0, g.Select(s => s.Resource).Distinct().Count());
            var result = new List<ResourceKind>();
            foreach (var k in new[] { ResourceKind.Item, ResourceKind.Fluid, ResourceKind.Gas })
                for (int i = 0; i < (max.TryGetValue(k, out var n) ? n : 0); i++) result.Add(k);
            return result;
        }
    }
}
