using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Reflet du curseur (DA §5) : un unique halo blanc inséré dans le panneau de verre le plus
    /// imbriqué sous le pointeur, derrière son contenu, découpé par overflow: hidden.</summary>
    public sealed class GlassLight
    {
        private const int Size = 360;
        private readonly VisualElement light;
        private VisualElement host;

        public GlassLight(VisualElement root)
        {
            light = new VisualElement { pickingMode = PickingMode.Ignore, name = "glass-light" };
            light.AddToClassList("glass-light");
            light.style.backgroundImage = new StyleBackground(BuildTexture());
            root.RegisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerLeaveEvent>(_ => Hide());
        }

        private void OnMove(PointerMoveEvent evt)
        {
            var glass = FindGlass(evt.target as VisualElement);
            if (glass == null) { Hide(); return; }
            if (glass != host)
            {
                host = glass;
                glass.Insert(0, light);
            }
            Vector2 local = glass.WorldToLocal(evt.position);
            light.style.left = local.x - Size / 2f;
            light.style.top = local.y - Size / 2f;
            light.AddToClassList("glass-light--visible");
        }

        private void Hide() => light.RemoveFromClassList("glass-light--visible");

        private static VisualElement FindGlass(VisualElement e)
        {
            for (; e != null; e = e.parent)
                if (e.ClassListContains("glass")) return e;
            return null;
        }

        private static Texture2D BuildTexture()
        {
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "GlassLight" };
            var px = new Color32[128 * 128];
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(64, 64)) / 64f);
                float a = 0.09f * (1f - d) * (1f - d);
                px[y * 128 + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
