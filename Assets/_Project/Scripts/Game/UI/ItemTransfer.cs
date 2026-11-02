using System;
using System.Runtime.CompilerServices;
using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Ce qu'on déplace : une ressource, une quantité, et d'où elle vient (null = inventaire du joueur).</summary>
    public sealed class Payload
    {
        public ResourceDef Resource;
        public int Amount;
        public Machine From;
        /// <summary>Emplacement d'inventaire d'où vient la pile (-1 : machine).</summary>
        public int Slot = -1;

        public Payload(ResourceDef r, int amount, Machine from = null, int slot = -1) { Resource = r; Amount = amount; From = from; Slot = slot; }
        public Payload With(int amount) => new(Resource, amount, From, Slot);
    }

    /// <summary>Déplacement d'objets entre l'inventaire et les machines, comme dans Minecraft :
    /// <list type="bullet">
    /// <item>glisser-déposer une case vers une autre zone ;</item>
    /// <item>ou cliquer une case de l'inventaire (elle suit le curseur), puis cliquer la machine :
    /// clic gauche = tout, clic droit = une unité (1 objet ou 1 B de fluide) ;</item>
    /// <item>Échap ou un clic ailleurs annule.</item>
    /// </list>
    /// Les sources et cibles s'enregistrent sur des éléments ; tout est traité ici, au niveau du curseur.</summary>
    public sealed class ItemTransfer
    {
        private sealed class Source
        {
            public Func<Payload> Payload;
            /// <summary>Clic simple sans glisser. Renvoie vrai s'il a été traité (sinon l'objet est « pris en main »).</summary>
            public Func<bool, bool> Click;
        }

        private static readonly ConditionalWeakTable<VisualElement, Source> Sources = new();
        private sealed class Target
        {
            public Func<Payload, bool> Accepts;
            public Func<Payload, int> Drop;
        }

        private static readonly ConditionalWeakTable<VisualElement, Target> Targets = new();
        private const float DragThreshold = 6f;

        private readonly VisualElement root, ghost;
        private readonly ResourceIcon ghostIcon;
        private readonly Label ghostCount;
        private readonly GameContext ctx;

        private Source press;
        private Payload pressPayload;
        private Vector2 pressPos;
        private bool dragging;

        public ItemTransfer(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            this.root = root;
            var layer = root.Q("drag-layer");
            ghost = Ui.El("drag-ghost hidden", layer);
            ghost.pickingMode = PickingMode.Ignore;
            ghostIcon = new ResourceIcon();
            ghost.Add(ghostIcon);
            ghostCount = Ui.Text("", "rslot__count", ghost);
            ghostCount.pickingMode = PickingMode.Ignore;
            ctx.Session.BoardReset += () => Cancel();
        }

        /// <summary>Objet « en main » (cliqué dans l'inventaire, en attente d'être posé).</summary>
        public Payload Held { get; private set; }
        public bool Busy => Held != null || dragging;

        public static void RegisterSource(VisualElement e, Func<Payload> payload, Func<bool, bool> click = null) =>
            Sources.AddOrUpdate(e, new Source { Payload = payload, Click = click });

        /// <summary>Zone qui reçoit ce qu'on y lâche. <paramref name="accepts"/> dit si elle est concernée (sinon on cherche plus haut).</summary>
        public static void RegisterTarget(VisualElement e, Func<Payload, bool> accepts, Func<Payload, int> drop) =>
            Targets.AddOrUpdate(e, new Target { Accepts = accepts, Drop = drop });

        public bool Cancel()
        {
            bool had = Held != null || press != null;
            Held = null;
            press = null;
            dragging = false;
            Ui.SetVisible(ghost, false);
            return had;
        }

        public void Update()
        {
            var panel = root.panel;
            var mouse = Mouse.current;
            if (panel == null || mouse == null) return;
            var screen = mouse.position.ReadValue();
            var pos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            bool shift = Keyboard.current?.shiftKey.isPressed == true;
            bool leftDown = mouse.leftButton.wasPressedThisFrame, rightDown = mouse.rightButton.wasPressedThisFrame;

            bool wasHeld = Held != null;
            if (Held != null && (leftDown || rightDown))
            {
                var el = panel.Pick(pos);
                var target = FindTarget(el, Held);
                if (target != null)
                {
                    int n = rightDown ? One(Held.Resource) : Held.Amount;
                    int moved = target.Drop(Held.With(Mathf.Min(n, Held.Amount)));
                    Held.Amount -= moved;
                    if (Held != null && Held.Amount <= 0) Held = null;
                    leftDown = false;
                }
                else
                {
                    Held = null;
                    if (rightDown) leftDown = false;
                }
            }

            // Clic droit sur une case, mains vides : prendre la moitié du stack (comme dans Minecraft).
            if (rightDown && !wasHeld && press == null)
            {
                var (s, _) = FindSource(panel.Pick(pos));
                var payload = s?.Payload();
                if (payload != null && payload.Amount > 0)
                {
                    int half = payload.Resource.Kind == ResourceKind.Item
                        ? (payload.Amount + 1) / 2
                        : Mathf.Max(1, payload.Amount / 2);
                    Held = payload.With(half);
                }
            }

            if (leftDown)
            {
                var el = panel.Pick(pos);
                var (s, _) = FindSource(el);
                var payload = s?.Payload();
                if (payload != null && payload.Amount > 0)
                {
                    press = s;
                    pressPayload = payload;
                    pressPos = pos;
                    dragging = false;
                }
            }

            if (press != null)
            {
                if (!dragging && Vector2.Distance(pos, pressPos) > DragThreshold) dragging = true;
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    if (dragging)
                    {
                        var target = FindTarget(panel.Pick(pos), pressPayload);
                        target?.Drop(pressPayload);
                    }
                    else if (press.Click == null || !press.Click(shift)) Held = pressPayload;
                    press = null;
                    dragging = false;
                }
            }

            if (Held != null && Held.From == null)
            {
                // La pile tenue reste dans son emplacement tant qu'elle n'est pas posée : on suit ce qu'il en reste.
                var slots = ctx.Session.Inventory.Slots;
                bool same = Held.Slot >= 0 && Held.Slot < slots.Count && slots[Held.Slot].Resource == Held.Resource;
                Held.Amount = same ? Mathf.Min(Held.Amount, slots[Held.Slot].Amount) : 0;
            }
            if (Held != null && Held.Amount <= 0) Held = null;

            var shown = dragging ? pressPayload : Held;
            Ui.SetVisible(ghost, shown != null);
            if (shown != null)
            {
                ghostIcon.Resource = shown.Resource;
                ghostIcon.Canister = shown.Resource.Kind is ResourceKind.Fluid or ResourceKind.Gas;
                ghostIcon.Level = ghostIcon.Canister ? (float)shown.Amount / ctx.Session.Inventory.CanisterCapacity : 1f;
                ghostCount.text = Ui.Amount(shown.Resource, shown.Amount).TrimStart('×');
                ghost.style.left = pos.x - 22f;
                ghost.style.top = pos.y - 22f;
            }
        }

        private static int One(ResourceDef r) => r.Kind == ResourceKind.Item ? 1 : 1000;

        private static (Source, VisualElement) FindSource(VisualElement e)
        {
            for (; e != null; e = e.parent)
                if (Sources.TryGetValue(e, out var s)) return (s, e);
            return (null, null);
        }

        private static Target FindTarget(VisualElement e, Payload p)
        {
            for (; e != null; e = e.parent)
                if (Targets.TryGetValue(e, out var t) && t.Accepts(p)) return t;
            return null;
        }
    }
}
