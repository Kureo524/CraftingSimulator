using System;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nucleaire.Game
{
    /// <summary>Interaction souris avec l'atelier.
    /// Mode sélection : clic gauche = inspecter, clic droit = démonter.
    /// Mode pose (objet choisi dans l'inventaire) : clic gauche = poser (glisser pour les tuyaux), clic droit / Échap = annuler.</summary>
    public sealed class BuildController : MonoBehaviour
    {
        private GameContext ctx;
        private IsoCameraRig rig;
        private WorldView view;
        private Vector2Int? lastPlaced;

        public Func<bool> PointerOverUI { get; set; } = () => false;
        public Vector2Int? HoveredCell { get; private set; }

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

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && ctx.BuildItem != null)
                ctx.BuildItem = null;

            bool overUI = PointerOverUI();
            HoveredCell = null;
            if (!overUI && rig.ScreenToCell(mouse.position.ReadValue(), out var cell, out _) && ctx.World.InBounds(cell))
                HoveredCell = cell;

            bool building = ctx.BuildItem != null;
            bool valid = false;
            if (HoveredCell is { } c && building)
            {
                var def = ctx.Db.PlaceableForItem(ctx.BuildItem);
                valid = ctx.World.CanPlace(def, c, out _) && ctx.Session.Inventory.Count(ctx.BuildItem) > 0;
            }
            view.SetHover(HoveredCell, valid, building);

            if (!mouse.leftButton.isPressed) lastPlaced = null;
            if (overUI || HoveredCell is not { } target) return;

            if (building)
            {
                // Clic, ou glisser pour poser une ligne de transmetteurs.
                bool isTransmitter = ctx.Db.PlaceableForItem(ctx.BuildItem) is Data.TransmitterDef;
                bool click = mouse.leftButton.wasPressedThisFrame || (isTransmitter && mouse.leftButton.isPressed && lastPlaced != target);
                if (click && ctx.World.IsFree(target))
                {
                    if (ctx.Session.Actions.TryPlace(ctx.BuildItem, target, out var error)) lastPlaced = target;
                    else ctx.Notify(error);
                    if (ctx.Session.Inventory.Count(ctx.BuildItem) == 0) ctx.BuildItem = null;
                }
                else if (mouse.leftButton.wasPressedThisFrame && !ctx.World.IsFree(target))
                    ctx.Notify("Case déjà occupée.");
                if (mouse.rightButton.wasPressedThisFrame) ctx.BuildItem = null;
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                var clicked = ctx.World.At(target);
                if (clicked == null) ctx.Selection = null;
                else if (ctx.Require(Data.PlayerPermission.Inspect)) ctx.Selection = clicked;
            }

            if (mouse.rightButton.wasPressedThisFrame && ctx.World.At(target) is { } entity)
            {
                if (ctx.Session.Actions.TryPickUp(target, out var error))
                    ctx.Notify($"{entity.Def.DisplayName} démonté et rangé dans l'inventaire.", EventSeverity.Info);
                else ctx.Notify(error);
            }
        }
    }
}
