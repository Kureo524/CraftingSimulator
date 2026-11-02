using System.Linq;
using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Trieur (à la Mekanism) : collé à une machine ou à un vault, il en tire la ressource choisie
    /// et la pousse par son avant (vers une machine, un câble…). Sans filtre, il laisse tout passer.</summary>
    public sealed class SorterMachine : Machine
    {
        public SorterMachine(MachineDef def, World world, Vector3Int pos) : base(def, world, pos, SideMode.None)
        {
            Outputs = new ResourceStorage(new[] { ResourceKind.Item, ResourceKind.Fluid, ResourceKind.Gas }, CapacityFor);
        }

        /// <summary>Seule ressource qui passe (null : toutes).</summary>
        public ResourceDef Filter { get; private set; }

        public void SetFilter(ResourceDef r)
        {
            Filter = r != null && r.Kind == ResourceKind.Energy ? null : r;
        }

        /// <summary>Face d'où il tire (derrière lui).</summary>
        public Direction Back => Facing.Opposite();
        public Machine Source => World.At(Position + Back.Offset()) as Machine;
        public IGridEntity Target => World.At(Position + Facing.Offset());

        public override bool HasOutputSides => false;

        protected internal override void OnPlaced()
        {
            foreach (var d in DirectionExtensions.All) Sides.Set(d, d == Facing ? SideMode.Output : SideMode.None);
        }

        protected override void OnTick()
        {
            var source = Source;
            if (source == null) { SetStatus(MachineStatus.Idle, "Colle-le contre une machine ou un vault : il tire de ce qui est derrière lui."); return; }
            if (Target == null) { SetStatus(MachineStatus.Idle, "Rien devant lui : il pousse par son avant (machine ou câble)."); return; }

            int moved = 0;
            foreach (var (r, available) in source.Pullable().ToList())
            {
                if (r == null || r.Kind == ResourceKind.Energy || (Filter != null && r != Filter)) continue;
                int n = Mathf.Min(available, World.Settings.DirectTransfer(r.Kind));
                n = Outputs.Insert(r, n, simulate: true);
                if (n <= 0) continue;
                n = source.Pull(r, n);
                Outputs.Insert(r, n);
                moved += n;
                if (Filter == null) break;
            }
            if (moved > 0) SetStatus(MachineStatus.Working, "");
            else if (Outputs.Slots.Any(s => !s.IsEmpty)) SetStatus(MachineStatus.OutputFull, "Ce qui est devant n'accepte pas : vérifie sa face d'entrée.");
            else SetStatus(MachineStatus.Idle, Filter == null ? "Rien à prendre derrière." : $"Pas de {Filter.DisplayName.ToLowerInvariant()} derrière.");
        }
    }
}
