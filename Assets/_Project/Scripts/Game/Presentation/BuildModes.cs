using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;

namespace Nucleaire.Game
{
    /// <summary>Pose multiple : les cases prévues (toutes sur le même étage), affichées en aperçu,
    /// posées seulement quand le joueur valide.</summary>
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
        /// <summary>Étage de la pose multiple (celui de la première case).</summary>
        public int Layer { get; private set; } = -1;

        public bool Contains(Vector3Int c) => cells.Contains(c);

        public bool Add(Vector3Int c)
        {
            if (cells.Count == 0) Layer = c.z;
            if (c.z != Layer || cells.Contains(c) || !ctx.World.InBounds(c)) return false;
            cells.Add(c);
            Changed?.Invoke();
            return true;
        }

        public bool Remove(Vector3Int c)
        {
            if (!cells.Remove(c)) return false;
            if (cells.Count == 0) Layer = -1;
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            if (cells.Count == 0) return;
            cells.Clear();
            Layer = -1;
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
    /// Tant qu'il est actif, aucun autre panneau n'est accessible.</summary>
    public sealed class DeleteMode
    {
        private readonly GameContext ctx;
        private readonly List<IGridEntity> marks = new();

        public DeleteMode(GameContext ctx)
        {
            this.ctx = ctx;
            ctx.World.EntityRemoved += e => { if (marks.Remove(e)) Changed?.Invoke(); };
            ctx.Session.BoardReset += () => Exit();
        }

        public event Action Changed;
        public bool Active { get; private set; }
        public IReadOnlyList<IGridEntity> Marks => marks;
        /// <summary>Étage du premier élément marqué pendant un glisser.</summary>
        public int DragLayer { get; set; } = -1;

        public bool Toggle()
        {
            if (Active) { Exit(); return true; }
            if (!ctx.Require(PlayerPermission.PickUp)) return false;
            ctx.ContextTarget = null;
            ctx.MovingMachine = null;
            ctx.BuildItem = null;
            ctx.Selection = null;
            Active = true;
            Changed?.Invoke();
            return true;
        }

        public bool IsMarked(IGridEntity e) => marks.Contains(e);

        public void Mark(IGridEntity e, bool on)
        {
            if (e == null || on == marks.Contains(e)) return;
            if (on) marks.Add(e); else marks.Remove(e);
            Changed?.Invoke();
        }

        /// <summary>Ce que la suppression rendra à l'inventaire (les machines et leurs objets).</summary>
        public Dictionary<ResourceDef, int> Refund()
        {
            var r = new Dictionary<ResourceDef, int>();
            void Add(ResourceDef res, int n) { if (res != null && n > 0) r[res] = (r.TryGetValue(res, out var v) ? v : 0) + n; }
            foreach (var e in marks)
            {
                Add(e.Def.Item, 1);
                if (e is Machine m) foreach (var s in m.SalvageableItems()) Add(s.Resource, s.Amount);
            }
            return r;
        }

        public int Confirm()
        {
            int n = 0;
            foreach (var e in marks.ToList())
                if (ctx.Session.Actions.TryPickUp(e.Position, out _)) n++;
            if (n > 0) ctx.Notify($"{n} élément(s) démonté(s), rendus à l'inventaire.", EventSeverity.Success);
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
}
