using System.Linq;
using Nucleaire.Data;
using Nucleaire.Objectives;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Tutoriel guidé : une étape à la fois, validée automatiquement par ce que fait le joueur.
    /// Pendant le tutoriel, seules les actions déjà présentées sont permises. « Passer le tutoriel » débloque tout.</summary>
    public sealed class TutorialPanel
    {
        private readonly GameContext ctx;
        private readonly TutorialTracker tracker;
        private readonly VisualElement panel;
        private float nextCheck;
        private bool confirmSkip;

        public TutorialPanel(GameContext ctx, TutorialTracker tracker, VisualElement root)
        {
            this.ctx = ctx;
            this.tracker = tracker;
            panel = root.Q("tutorial");
            tracker.Advanced += () => { confirmSkip = false; Rebuild(); };
            ctx.RecipeBookRequested += _ => tracker.NotifyRecipeBookOpened();
            Rebuild();
        }

        private void Rebuild()
        {
            panel.Clear();
            Ui.SetVisible(panel, tracker.Active);
            if (!tracker.Active) return;

            var step = tracker.Current;
            var head = Ui.El("row", panel);
            Ui.Text($"Tutoriel · étape {tracker.Index + 1} / {tracker.Count}", "meta grow", head);
            var skip = Ui.Button(confirmSkip ? "Confirmer" : "Passer le tutoriel", () =>
            {
                if (confirmSkip) tracker.Skip();
                else { confirmSkip = true; Rebuild(); }
            }, confirmSkip ? "btn btn--danger btn--small" : "btn btn--ghost btn--small", head);
            Tooltips.Register(skip, "Passer le tutoriel", "Débloque immédiatement toutes les actions et affiche l'objectif final.");

            var gauge = Ui.Gauge(panel, out var fill);
            gauge.AddToClassList("tutorial__progress");
            Ui.SetGauge(fill, (float)tracker.Index / tracker.Count);

            Ui.Text(step.title, "title", panel).style.marginBottom = 4;
            Ui.Text(step.body, "text-2 wrap", panel);

            var unlocked = System.Enum.GetValues(typeof(PlayerPermission)).Cast<PlayerPermission>()
                .Where(p => p != PlayerPermission.None && p != PlayerPermission.All && (step.unlocks & p) == p)
                .Select(p => p.DisplayName()).ToList();
            if (unlocked.Count > 0 && step.unlocks != PlayerPermission.All)
                Ui.System("Débloqué : " + string.Join(", ", unlocked) + ".", panel).style.marginTop = 8;

            var actions = Ui.El("row tutorial__actions", panel);
            if (tracker.Index > 0) Ui.Button("Précédent", tracker.Previous, "btn btn--ghost btn--small", actions);
            Ui.El("spacer", actions);
            if (step.condition == TutorialCondition.None)
                Ui.Button(tracker.Index == tracker.Count - 1 ? "Commencer" : "Suivant", tracker.Next, "btn btn--primary btn--small", actions);
            else
                Ui.Badge("À toi de jouer", "accent", actions);
        }

        public void Update()
        {
            if (!tracker.Active || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.25f;
            var step = tracker.Current;
            if (tracker.Check()) ctx.Notify($"Étape réussie : {step.title}", EventSeverity.Success);
        }
    }
}
