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
    /// Rester appuyé et changer de case = pose multiple : un aperçu sur l'étage de la première case,
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
        private Vector2 pressScreen;
        private const float DragThreshold = 8f;
        private bool dragging, erasing;
        private IGridEntity lastDeleteTouched;
        private bool deleteErasing;

        public Func<bool> PointerOverUI { get; set; } = () => false;
        public IGridEntity HoveredEntity { get; private set; }
        public Vector3Int? TargetCell { get; private set; }

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
            TargetCell = null;
            var ray = rig.Camera.ScreenPointToRay(mouse.position.ReadValue());
            if (!overUI) Pick(ray);

            if (ctx.Deletion.Active) { UpdateDelete(mouse, overUI); return; }
            preview.SetDeleteHover(null);

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
                if (HoveredEntity == null) ctx.Selection = null;
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
            // Pendant une pose multiple, l'aperçu suit le curseur sur l'étage de la pose.
            Vector3Int? cell = TargetCell;
            if (ctx.Plan.Active || dragging) cell = LayerCell(rig.Camera.ScreenPointToRay(Mouse.current.position.ReadValue()), ctx.Plan.Active ? ctx.Plan.Layer : pressCell?.z ?? 0);
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
                    pressCell = LayerCell(ray, plan.Layer);
                    erasing = pressCell is { } p && plan.Contains(p);
                }
                else
                {
                    pressCell = TargetCell;
                    erasing = false;
                }
                lastPaint = pressCell;
                pressScreen = mouse.position.ReadValue();
                dragging = false;
            }
            if (pressCell is not { } start) return;

            int layer = plan.Active ? plan.Layer : start.z;
            if (mouse.leftButton.isPressed)
            {
                var cell = LayerCell(ray, layer);
                bool moved = dragging || Vector2.Distance(mouse.position.ReadValue(), pressScreen) > DragThreshold;
                if (moved && cell is { } c && c != lastPaint)
                {
                    if (!dragging)
                    {
                        dragging = true;
                        Paint(start);
                    }
                    foreach (var step in Line(lastPaint ?? start, c)) Paint(step);
                    lastPaint = c;
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (!dragging)
                {
                    if (plan.Active)
                    {
                        if (erasing) plan.Remove(start); else plan.Add(start);
                    }
                    else PlaceOne(start);
                }
                ResetPress();
            }
        }

        private void Paint(Vector3Int c)
        {
            if (erasing) ctx.Plan.Remove(c);
            else ctx.Plan.Add(c);
        }

        private void PlaceOne(Vector3Int cell)
        {
            if (ctx.Session.Actions.TryPlace(ctx.BuildItem, cell, out var error, ctx.BuildPlaceable, ctx.BuildFacing)) { }
            else ctx.Notify(error);
            if (ctx.Session.Inventory.Count(ctx.BuildItem) == 0) ctx.BuildItem = null;
        }

        /// <summary>Cases entre deux cases d'un même étage (sans trou, en escalier).</summary>
        private static IEnumerable<Vector3Int> Line(Vector3Int a, Vector3Int b)
        {
            int x = a.x, y = a.y;
            int dx = Math.Abs(b.x - a.x), dy = Math.Abs(b.y - a.y);
            int sx = Math.Sign(b.x - a.x), sy = Math.Sign(b.y - a.y);
            int err = dx - dy;
            while (x != b.x || y != b.y)
            {
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x += sx; }
                else { err += dx; y += sy; }
                yield return new Vector3Int(x, y, b.z);
            }
        }

        // ---------------- Suppression ----------------

        private void UpdateDelete(Mouse mouse, bool overUI)
        {
            var del = ctx.Deletion;
            preview.SetHover(null, Direction.North, null, false);
            view.SetHoverEntity(null);
            var hovered = HoveredEntity;
            if (hovered != null && del.DragLayer >= 0 && hovered.Position.z != del.DragLayer) hovered = null;
            preview.SetDeleteHover(hovered);

            if (!overUI && mouse.rightButton.wasPressedThisFrame && HoveredEntity != null) del.Mark(HoveredEntity, false);
            if (!overUI && mouse.leftButton.wasPressedThisFrame && HoveredEntity != null)
            {
                deleteErasing = del.IsMarked(HoveredEntity);
                del.DragLayer = HoveredEntity.Position.z;
                del.Mark(HoveredEntity, !deleteErasing);
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

            var entityRef = hit.collider.GetComponentInParent<EntityRef>();
            if (entityRef != null)
            {
                HoveredEntity = entityRef.Entity;
                if (GridMath.NormalToDirection(hit.normal, out var face))
                {
                    var cell = HoveredEntity.Position + face.Offset();
                    if (ctx.World.InBounds(cell)) TargetCell = cell;
                }
                return;
            }
            if (hit.collider.GetComponent<FloorMarker>() != null)
            {
                var cell = GridMath.WorldToCell(hit.point + Vector3.up * 0.01f);
                cell.z = 0;
                if (ctx.World.InBounds(cell)) TargetCell = cell;
            }
        }

        /// <summary>Case sous le curseur sur un étage donné (plan horizontal à la base de l'étage).</summary>
        private Vector3Int? LayerCell(Ray ray, int z)
        {
            float h = z * GridMath.LevelHeight;
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return null;
            float t = (h - ray.origin.y) / ray.direction.y;
            if (t <= 0f) return null;
            var p = ray.origin + ray.direction * t;
            var cell = new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z), z);
            return ctx.World.InBounds(cell) ? cell : null;
        }
    }
}
