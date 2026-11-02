using Nucleaire.Objectives;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Tutoriel guidé : une étape à la fois, validée automatiquement par l'état de la partie.</summary>
    public sealed class TutorialPanel
    {
        private readonly GameContext ctx;
        private readonly TutorialTracker tracker;
        private readonly VisualElement panel;
        private readonly Button reopen;
        private float nextCheck;
        private bool hidden;

        public TutorialPanel(GameContext ctx, TutorialTracker tracker, VisualElement root)
        {
            this.ctx = ctx;
            this.tracker = tracker;
            panel = root.Q("tutorial");
            reopen = Ui.Button("Tutoriel", () => SetHidden(false), "btn btn--small hidden", root.Q("top-right"));
            reopen.SendToBack();
            Tooltips.Register(reopen, "Tutoriel", "Réafficher le tutoriel guidé.");
            tracker.Advanced += Rebuild;
            ctx.RecipeBookRequested += _ => tracker.NotifyRecipeBookOpened();
            Rebuild();
        }

        private void SetHidden(bool h)
        {
            hidden = h;
            Rebuild();
        }

        private void Rebuild()
        {
            panel.Clear();
            bool show = !hidden && !tracker.Finished && tracker.Def != null;
            Ui.SetVisible(panel, show);
            Ui.SetVisible(reopen, tracker.Def != null && !show);
            if (!show) { if (tracker.Finished) reopen.text = "Revoir le tutoriel"; return; }

            var step = tracker.Current;
            var head = Ui.El("row", panel);
            Ui.Text($"Tutoriel · étape {tracker.Index + 1} / {tracker.Count}", "meta grow", head);
            var close = Ui.Button("×", () => SetHidden(true), "btn btn--ghost btn--icon", head);
            Tooltips.Register(close, "Masquer le tutoriel", "Il reste accessible avec le bouton « Tutoriel » en haut à droite.");

            Ui.Text(step.title, "title", panel).style.marginBottom = 4;
            Ui.Text(step.body, "text-2 wrap", panel);

            var actions = Ui.El("row tutorial__actions", panel);
            if (tracker.Index > 0) Ui.Button("Précédent", tracker.Previous, "btn btn--ghost btn--small", actions);
            Ui.El("spacer", actions);
            if (step.condition == Data.TutorialCondition.None)
                Ui.Button(tracker.Index == tracker.Count - 1 ? "Terminer" : "Suivant", tracker.Next, "btn btn--primary btn--small", actions);
            else
            {
                Ui.Badge("À faire", "accent", actions).style.marginRight = 6;
                Ui.Button("Passer", tracker.Next, "btn btn--ghost btn--small", actions);
            }
        }

        public void Update()
        {
            if (tracker.Finished || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.25f;
            var step = tracker.Current;
            if (tracker.Check()) ctx.Notify($"Étape réussie : {step.title}", EventSeverity.Success);
        }
    }
}
