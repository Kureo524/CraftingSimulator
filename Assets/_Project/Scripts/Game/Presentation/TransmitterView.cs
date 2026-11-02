using Nucleaire.Data;
using Nucleaire.Logistics;
using Nucleaire.Simulation;
using UnityEngine;

namespace Nucleaire.Game
{
    /// <summary>Segment de câble/tuyau/tube/convoyeur : un nœud central et des bras vers les voisins connectés
    /// (segments du même type, ou faces de machine ouvertes).</summary>
    public sealed class TransmitterView : MonoBehaviour
    {
        private TransmitterSegment segment;
        private World world;
        private readonly Transform[] arms = new Transform[6];

        public TransmitterSegment Segment => segment;

        public void Init(TransmitterSegment seg, World w)
        {
            segment = seg;
            world = w;
            transform.localPosition = GridMath.CellCenter(seg.Position);
            BuildGeometry(transform, seg.TransmitterDef, arms);

            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.2f, 0);
            col.size = new Vector3(0.8f, 0.4f, 0.8f);
            gameObject.AddComponent<EntityRef>().Entity = seg;
            Refresh();
        }

        /// <summary>Nœud central et bras (un par direction, à activer selon les connexions). Sert aussi à l'aperçu de pose.</summary>
        public static void BuildGeometry(Transform transform, TransmitterDef def, Transform[] arms)
        {
            var color = Palette.Muted(def.Color, 0.25f);
            // Même forme pour tous les câbles (tube à nœud carré) ; seuls l'épaisseur et la couleur changent.
            var (thickness, height) = def.Carries switch
            {
                ResourceKind.Energy => (0.09f, 0.12f),
                _ => (0.17f, 0.16f),
            };
            Shapes.Box(transform, Vector3.up * height, Vector3.one * thickness * 1.5f, color, "Node");
            foreach (var d in DirectionExtensions.Horizontal)
            {
                var dir = GridMath.DirectionVector(d);
                var arm = Shapes.Box(transform, dir * 0.25f + Vector3.up * height, Vector3.one, color, "Arm " + d);
                arm.localScale = dir.x != 0 ? new Vector3(0.5f, thickness, thickness) : new Vector3(thickness, thickness, 0.5f);
                arms[(int)d] = arm;
            }
            // Bras verticaux : vers l'étage du dessus et vers le sol de la case.
            arms[(int)Direction.Up] = Shapes.Box(transform, new Vector3(0, height + (GridMath.LevelHeight - height) / 2f, 0), new Vector3(thickness, GridMath.LevelHeight - height, thickness), color, "Arm Up");
            arms[(int)Direction.Down] = Shapes.Box(transform, new Vector3(0, height / 2f, 0), new Vector3(thickness, height, thickness), color, "Arm Down");
        }

        private readonly Transform[] markers = new Transform[6];

        public void Refresh()
        {
            for (int i = 0; i < 6; i++)
            {
                var d = (Direction)i;
                var n = world.At(segment.Position + d.Offset());
                var cs = segment[d];
                bool connected = n switch
                {
                    TransmitterSegment other => other.ConnectsTo(segment),
                    Machine m => m.Sides[d.Opposite()].Mode != SideMode.None || cs is CableSide.Pull or CableSide.Push || m.SorterAt(d.Opposite()) != null,
                    _ => false,
                };
                arms[i].gameObject.SetActive(connected);

                // Tirer (lavande) / Pousser (bleu) : une bague au bout du bras, côté machine.
                bool marked = n is Machine && cs is CableSide.Pull or CableSide.Push;
                if (marked && markers[i] == null)
                {
                    var p = GridMath.DirectionVector(d) * 0.42f + arms[i].localPosition.y * Vector3.up;
                    if (d.IsVertical()) p = new Vector3(0, d == Direction.Up ? 0.85f : 0.05f, 0);
                    markers[i] = Shapes.Box(transform, p, Vector3.one * 0.2f, Palette.Lavender, "Marker " + d);
                }
                if (markers[i] != null)
                {
                    markers[i].gameObject.SetActive(marked);
                    if (marked) Shapes.SetColor(markers[i], cs == CableSide.Pull ? Palette.Lavender : Palette.Accent);
                }
            }
        }
    }
}
