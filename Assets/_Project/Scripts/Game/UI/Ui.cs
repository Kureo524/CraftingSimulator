using System;
using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Petites fabriques d'éléments UI Toolkit aux classes de la DA.</summary>
    public static class Ui
    {
        public static VisualElement El(string classes = null, VisualElement parent = null, string name = null)
        {
            var e = new VisualElement();
            if (name != null) e.name = name;
            AddClasses(e, classes);
            parent?.Add(e);
            return e;
        }

        public static Label Text(string text, string classes = null, VisualElement parent = null)
        {
            var l = new Label(text);
            AddClasses(l, classes);
            parent?.Add(l);
            return l;
        }

        /// <summary>Message « système » de la DA : mono, entre crochets.</summary>
        public static Label System(string text, VisualElement parent = null, string extra = null) =>
            Text($"[{text}]", "system " + extra, parent);

        public static Button Button(string text, Action onClick, string classes = "btn", VisualElement parent = null)
        {
            var b = new Button(onClick) { text = text };
            b.ClearClassList();
            AddClasses(b, classes);
            parent?.Add(b);
            return b;
        }

        public static VisualElement Swatch(Color c, VisualElement parent = null, bool large = false)
        {
            var s = El(large ? "swatch swatch--large" : "swatch", parent);
            s.style.backgroundColor = c;
            return s;
        }

        public static VisualElement Badge(string text, string state, VisualElement parent = null)
        {
            var b = El("badge" + (state != null ? " badge--" + state : ""), parent);
            El("badge__dot", b);
            Text(text, "badge__text", b);
            return b;
        }

        public static void SetBadge(VisualElement badge, string text, string state)
        {
            foreach (var s in new[] { "success", "warning", "danger", "accent" }) badge.RemoveFromClassList("badge--" + s);
            if (state != null) badge.AddToClassList("badge--" + state);
            badge.Q<Label>().text = text;
        }

        public static VisualElement Gauge(VisualElement parent, out VisualElement fill, bool thick = false)
        {
            var g = El(thick ? "gauge gauge--thick" : "gauge", parent);
            fill = El("gauge__fill", g);
            return g;
        }

        public static void SetGauge(VisualElement fill, float ratio, string state = null)
        {
            fill.style.width = Length.Percent(Mathf.Clamp01(ratio) * 100f);
            foreach (var s in new[] { "success", "warning", "danger" }) fill.RemoveFromClassList("gauge__fill--" + s);
            if (state != null) fill.AddToClassList("gauge__fill--" + state);
        }

        public static VisualElement Section(string title, VisualElement parent, string description = null)
        {
            var s = El("glass section", parent);
            if (title != null) Text(title, "section-title", s);
            if (description != null) Text(description, "text-3 wrap", s).style.marginBottom = 6;
            return s;
        }

        public static VisualElement HelpBox(string text, string state, VisualElement parent)
        {
            var h = El("helpbox helpbox--" + state, parent);
            Text(text, "wrap", h);
            return h;
        }

        /// <summary>Pastille cliquable « ressource + quantité » (ouvre le livre de recettes).</summary>
        public static VisualElement Chip(ResourceDef r, string amount, Action onClick, VisualElement parent, string state = null)
        {
            var c = El("chip" + (state != null ? " chip--" + state : ""), parent);
            Swatch(Palette.Muted(r != null ? r.Color : Color.gray), c);
            Text(r != null ? r.DisplayName : "?", null, c);
            if (!string.IsNullOrEmpty(amount)) Text(amount, "meta", c).style.marginLeft = 6;
            if (onClick != null) c.RegisterCallback<ClickEvent>(_ => onClick());
            if (r != null) Tooltips.Register(c, () => (r.DisplayName, $"{r.Kind.DisplayName()}{(r.IsBaseResource ? " · ressource de base (vault)" : "")}\n{r.Description}"));
            return c;
        }

        public static void AddClasses(VisualElement e, string classes)
        {
            if (string.IsNullOrWhiteSpace(classes)) return;
            foreach (var c in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries)) e.AddToClassList(c);
        }

        public static void SetVisible(VisualElement e, bool visible) => e.EnableInClassList("hidden", !visible);

        // ---------------- Format ----------------

        public static string Amount(ResourceDef r, long amount) => r == null ? amount.ToString() : r.Kind switch
        {
            ResourceKind.Item => $"×{amount}",
            ResourceKind.Energy => Energy(amount),
            _ => amount >= 10000 ? $"{amount / 1000f:0.#} B" : $"{amount} mB",
        };

        public static string Energy(long j) => j >= 1_000_000 ? $"{j / 1_000_000f:0.##} MJ" : j >= 10_000 ? $"{j / 1000f:0.#} kJ" : $"{j} J";

        public static string Stack(ResourceStack s) => s.Resource == null ? "?" : $"{s.Resource.DisplayName} {Amount(s.Resource, s.Amount)}";

        public static string StatusState(MachineStatus s) => s switch
        {
            MachineStatus.Working => "success",
            MachineStatus.NoPower or MachineStatus.Meltdown => "danger",
            MachineStatus.MissingInput or MachineStatus.LowPower or MachineStatus.OutputFull or MachineStatus.Overheating => "warning",
            _ => null,
        };

        public static string Seconds(long ticks, int tps) => TimeSpan.FromSeconds(ticks / (double)tps).ToString(@"mm\:ss");
    }
}
