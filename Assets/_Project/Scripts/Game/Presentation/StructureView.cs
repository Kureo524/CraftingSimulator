using System.Collections.Generic;
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
            switch (def.Role)
            {
                case StructureRole.Frame:
                    MachineModels.WallBlock(t, Palette.MachineBody, MachineModels.WallEdge);
                    // Croisillon léger en relief sur les faces latérales et le dessus.
                    foreach (var (pos, rot) in FaceCenters())
                    {
                        var plate = Shapes.Box(t, pos, new Vector3(0.5f, 0.5f, 0.02f), Color.Lerp(Palette.MachineBody, Palette.MachineBodyDark, 0.6f), "Panel");
                        plate.localRotation = rot;
                    }
                    break;
                case StructureRole.Glass:
                {
                    MachineModels.WallBlock(t, default, MachineModels.WallEdge, 0.07f, solid: false);
                    var pane = Shapes.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.9f, 0.9f, 0.9f), Palette.WithAlpha(Color.Lerp(c, Color.white, 0.2f), 0.2f), "Glass");
                    var r = pane.GetComponent<Renderer>();
                    r.sharedMaterial = WorldView.TransparentMaterial;
                    r.shadowCastingMode = ShadowCastingMode.Off;
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

        /// <summary>Centres des faces visibles d'un bloc (4 côtés et dessus), juste devant la carrosserie de 0,98.</summary>
        private static IEnumerable<(Vector3 pos, Quaternion rot)> FaceCenters()
        {
            const float d = 0.5f;
            yield return (new Vector3(0, 0.5f, d), Quaternion.identity);
            yield return (new Vector3(0, 0.5f, -d), Quaternion.identity);
            yield return (new Vector3(d, 0.5f, 0), Quaternion.Euler(0, 90f, 0));
            yield return (new Vector3(-d, 0.5f, 0), Quaternion.Euler(0, 90f, 0));
            yield return (new Vector3(0, 0.5f + d, 0), Quaternion.Euler(90f, 0, 0));
        }
    }
}
