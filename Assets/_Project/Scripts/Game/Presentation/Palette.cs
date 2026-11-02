using UnityEngine;

namespace Nucleaire.Game
{
    /// <summary>Couleurs de la DA « Verre d'encre » utilisées côté monde 3D (l'UI utilise Theme.uss).</summary>
    public static class Palette
    {
        public static readonly Color Bg = Hex("0c0c12");
        public static readonly Color Text = Hex("e8eefa");
        public static readonly Color Text2 = Hex("a3a6b6");
        public static readonly Color Accent = Hex("8aa8e0");
        public static readonly Color AccentText = Hex("a9c4f0");
        public static readonly Color Lavender = Hex("a597c0");
        public static readonly Color Success = Hex("9ebd85");
        public static readonly Color Warning = Hex("e7b52f");
        public static readonly Color Danger = Hex("e0606a");

        // Monde
        public static readonly Color Floor = Hex("14141c");
        public static readonly Color FloorLine = new(232 / 255f, 238 / 255f, 250 / 255f, 0.07f);
        public static readonly Color FloorEdge = new(232 / 255f, 238 / 255f, 250 / 255f, 0.24f);
        public static readonly Color MachineBody = Hex("2a2b36");
        public static readonly Color MachineBodyDark = Hex("1d1e27");
        public static readonly Color SideInput = Accent;
        public static readonly Color SideOutput = Lavender;

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;

        /// <summary>Atténue une couleur de donnée pour qu'elle reste dans l'ambiance froide et sombre.</summary>
        public static Color Muted(Color c, float amount = 0.35f)
        {
            Color.RGBToHSV(c, out var h, out var s, out var v);
            return Color.HSVToRGB(h, s * (1f - amount), Mathf.Lerp(v, 0.7f, amount));
        }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
