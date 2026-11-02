using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using Nucleaire.Logistics;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>L'atelier : grille, entités posées, ordre des phases d'un tick, statistiques de production.
    /// C# pur, déterministe, piloté par un composant de présentation (ou par les tests).</summary>
    public sealed class World
    {
        private readonly Dictionary<Vector3Int, IGridEntity> grid = new();
        private readonly List<Machine> machines = new();
        private readonly Dictionary<ResourceDef, long> produced = new();

        public World(RecipeDatabase db, SimulationSettings settings)
        {
            Db = db ? db : throw new ArgumentNullException(nameof(db));
            Settings = settings ? settings : throw new ArgumentNullException(nameof(settings));
            Logistics = new LogisticsSystem(this);
        }

        public RecipeDatabase Db { get; }
        public SimulationSettings Settings { get; }
        public LogisticsSystem Logistics { get; }
        public EventLog Events { get; } = new();
        public long CurrentTick { get; private set; }

        public IReadOnlyList<Machine> Machines => machines;
        public IEnumerable<IGridEntity> Entities => grid.Values;

        public event Action<IGridEntity> EntityPlaced;
        public event Action<IGridEntity> EntityRemoved;
        /// <summary>Une machine a changé de case (ancienne position en second argument).</summary>
        public event Action<IGridEntity, Vector3Int> EntityMoved;
        public event Action<ResourceDef, int> ResourceProduced;
        public event Action Ticked;

        // ---------------- Grille ----------------

        public bool InBounds(Vector3Int p) => p.x >= 0 && p.y >= 0 && p.z >= 0 && p.x < Settings.gridSize.x && p.y < Settings.gridSize.y && p.z < Settings.levels;
        public IGridEntity At(Vector3Int p) => grid.TryGetValue(p, out var e) ? e : null;
        public bool IsFree(Vector3Int p) => InBounds(p) && !grid.ContainsKey(p);

        public bool CanPlace(PlaceableDef def, Vector3Int p, out string reason)
        {
            reason = null;
            if (def == null) reason = "Rien à poser.";
            else if (!InBounds(p)) reason = "Hors de l'atelier.";
            else if (grid.ContainsKey(p)) reason = "Case déjà occupée.";
            return reason == null;
        }

        public IGridEntity Place(PlaceableDef def, Vector3Int p, Direction facing = Direction.North)
        {
            if (!CanPlace(def, p, out var reason)) throw new InvalidOperationException(reason);
            IGridEntity entity;
            switch (def)
            {
                case MachineDef md:
                    var m = MachineFactory.Create(md, this, p);
                    m.Facing = facing.IsVertical() ? Direction.North : facing;
                    machines.Add(m);
                    grid[p] = m;
                    entity = m;
                    break;
                case TransmitterDef td:
                    var seg = new TransmitterSegment(td, p);
                    grid[p] = seg;
                    Logistics.OnSegmentAdded(seg);
                    entity = seg;
                    break;
                default:
                    throw new ArgumentException($"Type de placeable inconnu : {def.GetType().Name}");
            }
            EntityPlaced?.Invoke(entity);
            return entity;
        }

        /// <summary>Déplace une machine en conservant son contenu, ses faces et ses améliorations.</summary>
        public bool CanMove(Vector3Int from, Vector3Int to, out string reason)
        {
            reason = null;
            if (!grid.TryGetValue(from, out var e)) reason = "Rien à déplacer.";
            else if (e is not Machine) reason = "Seules les machines se déplacent ; démonte les tuyaux.";
            else if (from == to) reason = "C'est déjà sa place.";
            else if (!InBounds(to)) reason = "Hors de l'atelier.";
            else if (grid.ContainsKey(to)) reason = "Case déjà occupée.";
            return reason == null;
        }

        public bool Move(Vector3Int from, Vector3Int to)
        {
            if (!CanMove(from, to, out _)) return false;
            var m = (Machine)grid[from];
            grid.Remove(from);
            m.Position = to;
            grid[to] = m;
            EntityMoved?.Invoke(m, from);
            return true;
        }

        public IGridEntity Remove(Vector3Int p)
        {
            if (!grid.TryGetValue(p, out var e)) return null;
            grid.Remove(p);
            if (e is Machine m) machines.Remove(m);
            if (e is TransmitterSegment seg) Logistics.OnSegmentRemoved(seg);
            EntityRemoved?.Invoke(e);
            return e;
        }

        // ---------------- Simulation ----------------

        /// <summary>Un tick : 1) sorties poussées, 2) réseaux distribuent, 3) machines travaillent.</summary>
        public void Step()
        {
            CurrentTick++;
            foreach (var m in machines.ToList()) Logistics.PushOutputs(m);
            Logistics.Distribute();
            foreach (var m in machines.ToList()) m.Tick();
            Ticked?.Invoke();
        }

        public void Step(int ticks)
        {
            for (int i = 0; i < ticks; i++) Step();
        }

        // ---------------- Statistiques & journal ----------------

        /// <summary>Retire toutes les entités de l'atelier.</summary>
        public void Clear()
        {
            foreach (var p in grid.Keys.ToList()) Remove(p);
        }

        /// <summary>Remet à zéro les statistiques de production.</summary>
        public void ResetProduction() => produced.Clear();

        public long Produced(ResourceDef r) => r != null && produced.TryGetValue(r, out var n) ? n : 0;

        public void RecordProduction(ResourceDef r, int amount)
        {
            if (r == null || amount <= 0) return;
            produced[r] = Produced(r) + amount;
            ResourceProduced?.Invoke(r, amount);
        }

        public void Log(EventSeverity severity, string message, IGridEntity source = null) =>
            Events.Add(new SimEvent(CurrentTick, severity, message, source));

        public T FirstMachine<T>() where T : Machine => machines.OfType<T>().FirstOrDefault();
    }
}
