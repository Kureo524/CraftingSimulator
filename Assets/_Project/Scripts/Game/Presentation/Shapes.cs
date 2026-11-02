using UnityEngine;
using UnityEngine.Rendering;

namespace Nucleaire.Game
{
    /// <summary>Formes simples colorées (le sujet évalue le système, pas les graphismes).
    /// Une seule matière partagée, couleur passée par MaterialPropertyBlock.</summary>
    public static class Shapes
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock block;

        public static Material Lit { get; set; }

        public static Transform Box(Transform parent, Vector3 localPos, Vector3 size, Color color, string name = "Box") =>
            Make(PrimitiveType.Cube, parent, localPos, size, color, name);

        public static Transform Cylinder(Transform parent, Vector3 localPos, float diameter, float height, Color color, string name = "Cylinder") =>
            Make(PrimitiveType.Cylinder, parent, localPos, new Vector3(diameter, height / 2f, diameter), color, name);

        public static Transform Sphere(Transform parent, Vector3 localPos, float diameter, Color color, string name = "Sphere") =>
            Make(PrimitiveType.Sphere, parent, localPos, Vector3.one * diameter, color, name);

        private static Transform Make(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color, string name)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Lit;
            r.shadowCastingMode = ShadowCastingMode.On;
            SetColor(r, color);
            return t;
        }

        public static void SetColor(Renderer r, Color c)
        {
            block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(BaseColor, c);
            r.SetPropertyBlock(block);
        }

        public static void SetColor(Transform t, Color c) => SetColor(t.GetComponent<Renderer>(), c);
    }
}
