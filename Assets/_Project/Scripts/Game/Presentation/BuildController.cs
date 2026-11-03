using System;
using System.Collections.Generic;
using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nucleaire.Game
{
    /// <summary>Interaction souris avec l'atelier, par rayon sur les objets (pas sur la case au sol).
    /// <list type="bullet">
    /// <item>Sans objet équipé : clic gauche = ouvrir une machine, clic droit = menu contextuel.</item>
    /// <item>Objet équipé : clic = poser contre la face visée ; R = tourner la machine.
    /// Rester appuyé et changer de case = pose multiple : un aperçu dans le plan de la face du premier clic
    /// (sol ou dessus d'un bloc : un étage ; côté d'un bloc : un mur vertical),
    /// à compléter (clic ou glisser sur une case vide) ou à retoucher (clic ou glisser sur une case prévue), puis à valider.</item>
    /// <item>Mode suppression (A) : clic ou glisser pour marquer / démarquer, puis valider.</item>
    /// </list></summary>
    public sealed class BuildController : MonoBehaviour
    {
        private GameContext ctx;
        private IsoCameraRig rig;
        private WorldView view;
        private BuildPreview preview;

        private Vector3Int? pressCell, lastPaint;
        private Direction pressFace = Direction.Up;
        private Vector2 pressScreen;
        private const float DragThreshold = 8f;
        private bool dragging, erasing;
        private IGridEntity lastDeleteTouched;
        private bool deleteErasing;

        public Func<bool> PointerOverUI { get; set; } = () => false;
        /// <summary>Position écran imposée (captures de vérification) à la place de la souris.</summary>
        public static Vector2? DebugPointer;
        private static Vector2 Pointer(Mouse m) => DebugPointer ?? m.position.ReadValue();
        public IGridEntity HoveredEntity { get; private set; }
        public Vector3Int? TargetCell { get; private set; }
        /// <summary>Face par laquelle on pose sur <see cref="TargetCell"/> (de la case d'appui vers elle ; le sol donne Haut).</summary>
        public Direction TargetFace { get; private set; } = Direction.Up;
        /// <summary>Trieur (plaque) sous le curseur.</summary>
        public FaceSorter HoveredSorter { get; private set; }
        /// <summary>Face de l'entité visée (machine : face touchée ; câble : côté le plus proche du point touché).</summary>
        public Direction? HoveredFace { get; private set; }

        public void Init(GameContext context, IsoCameraRig cameraRig, WorldView worldView)
        {
            ctx = context;
            rig = cameraRig;
            view = worldView;
            preview = worldView.gameObject.AddComponent<BuildPreview>();
            preview.Init(ctx);
            ctx.BuildItemChanged += _ => ResetPress();
        }

        private void ResetPress()
        {
            pressCell = lastPaint = null;
            dragging = erasing = false;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || ctx == null) return;

            bool overUI = PointerOverUI();
            HoveredEntity = null;
            HoveredSorter = null;
            HoveredFace = null;
            TargetCell = null;
            TargetFace = Direction.Up;
            var ray = rig.Camera.ScreenPointToRay(Pointer(mouse));
            if (!overUI) Pick(ray);

            ctx.Hover = (HoveredEntity, HoveredFace, HoveredSorter);
            if (ctx.Deletion.Active) { preview.SetFaceHighlight(null, Direction.North); UpdateDelete(mouse, overUI); return; }
            preview.SetDeleteHover(null);
            if (ctx.Configure.Active) { UpdateConfigure(mouse, overUI); return; }
            preview.SetFaceHighlight(null, Direction.North);
            bool sorterEquipped = ctx.BuildItem != null && ctx.BuildItem == ctx.Db.SorterItem;
            if (sorterEquipped) { UpdateSorterPlacement(mouse, overUI); return; }
            preview.SetSorterGhost(null, null, false);

            if (ctx.BuildItem != null && ctx.BuildPlaceable is MachineDef && ctx.Input.Pressed(GameInput.RotatePiece))
                ctx.BuildFacing = (Direction)(((int)ctx.BuildFacing + 1) % 4);

            UpdatePreview();
            view.SetHoverEntity(ctx.BuildItem != null || ctx.MovingMachine != null ? null : HoveredEntity);

            bool left = mouse.leftButton.wasPressedThisFrame, right = mouse.rightButton.wasPressedThisFrame;
            if (overUI && pressCell == null) return;

            // Un clic dans le monde ferme d'abord le menu contextuel.
            if (ctx.ContextTarget != null && (left || right)) { ctx.ContextTarget = null; return; }

            if (ctx.MovingMachine != null)
            {
                if (right) { ctx.MovingMachine = null; return; }
                if (left && TargetCell is { } to)
                {
                    var m = ctx.MovingMachine;
                    if (ctx.Session.Actions.TryMove(m.Position, to, out var err)) { ctx.MovingMachine = null; ctx.Selection = m; }
                    else ctx.Notify(err);
                }
                return;
            }

            if (ctx.BuildItem != null) { UpdateBuild(mouse, ray, left, right); return; }

            if (left)
            {
                if (HoveredSorter != null)
                {
                    // Clic sur un trieur : ouvre sa machine, onglet Trieurs.
                    if (ctx.Require(PlayerPermission.Inspect)) { ctx.FocusSorterFace = HoveredSorter.Face; ctx.Selection = null; ctx.Selection = HoveredSorter.Machine; }
                }
                else if (HoveredEntity == null) ctx.Selection = null;
                else if (ctx.Require(PlayerPermission.Inspect)) ctx.Selection = HoveredEntity;
            }
            if (right && HoveredEntity != null) ctx.ContextTarget = HoveredEntity;
        }

        private void UpdatePreview()
        {
            if (ctx.MovingMachine is { } moving)
            {
                bool ok = TargetCell is { } t && ctx.World.CanMove(moving.Position, t, out _);
                preview.SetHover(moving.MachineDef, moving.Facing, TargetCell, ok);
                return;
            }
            // Pendant une pose multiple, l'aperçu suit le curseur dans le plan de la pose.
            Vector3Int? cell = TargetCell;
            if (ctx.Plan.Active || dragging) cell = DragCell(rig.Camera.ScreenPointToRay(Pointer(Mouse.current)));
            if (cell is { } c && ctx.Plan.Contains(c)) cell = null;
            bool valid = cell is { } v && ctx.World.CanPlace(ctx.BuildPlaceable, v, out _) && ctx.Session.Inventory.Count(ctx.BuildItem) > 0;
            preview.SetHover(ctx.BuildItem != null && !PointerOverUI() ? ctx.BuildPlaceable : null, ctx.BuildFacing, cell, valid);
        }

        // ---------------- Pose ----------------

        private void UpdateBuild(Mouse mouse, Ray ray, bool left, bool right)
        {
            var plan = ctx.Plan;
            if (right)
            {
                if (plan.Active) plan.Clear(); else ctx.BuildItem = null;
                ResetPress();
                return;
            }

            if (left && !PointerOverUI())
            {
                if (plan.Active)
                {
                    pressCell = PlaneCell(ray, plan.Origin, plan.Face);
                    erasing = pressCell is { } p && plan.Contains(p);
                }
                else
                {
                    pressCell = TargetCell;
                    pressFace = TargetFace;
                    erasing = false;
                }
                lastPaint = pressCell;
                pressScreen = Pointer(mouse);
                dragging = false;
            }
            if (pressCell is not { } start) return;

            int fixedAxis = BuildPlan.AxisOf(plan.Active ? plan.Face : pressFace);
            if (mouse.leftButton.isPressed)
            {
                var cell = DragCell(ray);
                bool moved = dragging || Vector2.Distance(Pointer(mouse), pressScreen) > DragThreshold;
                if (moved && cell is { } c && c != lastPaint)
                {
                    if (!dragging)
                    {
                        dragging = true;
                        Paint(start);
                    }
                    foreach (var step in Line(lastPaint ?? start, c, fixedAxis)) Paint(step);
                    lastPaint = c;
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (!dragging)
                {
                    if (plan.Active)
                    {
                        if (erasing) plan.Remove(start); else plan.Add(start, pressFace);
                    }
                    else PlaceOne(start);
                }
                ResetPress();
            }
        }

        private void Paint(Vector3Int c)
        {
            if (erasing) ctx.Plan.Remove(c);
            else ctx.Plan.Add(c, pressFace);
        }

        private void PlaceOne(Vector3Int cell)
        {
            if (ctx.Session.Actions.TryPlace(ctx.BuildItem, cell, out var error, ctx.BuildPlaceable, ctx.BuildFacing)) { }
            else ctx.Notify(error);
            if (ctx.Session.Inventory.Count(ctx.BuildItem) == 0) ctx.BuildItem = null;
        }

        /// <summary>Cases entre deux cases d'un même plan (sans trou, en escalier). <paramref name="fixedAxis"/> : l'axe fixé par le plan.</summary>
        private static IEnumerable<Vector3Int> Line(Vector3Int a, Vector3Int b, int fixedAxis)
        {
            int u = fixedAxis == 0 ? 1 : 0, v = fixedAxis == 2 ? 1 : 2;
            var p = a;
            int du = Math.Abs(b[u] - a[u]), dv = Math.Abs(b[v] - a[v]);
            int su = Math.Sign(b[u] - a[u]), sv = Math.Sign(b[v] - a[v]);
            int err = du - dv;
            while (p[u] != b[u] || p[v] != b[v])
            {
                int e2 = 2 * err;
                if (e2 > -dv) { err -= dv; p[u] += su; }
                else { err += du; p[v] += sv; }
                yield return p;
            }
        }

        // ---------------- Trieur (plaque sur une face) ----------------

        private void UpdateSorterPlacement(Mouse mouse, bool overUI)
        {
            preview.SetHover(null, Direction.North, null, false);
            view.SetHoverEntity(null);
            var m = HoveredEntity as Machine;
            Direction? face = m != null ? HoveredFace : null;
            bool valid = m != null && face != null && m.SorterAt(face.Value) == null && ctx.Session.Inventory.Count(ctx.BuildItem) > 0;
            preview.SetSorterGhost(overUI ? null : m, face, valid);
            if (overUI) return;
            if (mouse.rightButton.wasPressedThisFrame) { ctx.BuildItem = null; preview.SetSorterGhost(null, null, false); return; }
            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (m == null || face == null) { ctx.Notify("Vise une face de machine ou de vault pour y coller le trieur."); return; }
            if (!ctx.Session.Actions.TryAttachSorter(m, face.Value, out var err)) ctx.Notify(err);
            else
            {
                preview.SetSorterGhost(null, null, false);
                ctx.Notify($"Trieur collé sur la face {face.Value.DisplayName().ToLowerInvariant()} : ouvre la machine pour choisir ce qu'il laisse passer.", EventSeverity.Info);
                if (ctx.Session.Inventory.Count(ctx.BuildItem) == 0) ctx.BuildItem = null;
            }
        }

        // ---------------- Modification ----------------

        private void UpdateConfigure(Mouse mouse, bool overUI)
        {
            preview.SetHover(null, Direction.North, null, false);
            preview.SetSorterGhost(null, null, false);
            view.SetHoverEntity(null);
            var e = HoveredEntity;
            var face = HoveredFace;
            preview.SetFaceHighlight(!overUI && e != null && face != null ? e.Position : null, face ?? Direction.North, e is Logistics.TransmitterSegment);
            if (overUI || e == null || face is not { } d) return;
            int step = mouse.leftButton.wasPressedThisFrame ? 1 : mouse.rightButton.wasPressedThisFrame ? -1 : 0;
            if (step == 0) return;
            if (!ctx.Require(PlayerPermission.Sides)) return;
            switch (e)
            {
                case Machine m:
                    if (m.SorterAt(d) != null) { ctx.Configure.Report($"{m.MachineDef.DisplayName} · face {d.DisplayName().ToLowerInvariant()} : un trieur l'occupe."); return; }
                    m.CycleSide(d, step);
                    ctx.Configure.Report($"{m.MachineDef.DisplayName} · face {d.DisplayName().ToLowerInvariant()} : {m.SideLabel(d)}");
                    break;
                case Logistics.TransmitterSegment seg:
                    var result = ctx.World.Logistics.CycleCableSide(seg, d, step);
                    ctx.Configure.Report(result == null
                        ? "Les réglages Tirer / Pousser se font sur un côté de câble collé à une machine ; entre deux câbles, on coupe ou on relie."
                        : $"{seg.TransmitterDef.DisplayName} · côté {d.DisplayName().ToLowerInvariant()} : {result}");
                    break;
            }
        }

        // ---------------- Suppression ----------------

        private void UpdateDelete(Mouse mouse, bool overUI)
        {
            var del = ctx.Deletion;
            preview.SetHover(null, Direction.North, null, false);
            view.SetHoverEntity(null);
            // Un trieur visé se marque seul (sans sa machine).
            object target = HoveredSorter != null ? HoveredSorter : HoveredEntity;
            var hovered = HoveredEntity;
            if (hovered != null && del.DragLayer >= 0 && hovered.Position.z != del.DragLayer) hovered = null;
            preview.SetDeleteHover(overUI ? null : HoveredSorter != null ? HoveredSorter : hovered);

            if (!overUI && mouse.rightButton.wasPressedThisFrame && target != null) del.Mark(target, false);
            if (!overUI && mouse.leftButton.wasPressedThisFrame && target != null)
            {
                deleteErasing = del.IsMarked(target);
                del.Mark(target, !deleteErasing);
                if (HoveredSorter != null) return;
                del.DragLayer = HoveredEntity.Position.z;
                lastDeleteTouched = HoveredEntity;
            }
            else if (mouse.leftButton.isPressed && del.DragLayer >= 0 && hovered != null && hovered != lastDeleteTouched)
            {
                del.Mark(hovered, !deleteErasing);
                lastDeleteTouched = hovered;
            }
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                del.DragLayer = -1;
                lastDeleteTouched = null;
            }
        }

        // ---------------- Rayons ----------------

        /// <summary>Rayon depuis la souris : entité touchée, et case de pose contre la face touchée.</summary>
        private void Pick(Ray ray)
        {
            if (!Physics.Raycast(ray, out var hit, 500f)) return;

            var sorterRef = hit.collider.GetComponent<SorterRef>();
            if (sorterRef != null)
            {
                HoveredSorter = sorterRef.Sorter;
                HoveredEntity = sorterRef.Sorter.Machine;
                HoveredFace = sorterRef.Sorter.Face;
                return;
            }
            var entityRef = hit.collider.GetComponentInParent<EntityRef>();
            if (entityRef != null)
            {
                HoveredEntity = entityRef.Entity;
                if (GridMath.NormalToDirection(hit.normal, out var face))
                {
                    HoveredFace = face;
                    var cell = HoveredEntity.Position + face.Offset();
                    if (ctx.World.InBounds(cell)) { TargetCell = cell; TargetFace = face; }
                }
                if (HoveredEntity is Logistics.TransmitterSegment seg) HoveredFace = CableSideAt(seg, hit);
                return;
            }
            if (hit.collider.GetComponent<FloorMarker>() != null)
            {
                var cell = GridMath.WorldToCell(hit.point + Vector3.up * 0.01f);
                cell.z = 0;
                if (ctx.World.InBounds(cell)) TargetCell = cell;
            }
        }

        /// <summary>Côté de câble visé : le plus proche du point touché (haut / bas si on vise le dessus près du centre).</summary>
        private static Direction CableSideAt(Logistics.TransmitterSegment seg, RaycastHit hit)
        {
            var c = GridMath.CellCenter(seg.Position);
            var local = hit.point - c;
            var flat = new Vector3(local.x, 0f, local.z);
            if (flat.magnitude < 0.16f && Mathf.Abs(hit.normal.y) > 0.7f) return hit.normal.y > 0 ? Direction.Up : Direction.Down;
            GridMath.NormalToDirection(flat.sqrMagnitude < 1e-6f ? Vector3.forward : flat, out var d);
            return d;
        }

        /// <summary>Case visée pendant une pose multiple : dans le plan de la pose en cours, sinon celui du premier clic.</summary>
        private Vector3Int? DragCell(Ray ray)
        {
            if (ctx.Plan.Active) return PlaneCell(ray, ctx.Plan.Origin, ctx.Plan.Face);
            return pressCell is { } start ? PlaneCell(ray, start, pressFace) : null;
        }

        /// <summary>Case sous le curseur dans le plan de pose passant par <paramref name="origin"/> :
        /// le rayon coupe la face d'appui du premier clic (sol, dessus ou côté d'un bloc), prolongée.</summary>
        private Vector3Int? PlaneCell(Ray ray, Vector3Int origin, Direction face)
        {
            int axis = BuildPlan.AxisOf(face);
            var o = face.Offset();
            // Face d'appui : côté « bas » de la case posée si on pose vers le +, côté « haut » sinon.
            float bound = origin[axis] + (o.x + o.y + o.z > 0 ? 0 : 1);
            // Axe de grille → axe monde (x → x, y → z, étage → y).
            int worldAxis = axis switch { 0 => 0, 1 => 2, _ => 1 };
            if (axis == 2) bound *= GridMath.LevelHeight;
            float dir = ray.direction[worldAxis];
            if (Mathf.Abs(dir) < 1e-4f) return null;
            float t = (bound - ray.origin[worldAxis]) / dir;
            if (t <= 0f) return null;
            var cell = GridMath.WorldToCell(ray.origin + ray.direction * t);
            cell[axis] = origin[axis];
            return ctx.World.InBounds(cell) ? cell : null;
        }
    }
}
