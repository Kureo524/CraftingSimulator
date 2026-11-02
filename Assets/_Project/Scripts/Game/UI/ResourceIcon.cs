using System;
using Nucleaire.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Icône vectorielle d'une ressource, dessinée avec Painter2D. Règles de lecture (comme dans JEI/NEI) :
    /// <list type="bullet">
    /// <item>solide : une silhouette (minerai, poudre, lingot, gemme…) sur fond transparent ;</item>
    /// <item>liquide : remplit tout l'emplacement, opaque, avec des vaguelettes horizontales ;</item>
    /// <item>gaz : remplit tout l'emplacement, translucide, avec des volutes et des bulles ;</item>
    /// <item>énergie : un éclair.</item>
    /// </list>
    /// Le nom n'est jamais écrit : il apparaît au survol.</summary>
    public sealed class ResourceIcon : VisualElement
    {
        private ResourceDef resource;

        public ResourceIcon(ResourceDef r = null)
        {
            AddToClassList("ricon");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            Resource = r;
        }

        /// <summary>Affiche la ressource dans un contenant (bonbonne de gaz, jerrican de liquide) rempli à <see cref="Level"/>.</summary>
        public bool Canister
        {
            get => canister;
            set { if (canister == value) return; canister = value; MarkDirtyRepaint(); }
        }

        /// <summary>Remplissage du contenant, de 0 à 1.</summary>
        public float Level
        {
            get => level;
            set { value = Mathf.Clamp01(value); if (Mathf.Approximately(level, value)) return; level = value; MarkDirtyRepaint(); }
        }

        private bool canister;
        private float level = 1f;

        public ResourceDef Resource
        {
            get => resource;
            set { if (resource == value) return; resource = value; MarkDirtyRepaint(); }
        }

        // ---------------- Forme ----------------

        public static IconShape ShapeOf(ResourceDef r)
        {
            if (r == null) return IconShape.Lump;
            if (r.IconShape != IconShape.Auto) return r.IconShape;
            string id = r.Id;
            if (id.StartsWith("item_upgrade")) return IconShape.Upgrade;
            if (id is "item_cable" or "item_pipe" or "item_pressurized_tube" or "item_conveyor") return IconShape.Transmitter;
            if (id.StartsWith("item_")) return IconShape.Machine;
            if (id.EndsWith("_ore")) return IconShape.Ore;
            if (id.EndsWith("_dust") || id is "yellowcake" or "sand") return IconShape.Dust;
            if (id.EndsWith("_ingot")) return IconShape.Ingot;
            if (id.EndsWith("_pellet")) return IconShape.Pellet;
            if (id.Contains("circuit")) return IconShape.Circuit;
            if (id.Contains("glass")) return IconShape.Pane;
            if (id is "fluorite" or "sulfur") return IconShape.Crystal;
            return IconShape.Lump;
        }

        // ---------------- Dessin ----------------

        private static readonly Color Outline = new(0.04f, 0.04f, 0.07f, 0.9f);
        private static readonly Color Stone = new(0.38f, 0.39f, 0.44f);

        private Rect box;
        private Vector2 P(float x, float y) => new(box.x + x * box.width, box.y + y * box.height);
        private float S(float v) => v * box.width;

        private void Draw(MeshGenerationContext mgc)
        {
            if (resource == null) return;
            var rect = contentRect;
            float side = Mathf.Min(rect.width, rect.height);
            if (side < 2f) return;
            box = new Rect(rect.x + (rect.width - side) / 2f, rect.y + (rect.height - side) / 2f, side, side);
            var p = mgc.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            var c = Palette.Muted(resource.Color, 0.15f);

            if (canister && resource.Kind == ResourceKind.Gas) { GasBottle(p, c); return; }
            if (canister && resource.Kind == ResourceKind.Fluid) { Jerrycan(p, c); return; }
            switch (resource.Kind)
            {
                case ResourceKind.Fluid: Fluid(p, c); return;
                case ResourceKind.Gas: Gas(p, c); return;
                case ResourceKind.Energy: Energy(p, c); return;
            }

            switch (ShapeOf(resource))
            {
                case IconShape.Ore: Ore(p, c); break;
                case IconShape.Dust: Dust(p, c); break;
                case IconShape.Ingot: Ingot(p, c); break;
                case IconShape.Crystal: Crystal(p, c); break;
                case IconShape.Pellet: Pellet(p, c); break;
                case IconShape.Pane: Pane(p, c); break;
                case IconShape.Circuit: Circuit(p, c); break;
                case IconShape.Machine: Machine(p, c); break;
                case IconShape.Transmitter: Transmitter(p, c); break;
                case IconShape.Upgrade: Upgrade(p, c); break;
                default: Lump(p, c); break;
            }
        }

        private void Poly(Painter2D p, Color fill, params float[] xy) => Poly(p, fill, Outline, 1.2f, xy);

        private void Poly(Painter2D p, Color fill, Color stroke, float width, params float[] xy)
        {
            p.BeginPath();
            p.MoveTo(P(xy[0], xy[1]));
            for (int i = 2; i < xy.Length; i += 2) p.LineTo(P(xy[i], xy[i + 1]));
            p.ClosePath();
            p.fillColor = fill;
            p.Fill();
            if (width > 0f)
            {
                p.strokeColor = stroke;
                p.lineWidth = width;
                p.Stroke();
            }
        }

        private void Circle(Painter2D p, float x, float y, float r, Color fill, Color? stroke = null, float width = 1f)
        {
            p.BeginPath();
            p.Arc(P(x, y), S(r), Angle.Degrees(0), Angle.Degrees(360));
            p.ClosePath();
            if (fill.a > 0f) { p.fillColor = fill; p.Fill(); }
            if (stroke.HasValue) { p.strokeColor = stroke.Value; p.lineWidth = width; p.Stroke(); }
        }

        private void RoundRect(Painter2D p, float x0, float y0, float x1, float y1, float radius, Color fill, Color? stroke = null, float width = 1f)
        {
            float r = radius;
            p.BeginPath();
            p.MoveTo(P(x0 + r, y0));
            p.LineTo(P(x1 - r, y0));
            p.ArcTo(P(x1, y0), P(x1, y0 + r), S(r));
            p.LineTo(P(x1, y1 - r));
            p.ArcTo(P(x1, y1), P(x1 - r, y1), S(r));
            p.LineTo(P(x0 + r, y1));
            p.ArcTo(P(x0, y1), P(x0, y1 - r), S(r));
            p.LineTo(P(x0, y0 + r));
            p.ArcTo(P(x0, y0), P(x0 + r, y0), S(r));
            p.ClosePath();
            if (fill.a > 0f) { p.fillColor = fill; p.Fill(); }
            if (stroke.HasValue) { p.strokeColor = stroke.Value; p.lineWidth = width; p.Stroke(); }
        }

        private void Line(Painter2D p, Color c, float w, params float[] xy)
        {
            p.BeginPath();
            p.MoveTo(P(xy[0], xy[1]));
            for (int i = 2; i < xy.Length; i += 2) p.LineTo(P(xy[i], xy[i + 1]));
            p.strokeColor = c;
            p.lineWidth = w;
            p.Stroke();
        }

        private static Color Shade(Color c, float k) => new(c.r * k, c.g * k, c.b * k, c.a);
        private static Color Light(Color c, float t) => Color.Lerp(c, Color.white, t);
        private static Color A(Color c, float a) { c.a = a; return c; }

        // --- Liquide : plein cadre, opaque, vaguelettes ---
        private void Fluid(Painter2D p, Color c)
        {
            RoundRect(p, 0.08f, 0.08f, 0.92f, 0.92f, 0.12f, Shade(c, 0.85f), Outline, 1.2f);
            RoundRect(p, 0.08f, 0.08f, 0.92f, 0.3f, 0.12f, Light(c, 0.18f));
            var wave = A(Light(c, 0.55f), 0.55f);
            float w = S(0.045f);
            p.BeginPath();
            p.MoveTo(P(0.16f, 0.5f));
            p.BezierCurveTo(P(0.3f, 0.42f), P(0.4f, 0.58f), P(0.52f, 0.5f));
            p.BezierCurveTo(P(0.64f, 0.42f), P(0.74f, 0.58f), P(0.84f, 0.5f));
            p.strokeColor = wave; p.lineWidth = w; p.Stroke();
            p.BeginPath();
            p.MoveTo(P(0.22f, 0.72f));
            p.BezierCurveTo(P(0.34f, 0.65f), P(0.44f, 0.79f), P(0.56f, 0.72f));
            p.BezierCurveTo(P(0.66f, 0.66f), P(0.72f, 0.77f), P(0.78f, 0.72f));
            p.strokeColor = A(wave, 0.35f); p.Stroke();
            Line(p, A(Color.white, 0.5f), S(0.04f), 0.18f, 0.2f, 0.4f, 0.2f);
        }

        // --- Gaz : plein cadre, translucide, volutes et bulles ---
        private void Gas(Painter2D p, Color c)
        {
            RoundRect(p, 0.08f, 0.08f, 0.92f, 0.92f, 0.3f, A(c, 0.22f), A(Light(c, 0.4f), 0.75f), S(0.04f));
            var puff = A(Light(c, 0.3f), 0.5f);
            Circle(p, 0.38f, 0.58f, 0.2f, puff);
            Circle(p, 0.6f, 0.46f, 0.22f, puff);
            Circle(p, 0.55f, 0.68f, 0.15f, A(Light(c, 0.45f), 0.45f));
            var bubble = A(Color.white, 0.75f);
            Circle(p, 0.26f, 0.3f, 0.06f, A(Color.white, 0.12f), bubble, S(0.03f));
            Circle(p, 0.74f, 0.24f, 0.045f, A(Color.white, 0.12f), bubble, S(0.03f));
            Circle(p, 0.78f, 0.74f, 0.035f, A(Color.white, 0.12f), bubble, S(0.03f));
        }

        // --- Contenants de l'inventaire ---
        private static readonly Color Metal = new(0.55f, 0.57f, 0.64f);

        /// <summary>Remplit le bas d'un rectangle arrondi jusqu'au niveau donné.</summary>
        private void FillBottom(Painter2D p, float x0, float y0, float x1, float y1, float r, float lvl, Color color)
        {
            if (lvl <= 0.001f) return;
            if (lvl >= 0.985f) { RoundRect(p, x0, y0, x1, y1, r, color); return; }
            float yl = Mathf.Min(y1 - (y1 - y0) * lvl, y1 - r * 0.6f);
            p.BeginPath();
            p.MoveTo(P(x0, yl));
            p.LineTo(P(x1, yl));
            p.LineTo(P(x1, y1 - r));
            p.ArcTo(P(x1, y1), P(x1 - r, y1), S(r));
            p.LineTo(P(x0 + r, y1));
            p.ArcTo(P(x0, y1), P(x0, y1 - r), S(r));
            p.ClosePath();
            p.fillColor = color;
            p.Fill();
        }

        /// <summary>Bonbonne de gaz : corps arrondi en verre, col et robinet ; intérieur translucide à bulles.</summary>
        private void GasBottle(Painter2D p, Color c)
        {
            const float x0 = 0.27f, y0 = 0.25f, x1 = 0.73f, y1 = 0.93f, r = 0.17f;
            RoundRect(p, 0.43f, 0.15f, 0.57f, 0.27f, 0.02f, Shade(Metal, 0.8f), Outline, S(0.03f));
            RoundRect(p, 0.34f, 0.07f, 0.66f, 0.15f, 0.03f, Metal, Outline, S(0.03f));
            RoundRect(p, x0, y0, x1, y1, r, new Color(0.08f, 0.09f, 0.13f, 0.85f));
            float lvl = level;
            FillBottom(p, x0 + 0.03f, y0 + 0.03f, x1 - 0.03f, y1 - 0.03f, r - 0.03f, lvl, A(Light(c, 0.15f), 0.6f));
            if (lvl > 0.08f)
            {
                float top = y1 - 0.03f - (y1 - y0 - 0.06f) * lvl;
                var bubble = A(Color.white, 0.7f);
                if (lvl > 0.35f) Circle(p, 0.42f, Mathf.Max(top + 0.12f, 0.5f), 0.04f, A(Color.white, 0.1f), bubble, S(0.025f));
                Circle(p, 0.58f, 0.78f, 0.05f, A(Color.white, 0.1f), bubble, S(0.025f));
                Circle(p, 0.46f, 0.86f, 0.03f, A(Color.white, 0.1f), bubble, S(0.025f));
            }
            RoundRect(p, x0, y0, x1, y1, r, new Color(0, 0, 0, 0), A(Light(c, 0.5f), 0.9f), S(0.045f));
            Line(p, A(Color.white, 0.45f), S(0.035f), 0.35f, 0.38f, 0.35f, 0.62f);
        }

        /// <summary>Jerrican de liquide : corps rectangulaire, poignée et bec ; intérieur opaque avec une surface.</summary>
        private void Jerrycan(Painter2D p, Color c)
        {
            const float x0 = 0.18f, y0 = 0.27f, x1 = 0.82f, y1 = 0.92f, r = 0.08f;
            RoundRect(p, 0.26f, 0.12f, 0.54f, 0.3f, 0.06f, new Color(0, 0, 0, 0), Metal, S(0.06f));
            RoundRect(p, 0.62f, 0.12f, 0.76f, 0.28f, 0.02f, Shade(Metal, 0.85f), Outline, S(0.03f));
            RoundRect(p, x0, y0, x1, y1, r, new Color(0.08f, 0.09f, 0.13f, 0.9f));
            float lvl = level;
            FillBottom(p, x0 + 0.03f, y0 + 0.03f, x1 - 0.03f, y1 - 0.03f, r - 0.02f, lvl, Shade(c, 0.9f));
            if (lvl > 0.04f && lvl < 0.985f)
            {
                float yl = y1 - 0.03f - (y1 - y0 - 0.06f) * lvl;
                Line(p, A(Light(c, 0.55f), 0.8f), S(0.035f), x0 + 0.05f, yl + 0.01f, x1 - 0.05f, yl + 0.01f);
            }
            RoundRect(p, x0, y0, x1, y1, r, new Color(0, 0, 0, 0), Metal, S(0.05f));
            Line(p, A(Metal, 0.7f), S(0.03f), x0 + 0.06f, y0 + 0.08f, x1 - 0.06f, y1 - 0.08f);
        }

        // --- Énergie : éclair ---
        private void Energy(Painter2D p, Color c)
        {
            var y = Palette.Warning;
            Poly(p, y, Outline, S(0.035f), 0.58f, 0.08f, 0.22f, 0.56f, 0.46f, 0.56f, 0.38f, 0.92f, 0.78f, 0.4f, 0.54f, 0.4f);
        }

        // --- Solides ---
        private void Ore(Painter2D p, Color c)
        {
            var rock = new[] { 0.2f, 0.3f, 0.45f, 0.14f, 0.74f, 0.2f, 0.88f, 0.48f, 0.78f, 0.82f, 0.42f, 0.88f, 0.14f, 0.68f };
            Poly(p, Stone, Outline, S(0.035f), rock);
            Poly(p, Light(Stone, 0.18f), Outline, 0f, 0.2f, 0.3f, 0.45f, 0.14f, 0.74f, 0.2f, 0.5f, 0.4f);
            var ore = Light(c, 0.1f);
            Poly(p, ore, Outline, S(0.02f), 0.32f, 0.48f, 0.42f, 0.42f, 0.46f, 0.54f, 0.36f, 0.58f);
            Poly(p, ore, Outline, S(0.02f), 0.6f, 0.36f, 0.72f, 0.34f, 0.7f, 0.46f);
            Poly(p, ore, Outline, S(0.02f), 0.56f, 0.64f, 0.68f, 0.6f, 0.7f, 0.72f, 0.58f, 0.74f);
            Poly(p, ore, Outline, S(0.02f), 0.26f, 0.7f, 0.34f, 0.68f, 0.33f, 0.76f);
        }

        private void Lump(Painter2D p, Color c)
        {
            Poly(p, c, Outline, S(0.035f), 0.22f, 0.32f, 0.5f, 0.16f, 0.8f, 0.3f, 0.84f, 0.66f, 0.56f, 0.86f, 0.2f, 0.72f);
            Poly(p, Light(c, 0.25f), Outline, 0f, 0.22f, 0.32f, 0.5f, 0.16f, 0.8f, 0.3f, 0.52f, 0.46f);
            Poly(p, Shade(c, 0.7f), Outline, 0f, 0.52f, 0.46f, 0.8f, 0.3f, 0.84f, 0.66f, 0.56f, 0.86f);
        }

        private void Dust(Painter2D p, Color c)
        {
            p.BeginPath();
            p.MoveTo(P(0.1f, 0.8f));
            p.BezierCurveTo(P(0.22f, 0.62f), P(0.36f, 0.3f), P(0.5f, 0.3f));
            p.BezierCurveTo(P(0.64f, 0.3f), P(0.78f, 0.62f), P(0.9f, 0.8f));
            p.ClosePath();
            p.fillColor = c; p.Fill();
            p.strokeColor = Outline; p.lineWidth = S(0.035f); p.Stroke();
            p.BeginPath();
            p.MoveTo(P(0.3f, 0.6f));
            p.BezierCurveTo(P(0.38f, 0.44f), P(0.44f, 0.36f), P(0.5f, 0.36f));
            p.strokeColor = A(Light(c, 0.5f), 0.7f); p.lineWidth = S(0.04f); p.Stroke();
            var grain = Shade(c, 0.65f);
            Circle(p, 0.42f, 0.64f, 0.025f, grain);
            Circle(p, 0.58f, 0.56f, 0.025f, grain);
            Circle(p, 0.66f, 0.7f, 0.025f, grain);
        }

        private void Ingot(Painter2D p, Color c)
        {
            Poly(p, Shade(c, 0.78f), Outline, S(0.035f), 0.12f, 0.5f, 0.7f, 0.5f, 0.7f, 0.74f, 0.12f, 0.74f);
            Poly(p, Shade(c, 0.58f), Outline, S(0.035f), 0.7f, 0.5f, 0.88f, 0.3f, 0.88f, 0.54f, 0.7f, 0.74f);
            Poly(p, Light(c, 0.25f), Outline, S(0.035f), 0.26f, 0.3f, 0.88f, 0.3f, 0.7f, 0.5f, 0.12f, 0.5f);
            Line(p, A(Color.white, 0.6f), S(0.035f), 0.3f, 0.36f, 0.6f, 0.36f);
        }

        private void Crystal(Painter2D p, Color c)
        {
            Poly(p, c, Outline, S(0.035f), 0.5f, 0.1f, 0.78f, 0.32f, 0.78f, 0.68f, 0.5f, 0.9f, 0.22f, 0.68f, 0.22f, 0.32f);
            Poly(p, Light(c, 0.35f), Outline, 0f, 0.5f, 0.1f, 0.5f, 0.5f, 0.22f, 0.68f, 0.22f, 0.32f);
            Poly(p, Shade(c, 0.7f), Outline, 0f, 0.5f, 0.5f, 0.78f, 0.68f, 0.5f, 0.9f);
            Line(p, A(Outline, 0.5f), S(0.02f), 0.5f, 0.1f, 0.5f, 0.5f, 0.78f, 0.68f);
            Line(p, A(Outline, 0.5f), S(0.02f), 0.5f, 0.5f, 0.22f, 0.68f);
        }

        private void Pellet(Painter2D p, Color c)
        {
            Circle(p, 0.5f, 0.5f, 0.4f, A(c, 0.18f));
            Circle(p, 0.5f, 0.5f, 0.3f, c, Outline, S(0.035f));
            Circle(p, 0.5f, 0.5f, 0.18f, Light(c, 0.35f));
            Circle(p, 0.42f, 0.4f, 0.06f, A(Color.white, 0.75f));
        }

        private void Pane(Painter2D p, Color c)
        {
            RoundRect(p, 0.18f, 0.14f, 0.82f, 0.86f, 0.06f, A(Light(c, 0.3f), 0.28f), A(Light(c, 0.5f), 0.9f), S(0.04f));
            Line(p, A(Color.white, 0.7f), S(0.05f), 0.3f, 0.5f, 0.52f, 0.26f);
            Line(p, A(Color.white, 0.45f), S(0.035f), 0.4f, 0.66f, 0.68f, 0.38f);
        }

        private void Circuit(Painter2D p, Color c)
        {
            var pin = Light(Palette.Text2, 0.1f);
            for (int i = 0; i < 3; i++)
            {
                float t = 0.36f + i * 0.14f;
                Line(p, pin, S(0.045f), t, 0.14f, t, 0.86f);
                Line(p, pin, S(0.045f), 0.14f, t, 0.86f, t);
            }
            RoundRect(p, 0.24f, 0.24f, 0.76f, 0.76f, 0.06f, new Color(0.16f, 0.3f, 0.22f), Outline, S(0.035f));
            RoundRect(p, 0.38f, 0.38f, 0.62f, 0.62f, 0.03f, c);
        }

        private void Machine(Painter2D p, Color c)
        {
            var body = Color.Lerp(Palette.MachineBody, c, 0.35f);
            Poly(p, Light(body, 0.3f), Outline, S(0.035f), 0.5f, 0.12f, 0.88f, 0.31f, 0.5f, 0.5f, 0.12f, 0.31f);
            Poly(p, body, Outline, S(0.035f), 0.12f, 0.31f, 0.5f, 0.5f, 0.5f, 0.9f, 0.12f, 0.71f);
            Poly(p, Shade(body, 0.7f), Outline, S(0.035f), 0.5f, 0.5f, 0.88f, 0.31f, 0.88f, 0.71f, 0.5f, 0.9f);
            Poly(p, Light(c, 0.15f), Outline, S(0.02f), 0.6f, 0.53f, 0.78f, 0.44f, 0.78f, 0.62f, 0.6f, 0.71f);
            Poly(p, A(c, 0.85f), Outline, 0f, 0.42f, 0.25f, 0.5f, 0.21f, 0.58f, 0.25f, 0.5f, 0.29f);
        }

        private void Transmitter(Painter2D p, Color c)
        {
            RoundRect(p, 0.14f, 0.38f, 0.86f, 0.62f, 0.04f, c, Outline, S(0.035f));
            Line(p, A(Color.white, 0.45f), S(0.04f), 0.24f, 0.44f, 0.76f, 0.44f);
            RoundRect(p, 0.08f, 0.3f, 0.22f, 0.7f, 0.04f, Shade(c, 0.75f), Outline, S(0.035f));
            RoundRect(p, 0.78f, 0.3f, 0.92f, 0.7f, 0.04f, Shade(c, 0.75f), Outline, S(0.035f));
        }

        private void Upgrade(Painter2D p, Color c)
        {
            RoundRect(p, 0.16f, 0.16f, 0.84f, 0.84f, 0.18f, Shade(c, 0.45f), Light(c, 0.2f), S(0.045f));
            Line(p, Light(c, 0.3f), S(0.08f), 0.32f, 0.5f, 0.5f, 0.34f, 0.68f, 0.5f);
            Line(p, Light(c, 0.3f), S(0.08f), 0.32f, 0.7f, 0.5f, 0.54f, 0.68f, 0.7f);
        }
    }

    /// <summary>Emplacement « à la JEI » : un carré sombre avec l'icône et, éventuellement, une quantité en coin.
    /// Le nom s'affiche au survol. R / U sur un emplacement survolé ouvrent sa recette / ses usages.</summary>
    public static class IconSlot
    {
        /// <summary>Texte des raccourcis R / U, fourni par l'interface (les touches sont configurables).</summary>
        public static Func<string> KeyHint = () => "R : recette · U : usages";

        public static VisualElement Create(ResourceDef r, VisualElement parent, string amount = null, string classes = null,
            Func<string> extra = null)
        {
            var slot = Ui.El("rslot " + classes, parent);
            var icon = new ResourceIcon(r);
            slot.Add(icon);
            var count = Ui.Text(amount ?? "", "rslot__count", slot);
            count.pickingMode = PickingMode.Ignore;
            Ui.SetVisible(count, !string.IsNullOrEmpty(amount));
            Bind(slot, () => r, extra);
            return slot;
        }

        /// <summary>Rend un élément « survolable » : infobulle de la ressource et cible des touches R / U.</summary>
        public static void Bind(VisualElement e, Func<ResourceDef> resource, Func<string> extra = null, (string, string) whenEmpty = default)
        {
            e.userData = resource;
            Tooltips.Register(e, () =>
            {
                var r = resource();
                if (r == null) return whenEmpty;
                return (r.DisplayName, Describe(r, extra?.Invoke()));
            });
        }

        public static string Describe(ResourceDef r, string extra = null)
        {
            string kind = r.Kind switch
            {
                ResourceKind.Fluid => "Liquide",
                ResourceKind.Gas => "Gaz",
                ResourceKind.Energy => "Énergie",
                _ => "Objet",
            };
            string text = kind + (r.IsBaseResource ? " · ressource de base (vault)" : "");
            if (!string.IsNullOrEmpty(extra)) text += "\n" + extra;
            return text + "\n" + KeyHint();
        }

        /// <summary>Ressource sous un élément (en remontant ses parents), ou null.</summary>
        public static ResourceDef ResourceUnder(VisualElement e)
        {
            for (; e != null; e = e.parent)
                if (e.userData is Func<ResourceDef> f) return f();
            return null;
        }
    }
}
