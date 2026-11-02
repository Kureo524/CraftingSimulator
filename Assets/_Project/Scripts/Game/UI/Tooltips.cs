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

        private void Move(Vector2 pos)
        {
            var local = layer.WorldToLocal(pos);
            float w = float.IsNaN(box.layout.width) ? 200 : box.layout.width;
            float h = float.IsNaN(box.layout.height) ? 40 : box.layout.height;
            float x = local.x + 16, y = local.y + 18;
            if (x + w > layer.layout.width - 8) x = local.x - w - 12;
            if (y + h > layer.layout.height - 8) y = local.y - h - 12;
            box.style.left = x;
            box.style.top = y;
        }

        private void Hide()
        {
            pending?.Pause();
            Ui.SetVisible(box, false);
        }
    }
}
