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
        private VisualElement lastIcon;
        private string hint;
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
            hint = null;
            if (card != null && !panel.Celebrating && layer.panel != null) Resolve(tracker.Current);

            if (hasTarget)
            {
                var cb = card.worldBound;
                anchor = new Vector2(cb.xMax, cb.y + Mathf.Min(64f, cb.height / 2f));
                tag.text = $"[{hint ?? tracker.Current.objective}]";
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
                    PointUi(root.Q("hotbar-book"), $"Ouvre le livre ({ctx.Input.Display(GameInput.RecipeBook)})");
                    break;

                case TutorialCondition.HaveItem:
                    GuideTake(s.resource, vault);
                    break;

                case TutorialCondition.PlaceMachine:
                case TutorialCondition.MachinePowered:
                {
                    var placed = w.Machines.FirstOrDefault(m => m.MachineDef == s.machine);
                    var item = s.machine != null ? s.machine.Item : null;
                    if (placed == null)
                    {
                        if (ModalOpen()) { CloseHint($"Ferme ce panneau, puis équipe : {item?.DisplayName}"); break; }
                        if (ctx.BuildItem == item && item != null)
                        {
                            if (s.condition == TutorialCondition.MachinePowered)
                                PointMachine(w.Machines.LastOrDefault(m => m is not VaultMachine) ?? vault, "Pose-le collé à cette machine (clic gauche)");
                            break;
                        }
                        int slot = ctx.Hotbar.IndexOf(item);
                        if (slot >= 0) PointIcon("hotbar", item, $"Appuie sur {ctx.Input.Display(GameInput.HotbarSlot(slot))} (ou clique) pour l'équiper");
                        else PointUi(root.Q("hotbar-inventory"), $"Range {item?.DisplayName} dans la barre rapide");
                        break;
                    }
                    if (ModalOpen()) { CloseHint(); break; }
                    PointMachine(placed, s.condition == TutorialCondition.MachinePowered
                        ? "Pas d'énergie : colle-le au panneau solaire, ou relie-les par un câble"
                        : placed.MachineDef.DisplayName);
                    break;
                }

                case TutorialCondition.Produce:
                    GuideProduce(s.resource, vault);
                    break;

                case TutorialCondition.VaultOutputFiltered:
                    GuideVaultFaces(vault);
                    break;

                case TutorialCondition.ConfigureOutputSide:
                    PointMachine(w.Machines.OfType<ProcessorMachine>().FirstOrDefault(m => m.Recipes.Any(r => r.Inputs.Count > 0)), "Ouvre la machine, onglet Faces");
                    break;
            }
        }

        /// <summary>Prendre une ressource de base dans le vault : ouvrir le vault, choisir la ressource, puis la quantité.</summary>
        private void GuideTake(ResourceDef r, VaultMachine vault)
        {
            if (r == null) return;
            if (ctx.Selection is VaultMachine)
            {
                if (PointIcon("inspector", r, $"Clique sur : {r.DisplayName}"))
                {
                    if (lastIcon != null && lastIcon.ClassListContains("rslot--picked"))
                        PointUi(root.Q("inspector").Query(className: "vault-picker__btn").AtIndex(1), "Choisis une quantité, ×10 par exemple");
                    return;
                }
                PointTab("inspector", "Ressources", "Onglet Ressources");
                return;
            }
            if (ctx.Selection != null) { CloseHint("Ferme ce panneau, puis ouvre le vault"); return; }
            PointMachine(vault, $"Clique sur le vault pour prendre : {r.DisplayName}");
        }

        /// <summary>Produire une ressource : à l'établi, ou dans une machine (avec ses ingrédients d'abord).</summary>
        private void GuideProduce(ResourceDef r, VaultMachine vault)
        {
            if (r == null) return;
            if (ctx.Db.RecipesProducing(r).Any(e => e.Source == RecipeSource.Workbench)) { GuideCraft(r); return; }

            var inv = ctx.Session.Inventory;
            var maker = ctx.World.Machines.OfType<ProcessorMachine>().FirstOrDefault(m => m.Recipes.Any(x => x.Outputs.Any(o => o.Resource == r)));
            if (maker == null) return;
            var recipes = maker.Recipes.Where(x => x.Outputs.Any(o => o.Resource == r)).ToList();
            var recipe = recipes.FirstOrDefault(x => x.Inputs.All(i => inv.Count(i.Resource) > 0 || Loaded(maker, i.Resource)))
                         ?? recipes.FirstOrDefault(x => x.Inputs.All(i => i.Resource.IsBaseResource))
                         ?? recipes[0];
            var input = recipe.Inputs.Select(i => i.Resource).FirstOrDefault(x => !Loaded(maker, x)) ?? recipe.Inputs[0].Resource;
            bool loaded = recipe.Inputs.All(i => Loaded(maker, i.Resource));
            bool have = inv.Count(input) > 0;
            string machine = maker.MachineDef.DisplayName.ToLowerInvariant();

            if (ctx.Selection == maker)
            {
                if (PointIcon("inspector", r, $"Clique pour récupérer : {r.DisplayName}")) return;
                if (loaded)
                {
                    if (maker.Status is MachineStatus.NoPower or MachineStatus.LowPower) PointIcon("inspector", input, "Pas assez d'énergie : relie la machine au panneau solaire");
                    else PointIcon("inspector", input, "En cours… le produit apparaîtra à droite");
                    return;
                }
                if (have) { PointIcon("inventory", input, $"Glisse ceci sur le {machine} : {input.DisplayName}"); return; }
                CloseHint($"Il te faut : {input.DisplayName}. Ferme, puis va au vault");
                return;
            }
            if (ctx.Selection is VaultMachine && !have && !loaded) { GuideTake(input, vault); return; }
            if (ctx.Selection != null) { CloseHint(); return; }
            if (have || loaded || maker.Outputs.Slots.Any(x => x.Resource == r && x.Amount > 0))
                PointMachine(maker, $"Ouvre le {machine}");
            else
                PointMachine(vault, $"Prends au vault : {input.DisplayName}");
        }

        private static bool Loaded(Machine m, ResourceDef r) => m.Inputs != null && m.Inputs.Slots.Any(x => x.Resource == r && x.Amount > 0);

        /// <summary>Fabriquer à l'établi : l'ouvrir, aller au bon onglet, puis cliquer « Fabriquer » (ou dire ce qu'il manque).</summary>
        private void GuideCraft(ResourceDef r)
        {
            var wb = ctx.Session.Workbench;
            var recipe = wb.Recipes.FirstOrDefault(c => c.Result.Resource == r);
            bool can = recipe != null && wb.CanCraft(recipe, out _);
            string missing = recipe == null ? null : Missing(recipe);

            var layerWb = root.Q("workbench-layer");
            if (layerWb != null && !layerWb.ClassListContains("hidden"))
            {
                var craft = root.Q("craft-" + r.Id);
                if (craft != null && IsShown(craft))
                {
                    if (can) PointUi(craft, "Clique sur Fabriquer");
                    else PointUi(root.Q("card-" + r.Id), "Il manque : " + missing);
                    return;
                }
                PointTab("workbench", WorkbenchPanel.CategoryLabel(ctx.Db, r), $"Onglet {WorkbenchPanel.CategoryLabel(ctx.Db, r)}");
                return;
            }
            var fromBook = root.Q("book-craft-" + r.Id);
            if (fromBook != null && IsShown(fromBook)) { PointUi(fromBook, "Ouvre l'établi sur cette recette"); return; }
            if (ModalOpen()) { CloseHint($"Ferme, puis ouvre l'établi ({ctx.Input.Display(GameInput.Workbench)})"); return; }
            PointUi(root.Q("hotbar-workbench"), can ? $"Ouvre l'établi ({ctx.Input.Display(GameInput.Workbench)})" : "Il manque : " + missing);
        }

        private string Missing(CraftingRecipe c)
        {
            var inv = ctx.Session.Inventory;
            var parts = c.Ingredients.Where(i => i.IsValid && inv.Count(i.Resource) < i.Amount)
                .Select(i => $"{i.Amount - inv.Count(i.Resource)} × {i.Resource.DisplayName.ToLowerInvariant()}").ToList();
            return parts.Count == 0 ? "rien" : string.Join(", ", parts);
        }

        /// <summary>Mettre une face du vault en sortie filtrée : onglet Faces, une face en Sortie, puis la ressource.</summary>
        private void GuideVaultFaces(VaultMachine vault)
        {
            if (vault == null) return;
            if (ctx.Selection == vault)
            {
                if (PointTab("inspector", "Faces", "Onglet Faces")) return;
                var output = DirectionExtensions.All.Where(d => vault.Sides[d].Mode == SideMode.Output).ToList();
                if (output.Count == 0) { PointUi(root.Q("side-" + Direction.East), "Clique jusqu'à « Sortie »"); return; }
                PointUi(root.Q("side-filter-" + output[0]), "Choisis la ressource qui sortira");
                return;
            }
            if (ctx.Selection != null) { CloseHint("Ferme, puis ouvre le vault"); return; }
            PointMachine(vault, "Ouvre le vault");
        }

        /// <summary>Montre le bouton de fermeture du panneau ouvert (le plus haut d'abord).</summary>
        private void CloseHint(string text = "Ferme ce panneau")
        {
            foreach (var (layerName, button) in new[] { ("options-layer", "options-close"), ("book-layer", "book-close"), ("workbench-layer", "workbench-close"), ("inventory-layer", "inventory-close") })
            {
                if (root.Q(layerName) is not { } l || l.ClassListContains("hidden")) continue;
                PointUi(root.Q(button), text);
                return;
            }
        }

        /// <summary>Montre un onglet s'il n'est pas déjà actif. Renvoie vrai s'il a fallu le montrer.</summary>
        private bool PointTab(string container, string label, string text)
        {
            var tab = root.Q(container)?.Query<Button>(className: "segmented__item").Where(b => b.text == label).First();
            if (tab == null || tab.ClassListContains("segmented__item--active")) return false;
            PointUi(tab, text);
            return hasTarget;
        }

        /// <summary>Un panneau plein écran couvre le monde : inutile d'y montrer une machine.</summary>
        private bool ModalOpen() => new[] { "inventory-layer", "workbench-layer", "book-layer", "options-layer" }
            .Any(n => root.Q(n) is { } l && !l.ClassListContains("hidden"));

        private void PointUi(VisualElement e, string text = null)
        {
            if (e == null || !IsShown(e)) return;
            hasTarget = true;
            targetIsRect = true;
            targetRect = e.worldBound;
            hint = text;
        }

        private bool PointIcon(string container, ResourceDef r, string text = null)
        {
            if (r == null) return false;
            var c = root.Q(container);
            if (c == null || !IsShown(c)) return false;
            var e = c.Query<VisualElement>().Where(x => x.userData is Func<ResourceDef> f && f() == r && IsShown(x)).First();
            if (e == null) return false;
            lastIcon = e;
            PointUi(e, text);
            return hasTarget;
        }

        private void PointMachine(Machine m, string text = null)
        {
            if (m == null) return;
            // Un panneau couvre l'atelier : d'abord le fermer.
            if (ModalOpen()) { CloseHint(text != null ? $"Ferme ce panneau, puis : {Lower(text)}" : "Ferme ce panneau"); return; }
            if (!view.IsVisible(m)) return;
            hint = text;
            var p3 = GridMath.CellCenter(m.Position, 0.55f);
            if (cam.WorldToViewportPoint(p3).z <= 0) return;
            hasTarget = true;
            targetIsRect = false;
            targetPoint = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, p3, cam);
        }

        private static string Lower(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

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
