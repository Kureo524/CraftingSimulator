using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Objectifs (haut-gauche), contrôles de simulation (haut-droite), journal (bas-gauche),
    /// messages éphémères et rappel du mode en cours.</summary>
    public sealed class HudPanel
    {
        private readonly GameContext ctx;
        private readonly VisualElement objectives, log, toasts;
        private readonly Label clock, modeHint;
        private readonly List<(ObjectiveDef def, VisualElement fill, Label amount, VisualElement badge)> rows = new();
        private readonly VisualElement compassNeedle;
        private SegmentedControl speed;

        public HudPanel(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            objectives = root.Q("objectives");
            log = root.Q("log");
            toasts = root.Q("toasts");
            modeHint = root.Q<Label>("mode-hint");

            BuildObjectives();
            var topRight = root.Q("top-right");
            clock = BuildControls(topRight, out compassNeedle);

            ctx.World.Events.Added += AddLogLine;
            foreach (var e in ctx.World.Events.Entries) AddLogLine(e);
            ctx.Session.Objectives.Completed += _ => RefreshObjectives();
            ctx.Toast += ShowToast;
            ctx.BuildItemChanged += _ => RefreshModeHint();
            ctx.Runner.SpeedChanged += () => speed.Select(ctx.Runner.SpeedIndex, notify: false);
            RefreshModeHint();
        }

        // ---------------- Objectifs ----------------

        private Label goalPercent, goalSteps;
        private VisualElement goalFill, goalNext;
        private int lastGoalDone = -1;

        private void BuildObjectives()
        {
            objectives.Clear();
            var header = Ui.El("row", objectives);
            Ui.Text("Objectif final", "title grow", header);
            var book = Ui.Button("Livre de recettes", () => ctx.OpenRecipeBook(), "btn btn--small", header);
            Tooltips.Register(book, "Livre de recettes (B)", "Toutes les façons d'obtenir chaque ressource, et ce qu'elle permet de fabriquer.");

            var goal = ctx.Session.Goal;
            if (goal != null)
            {
                var card = Ui.El("goal", objectives);
                var name = Ui.El("row goal__name", card);
                Ui.Swatch(Palette.Muted(goal.Goal.Color), name);
                Ui.Text(goal.Goal.DisplayName, "bold grow", name);
                name.RegisterCallback<ClickEvent>(_ => ctx.OpenRecipeBook(goal.Goal));

                var numbers = Ui.El("row goal__numbers", card);
                goalPercent = Ui.Text("0 %", "number", numbers);
                Ui.El("spacer", numbers);
                goalSteps = Ui.Text("", "meta", numbers);
                Ui.Gauge(card, out goalFill, thick: true);
                Tooltips.Register(card, "Progression", "Part des étapes de l'arbre de fabrication de l'antimatière que tu as déjà obtenues (ressources intermédiaires et machines nécessaires).");

                Ui.Text("Prochaines étapes possibles", "text-3", card).style.marginTop = 6;
                goalNext = Ui.El("row recipe-flow", card);
            }

            var milestones = Ui.El("milestones", objectives);
            Ui.Text("Jalons", "text-3", milestones).style.marginBottom = 4;
            foreach (var o in ctx.Session.Objectives.Objectives)
            {
                var row = Ui.El("objective", milestones);
                var line = Ui.El("row", row);
                Ui.Swatch(Palette.Muted(o.Target.Resource.Color), line);
                Ui.Text(o.Title, "grow", line);
                var amount = Ui.Text("", "mono", line);
                var badge = Ui.Badge("", null, line);
                badge.style.marginLeft = 6;
                Ui.Gauge(row, out var fill);
                row.RegisterCallback<ClickEvent>(_ => ctx.OpenRecipeBook(o.Target.Resource));
                Tooltips.Register(row, () => (o.Title, $"{o.Description}\n{Ui.Amount(o.Target.Resource, ctx.Session.Objectives.Progress(o))} / {Ui.Amount(o.Target.Resource, o.Target.Amount)}\nClique pour voir la recette."));
                rows.Add((o, fill, amount, badge));
            }
            var hint = Ui.El("status-window objective-hint", objectives);
            hint.name = "objective-hint";
            Ui.Text("", "system", hint);
            RefreshObjectives();
        }

        private void RefreshObjectives()
        {
            var tracker = ctx.Session.Objectives;
            foreach (var (o, fill, amount, badge) in rows)
            {
                bool done = tracker.IsCompleted(o);
                bool current = tracker.Current == o;
                float ratio = tracker.Ratio(o);
                Ui.SetGauge(fill, ratio, done ? "success" : null);
                amount.text = $"{ratio * 100f:0} %";
                Ui.SetBadge(badge, done ? "Réussi" : current ? "En cours" : "À venir", done ? "success" : current ? "accent" : null);
            }

            var goal = ctx.Session.Goal;
            if (goal != null)
            {
                goalPercent.text = $"{goal.Ratio * 100f:0} %";
                goalSteps.text = $"{goal.Done} / {goal.Steps.Count} étapes de l'arbre";
                Ui.SetGauge(goalFill, goal.Ratio, goal.Ratio >= 1f ? "success" : null);
                if (goal.Done != lastGoalDone)
                {
                    lastGoalDone = goal.Done;
                    goalNext.Clear();
                    foreach (var r in goal.Frontier.Take(4))
                        Ui.Chip(r, null, () => ctx.OpenRecipeBook(r), goalNext);
                    if (goalNext.childCount == 0) Ui.System(goal.Ratio >= 1f ? "Arbre complet !" : "Termine les étapes en cours.", goalNext);
                }
            }

            var hint = objectives.Q("objective-hint");
            var cur = tracker.Current;
            hint.Q<Label>().text = cur == null
                ? "[Tous les objectifs sont atteints. L'atelier a produit de l'antimatière.]"
                : $"[{cur.Hint}]";
        }

        // ---------------- Contrôles ----------------

        private Label BuildControls(VisualElement parent, out VisualElement needle)
        {
            var compass = Ui.El("glass glass--light compass", parent);
            needle = Ui.Text("N", "compass__label", compass);
            Tooltips.Register(compass, "Boussole", "Les faces des machines sont orientées Nord/Est/Sud/Ouest. Tourne la caméra avec Q/E (A/E en AZERTY).");

            speed = new SegmentedControl(new[] { "Pause", "×1", "×2", "×4" }, ctx.Runner.SpeedIndex);
            speed.Changed += i => ctx.Runner.SetSpeedIndex(i);
            parent.Add(speed);
            Tooltips.Register(speed, "Vitesse de simulation", "Espace : pause / reprise.");

            var clockBox = Ui.El("glass glass--light clock", parent);
            return Ui.Text("00:00", "mono", clockBox);
        }

        // ---------------- Journal ----------------

        private void AddLogLine(SimEvent e)
        {
            var line = Ui.El("log-line row", log);
            var dot = Ui.El("badge__dot", line);
            dot.style.backgroundColor = e.Severity switch
            {
                EventSeverity.Error => Palette.Danger,
                EventSeverity.Warning => Palette.Warning,
                EventSeverity.Success => Palette.Success,
                _ => Palette.Accent,
            };
            Ui.Text($"[{e.Message}]", "system log-text", line);
            while (log.childCount > 6) log.RemoveAt(0);
        }

        private void ShowToast(string message, EventSeverity severity)
        {
            var state = severity switch
            {
                EventSeverity.Error => "danger", EventSeverity.Warning => "warning", EventSeverity.Success => "success", _ => "info",
            };
            var t = Ui.HelpBox(message, state, toasts);
            t.AddToClassList("toast");
            t.pickingMode = PickingMode.Ignore;
            t.schedule.Execute(() => t.RemoveFromHierarchy()).StartingIn(3200);
            while (toasts.childCount > 3) toasts.RemoveAt(0);
        }

        private void RefreshModeHint()
        {
            var item = ctx.BuildItem;
            Ui.SetVisible(modeHint, item != null);
            if (item != null)
                modeHint.text = $"[Pose : {item.DisplayName} ×{ctx.Session.Inventory.Count(item)} — clic gauche pour poser, clic droit ou Échap pour arrêter.]";
        }

        // ---------------- Mise à jour ----------------

        public void Update(float cameraYaw)
        {
            clock.text = $"{Ui.Seconds(ctx.World.CurrentTick, ctx.World.Settings.ticksPerSecond)}  ·  tick {ctx.World.CurrentTick}";
            // Le « N » se place sur le bord de la boussole, dans la direction du nord à l'écran.
            float a = -cameraYaw * Mathf.Deg2Rad;
            compassNeedle.style.left = 19f - 7f + Mathf.Sin(a) * 11f;
            compassNeedle.style.top = 19f - 8f - Mathf.Cos(a) * 11f;
            if (Time.frameCount % 15 == 0) { RefreshObjectives(); RefreshModeHint(); }
        }
    }
}
