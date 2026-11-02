using System.Linq;
using Nucleaire.Data;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Panneaux de validation en haut de l'écran :
    /// pose multiple (ressources consommées / possédées) et mode suppression (ce qui sera rendu).</summary>
    public sealed class ModePanels
    {
        private readonly GameContext ctx;
        private readonly VisualElement root, plan, del;

        public ModePanels(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            this.root = root.Q("root") ?? root;
            plan = root.Q("plan-panel");
            del = root.Q("delete-panel");
            ctx.Plan.Changed += RefreshPlan;
            ctx.BuildItemChanged += _ => RefreshPlan();
            ctx.CableKindChanged += _ => RefreshPlan();
            ctx.Session.Inventory.Changed += () => { RefreshPlan(); RefreshDelete(); };
            ctx.Deletion.Changed += RefreshDelete;
            ctx.Input.Changed += () => { RefreshPlan(); RefreshDelete(); };
            RefreshPlan();
            RefreshDelete();
        }

        private void RefreshPlan()
        {
            plan.Clear();
            var p = ctx.Plan;
            bool show = p.Active && ctx.BuildItem != null;
            Ui.SetVisible(plan, show);
            if (!show) return;

            var item = ctx.BuildItem;
            int need = p.ValidCount, have = p.Available, ignored = p.Cells.Count - need;
            var head = Ui.El("row mode-panel__head", plan);
            Ui.Text($"Pose multiple · étage {p.Layer + 1}", "bold grow", head);
            Ui.Text($"{p.Cells.Count} case(s)", "meta", head);

            var line = Ui.El("row mode-panel__line", plan);
            IconSlot.Create(item, line, classes: "rslot--small");
            Ui.Text($"{need} × {ctx.BuildPlaceable?.DisplayName ?? item.DisplayName}", "grow", line);
            Ui.Text($"tu en as {have}", "mono " + (need <= have ? "mode-panel__ok" : "mode-panel__missing"), line);
            if (need > have) Ui.Text($"[Il en manque {need - have}.]", "system mode-panel__missing", plan);
            if (ignored > 0) Ui.Text($"[{ignored} case(s) occupée(s) : ignorée(s).]", "system", plan);
            Ui.Text($"[Clic ou glisser : ajouter des cases · sur une case prévue : en retirer · {ctx.Input.Display(GameInput.RotatePiece)} : tourner · clic droit : annuler.]", "system mode-panel__hint", plan);

            var actions = Ui.El("row mode-panel__actions", plan);
            Ui.El("spacer", actions);
            Ui.Button("Annuler", p.Clear, "btn btn--ghost", actions);
            var ok = Ui.Button("Valider", () => p.Confirm(), "btn btn--primary", actions);
            ok.style.marginLeft = 6;
            ok.SetEnabled(p.CanConfirm);
        }

        private void RefreshDelete()
        {
            var d = ctx.Deletion;
            root.EnableInClassList("delete-mode", d.Active);
            del.Clear();
            Ui.SetVisible(del, d.Active);
            if (!d.Active) return;

            var head = Ui.El("row mode-panel__head", del);
            Ui.Text("Mode suppression", "bold grow mode-panel__danger", head);
            Ui.Text($"{d.Marks.Count} élément(s) marqué(s)", "meta", head);

            var refund = d.Refund();
            if (refund.Count > 0)
            {
                Ui.Text("Rendu à l'inventaire :", "meta mode-panel__label", del);
                var row = Ui.El("row mode-panel__refund", del);
                foreach (var (r, n) in refund.OrderByDescending(kv => ctx.Db.PlaceableForItem(kv.Key) != null).ThenBy(kv => kv.Key.DisplayName).Take(12))
                    IconSlot.Create(r, row, r.Kind == ResourceKind.Item ? n.ToString() : null, "rslot--small", () => Ui.Amount(r, n));
                if (d.Marks.Any(e => e is Simulation.Machine m && (m.Inputs?.Slots.Any(s => s.Kind != ResourceKind.Item && s.Amount > 0) == true || m.Outputs?.Slots.Any(s => s.Kind != ResourceKind.Item && s.Amount > 0) == true)))
                    Ui.Text("[Les liquides et gaz contenus seront perdus.]", "system mode-panel__missing", del);
            }
            Ui.Text($"[Clic ou glisser : marquer · sur un élément marqué : le démarquer · clic droit : démarquer · {ctx.Input.Display(GameInput.DeleteToggle)} ou Échap : quitter.]", "system mode-panel__hint", del);

            var actions = Ui.El("row mode-panel__actions", del);
            Ui.El("spacer", actions);
            Ui.Button("Annuler", d.Exit, "btn btn--ghost", actions);
            var ok = Ui.Button($"Supprimer ({d.Marks.Count})", () => d.Confirm(), "btn btn--danger", actions);
            ok.style.marginLeft = 6;
            ok.SetEnabled(d.Marks.Count > 0);
        }
    }
}
