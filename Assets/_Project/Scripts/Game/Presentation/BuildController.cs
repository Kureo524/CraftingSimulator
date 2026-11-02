using System;
using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nucleaire.Game
{
    /// <summary>Interaction souris avec l'atelier, par rayon sur les objets (pas sur la case au sol).
    /// Sélection : clic gauche sur une machine. Clic droit : menu contextuel (déplacer, supprimer).
    /// Pose : l'objet se place contre la face visée (au-dessus d'une machine pour construire en hauteur).</summary>
    public sealed class BuildController : MonoBehaviour
    {
        private GameContext ctx;
        private IsoCameraRig rig;
        private WorldView view;
        private Vector3Int? lastPlaced;

        public Func<bool> PointerOverUI { get; set; } = () => false;
        public IGridEntity HoveredEntity { get; private set; }
        public Vector3Int? TargetCell { get; private set; }

        public void Init(GameContext context, IsoCameraRig cameraRig, WorldView worldView)
        {
            ctx = context;
            rig = cameraRig;
            view = worldView;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || ctx == null) return;

            bool overUI = PointerOverUI();
            HoveredEntity = null;
            TargetCell = null;
            if (!overUI) Pick(mouse.position.ReadValue());

            bool placing = ctx.BuildItem != null || ctx.MovingMachine != null;
            bool valid = false;
            if (placing && TargetCell is { } t)
            {
                if (ctx.MovingMachine != null) valid = ctx.World.CanMove(ctx.MovingMachine.Position, t, out _);
                else valid = ctx.World.CanPlace(ctx.Db.PlaceableForItem(ctx.BuildItem), t, out _) && ctx.Session.Inventory.Count(ctx.BuildItem) > 0;
            }
            view.SetGhost(placing ? TargetCell : null, valid);
            view.SetHoverEntity(placing ? null : HoveredEntity);

            if (!mouse.leftButton.isPressed) lastPlaced = null;
            if (overUI) return;

            bool left = mouse.leftButton.wasPressedThisFrame, right = mouse.rightButton.wasPressedThisFrame;

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

            if (ctx.BuildItem != null)
            {
                if (right) { ctx.BuildItem = null; return; }
                if (TargetCell is not { } target) return;
                bool isTransmitter = ctx.Db.PlaceableForItem(ctx.BuildItem) is TransmitterDef;
                bool click = left || (isTransmitter && mouse.leftButton.isPressed && lastPlaced != target && lastPlaced != null);
                if (!click) return;
                if (ctx.Session.Actions.TryPlace(ctx.BuildItem, target, out var error)) lastPlaced = target;
                else if (left) ctx.Notify(error);
                if (ctx.Session.Inventory.Count(ctx.BuildItem) == 0) ctx.BuildItem = null;
                return;
            }

            if (left)
            {
                if (HoveredEntity == null) ctx.Selection = null;
                else if (ctx.Require(PlayerPermission.Inspect)) ctx.Selection = HoveredEntity;
            }
            if (right && HoveredEntity != null) ctx.ContextTarget = HoveredEntity;
        }

        /// <summary>Rayon depuis la souris : entité touchée, et case de pose contre la face touchée.</summary>
        private void Pick(Vector2 screen)
        {
            var ray = rig.Camera.ScreenPointToRay(screen);
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
    }
}
