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
        private Color color;
        private float thickness, height;

        public TransmitterSegment Segment => segment;

        public void Init(TransmitterSegment seg, World w)
        {
            segment = seg;
            world = w;
            transform.localPosition = GridMath.CellCenter(seg.Position);
            color = Palette.Muted(seg.TransmitterDef.Color, 0.25f);
            (thickness, height) = seg.TransmitterDef.Carries switch
            {
                ResourceKind.Energy => (0.09f, 0.12f),
                ResourceKind.Item => (0.38f, 0.06f),
                _ => (0.17f, 0.16f),
            };

            float node = seg.TransmitterDef.Carries == ResourceKind.Item ? thickness : thickness * 1.5f;
            float nodeH = seg.TransmitterDef.Carries == ResourceKind.Item ? thickness * 0.2f : thickness * 1.5f;
            Shapes.Box(transform, Vector3.up * height, new Vector3(node, nodeH, node), color, "Node");
            float thick = seg.TransmitterDef.Carries == ResourceKind.Item ? thickness * 0.2f : thickness;
            foreach (var d in DirectionExtensions.Horizontal)
            {
                var dir = GridMath.DirectionVector(d);
                var arm = Shapes.Box(transform, dir * 0.25f + Vector3.up * height, Vector3.one, color, "Arm " + d);
                arm.localScale = dir.x != 0 ? new Vector3(0.5f, thick, thickness) : new Vector3(thickness, thick, 0.5f);
                arms[(int)d] = arm;
            }
            // Bras verticaux : vers l'étage du dessus et vers le sol de la case.
            float vThick = seg.TransmitterDef.Carries == ResourceKind.Item ? thickness * 0.5f : thickness;
            var up = Shapes.Box(transform, new Vector3(0, height + (GridMath.LevelHeight - height) / 2f, 0), new Vector3(vThick, GridMath.LevelHeight - height, vThick), color, "Arm Up");
            arms[(int)Direction.Up] = up;
            var down = Shapes.Box(transform, new Vector3(0, height / 2f, 0), new Vector3(vThick, height, vThick), color, "Arm Down");
            arms[(int)Direction.Down] = down;

            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.2f, 0);
            col.size = new Vector3(0.8f, 0.4f, 0.8f);
            gameObject.AddComponent<EntityRef>().Entity = seg;
            Refresh();
        }

        public void Refresh()
        {
            for (int i = 0; i < 6; i++)
            {
                var d = (Direction)i;
                var n = world.At(segment.Position + d.Offset());
                bool connected = n switch
                {
                    TransmitterSegment other => other.ConnectsTo(segment),
                    Machine m => m.Sides[d.Opposite()].Mode != SideMode.None,
                    _ => false,
                };
                arms[i].gameObject.SetActive(connected);
            }
        }
    }
}
