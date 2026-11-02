using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Port d'un multi-bloc : dans la paroi, il fait entrer et sortir les ressources et l'énergie du contrôleur.
    /// Ses faces se règlent comme celles d'une machine (entrée / sortie, emplacement précis).</summary>
    public sealed class PortMachine : Machine
    {
        public PortMachine(MachineDef def, World world, Vector3Int pos) : base(def, world, pos, SideMode.Input) { }

        /// <summary>Contrôleur de la structure complète dont il fait partie (null sinon).</summary>
        public Machine Controller { get; private set; }

        internal void Link(Machine controller)
        {
            Controller = controller;
            Inputs = controller?.Inputs;
            Outputs = controller?.Outputs;
        }

        public override bool ConsumesEnergy => Controller?.ConsumesEnergy ?? false;
        public override bool ProducesEnergy => Controller?.ProducesEnergy ?? false;
        public override float Progress => Controller?.Progress ?? 0f;

        protected override void OnTick()
        {
            if (Controller == null) SetStatus(MachineStatus.Idle, "Pas relié : il doit faire partie de la paroi d'une structure complète.");
            else SetStatus(MachineStatus.Working, $"Port de : {Controller.MachineDef.DisplayName}.");
        }

        protected override int Accept(ResourceDef r, int amount, bool simulate) => Controller == null ? 0 : Controller.AcceptFromPort(r, amount, simulate);

        public override IEnumerable<(ResourceDef resource, int amount)> Extractable(Direction side)
        {
            var s = Sides[side];
            if (Controller == null || s.Mode != SideMode.Output) yield break;
            if (Outputs != null)
            {
                if (s.Slot >= 0)
                {
                    if (s.Slot < Outputs.Slots.Count && !Outputs.Slots[s.Slot].IsEmpty)
                        yield return (Outputs.Slots[s.Slot].Resource, Outputs.Slots[s.Slot].Amount);
                    yield break;
                }
                foreach (var kv in Outputs.Contents.ToList()) yield return (kv.Key, kv.Value);
            }
            int energy = Controller.ExportableEnergy;
            if (energy > 0) yield return (World.Db.Energy, energy);
        }

        public override int Extract(ResourceDef r, int amount) => Controller?.Extract(r, amount) ?? 0;
        public override IEnumerable<(ResourceDef resource, int amount)> Pullable() => Controller?.Pullable() ?? Enumerable.Empty<(ResourceDef, int)>();
        public override int Pull(ResourceDef r, int amount) => Controller?.Pull(r, amount) ?? 0;
        public override IEnumerable<ResourceStack> SalvageableItems() => Enumerable.Empty<ResourceStack>();
    }
}
