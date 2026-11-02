using System;
using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Représentations distinctes par type de contenu, pour que le joueur ne confonde jamais :
    /// objet = case carrée avec quantité ; liquide = cuve pleine ; gaz = réservoir arrondi à bulles ; énergie = barre fine.</summary>
    public static class SlotWidgets
    {
        private static Texture2D bubbles;

        /// <summary>Case d'objet. Clic : action (prendre). Renvoie la fonction de mise à jour.</summary>
        public static Action ItemSlot(VisualElement parent, StorageSlot slot, Func<int> capacity, Action<ResourceDef> onClick, string caption = null)
        {
            var root = Ui.El("islot-wrap", parent);
            var box = Ui.El("islot", root);
            var swatch = Ui.El("islot__fill", box);
            var count = Ui.Text("", "number--small islot__count", box);
            var name = Ui.Text("", "meta islot__name", root);
            if (caption != null) Ui.Text(caption, "meta islot__caption", root);
            box.RegisterCallback<ClickEvent>(_ => { if (!slot.IsEmpty) onClick?.Invoke(slot.Resource); });
            Tooltips.Register(box, () => slot.IsEmpty
                ? ("Emplacement d'objet vide", "Dépose un objet depuis l'inventaire (liste plus bas), ou relie une face d'entrée.")
                : (slot.Resource.DisplayName, $"×{slot.Amount} / {capacity()}\nClic : récupérer dans l'inventaire."));
            return () =>
            {
                bool empty = slot.IsEmpty;
                box.EnableInClassList("islot--empty", empty);
                swatch.style.backgroundColor = empty ? new StyleColor(Color.clear) : Palette.WithAlpha(Palette.Muted(slot.Resource.Color), 0.55f);
                count.text = empty ? "" : slot.Amount.ToString();
                name.text = empty ? "Vide" : slot.Resource.DisplayName;
            };
        }

        /// <summary>Cuve (liquide) ou réservoir (gaz). Bouton « Vider » pour se débarrasser d'un contenu indésirable.</summary>
        public static Action Tank(VisualElement parent, StorageSlot slot, Func<int> capacity, Action<ResourceDef> onDump)
        {
            bool gas = slot.Kind == ResourceKind.Gas;
            var root = Ui.El("tank-wrap", parent);
            var tank = Ui.El("tank " + (gas ? "tank--gas" : "tank--fluid"), root);
            var fill = Ui.El("tank__fill", tank);
            if (gas)
            {
                var b = Ui.El("tank__bubbles", fill);
                b.style.backgroundImage = new StyleBackground(Bubbles());
            }
            else Ui.El("tank__surface", fill);
            var name = Ui.Text("", "meta tank__name", root);
            var amount = Ui.Text("", "mono tank__amount", root);
            Ui.Text(gas ? "gaz" : "liquide", "meta tank__kind", root);
            var dump = Ui.Button("Vider", () => { if (!slot.IsEmpty) onDump?.Invoke(slot.Resource); }, "btn btn--ghost btn--small tank__dump", root);
            Tooltips.Register(dump, "Vider", "Rejette tout le contenu de ce réservoir (il est perdu). Utile pour débloquer une machine.");
            Tooltips.Register(tank, () => slot.IsEmpty
                ? (gas ? "Réservoir de gaz vide" : "Cuve de liquide vide", "Se remplit par une face d'entrée reliée à un tube (gaz) ou un tuyau (liquide).")
                : (slot.Resource.DisplayName, $"{slot.Amount} / {capacity()} mB"));
            return () =>
            {
                bool empty = slot.IsEmpty;
                int cap = Mathf.Max(1, capacity());
                fill.style.height = Length.Percent(empty ? 0 : Mathf.Clamp01((float)slot.Amount / cap) * 100f);
                if (!empty)
                {
                    var c = Palette.Muted(slot.Resource.Color, 0.2f);
                    fill.style.backgroundColor = Palette.WithAlpha(c, gas ? 0.42f : 0.85f);
                }
                name.text = empty ? "Vide" : slot.Resource.DisplayName;
                amount.text = empty ? "" : $"{slot.Amount} mB";
                dump.SetEnabled(!empty);
            };
        }

        /// <summary>Barre d'énergie verticale.</summary>
        public static Action EnergyBar(VisualElement parent, Machine m)
        {
            var root = Ui.El("tank-wrap", parent);
            var bar = Ui.El("tank tank--energy", root);
            var fill = Ui.El("tank__fill", bar);
            Ui.Text("Énergie", "meta tank__name", root);
            var amount = Ui.Text("", "mono tank__amount", root);
            Tooltips.Register(bar, () => ("Énergie", $"{Ui.Energy(m.Energy)} / {Ui.Energy(m.EnergyCapacity)}\nArrive par un câble relié à une face d'entrée."));
            return () =>
            {
                float r = m.EnergyCapacity == 0 ? 0 : (float)m.Energy / m.EnergyCapacity;
                fill.style.height = Length.Percent(r * 100f);
                fill.EnableInClassList("tank__fill--empty", m.Energy == 0);
                amount.text = Ui.Energy(m.Energy);
            };
        }

        private static Texture2D Bubbles()
        {
            if (bubbles != null) return bubbles;
            const int size = 32;
            bubbles = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, name = "Bubbles" };
            var px = new Color32[size * size];
            var dots = new[] { (6f, 7f, 2.2f), (20f, 4f, 1.4f), (14f, 18f, 2.8f), (27f, 22f, 1.8f), (5f, 26f, 1.3f) };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = 0f;
                foreach (var (cx, cy, r) in dots)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    a = Mathf.Max(a, Mathf.Clamp01(r - d + 0.5f) * 0.35f);
                    if (Mathf.Abs(d - r) < 0.6f) a = Mathf.Max(a, 0.55f);
                }
                px[y * size + x] = new Color(1, 1, 1, a);
            }
            bubbles.SetPixels32(px);
            bubbles.Apply();
            return bubbles;
        }
    }
}
