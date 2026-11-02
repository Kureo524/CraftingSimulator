using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Nucleaire.Data;
using Nucleaire.Objectives;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Tutoriel mis en scène comme les « scénarios » d'une fenêtre système :
    /// <list type="bullet">
    /// <item>les étapes d'annonce (sans condition) s'ouvrent au centre, en fenêtre système qui s'écrit ;</item>
    /// <item>les étapes à faire deviennent une carte de quête compacte (case à cocher + compteur) reliée par
    /// un fil de constellation à ce qu'il faut toucher (voir <see cref="TutorialGuide"/>) ;</item>
    /// <item>une étape réussie se coche, annonce sa récompense (actions débloquées), puis laisse place à la suivante.</item>
    /// </list>
    /// Pendant le tutoriel, seules les actions déjà présentées sont permises. « Passer le tutoriel » débloque tout.</summary>
    public sealed class TutorialPanel
    {
        private const float CelebrateSeconds = 1.8f;

        private readonly GameContext ctx;
        private readonly TutorialTracker tracker;
        private readonly VisualElement card, scenarioLayer, scenario;
        private readonly Typewriter writer = new();
        private float nextCheck, celebrateUntil = -1f;
        private bool confirmSkip, collapsed;
        private Label counter;
        private VisualElement check;

        public TutorialPanel(GameContext ctx, TutorialTracker tracker, VisualElement root)
        {
            this.ctx = ctx;
            this.tracker = tracker;
            card = root.Q("tutorial");
            scenarioLayer = root.Q("scenario-layer");
            scenario = root.Q("scenario");
            tracker.Advanced += () => { confirmSkip = false; celebrateUntil = -1f; Rebuild(); };
            ctx.RecipeBookRequested += (_, _) => tracker.NotifyRecipeBookOpened();
            Rebuild();
        }

        /// <summary>Un panneau est ouvert : la carte se réduit à son titre et sa consigne.</summary>
        public bool Compact
        {
            set => card.EnableInClassList("quest--compact", value);
        }

        /// <summary>Carte de quête visible (le guide s'y accroche).</summary>
        public VisualElement QuestCard => tracker.Active && !IsAnnouncement && !collapsed ? card : null;
        public bool Celebrating => celebrateUntil > 0f;
        private bool IsAnnouncement => tracker.Current?.condition == TutorialCondition.None;

        // ---------------- Construction ----------------

        private void Rebuild()
        {
            card.Clear();
            card.RemoveFromClassList("quest--done");
            scenario.Clear();
            Ui.SetVisible(card, false);
            Ui.SetVisible(scenarioLayer, false);
            counter = null;
            check = null;
            if (!tracker.Active) return;
            if (IsAnnouncement) BuildAnnouncement();
            else BuildQuest();
        }

        /// <summary>Fenêtre système centrée : titre, texte qui s'écrit, récompense, boutons.</summary>
        private void BuildAnnouncement()
        {
            var step = tracker.Current;
            bool first = tracker.Index == 0, last = tracker.Index == tracker.Count - 1;
            Ui.SetVisible(scenarioLayer, true);
            Enter(scenario, "scenario--enter");

            Ui.Text(first ? "[Un nouveau scénario commence.]" : last ? "[Fin du tutoriel.]" : $"[Scénario {tracker.Index + 1} / {tracker.Count}]",
                "system scenario__kicker", scenario);
            Ui.Text(step.title, "scenario__title", scenario);
            Rail(scenario);

            var rules = SplitRules(step.body);
            if (rules == null)
            {
                var body = Ui.Text("", "scenario__body wrap", scenario);
                writer.Play(body, step.body);
                body.RegisterCallback<ClickEvent>(_ => writer.Finish());
            }
            else
            {
                if (rules.Value.intro.Length > 0) Ui.Text(rules.Value.intro, "scenario__body wrap", scenario);
                int n = 1;
                foreach (var rule in rules.Value.items)
                {
                    var row = Ui.El("row scenario__rule", scenario);
                    Ui.Text(n++.ToString(), "number scenario__rule-n", row);
                    Ui.Text(rule, "text-2 wrap grow", row);
                }
            }

            var unlocked = Unlocks(step.unlocks);
            if (unlocked != null) Ui.Text(unlocked, "system scenario__reward", scenario);

            var actions = Ui.El("row scenario__actions", scenario);
            if (!last) SkipButton(actions);
            Ui.El("spacer", actions);
            if (tracker.Index > 0) Ui.Button("Précédent", tracker.Previous, "btn btn--ghost", actions).style.marginRight = 6;
            Ui.Button(first ? "Accepter le scénario" : last ? "Commencer" : "Continuer", Continue, "btn btn--primary", actions);
        }

        /// <summary>Carte de quête compacte en haut à gauche.</summary>
        private void BuildQuest()
        {
            var step = tracker.Current;
            Ui.SetVisible(card, true);
            Enter(card, "quest--enter");
            card.EnableInClassList("quest--collapsed", collapsed);

            var head = Ui.El("row quest__head", card);
            Ui.Text($"[Scénario {tracker.Index + 1} / {tracker.Count}]", "system grow", head);
            if (tracker.Index > 0)
            {
                var prev = Ui.Button("‹", tracker.Previous, "btn btn--ghost btn--icon", head);
                Tooltips.Register(prev, "Étape précédente");
            }
            var fold = Ui.Button(collapsed ? "+" : "–", () => { collapsed = !collapsed; Rebuild(); }, "btn btn--ghost btn--icon", head);
            Tooltips.Register(fold, collapsed ? "Déplier" : "Replier", "Le fil vers la cible reste visible quand la carte est dépliée.");

            Ui.Text(step.title, "quest__title", card);

            var task = Ui.El("row quest__task", card);
            check = Ui.El("quest__check", task);
            Ui.Text("✓", "quest__check-mark", check);
            Ui.Text(string.IsNullOrEmpty(step.objective) ? step.title : step.objective, "quest__objective grow", task);
            counter = Ui.Text("", "number--small quest__counter", task);
            UpdateCounter();

            if (collapsed) return;
            var body = Ui.Text("", "text-2 wrap quest__body", card);
            writer.Play(body, step.body);
            body.RegisterCallback<ClickEvent>(_ => writer.Finish());

            Rail(card);
            Ui.Text("", "system quest__status", card).name = "quest-status";

            var foot = Ui.El("row quest__foot", card);
            Ui.El("spacer", foot);
            SkipButton(foot);
        }

        private void SkipButton(VisualElement parent)
        {
            var skip = Ui.Button(confirmSkip ? "Confirmer : tout débloquer" : "Passer le tutoriel", () =>
            {
                if (confirmSkip) tracker.Skip();
                else { confirmSkip = true; Rebuild(); }
            }, confirmSkip ? "btn btn--danger btn--small" : "btn btn--ghost btn--small", parent);
            Tooltips.Register(skip, "Passer le tutoriel", "Débloque toutes les actions, remet l'atelier à zéro et affiche l'objectif final.");
        }

        /// <summary>Progression en constellation : une étoile par étape, reliées par un trait fin.</summary>
        private void Rail(VisualElement parent)
        {
            var rail = Ui.El("row rail", parent);
            for (int i = 0; i < tracker.Count; i++)
            {
                if (i > 0) Ui.El("rail__link" + (i <= tracker.Index ? " rail__link--done" : ""), rail);
                var star = Ui.El("rail__star", rail);
                star.EnableInClassList("rail__star--done", i < tracker.Index);
                star.EnableInClassList("rail__star--current", i == tracker.Index);
                int index = i;
                Tooltips.Register(star, tracker.Def.Steps[index].title, index < tracker.Index ? "Accompli." : index == tracker.Index ? "En cours." : null);
            }
        }

        private void Continue()
        {
            if (!writer.Done) { writer.Finish(); return; }
            tracker.Next();
        }

        // ---------------- Mise à jour ----------------

        public void Update()
        {
            writer.Update();
            if (!tracker.Active) return;

            if (celebrateUntil > 0f)
            {
                if (Time.unscaledTime >= celebrateUntil) tracker.Next();
                return;
            }
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.2f;
            UpdateCounter();
            var step = tracker.Current;
            if (step.condition != TutorialCondition.None && tracker.IsMet(step)) Celebrate(step);
        }

        private void UpdateCounter()
        {
            if (counter == null) return;
            var (have, need) = tracker.ProgressOf(tracker.Current);
            counter.text = need > 1 ? $"{have}/{need}" : "";
        }

        /// <summary>L'étape est réussie : la case se coche, la récompense s'affiche, puis l'étape suivante arrive.</summary>
        private void Celebrate(TutorialStep step)
        {
            celebrateUntil = Time.unscaledTime + CelebrateSeconds;
            writer.Finish();
            if (collapsed) { collapsed = false; Rebuild(); }
            card.AddToClassList("quest--done");
            UpdateCounter();
            var status = card.Q<Label>("quest-status");
            if (status != null)
            {
                var next = tracker.Index + 1 < tracker.Count ? tracker.Def.Steps[tracker.Index + 1] : null;
                string reward = next != null ? Unlocks(next.unlocks) : null;
                status.text = "[Scénario accompli.]" + (reward != null ? "\n" + reward.Replace("[Débloqué", "[Récompense") : "");
                status.AddToClassList("quest__status--done");
            }
            ctx.Notify($"Scénario accompli : {step.title}", EventSeverity.Success);
        }

        // ---------------- Outils ----------------

        private static void Enter(VisualElement e, string enterClass)
        {
            e.AddToClassList(enterClass);
            e.schedule.Execute(() => e.RemoveFromClassList(enterClass)).StartingIn(30);
        }

        private static string Unlocks(PlayerPermission p)
        {
            if (p == PlayerPermission.All) return "[Débloqué : toutes les actions.]";
            var names = System.Enum.GetValues(typeof(PlayerPermission)).Cast<PlayerPermission>()
                .Where(x => x != PlayerPermission.None && x != PlayerPermission.All && (p & x) == x)
                .Select(x => x.DisplayName()).ToList();
            return names.Count == 0 ? null : "[Débloqué : " + string.Join(", ", names) + ".]";
        }

        /// <summary>Un texte « 1) … 2) … » devient une liste de règles numérotées.</summary>
        private static (string intro, List<string> items)? SplitRules(string body)
        {
            var parts = Regex.Split(body ?? "", @"\s*\d\)\s+");
            if (parts.Length < 3) return null;
            return (parts[0].Trim(), parts.Skip(1).Select(x => x.Trim()).Where(x => x.Length > 0).ToList());
        }
    }

    /// <summary>Texte qui s'écrit lettre à lettre, sans faire bouger la mise en page (le reste est transparent).</summary>
    public sealed class Typewriter
    {
        private const float CharsPerSecond = 90f;
        private Label label;
        private string full;
        private float start;

        public bool Done { get; private set; } = true;

        public void Play(Label l, string text)
        {
            label = l;
            full = (text ?? "").Replace("<", "‹");
            start = Time.unscaledTime;
            Done = false;
            label.enableRichText = true;
            Update();
        }

        public void Finish()
        {
            if (label == null) return;
            label.text = full;
            Done = true;
        }

        public void Update()
        {
            if (Done || label == null) return;
            if (label.panel == null) { Done = true; return; }
            int n = Mathf.FloorToInt((Time.unscaledTime - start) * CharsPerSecond);
            if (n >= full.Length) { Finish(); return; }
            label.text = full.Substring(0, n) + "<alpha=#00>" + full.Substring(n);
        }
    }
}
