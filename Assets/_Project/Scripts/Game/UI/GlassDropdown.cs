using System;
using System.Collections.Generic;
using Nucleaire.Data;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Liste déroulante de la DA (remplace le menu par défaut d'Unity) : bouton de verre, menu de verre
    /// presque opaque, élément actif marqué d'une coche d'accent, icône de ressource quand il y en a une.
    /// Le menu est assez large pour lire les noms en entier et peut s'ouvrir vers le haut.</summary>
    public sealed class GlassDropdown : VisualElement
    {
        public readonly struct Option
        {
            public readonly string Label;
            public readonly ResourceDef Icon;
            public Option(string label, ResourceDef icon = null) { Label = label; Icon = icon; }
        }

        private static VisualElement layer;
        private static GlassDropdown open;
        private static VisualElement catcher, menu;

        private readonly List<Option> options;
        private readonly bool openUp;
        private readonly Label label;
        private readonly ResourceIcon icon;

        public event Action<int> Changed;
        public int Index { get; private set; }

        /// <summary>À appeler une fois avec la couche qui accueille les menus ouverts.</summary>
        public static void Init(VisualElement popupLayer) => layer = popupLayer;

        /// <summary>Ferme le menu ouvert, s'il y en a un.</summary>
        public static bool CloseOpen()
        {
            if (open == null) return false;
            catcher?.RemoveFromHierarchy();
            menu?.RemoveFromHierarchy();
            open.RemoveFromClassList("gdrop--open");
            open = null;
            return true;
        }

        public GlassDropdown(IEnumerable<Option> options, int index = 0, bool openUp = false)
        {
            this.options = new List<Option>(options);
            this.openUp = openUp;
            AddToClassList("gdrop");
            icon = new ResourceIcon();
            icon.AddToClassList("gdrop__icon");
            Add(icon);
            label = Ui.Text("", "gdrop__label", this);
            Ui.Text(openUp ? "▴" : "▾", "gdrop__chevron", this);
            RegisterCallback<ClickEvent>(_ => { if (open == this) CloseOpen(); else Open(); });
            RegisterCallback<DetachFromPanelEvent>(_ => { if (open == this) CloseOpen(); });
            Select(index, notify: false);
        }

        public void Select(int index, bool notify = true)
        {
            if (index < 0 || index >= options.Count) return;
            bool changed = index != Index;
            Index = index;
            label.text = options[index].Label;
            icon.Resource = options[index].Icon;
            Ui.SetVisible(icon, options[index].Icon != null);
            if (changed && notify) Changed?.Invoke(index);
        }

        private void Open()
        {
            CloseOpen();
            if (layer == null || panel == null) return;
            open = this;
            AddToClassList("gdrop--open");

            catcher = Ui.El("gdrop__catcher", layer);
            catcher.pickingMode = PickingMode.Position;
            catcher.RegisterCallback<PointerDownEvent>(_ => CloseOpen());

            menu = Ui.El("gdrop__menu", layer);
            var bounds = layer.WorldToLocal(worldBound);
            menu.style.left = bounds.x;
            menu.style.minWidth = Math.Max(bounds.width, 240f);
            if (openUp) menu.style.bottom = layer.layout.height - bounds.y + 4f;
            else menu.style.top = bounds.yMax + 4f;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("gdrop__scroll");
            menu.Add(scroll);
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var item = Ui.El("row gdrop__item", scroll.contentContainer);
                item.EnableInClassList("gdrop__item--active", i == Index);
                Ui.Text(i == Index ? "✓" : "", "gdrop__check", item);
                if (options[i].Icon != null)
                {
                    var ic = new ResourceIcon(options[i].Icon);
                    ic.AddToClassList("gdrop__item-icon");
                    item.Add(ic);
                }
                Ui.Text(options[i].Label, "gdrop__item-label", item);
                item.RegisterCallback<ClickEvent>(_ => { CloseOpen(); Select(index); });
            }
            if (Index > 6) scroll.schedule.Execute(() => scroll.ScrollTo(scroll.contentContainer[Index])).StartingIn(20);
        }
    }
}
