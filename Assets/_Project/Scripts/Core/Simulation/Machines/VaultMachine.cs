using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Stock illimité des ressources de base. Ses faces de sortie poussent la ressource choisie en filtre.</summary>
    public sealed class VaultMachine : Machine
    {
        public VaultMachine(MachineDef def, World world, Vector2Int pos) : base(def, world, pos, SideMode.None)
        {
            BaseResources = world.Db.BaseResources.ToList();
        }

        public IReadOnlyList<ResourceDef> BaseResources { get; }

        public bool Provides(ResourceDef r) => r != null && r.IsBaseResource;

        protected override void OnTick()
        {
            bool unfiltered = DirectionExtensions.All.Any(d => Sides[d].Mode == SideMode.Output && Sides[d].Filter == null);
            if (unfiltered) SetStatus(MachineStatus.Idle, "Une face de sortie sans filtre ne sort rien : choisis une ressource.");
            else SetStatus(MachineStatus.Idle, "");
        }

        protected override int Accept(ResourceDef r, int amount, bool simulate) => 0;

        public override IEnumerable<(ResourceDef resource, int amount)> Extractable(Direction side)
        {
            var s = Sides[side];
            if (s.Mode == SideMode.Output && Provides(s.Filter)) yield return (s.Filter, int.MaxValue);
        }

        public override int Extract(ResourceDef r, int amount) => Provides(r) ? amount : 0;

        public override IEnumerable<ResourceStack> SalvageableItems() => Enumerable.Empty<ResourceStack>();
    }
}
