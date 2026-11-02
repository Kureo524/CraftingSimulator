using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nucleaire.Game
{
    /// <summary>Bloc de multi-bloc : cadre, verre (translucide) ou composant interne.</summary>
    public sealed class StructureView : MonoBehaviour
    {
        public void Init(StructureBlock b)
        {
            transform.localPosition = GridMath.CellCenter(b.Position);
            BuildModel(transform, b.StructureDef);
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.5f, 0);
            col.size = new Vector3(0.98f, 1f, 0.98f);
            gameObject.AddComponent<EntityRef>().Entity = b;
        }

        /// <summary>Forme d'un bloc de structure (aussi utilisée pour l'aperçu de pose).</summary>
        public static void BuildModel(Transform t, StructureDef def)
        {
            var c = Palette.Muted(def.Color, 0.2f);
            var frame = Color.Lerp(Palette.MachineBody, Palette.Text2, 0.18f);
            switch (def.Role)
            {
                case StructureRole.Frame:
                    Shapes.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.96f, 0.96f, 0.96f), Palette.MachineBody, "Body");
                    Posts(t, frame, 0.14f);
                    break;
                case StructureRole.Glass:
                {
                    var pane = Shapes.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.94f, 0.94f, 0.94f), Palette.WithAlpha(Color.Lerp(c, Color.white, 0.2f), 0.22f), "Glass");
                    var r = pane.GetComponent<Renderer>();
                    r.sharedMaterial = WorldView.TransparentMaterial;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    Posts(t, frame, 0.08f);
                    break;
                }
                case StructureRole.FuelAssembly:
                    Shapes.Box(t, new Vector3(0, 0.04f, 0), new Vector3(0.7f, 0.08f, 0.7f), Palette.MachineBodyDark, "Base");
                    for (int i = 0; i < 4; i++)
                    {
                        var p = new Vector3(i % 2 == 0 ? -0.15f : 0.15f, 0.48f, i < 2 ? -0.15f : 0.15f);
                        Shapes.Cylinder(t, p, 0.18f, 0.82f, c, "Rod");
                    }
                    Shapes.Box(t, new Vector3(0, 0.92f, 0), new Vector3(0.6f, 0.06f, 0.6f), Palette.MachineBody, "Cap");
                    break;
                case StructureRole.FusionCoil:
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * 90f;
                        var bar = Shapes.Box(t, Quaternion.Euler(0, a, 0) * new Vector3(0, 0.5f, 0.32f), new Vector3(0.74f, 0.16f, 0.12f), c, "Ring");
                        bar.localRotation = Quaternion.Euler(0, a, 0);
                    }
                    Shapes.Sphere(t, new Vector3(0, 0.5f, 0), 0.32f, Color.Lerp(c, Color.white, 0.35f), "Plasma");
                    break;
                case StructureRole.SpsCoil:
                    Shapes.Cylinder(t, new Vector3(0, 0.5f, 0), 0.36f, 0.9f, Palette.MachineBody, "Core");
                    for (int i = 0; i < 3; i++) Shapes.Cylinder(t, new Vector3(0, 0.22f + i * 0.28f, 0), 0.62f, 0.08f, c, "Winding");
                    break;
                case StructureRole.InductionCell:
                    Shapes.Box(t, new Vector3(0, 0.45f, 0), new Vector3(0.78f, 0.9f, 0.78f), Palette.MachineBody, "Cell");
                    for (int i = 0; i < 3; i++) Shapes.Box(t, new Vector3(0, 0.2f + i * 0.25f, 0), new Vector3(0.8f, 0.06f, 0.8f), c, "Stripe");
                    break;
                case StructureRole.InductionProvider:
                    Shapes.Box(t, new Vector3(0, 0.45f, 0), new Vector3(0.7f, 0.9f, 0.7f), Palette.MachineBody, "Provider");
                    Shapes.Box(t, new Vector3(0, 0.45f, 0), new Vector3(0.74f, 0.3f, 0.74f), c, "Band");
                    break;
            }
        }

        private static void Posts(Transform t, Color color, float w)
        {
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(i % 2 == 0 ? -0.48f + w / 2 : 0.48f - w / 2, 0.5f, i < 2 ? -0.48f + w / 2 : 0.48f - w / 2);
                Shapes.Box(t, p, new Vector3(w, 0.98f, w), color, "Post");
            }
            Shapes.Box(t, new Vector3(0, 0.98f - w / 4, 0), new Vector3(0.98f, w / 2, 0.98f), color, "Rim");
        }
    }
}
