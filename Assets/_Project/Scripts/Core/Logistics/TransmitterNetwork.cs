using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using Nucleaire.Simulation;

namespace Nucleaire.Logistics
{
    /// <summary>Ensemble de segments connectés du même type. Contenu partagé, débit de distribution limité par tick.
    /// Liquides, gaz et énergie : une seule ressource à la fois dans le réseau. Solides : mélange autorisé.</summary>
    public sealed class TransmitterNetwork
    {
        private readonly HashSet<TransmitterSegment> segments = new();
        private readonly Dictionary<ResourceDef, int> contents = new();
        private int roundRobin;

        public TransmitterNetwork(TransmitterDef def) { Def = def; }

        public TransmitterDef Def { get; }
        public ResourceKind Carries => Def.Carries;
        public IReadOnlyCollection<TransmitterSegment> Segments => segments;
        public int Capacity => segments.Count * Def.CapacityPerSegment;
        public int Stored => contents.Values.Sum();
        public IEnumerable<KeyValuePair<ResourceDef, int>> Contents => contents.Where(kv => kv.Value > 0);
        public bool AllowsMixing => Carries == ResourceKind.Item;
        /// <summary>Quantité distribuée au dernier tick (pour l'affichage).</summary>
        public int LastMoved { get; private set; }

        internal void AddSegment(TransmitterSegment s) { segments.Add(s); s.Network = this; }

        internal void ClearSegments() => segments.Clear();

        internal void AddContents(ResourceDef r, int amount)
        {
            if (r == null || amount <= 0) return;
            contents[r] = (contents.TryGetValue(r, out var a) ? a : 0) + amount;
        }

        internal Dictionary<ResourceDef, int> TakeAllContents()
        {
            var copy = contents.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
            contents.Clear();
            return copy;
        }

        public bool CanCarry(ResourceDef r)
        {
            if (r == null || r.Kind != Carries) return false;
            if (AllowsMixing) return true;
            var current = contents.FirstOrDefault(kv => kv.Value > 0).Key;
            return current == null || current == r;
        }

        /// <summary>Insertion depuis une face de sortie de machine.</summary>
        public int Insert(ResourceDef r, int amount)
        {
            if (!CanCarry(r) || amount <= 0) return 0;
            int n = Math.Min(amount, Capacity - Stored);
            if (n <= 0) return 0;
            AddContents(r, n);
            return n;
        }

        /// <summary>Distribue le contenu aux faces d'entrée voisines, dans la limite du débit.</summary>
        internal void Distribute(World world)
        {
            LastMoved = 0;
            if (Stored == 0) return;

            var acceptors = new List<(Machine machine, Direction side, bool force)>();
            foreach (var seg in segments)
                foreach (var d in DirectionExtensions.All)
                    if (world.At(seg.Position + d.Offset()) is Machine m)
                    {
                        var side = d.Opposite();
                        var cs = seg[d];
                        if (cs == CableSide.Pull) continue; // ce côté ne fait que tirer
                        bool force = cs == CableSide.Push;
                        if ((force || m.Sides[side].Mode == SideMode.Input) && !acceptors.Any(a => a.machine == m && a.side == side))
                            acceptors.Add((m, side, force));
                    }
            if (acceptors.Count == 0) return;

            int budget = Def.ThroughputPerTick;
            roundRobin = (roundRobin + 1) % acceptors.Count;
            var ordered = acceptors.Skip(roundRobin).Concat(acceptors.Take(roundRobin)).ToList();

            foreach (var r in contents.Keys.ToList())
            {
                // 1re passe : part équitable ; 2e passe : le reste à qui peut le prendre.
                for (int pass = 0; pass < 2 && budget > 0; pass++)
                {
                    int share = pass == 0 ? Math.Max(1, budget / ordered.Count) : budget;
                    foreach (var (m, side, force) in ordered)
                    {
                        int available = contents[r];
                        if (available <= 0 || budget <= 0) break;
                        int n = m.Receive(r, Math.Min(Math.Min(share, budget), available), side, false, force);
                        if (n <= 0) continue;
                        contents[r] = available - n;
                        budget -= n;
                        LastMoved += n;
                    }
                }
                if (contents[r] <= 0) contents.Remove(r);
            }
        }
    }
}
