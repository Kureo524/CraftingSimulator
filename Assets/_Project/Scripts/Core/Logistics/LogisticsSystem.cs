using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;

namespace Nucleaire.Logistics
{
    /// <summary>Gère les réseaux de transmetteurs : fusion à la pose, scission au retrait, distribution à chaque tick.</summary>
    public sealed class LogisticsSystem
    {
        private readonly World world;
        private readonly List<TransmitterNetwork> networks = new();

        public LogisticsSystem(World world) { this.world = world; }

        public IReadOnlyList<TransmitterNetwork> Networks => networks;

        internal void OnSegmentAdded(TransmitterSegment seg)
        {
            var neighbours = Neighbours(seg).Select(n => n.Network).Distinct().ToList();
            var net = new TransmitterNetwork(seg.TransmitterDef);
            net.AddSegment(seg);
            foreach (var old in neighbours) Absorb(net, old);
            networks.Add(net);
        }

        internal void OnSegmentRemoved(TransmitterSegment seg)
        {
            var old = seg.Network;
            if (old == null) return;
            networks.Remove(old);
            int oldCount = old.Segments.Count;
            var contents = old.TakeAllContents();
            var remaining = old.Segments.Where(s => s != seg).ToHashSet();
            old.ClearSegments();
            seg.Network = null;

            // Reconstruction par remplissage : chaque composante connexe devient un réseau.
            var created = new List<TransmitterNetwork>();
            while (remaining.Count > 0)
            {
                var start = remaining.First();
                var net = new TransmitterNetwork(start.TransmitterDef);
                var stack = new Stack<TransmitterSegment>();
                stack.Push(start);
                remaining.Remove(start);
                while (stack.Count > 0)
                {
                    var s = stack.Pop();
                    net.AddSegment(s);
                    foreach (var n in Neighbours(s).Where(remaining.Contains).ToList())
                    {
                        remaining.Remove(n);
                        stack.Push(n);
                    }
                }
                created.Add(net);
                networks.Add(net);
            }

            // Le contenu est réparti au prorata des segments ; la part du segment retiré est perdue.
            foreach (var kv in contents)
                foreach (var net in created)
                    net.AddContents(kv.Key, kv.Value * net.Segments.Count / oldCount);
        }

        /// <summary>Recalcule le réseau d'un segment après un changement de liaison.</summary>
        private void Reconnect(TransmitterSegment seg)
        {
            OnSegmentRemoved(seg);
            OnSegmentAdded(seg);
        }

        /// <summary>Réglage d'un côté de câble (mode Modification).
        /// Vers une machine : Normal → Tirer → Pousser. Vers un câble du même type : coupe ou rétablit la liaison.
        /// Renvoie une description du nouvel état, ou null si rien ne change.</summary>
        public string CycleCableSide(TransmitterSegment seg, Direction d, int step = 1)
        {
            var n = world.At(seg.Position + d.Offset());
            switch (n)
            {
                case Machine:
                {
                    var order = new[] { CableSide.Normal, CableSide.Pull, CableSide.Push };
                    int i = Array.IndexOf(order, seg[d]);
                    if (i < 0) i = 0;
                    var next = order[((i + step) % 3 + 3) % 3];
                    seg.SetSide(d, next);
                    SideChanged?.Invoke(seg);
                    return next switch { CableSide.Pull => "Tirer : le câble prend les produits de la machine", CableSide.Push => "Pousser : le câble remplit la machine", _ => "Normal : selon les faces de la machine" };
                }
                case TransmitterSegment other when other.TransmitterDef == seg.TransmitterDef:
                {
                    bool cut = seg[d] == CableSide.Cut || other[d.Opposite()] == CableSide.Cut;
                    var state = cut ? CableSide.Normal : CableSide.Cut;
                    seg.SetSide(d, state);
                    other.SetSide(d.Opposite(), state);
                    Reconnect(seg);
                    if (cut && other.Network != seg.Network) Reconnect(other);
                    SideChanged?.Invoke(seg);
                    SideChanged?.Invoke(other);
                    return cut ? "Liaison rétablie" : "Liaison coupée";
                }
                default:
                    if (seg[d] == CableSide.Normal) return null;
                    seg.SetSide(d, CableSide.Normal);
                    SideChanged?.Invoke(seg);
                    return "Normal";
            }
        }

        /// <summary>Un côté de câble a changé (pour l'affichage).</summary>
        public event Action<TransmitterSegment> SideChanged;

        private void Absorb(TransmitterNetwork into, TransmitterNetwork from)
        {
            if (from == null || from == into) return;
            networks.Remove(from);
            foreach (var s in from.Segments.ToList()) into.AddSegment(s);
            foreach (var kv in from.TakeAllContents())
                if (into.AllowsMixing || into.CanCarry(kv.Key)) into.AddContents(kv.Key, kv.Value);
            // Contenus incompatibles (deux fluides différents) : le second est perdu, comme une fuite.
        }

        private IEnumerable<TransmitterSegment> Neighbours(TransmitterSegment seg) =>
            DirectionExtensions.All
                .Select(d => world.At(seg.Position + d.Offset()) as TransmitterSegment)
                .Where(n => n != null && n.ConnectsTo(seg));

        // ---------------- Tick ----------------

        /// <summary>Phase 1 : chaque face de sortie pousse vers la machine voisine ou le réseau voisin.</summary>
        internal void PushOutputs(Machine m)
        {
            foreach (var d in DirectionExtensions.All)
            {
                if (m.Sides[d].Mode != SideMode.Output) continue;
                var neighbour = world.At(m.Position + d.Offset());
                if (neighbour == null) continue;

                foreach (var (r, available) in m.Extractable(d).ToList())
                {
                    int amount = Mathf.Min(available, world.Settings.DirectTransfer(r.Kind));
                    int moved = neighbour switch
                    {
                        Machine target => target.Receive(r, amount, d.Opposite()),
                        // Un câble réglé sur « Pousser » ne reçoit rien de la machine.
                        TransmitterSegment seg when seg[d.Opposite()] != CableSide.Push => seg.Network.Insert(r, amount),
                        _ => 0,
                    };
                    if (moved > 0) m.Extract(r, moved);
                }
            }
        }

        /// <summary>Phase 2 : chaque réseau distribue son contenu.</summary>
        internal void Distribute()
        {
            // Câbles en « Tirer » : ils prennent les produits de la machine voisine, quelle que soit sa face.
            foreach (var net in networks)
                foreach (var seg in net.Segments)
                    foreach (var d in DirectionExtensions.All)
                    {
                        if (seg[d] != CableSide.Pull || world.At(seg.Position + d.Offset()) is not Machine m) continue;
                        foreach (var (r, available) in m.Pullable().ToList())
                        {
                            if (!net.CanCarry(r)) continue;
                            int n = net.Insert(r, Mathf.Min(available, world.Settings.DirectTransfer(r.Kind)));
                            if (n > 0) m.Pull(r, n);
                        }
                    }
            foreach (var net in networks) net.Distribute(world);
        }

        public TransmitterNetwork NetworkAt(Vector3Int pos) => (world.At(pos) as TransmitterSegment)?.Network;
    }
}
