using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Base de toutes les machines posées : réservoirs d'entrée/sortie, énergie, faces, améliorations, état.</summary>
    public abstract class Machine : IGridEntity, IResourceReceiver
    {
        private readonly List<UpgradeDef> upgrades = new();
        private MachineStatus status = MachineStatus.Idle;

        protected Machine(MachineDef def, World world, Vector3Int position, SideMode defaultSideMode)
        {
            MachineDef = def;
            World = world;
            Position = position;
            Sides = new SideConfig(defaultSideMode);
        }

        public MachineDef MachineDef { get; }
        public PlaceableDef Def => MachineDef;
        public World World { get; }
        public Vector3Int Position { get; internal set; }
        public SideConfig Sides { get; }

        public ResourceStorage Inputs { get; protected set; }
        public ResourceStorage Outputs { get; protected set; }

        public int Energy { get; protected set; }
        public int EnergyCapacity => Mathf.RoundToInt(MachineDef.EnergyCapacity * CapacityMultiplier);
        public virtual bool ConsumesEnergy => false;
        public virtual bool ProducesEnergy => false;

        public bool Enabled { get; private set; } = true;
        public MachineStatus Status => status;
        public string StatusDetail { get; private set; } = "";
        /// <summary>Avancement du travail en cours (0..1).</summary>
        public virtual float Progress => 0f;

        public IReadOnlyList<UpgradeDef> Upgrades => upgrades;
        public float Multiplier(MachineStat stat) => MachineStats.Multiplier(upgrades, stat);
        protected float CapacityMultiplier => Multiplier(MachineStat.Capacity);

        public event Action<Machine> StatusChanged;

        /// <summary>Capacité par ressource, améliorations comprises.</summary>
        protected int CapacityFor(ResourceKind kind) => Mathf.RoundToInt(MachineDef.CapacityFor(kind) * CapacityMultiplier);

        protected ResourceDef EnergyDef => World.Db.Energy;

        // ---------------- Cycle de simulation ----------------

        public void Tick()
        {
            if (!Enabled) { SetStatus(MachineStatus.Disabled, "Arrêtée par le joueur."); return; }
            OnTick();
        }

        protected abstract void OnTick();

        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            if (enabled && status == MachineStatus.Disabled) SetStatus(MachineStatus.Idle, "");
        }

        protected void SetStatus(MachineStatus s, string detail)
        {
            if (s == status && detail == StatusDetail) return;
            bool changed = s != status;
            status = s;
            StatusDetail = detail ?? "";
            if (changed && s.IsProblem())
                World.Log(s == MachineStatus.Meltdown ? EventSeverity.Error : EventSeverity.Warning,
                    $"{MachineDef.DisplayName} : {s.DisplayName()}. {StatusDetail}", this);
            StatusChanged?.Invoke(this);
        }

        // ---------------- Échanges ----------------

        /// <summary>Réception par une face (transmetteur ou machine voisine).</summary>
        public int Receive(ResourceDef r, int amount, Direction side, bool simulate = false)
        {
            var s = Sides[side];
            if (s.Mode != SideMode.Input || !s.Allows(r)) return 0;
            return Accept(r, amount, simulate);
        }

        /// <summary>Insertion sans tenir compte des faces (dépôt manuel du joueur).</summary>
        public int InsertManual(ResourceDef r, int amount) => Accept(r, amount, false);

        protected virtual int Accept(ResourceDef r, int amount, bool simulate)
        {
            if (r == null || amount <= 0) return 0;
            if (r == EnergyDef)
            {
                if (!ConsumesEnergy) return 0;
                int n = Math.Min(amount, EnergyCapacity - Energy);
                if (n <= 0) return 0;
                if (!simulate) Energy += n;
                return n;
            }
            return Inputs?.Insert(r, amount, simulate) ?? 0;
        }

        /// <summary>Ressources que la machine peut pousser par une face de sortie.</summary>
        public virtual IEnumerable<(ResourceDef resource, int amount)> Extractable(Direction side)
        {
            var s = Sides[side];
            if (s.Mode != SideMode.Output) yield break;
            if (Outputs != null)
                foreach (var kv in Outputs.Contents.ToList())
                    if (s.Allows(kv.Key)) yield return (kv.Key, kv.Value);
            if (ProducesEnergy && Energy > 0 && s.Allows(EnergyDef)) yield return (EnergyDef, Energy);
        }

        public virtual int Extract(ResourceDef r, int amount)
        {
            if (r == EnergyDef && ProducesEnergy)
            {
                int n = Math.Min(amount, Energy);
                Energy -= n;
                return n;
            }
            return Outputs?.Extract(r, amount) ?? 0;
        }

        protected int AddEnergy(int amount)
        {
            int n = Math.Min(amount, EnergyCapacity - Energy);
            if (n > 0) Energy += n;
            return Math.Max(0, n);
        }

        protected int ConsumeEnergy(int amount)
        {
            int n = Math.Min(amount, Energy);
            Energy -= n;
            return n;
        }

        // ---------------- Améliorations ----------------

        public bool CanInstall(UpgradeDef u, out string reason)
        {
            reason = null;
            if (u == null) reason = "Amélioration inconnue.";
            else if (!u.IsCompatibleWith(MachineDef)) reason = $"{u.DisplayName} n'est pas compatible avec {MachineDef.DisplayName}.";
            else if (upgrades.Count >= MachineDef.UpgradeSlots) reason = "Plus d'emplacement libre.";
            else if (upgrades.Count(x => x == u) >= u.MaxPerMachine) reason = $"Maximum {u.MaxPerMachine} × {u.DisplayName}.";
            return reason == null;
        }

        public bool InstallUpgrade(UpgradeDef u)
        {
            if (!CanInstall(u, out _)) return false;
            upgrades.Add(u);
            StatusChanged?.Invoke(this);
            return true;
        }

        public bool RemoveUpgrade(UpgradeDef u)
        {
            bool ok = upgrades.Remove(u);
            if (ok)
            {
                if (Energy > EnergyCapacity) Energy = EnergyCapacity;
                StatusChanged?.Invoke(this);
            }
            return ok;
        }

        // ---------------- Démontage ----------------

        /// <summary>Solides récupérables quand le joueur démonte la machine (les fluides sont perdus).</summary>
        public virtual IEnumerable<ResourceStack> SalvageableItems()
        {
            var all = (Inputs?.Contents ?? Enumerable.Empty<KeyValuePair<ResourceDef, int>>())
                .Concat(Outputs?.Contents ?? Enumerable.Empty<KeyValuePair<ResourceDef, int>>());
            foreach (var kv in all.Where(kv => kv.Key.Kind == ResourceKind.Item))
                yield return new ResourceStack(kv.Key, kv.Value);
            foreach (var u in upgrades.Where(u => u.Item != null))
                yield return new ResourceStack(u.Item, 1);
        }
    }
}
