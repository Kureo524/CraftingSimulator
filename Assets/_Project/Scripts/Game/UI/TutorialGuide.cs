using System;
using System.Linq;
using Nucleaire.Data;
using Nucleaire.Objectives;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Fil de constellation du tutoriel : une ligne de points lavande part de la carte de quête et rejoint
    /// ce que le joueur doit toucher (bouton, icône, machine dans le monde). La cible est marquée d'une étoile
    /// et d'un anneau qui respire, avec la consigne écrite à côté. La cible suit l'action en cours :
    /// d'abord le vault, puis la bonne icône dans son panneau une fois ouvert, etc.</summary>
    public sealed class TutorialGuide
    {
        private readonly GameContext ctx;
        private readonly TutorialTracker tracker;
        private readonly TutorialPanel panel;
        private readonly VisualElement root, layer;
        private readonly Label tag;
        private readonly Camera cam;
        private readonly WorldView view;

        private bool hasTarget, targetIsRect;
        private Rect targetRect;
        private Vector2 targetPoint, anchor;

        private static readonly Color Lavender = Palette.Lavender;
        private static readonly Color Accent = Palette.Accent;

        public TutorialGuide(GameContext ctx, TutorialTracker tracker, TutorialPanel panel, VisualElement root, Camera cam, WorldView view)
        {
            this.ctx = ctx;
            this.tracker = tracker;
            this.panel = panel;
            this.root = root;
            this.cam = cam;
            this.view = view;
            layer = root.Q("tutorial-guide");
            layer.pickingMode = PickingMode.Ignore;
            layer.generateVisualContent += Draw;
            tag = Ui.Text("", "system guide__tag", layer);
            tag.pickingMode = PickingMode.Ignore;
        }

        public void Update()
        {
            var card = panel.QuestCard;
            hasTarget = false;
            if (card != null && !panel.Celebrating && layer.panel != null) Resolve(tracker.Current);

            if (hasTarget)
            {
                var cb = card.worldBound;
                anchor = new Vector2(cb.xMax, cb.y + Mathf.Min(64f, cb.height / 2f));
                tag.text = $"[{tracker.Current.objective}]";
                // Bouton ou icône : la consigne au-dessus ; machine : à droite de l'étoile.
                var at = targetIsRect ? new Vector2(targetRect.center.x, targetRect.y - 10f) : targetPoint + new Vector2(26f, 0f);
                bool flip = !targetIsRect && at.x + 260f > layer.layout.width;
                if (flip) at = targetPoint - new Vector2(26f, 0f);
                if (targetIsRect) at.x = Mathf.Clamp(at.x, 140f, layer.layout.width - 140f);
                tag.style.left = at.x;
                tag.style.top = at.y;
                tag.EnableInClassList("guide__tag--above", targetIsRect);
                tag.EnableInClassList("guide__tag--left", flip);
            }
            Ui.SetVisible(tag, hasTarget);
            layer.MarkDirtyRepaint();
        }

        // ---------------- Où pointer ? ----------------

        private void Resolve(TutorialStep s)
        {
            if (s == null) return;
            var w = ctx.World;
            var vault = w.Machines.OfType<VaultMachine>().FirstOrDefault();
            switch (s.condition)
            {
                case TutorialCondition.OpenRecipeBook:
                    PointUi(root.Q("hotbar-book"));
                    break;

                case TutorialCondition.HaveItem:
                    if (ctx.Selection is VaultMachine && PointIcon("inspector", s.resource)) break;
                    PointMachine(vault);
                    break;

                case TutorialCondition.PlaceMachine:
                case TutorialCondition.MachinePowered:
                {
                    var placed = w.Machines.FirstOrDefault(m => m.MachineDef == s.machine);
                    var item = s.machine != null ? s.machine.Item : null;
                    if (placed == null)
                    {
                        if (ctx.BuildItem == item && item != null)
                        {
                            // En mode pose : montrer à côté de quoi poser (la dernière machine posée, sinon le vault).
                            if (s.condition == TutorialCondition.MachinePowered)
                                PointMachine(w.Machines.LastOrDefault(m => m is not VaultMachine) ?? vault);
                            break;
                        }
                        if (PointIcon("inventory", item)) break;
                        PointIcon("hotbar", item);
                        break;
                    }
                    PointMachine(placed);
                    break;
                }

                case TutorialCondition.Produce:
                {
                    bool atWorkbench = ctx.Db.RecipesProducing(s.resource).Any(e => e.Source == RecipeSource.Workbench);
                    if (atWorkbench)
                    {
                        if (PointIcon("workbench", s.resource)) break;
                        PointUi(root.Q("hotbar-workbench"));
                        break;
                    }
                    var maker = w.Machines.OfType<ProcessorMachine>().FirstOrDefault(m => m.Recipes.Any(r => r.Outputs.Any(o => o.Resource == s.resource)));
                    if (maker != null && ctx.Selection == maker)
                    {
                        // Panneau ouvert : la sortie si elle est prête, sinon l'ingrédient à déposer.
                        if (PointIcon("inspector", s.resource)) break;
                        var input = maker.Recipes.Where(r => r.Outputs.Any(o => o.Resource == s.resource)).SelectMany(r => r.Inputs).Select(i => i.Resource).Distinct();
                        foreach (var r in input) if (PointIcon("inspector", r)) return;
                        break;
                    }
                    PointMachine(maker);
                    break;
                }

                case TutorialCondition.VaultOutputFiltered:
                    if (ctx.Selection == vault)
                    {
                        var tab = root.Q("inspector")?.Query<Button>(className: "segmented__item").Where(b => b.text == "Faces").First();
                        if (tab != null && !tab.ClassListContains("segmented__item--active")) PointUi(tab);
                        break;
                    }
                    PointMachine(vault);
                    break;

                case TutorialCondition.ConfigureOutputSide:
                    PointMachine(w.Machines.OfType<ProcessorMachine>().FirstOrDefault(m => m.Recipes.Any(r => r.Inputs.Count > 0)));
                    break;
            }
        }

        private void PointUi(VisualElement e)
        {
            if (e == null || !IsShown(e)) return;
            hasTarget = true;
            targetIsRect = true;
            targetRect = e.worldBound;
        }

        private bool PointIcon(string container, ResourceDef r)
        {
            if (r == null) return false;
            var c = root.Q(container);
            if (c == null || !IsShown(c)) return false;
            var e = c.Query<VisualElement>().Where(x => x.userData is Func<ResourceDef> f && f() == r && IsShown(x)).First();
            if (e == null) return false;
            PointUi(e);
            return hasTarget;
        }

        private void PointMachine(Machine m)
        {
            if (m == null || !view.IsVisible(m)) return;
            var p3 = GridMath.CellCenter(m.Position, 0.55f);
            if (cam.WorldToViewportPoint(p3).z <= 0) return;
            hasTarget = true;
            targetIsRect = false;
            targetPoint = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, p3, cam);
        }

        private static bool IsShown(VisualElement e)
        {
            for (var x = e; x != null; x = x.parent)
                if (x.resolvedStyle.display == DisplayStyle.None || x.ClassListContains("hidden")) return false;
            return e.worldBound.width > 0f;
        }

        // ---------------- Dessin ----------------

        private void Draw(MeshGenerationContext mgc)
        {
            if (!hasTarget) return;
            var p = mgc.painter2D;
            float t = Time.unscaledTime;
            float pulse = t / 1.8f % 1f;

            Vector2 end = targetIsRect ? ClosestPoint(targetRect, anchor) : targetPoint;
            // Courbe douce : le fil part vers la droite puis descend vers la cible.
            var c1 = anchor + new Vector2(Mathf.Max(60f, (end.x - anchor.x) * 0.5f), 0f);
            var c2 = end + new Vector2(0f, -Mathf.Clamp((end.y - anchor.y) * 0.4f, -120f, 120f));
            float length = Vector2.Distance(anchor, end) * 1.2f;
            int dots = Mathf.Clamp(Mathf.RoundToInt(length / 11f), 4, 160);
            float flow = t * 0.35f % (1f / dots);
            for (int i = 0; i < dots; i++)
            {
                float u = i / (float)dots + flow;
                if (u > 1f) continue;
                var q = Bezier(anchor, c1, c2, end, u);
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Min(u * 6f, (1f - u) * 10f));
                p.BeginPath();
                p.Arc(q, 1.6f, Angle.Degrees(0), Angle.Degrees(360));
                p.fillColor = new Color(Lavender.r, Lavender.g, Lavender.b, 0.75f * fade);
                p.Fill();
            }
            // Point d'attache sur la carte.
            Ring(p, anchor, 3.5f, new Color(Lavender.r, Lavender.g, Lavender.b, 0.9f), 1.4f);

            if (targetIsRect)
            {
                var r = targetRect;
                RoundRect(p, Expand(r, 3f), 10f, new Color(Accent.r, Accent.g, Accent.b, 0.95f), 1.6f);
                RoundRect(p, Expand(r, 3f + pulse * 10f), 10f + pulse * 6f, new Color(Accent.r, Accent.g, Accent.b, 0.55f * (1f - pulse)), 1.2f);
            }
            else
            {
                Ring(p, end, 9f, new Color(Accent.r, Accent.g, Accent.b, 0.95f), 1.6f);
                Ring(p, end, 12f + pulse * 22f, new Color(Accent.r, Accent.g, Accent.b, 0.6f * (1f - pulse)), 1.2f);
                Star(p, end, 5f, new Color(0.91f, 0.93f, 0.98f, 0.95f));
            }
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float u)
        {
            float v = 1f - u;
            return v * v * v * a + 3f * v * v * u * b + 3f * v * u * u * c + u * u * u * d;
        }

        private static Vector2 ClosestPoint(Rect r, Vector2 from) =>
            new(Mathf.Clamp(from.x, r.xMin - 4f, r.xMax + 4f), Mathf.Clamp(from.y, r.yMin - 4f, r.yMax + 4f));

        private static Rect Expand(Rect r, float by) => new(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);

        private static void Ring(Painter2D p, Vector2 c, float radius, Color color, float width)
        {
            p.BeginPath();
            p.Arc(c, radius, Angle.Degrees(0), Angle.Degrees(360));
            p.strokeColor = color;
            p.lineWidth = width;
            p.Stroke();
        }

        private static void Star(Painter2D p, Vector2 c, float r, Color color)
        {
            float k = r * 0.28f;
            p.BeginPath();
            p.MoveTo(c + new Vector2(0, -r));
            p.LineTo(c + new Vector2(k, -k));
            p.LineTo(c + new Vector2(r, 0));
            p.LineTo(c + new Vector2(k, k));
            p.LineTo(c + new Vector2(0, r));
            p.LineTo(c + new Vector2(-k, k));
            p.LineTo(c + new Vector2(-r, 0));
            p.LineTo(c + new Vector2(-k, -k));
            p.ClosePath();
            p.fillColor = color;
            p.Fill();
        }

        private static void RoundRect(Painter2D p, Rect r, float radius, Color color, float width)
        {
            radius = Mathf.Min(radius, r.width / 2f, r.height / 2f);
            p.BeginPath();
            p.MoveTo(new Vector2(r.x + radius, r.y));
            p.LineTo(new Vector2(r.xMax - radius, r.y));
            p.ArcTo(new Vector2(r.xMax, r.y), new Vector2(r.xMax, r.y + radius), radius);
            p.LineTo(new Vector2(r.xMax, r.yMax - radius));
            p.ArcTo(new Vector2(r.xMax, r.yMax), new Vector2(r.xMax - radius, r.yMax), radius);
            p.LineTo(new Vector2(r.x + radius, r.yMax));
            p.ArcTo(new Vector2(r.x, r.yMax), new Vector2(r.x, r.yMax - radius), radius);
            p.LineTo(new Vector2(r.x, r.y + radius));
            p.ArcTo(new Vector2(r.x, r.y), new Vector2(r.x + radius, r.y), radius);
            p.ClosePath();
            p.strokeColor = color;
            p.lineWidth = width;
            p.Stroke();
        }
    }
}
