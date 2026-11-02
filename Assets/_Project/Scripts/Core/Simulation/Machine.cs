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

        /// <summary>Orientation choisie à la pose (Nord, Est, Sud ou Ouest) : l'avant de la machine.</summary>
        public Direction Facing { get; internal set; } = Direction.North;
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
        /// <param name="force">Ignore le réglage de la face (trieur, câble en « Pousser »).</param>
        public int Receive(ResourceDef r, int amount, Direction side, bool simulate = false) => Receive(r, amount, side, simulate, false);

        public int Receive(ResourceDef r, int amount, Direction side, bool simulate, bool force)
        {
            var s = Sides[side];
            if (!force && (s.Mode != SideMode.Input || !s.Allows(r))) return 0;
            if (s.Mode == SideMode.Input && s.Slot >= 0 && r != EnergyDef && Inputs != null) return Inputs.InsertInto(s.Slot, r, amount, simulate);
            return Accept(r, amount, simulate);
        }

        // ---------------- Réglage des faces ----------------

        /// <summary>Réglages possibles d'une face, dans l'ordre du cycle : neutre, entrée (toutes puis chaque emplacement),
        /// sortie (globale puis chaque emplacement).</summary>
        public IReadOnlyList<SideSetting> SideOptions()
        {
            var list = new List<SideSetting> { new(SideMode.None), new(SideMode.Input) };
            if (Inputs != null && Inputs.Slots.Count > 1)
                for (int i = 0; i < Inputs.Slots.Count; i++) list.Add(new SideSetting(SideMode.Input, null, i));
            if (HasOutputSides && ((Outputs != null && Outputs.Slots.Count > 0) || ProducesEnergy))
            {
                list.Add(new SideSetting(SideMode.Output));
                if (Outputs != null && Outputs.Slots.Count > 1)
                    for (int i = 0; i < Outputs.Slots.Count; i++) list.Add(new SideSetting(SideMode.Output, null, i));
            }
            return list;
        }

        /// <summary>Passe la face au réglage suivant (step = 1) ou précédent (step = -1).</summary>
        public void CycleSide(Direction d, int step = 1)
        {
            var options = SideOptions();
            int i = 0;
            for (int k = 0; k < options.Count; k++) if (options[k].SameAs(Sides[d])) { i = k; break; }
            i = ((i + step) % options.Count + options.Count) % options.Count;
            Sides.Set(d, options[i]);
        }

        /// <summary>Nom lisible du réglage d'une face (« Entrée 2 · eau », « Sortie », « Neutre »).</summary>
        public string SideLabel(Direction d) => SideLabel(Sides[d]);

        public string SideLabel(SideSetting s)
        {
            if (s.Mode == SideMode.None) return "Neutre";
            bool input = s.Mode == SideMode.Input;
            if (s.Slot < 0) return input ? "Entrée" : "Sortie";
            var storage = input ? Inputs : Outputs;
            string what = storage != null && s.Slot < storage.Slots.Count
                ? storage.Slots[s.Slot].Resource?.DisplayName.ToLowerInvariant() ?? storage.Slots[s.Slot].Kind.DisplayName().ToLowerInvariant()
                : "";
            return $"{(input ? "Entrée" : "Sortie")} {s.Slot + 1}" + (what.Length > 0 ? $" · {what}" : "");
        }

        // ---------------- Trieurs (plaques sur les faces) ----------------

        private readonly FaceSorter[] sorters = new FaceSorter[6];
        public event Action AttachmentsChanged;
        public FaceSorter SorterAt(Direction d) => sorters[(int)d];
        public IEnumerable<FaceSorter> Sorters => sorters.Where(s => s != null);

        /// <summary>Colle un trieur sur une face (la face passe en neutre : c'est le trieur qui fait sortir).</summary>
        public FaceSorter AttachSorter(Direction d)
        {
            if (sorters[(int)d] != null) return null;
            var s = new FaceSorter(this, d);
            sorters[(int)d] = s;
            Sides.Set(d, SideMode.None);
            AttachmentsChanged?.Invoke();
            return s;
        }

        public bool DetachSorter(Direction d)
        {
            if (sorters[(int)d] == null) return false;
            sorters[(int)d] = null;
            AttachmentsChanged?.Invoke();
            return true;
        }

        internal void NotifyAttachments() => AttachmentsChanged?.Invoke();

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
            {
                if (s.Slot >= 0)
                {
                    if (s.Slot < Outputs.Slots.Count && !Outputs.Slots[s.Slot].IsEmpty)
                        yield return (Outputs.Slots[s.Slot].Resource, Outputs.Slots[s.Slot].Amount);
                    yield break;
                }
                foreach (var kv in Outputs.Contents.ToList())
                    if (s.Allows(kv.Key)) yield return (kv.Key, kv.Value);
            }
            if (ProducesEnergy && Energy > 0 && s.Allows(EnergyDef)) yield return (EnergyDef, Energy);
        }

        /// <summary>Ce qu'un trieur collé à la machine peut lui prendre (ses sorties, quelle que soit la face).</summary>
        public virtual IEnumerable<(ResourceDef resource, int amount)> Pullable() =>
            Outputs == null ? Enumerable.Empty<(ResourceDef, int)>() : Outputs.Contents.Select(kv => (kv.Key, kv.Value)).ToList();

        /// <summary>Prélèvement par un trieur.</summary>
        public virtual int Pull(ResourceDef r, int amount) => Outputs?.Extract(r, amount) ?? 0;

        /// <summary>Appelé une fois posée (orientation connue).</summary>
        protected internal virtual void OnPlaced() { }

        /// <summary>Les faces de cette machine peuvent-elles être mises en sortie ?</summary>
        public virtual bool HasOutputSides => true;

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
