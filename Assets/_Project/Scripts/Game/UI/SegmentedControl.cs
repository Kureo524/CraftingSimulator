using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Contrôle segmenté de la DA : une pastille de verre glisse sous l'élément actif.</summary>
    public sealed class SegmentedControl : VisualElement
    {
        private readonly List<Button> items = new();
        private readonly VisualElement pill;
        private int selected = -1;

        public event Action<int> Changed;
        public int Selected => selected;

        public SegmentedControl(IEnumerable<string> labels, int initial = 0)
        {
            AddToClassList("segmented");
            pill = Ui.El("segmented__pill", this);
            pill.pickingMode = PickingMode.Ignore;
            int i = 0;
            foreach (var label in labels)
            {
                int index = i++;
                var b = Ui.Button(label, () => Select(index), "segmented__item", this);
                items.Add(b);
            }
            RegisterCallback<GeometryChangedEvent>(_ => MovePill());
            Select(initial, notify: false);
        }

        public void SetLabel(int index, string label) => items[index].text = label;

        public void Select(int index, bool notify = true)
        {
            if (index < 0 || index >= items.Count) return;
            bool changed = index != selected;
            selected = index;
            for (int i = 0; i < items.Count; i++) items[i].EnableInClassList("segmented__item--active", i == index);
            MovePill();
            if (changed && notify) Changed?.Invoke(index);
        }

        private void MovePill()
        {
            if (selected < 0 || selected >= items.Count) return;
            var target = items[selected].layout;
            if (float.IsNaN(target.width)) return;
            pill.style.left = target.x;
            pill.style.width = target.width;
        }
    }
}
