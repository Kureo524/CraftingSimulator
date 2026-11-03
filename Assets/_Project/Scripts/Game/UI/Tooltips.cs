using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Infobulles en verre presque opaque, affichées après un court délai au survol.</summary>
    public sealed class Tooltips
    {
        private static Tooltips instance;
        private readonly VisualElement layer;
        private readonly VisualElement box;
        private readonly Label title, body;
        private IVisualElementScheduledItem pending;

        public Tooltips(VisualElement layer)
        {
            instance = this;
            this.layer = layer;
            box = Ui.El("tooltip hidden", layer);
            box.pickingMode = PickingMode.Ignore;
            title = Ui.Text("", "bold", box);
            body = Ui.Text("", "text-2 wrap", box);
            body.style.marginTop = 2;
            // La taille n'est connue qu'après la mise en page : on se replace dès qu'elle change.
            box.RegisterCallback<GeometryChangedEvent>(_ => Place());
        }

        public static void Register(VisualElement e, Func<(string title, string body)> content)
        {
            e.RegisterCallback<PointerEnterEvent>(evt => instance?.Show(content, evt.position));
            e.RegisterCallback<PointerMoveEvent>(evt => instance?.Move(evt.position));
            e.RegisterCallback<PointerLeaveEvent>(_ => instance?.Hide());
            e.RegisterCallback<DetachFromPanelEvent>(_ => instance?.Hide());
        }

        public static void Register(VisualElement e, string t, string b = null) => Register(e, () => (t, b));

        private void Show(Func<(string, string)> content, Vector2 pos)
        {
            pending?.Pause();
            pending = layer.schedule.Execute(() =>
            {
                var (t, b) = content();
                if (string.IsNullOrEmpty(t)) return;
                title.text = t ?? "";
                body.text = b ?? "";
                Ui.SetVisible(body, !string.IsNullOrEmpty(b));
                Ui.SetVisible(box, true);
                box.BringToFront();
            }).StartingIn(350);
            Move(pos);
        }

        private Vector2 pointer;

        private void Move(Vector2 pos)
        {
            pointer = pos;
            Place();
        }

        /// <summary>À droite et sous le pointeur ; de l'autre côté s'il n'y a pas la place, et toujours dans l'écran.</summary>
        private void Place()
        {
            const float margin = 8f;
            var local = layer.WorldToLocal(pointer);
            float w = float.IsNaN(box.layout.width) || box.layout.width < 1f ? 200 : box.layout.width;
            float h = float.IsNaN(box.layout.height) || box.layout.height < 1f ? 40 : box.layout.height;
            float maxX = layer.layout.width - margin, maxY = layer.layout.height - margin;
            float x = local.x + 16, y = local.y + 18;
            if (x + w > maxX) x = local.x - w - 12;
            if (y + h > maxY) y = local.y - h - 12;
            x = Mathf.Clamp(x, margin, Mathf.Max(margin, maxX - w));
            y = Mathf.Clamp(y, margin, Mathf.Max(margin, maxY - h));
            if (!Mathf.Approximately(box.resolvedStyle.left, x)) box.style.left = x;
            if (!Mathf.Approximately(box.resolvedStyle.top, y)) box.style.top = y;
        }

        private void Hide()
        {
            pending?.Pause();
            Ui.SetVisible(box, false);
        }
    }
}
