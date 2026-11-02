using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Vault : ressources de base illimitées, et stockage commun (l'inventaire du joueur), partagé par tous les vaults.
    /// Ses faces d'entrée rangent ce qu'on y pousse ; un trieur collé au vault en sort la ressource choisie.</summary>
    public sealed class VaultMachine : Machine
    {
        public VaultMachine(MachineDef def, World world, Vector3Int pos) : base(def, world, pos, SideMode.None)
        {
            BaseResources = world.Db.BaseResources.ToList();
        }

        public IReadOnlyList<ResourceDef> BaseResources { get; }

        public bool Provides(ResourceDef r) => r != null && r.IsBaseResource;

        public override bool HasOutputSides => false;

        protected override void OnTick() => SetStatus(MachineStatus.Idle, "");

        /// <summary>Ce qui entre par une face d'entrée va dans le stockage commun.</summary>
        protected override int Accept(ResourceDef r, int amount, bool simulate)
        {
            var store = World.SharedStorage;
            if (store == null || r == null || amount <= 0 || r.Kind == ResourceKind.Energy) return 0;
            if (!simulate) store.Add(r, amount);
            return amount;
        }

        public override IEnumerable<(ResourceDef resource, int amount)> Extractable(Direction side) => Enumerable.Empty<(ResourceDef, int)>();

        public override IEnumerable<(ResourceDef resource, int amount)> Pullable()
        {
            foreach (var r in BaseResources) yield return (r, int.MaxValue);
            if (World.SharedStorage == null) yield break;
            foreach (var kv in World.SharedStorage.Items.ToList())
                if (!Provides(kv.Key)) yield return (kv.Key, kv.Value);
        }

        public override int Pull(ResourceDef r, int amount)
        {
            if (Provides(r)) return amount;
            return World.SharedStorage?.Remove(r, amount) ?? 0;
        }

        public override int Extract(ResourceDef r, int amount) => 0;

        public override IEnumerable<ResourceStack> SalvageableItems() => Enumerable.Empty<ResourceStack>();
    }
}
