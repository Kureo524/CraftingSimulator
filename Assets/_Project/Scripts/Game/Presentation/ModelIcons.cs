using System.Collections.Generic;
using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nucleaire.Game
{
    /// <summary>Icônes des objets posables (machines, blocs de multi-bloc, trieur) : leur modèle 3D, rendu une fois
    /// hors champ sous le même angle que la caméra de jeu. L'icône de l'inventaire est donc exactement l'objet posé.</summary>
    public static class ModelIcons
    {
        /// <summary>Calque réservé aux modèles d'icônes : seule la caméra d'icônes le voit.</summary>
        public const int Layer = 31;
        private const int Size = 192;
        private static readonly Vector3 Stage = new(0f, -500f, 0f);

        private static readonly Dictionary<ResourceDef, Texture2D> cache = new();
        private static RecipeDatabase db;
        private static Camera cam;
        private static RenderTexture rt;

        public static void Init(RecipeDatabase database, Camera mainCamera)
        {
            db = database;
            if (mainCamera != null) mainCamera.cullingMask &= ~(1 << Layer);
        }

        /// <summary>Icône de la ressource si c'est un objet posable dessiné en 3D, sinon null (icône vectorielle).</summary>
        public static Texture2D For(ResourceDef r)
        {
            if (r == null || db == null) return null;
            if (cache.TryGetValue(r, out var tex)) return tex;
            tex = Render(r);
            cache[r] = tex;
            return tex;
        }

        private static Texture2D Render(ResourceDef r)
        {
            var root = new GameObject("Icon " + r.Id).transform;
            root.position = Stage;
            switch (db.PlaceableForItem(r))
            {
                case MachineDef md:
                    // Tournée vers le sud : la caméra (45°) voit sa façade.
                    MachineView.BuildModel(root, md, Direction.South);
                    break;
                case StructureDef sd:
                    StructureView.BuildModel(root, sd);
                    break;
                default:
                    if (r == db.SorterItem)
                    {
                        var plate = MachineView.BuildSorterPlate(root, Direction.South).transform;
                        plate.localPosition = new Vector3(0f, 0.3f, 0f);
                        plate.localScale *= 1.4f;
                        break;
                    }
                    Object.DestroyImmediate(root.gameObject);
                    return null;
            }

            var renderers = root.GetComponentsInChildren<Renderer>();
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = Layer;
            foreach (var rd in renderers)
            {
                rd.shadowCastingMode = ShadowCastingMode.Off;
                // Plus clair que dans l'atelier : l'icône doit se lire sur le fond sombre d'un emplacement.
                var c = Shapes.GetColor(rd);
                if (c.a >= 0.99f) Shapes.SetColor(rd, Color.Lerp(c, Palette.Text, 0.12f) * 1.45f);
            }

            EnsureCamera();
            Frame(renderers);
            var prev = RenderTexture.active;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false)
            {
                name = "Icon " + r.Id, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp,
            };
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply(true, true);
            RenderTexture.active = prev;
            root.gameObject.SetActive(false);
            Object.Destroy(root.gameObject);
            return tex;
        }

        private static void EnsureCamera()
        {
            if (cam != null) return;
            var go = new GameObject("Icon Camera");
            Object.DontDestroyOnLoad(go);
            cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << Layer;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 30f;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            go.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Icons" };
            cam.targetTexture = rt;
        }

        /// <summary>Cadre la caméra sur le modèle : centrée, avec une petite marge.</summary>
        private static void Frame(Renderer[] renderers)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            var t = cam.transform;
            t.position = b.center - t.forward * 12f;
            float half = 0f;
            var c = b.center;
            for (int i = 0; i < 8; i++)
            {
                var corner = c + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var local = t.InverseTransformPoint(corner);
                half = Mathf.Max(half, Mathf.Abs(local.x), Mathf.Abs(local.y));
            }
            cam.orthographicSize = half * 0.98f;
        }
    }
}
