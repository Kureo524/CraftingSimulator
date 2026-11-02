using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using Nucleaire.Logistics;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Trieur : une plaque collée sur une face de machine (ou de vault), sans prendre de case.
    /// À chaque tick, il tire de la machine les ressources de sa liste et les pousse vers ce qui touche cette face
    /// (machine ou câble). Liste vide : tout passe.</summary>
    public sealed class FaceSorter
    {
        private readonly List<ResourceDef> filters = new();

        internal FaceSorter(Machine machine, Direction face)
        {
            Machine = machine;
            Face = face;
        }

        public Machine Machine { get; }
        public Direction Face { get; }
        public IReadOnlyList<ResourceDef> Filters => filters;
        public IGridEntity Target => Machine.World.At(Machine.Position + Face.Offset());
        public int LastMoved { get; private set; }

        public bool Allows(ResourceDef r) => r != null && r.Kind != ResourceKind.Energy && (filters.Count == 0 || filters.Contains(r));

        public void AddFilter(ResourceDef r)
        {
            if (r == null || r.Kind == ResourceKind.Energy || filters.Contains(r)) return;
            filters.Add(r);
            Machine.NotifyAttachments();
        }

        public void RemoveFilter(ResourceDef r)
        {
            if (filters.Remove(r)) Machine.NotifyAttachments();
        }

        internal void Tick()
        {
            LastMoved = 0;
            var target = Target;
            if (target == null) return;
            var world = Machine.World;
            foreach (var (r, available) in Machine.Pullable().ToList())
            {
                if (!Allows(r)) continue;
                int n = Math.Min(available, world.Settings.DirectTransfer(r.Kind));
                int accepted = target switch
                {
                    Machine m => m.Receive(r, n, Face.Opposite(), true, true),
                    TransmitterSegment seg => seg.Network != null && seg.Network.CanCarry(r) ? Math.Min(n, seg.Network.Capacity - seg.Network.Stored) : 0,
                    _ => 0,
                };
                if (accepted <= 0) continue;
                int taken = Machine.Pull(r, accepted);
                int moved = target switch
                {
                    Machine m => m.Receive(r, taken, Face.Opposite(), false, true),
                    TransmitterSegment seg => seg.Network.Insert(r, taken),
                    _ => 0,
                };
                LastMoved += moved;
            }
        }
    }
}
