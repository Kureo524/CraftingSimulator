using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;

namespace Nucleaire.Game
{
    /// <summary>Pose multiple : les cases prévues (toutes dans un même plan), affichées en aperçu,
    /// posées seulement quand le joueur valide. Le plan dépend de la face du premier clic :
    /// le sol ou le dessus d'un bloc → un étage (X, Z) ; le côté d'un bloc → un mur vertical (X, Y) ou (Z, Y).</summary>
    public sealed class BuildPlan
    {
        private readonly GameContext ctx;
        private readonly List<Vector3Int> cells = new();

        public BuildPlan(GameContext ctx)
        {
            this.ctx = ctx;
            ctx.BuildItemChanged += _ => Clear();
            ctx.Session.BoardReset += Clear;
        }

        public event Action Changed;
        public IReadOnlyList<Vector3Int> Cells => cells;
        public bool Active => cells.Count > 0;
        /// <summary>Face visée au premier clic (de la case d'appui vers la case posée) : sa normale fixe le plan.</summary>
        public Direction Face { get; private set; } = Direction.Up;
        /// <summary>Première case : le plan passe par elle.</summary>
        public Vector3Int Origin { get; private set; }

        /// <summary>Axe de grille fixé par le plan (0 = x, 1 = y, 2 = étage).</summary>
        public static int AxisOf(Direction face) => face switch
        {
            Direction.East or Direction.West => 0,
            Direction.North or Direction.South => 1,
            _ => 2,
        };

        public bool InPlane(Vector3Int c) => !Active || c[AxisOf(Face)] == Origin[AxisOf(Face)];

        public bool Contains(Vector3Int c) => cells.Contains(c);

        /// <summary>Ajoute une case. La première fixe le plan, selon la face visée.</summary>
        public bool Add(Vector3Int c, Direction face = Direction.Up)
        {
            if (cells.Count == 0) { Face = face; Origin = c; }
            if (!InPlane(c) || cells.Contains(c) || !ctx.World.InBounds(c)) return false;
            cells.Add(c);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Libellé du plan, pour le panneau.</summary>
        public string PlaneLabel => AxisOf(Face) switch
        {
            2 => $"étage {Origin.z + 1}",
            _ => $"mur vertical {(AxisOf(Face) == 0 ? "nord-sud" : "est-ouest")}",
        };

        public bool Remove(Vector3Int c)
        {
            if (!cells.Remove(c)) return false;
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            if (cells.Count == 0) return;
            cells.Clear();
            Changed?.Invoke();
        }

        public bool IsValid(Vector3Int c) => ctx.BuildPlaceable != null && ctx.World.CanPlace(ctx.BuildPlaceable, c, out _);
        public int ValidCount => cells.Count(IsValid);
        public int Available => ctx.BuildItem == null ? 0 : ctx.Session.Inventory.Count(ctx.BuildItem);
        public bool CanConfirm => ValidCount > 0 && ValidCount <= Available;

        /// <summary>Pose toutes les cases valides, dans l'ordre où elles ont été tracées.</summary>
        public int Confirm()
        {
            var item = ctx.BuildItem;
            var def = ctx.BuildPlaceable;
            int placed = 0;
            foreach (var c in cells.ToList())
                if (ctx.Session.Actions.TryPlace(item, c, out _, def, ctx.BuildFacing)) placed++;
            Clear();
            if (placed > 0) ctx.Notify($"{placed} × {def.DisplayName.ToLowerInvariant()} posé(s).", EventSeverity.Success);
            if (item != null && ctx.Session.Inventory.Count(item) == 0) ctx.BuildItem = null;
            return placed;
        }
    }

    /// <summary>Mode suppression : le joueur marque des éléments (aperçu rouge), puis valide ou annule.
    /// Tant qu'il est actif, aucun autre panneau n'est accessible. Les trieurs (plaques) se marquent aussi.</summary>
    public sealed class DeleteMode
    {
        private readonly GameContext ctx;
        private readonly List<object> marks = new();

        public DeleteMode(GameContext ctx)
        {
            this.ctx = ctx;
            ctx.World.EntityRemoved += e =>
            {
                if (marks.RemoveAll(x => x == e || (x is FaceSorter s && s.Machine == e)) > 0) Changed?.Invoke();
            };
            ctx.Session.BoardReset += () => Exit();
        }

        public event Action Changed;
        public bool Active { get; private set; }
        /// <summary>Éléments marqués : entités de la grille ou trieurs.</summary>
        public IReadOnlyList<object> Marks => marks;
        /// <summary>Étage du premier élément marqué pendant un glisser.</summary>
        public int DragLayer { get; set; } = -1;

        public void Enter()
        {
            if (Active) return;
            ctx.ContextTarget = null;
            ctx.MovingMachine = null;
            ctx.BuildItem = null;
            ctx.Selection = null;
            Active = true;
            Changed?.Invoke();
        }

        public bool IsMarked(object e) => marks.Contains(e);

        public void Mark(object e, bool on)
        {
            if (e == null || on == marks.Contains(e)) return;
            if (on) marks.Add(e); else marks.Remove(e);
            Changed?.Invoke();
        }

        /// <summary>Ce que la suppression rendra à l'inventaire.</summary>
        public Dictionary<ResourceDef, int> Refund()
        {
            var r = new Dictionary<ResourceDef, int>();
            void Add(ResourceDef res, int n) { if (res != null && n > 0) r[res] = (r.TryGetValue(res, out var v) ? v : 0) + n; }
            foreach (var x in marks)
            {
                switch (x)
                {
                    case FaceSorter s:
                        if (!marks.Contains(s.Machine)) Add(ctx.Db.SorterItem, 1);
                        break;
                    case IGridEntity e:
                        Add(e.Def.Item, 1);
                        if (e is Machine m)
                        {
                            foreach (var st in m.SalvageableItems()) Add(st.Resource, st.Amount);
                            Add(ctx.Db.SorterItem, m.Sorters.Count());
                        }
                        break;
                }
            }
            return r;
        }

        public int Confirm()
        {
            int n = 0;
            string error = null;
            foreach (var x in marks.OfType<FaceSorter>().ToList())
                if (!marks.Contains(x.Machine) && ctx.Session.Actions.TryDetachSorter(x.Machine, x.Face, out error)) n++;
            foreach (var e in marks.OfType<IGridEntity>().ToList())
                if (ctx.Session.Actions.TryPickUp(e.Position, out var err)) n++;
                else error = err;
            if (n > 0) ctx.Notify($"{n} élément(s) démonté(s), rendus à l'inventaire.", EventSeverity.Success);
            if (error != null) ctx.Notify(error);
            Exit();
            return n;
        }

        public void Exit()
        {
            bool was = Active || marks.Count > 0;
            Active = false;
            marks.Clear();
            DragLayer = -1;
            if (was) Changed?.Invoke();
        }
    }

    /// <summary>Mode modification : on règle les faces des machines et les côtés des câbles directement dans l'atelier.</summary>
    public sealed class ConfigureMode
    {
        private readonly GameContext ctx;

        public ConfigureMode(GameContext ctx)
        {
            this.ctx = ctx;
            ctx.Session.BoardReset += Exit;
        }

        public event Action Changed;
        public bool Active { get; private set; }
        /// <summary>Dernier réglage appliqué (affiché en haut de l'écran).</summary>
        public string LastChange { get; private set; }

        public void Enter()
        {
            if (Active) return;
            ctx.ContextTarget = null;
            ctx.MovingMachine = null;
            ctx.BuildItem = null;
            ctx.Selection = null;
            LastChange = null;
            Active = true;
            Changed?.Invoke();
        }

        public void Exit()
        {
            if (!Active) return;
            Active = false;
            Changed?.Invoke();
        }

        public void Report(string change)
        {
            LastChange = change;
            Changed?.Invoke();
        }
    }
}
