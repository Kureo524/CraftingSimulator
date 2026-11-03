using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;

namespace Nucleaire.Game
{
    /// <summary>Représentation 3D d'une machine : corps sombre, plaque de couleur (identité), plaques de faces
    /// (bleu = entrée, lavande = sortie), et petits signes d'activité.</summary>
    public sealed class MachineView : MonoBehaviour
    {
        private const float BodyHeight = 0.55f;

        private Machine machine;
        private readonly Transform[] sidePlates = new Transform[6];
        private float topY = BodyHeight;
        private Transform accent, rotor;
        private Renderer accentRenderer;
        private Color accentColor;
        private float bob;

        public Machine Machine => machine;

        public void Init(Machine m)
        {
            machine = m;
            transform.localPosition = GridMath.CellCenter(m.Position);
            accentColor = Palette.Muted(m.MachineDef.Color);
            var model = BuildModel(transform, m.MachineDef, m.Facing);
            accent = model.accent;
            rotor = model.rotor;
            accentRenderer = accent.GetComponent<Renderer>();
            topY = ComputeTop();

            float plateOffset = PlateOffset(m.MachineDef);
            foreach (var d in DirectionExtensions.Horizontal)
            {
                var dir = GridMath.DirectionVector(d);
                // Bas de la carrosserie : les détails de façade restent visibles au-dessus.
                var plate = Shapes.Box(transform, dir * plateOffset + Vector3.up * 0.19f, Vector3.one, Palette.SideInput, "Side " + d);
                plate.localRotation = Quaternion.LookRotation(dir);
                plate.localScale = new Vector3(0.34f, 0.14f, 0.04f);
                sidePlates[(int)d] = plate;
            }
            // Haut : petite plaque sur le dessus. Bas : liseré coloré à la base.
            sidePlates[(int)Direction.Up] = Shapes.Box(transform, new Vector3(0.28f, TopAt(0.28f, 0.28f) + 0.015f, 0.28f), new Vector3(0.2f, 0.03f, 0.2f), Palette.SideInput, "Side Up");
            sidePlates[(int)Direction.Down] = Shapes.Box(transform, new Vector3(0, 0.012f, 0), new Vector3(0.94f, 0.024f, 0.94f), Palette.SideInput, "Side Down");

            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.5f, 0);
            col.size = IsFullCell(m.MachineDef) ? new Vector3(0.98f, 1f, 0.98f) : new Vector3(0.94f, 1f, 0.94f);
            gameObject.AddComponent<EntityRef>().Entity = m;
            m.Sides.Changed += RefreshSides;
            m.AttachmentsChanged += RefreshSorters;
            RefreshSides();
            RefreshSorters();
        }

        private readonly GameObject[] sorterPlates = new GameObject[6];

        /// <summary>Trieurs : plaques plates collées sur les faces, avec une flèche vers l'extérieur.</summary>
        private void RefreshSorters()
        {
            for (int i = 0; i < 6; i++)
            {
                var d = (Direction)i;
                var s = machine.SorterAt(d);
                if (s == null) { if (sorterPlates[i] != null) Destroy(sorterPlates[i]); sorterPlates[i] = null; continue; }
                if (sorterPlates[i] != null) continue;
                var plate = BuildSorterPlate(transform, d, topY);
                plate.AddComponent<SorterRef>().Sorter = s;
                var col = plate.AddComponent<BoxCollider>();
                col.size = new Vector3(1f, 1f, 2.5f);
                sorterPlates[i] = plate;
            }
        }

        /// <summary>Plaque de trieur sur une face (aussi utilisée pour l'aperçu de pose).</summary>
        public static GameObject BuildSorterPlate(Transform parent, Direction d, float topY = BodyHeight)
        {
            var go = new GameObject("Sorter " + d);
            go.transform.SetParent(parent, false);
            var t = go.transform;
            var lav = Palette.Lavender;
            if (d.IsVertical())
            {
                t.localPosition = new Vector3(0, d == Direction.Up ? topY + 0.03f : 0.03f, 0);
                t.localRotation = Quaternion.LookRotation(d == Direction.Up ? Vector3.up : Vector3.down, Vector3.forward);
            }
            else
            {
                var dir = GridMath.DirectionVector(d);
                t.localPosition = dir * 0.5f + Vector3.up * 0.27f;
                t.localRotation = Quaternion.LookRotation(dir);
            }
            t.localScale = new Vector3(0.6f, 0.46f, 0.05f);
            Shapes.Box(t, Vector3.zero, Vector3.one, Palette.MachineBodyDark, "Plate");
            Shapes.Box(t, new Vector3(0, 0, 0.55f), new Vector3(0.86f, 0.84f, 0.3f), Color.Lerp(lav, Palette.MachineBody, 0.1f), "Face");
            var a = Shapes.Box(t, new Vector3(-0.13f, 0, 0.8f), new Vector3(0.1f, 0.5f, 0.3f), Palette.Text, "Chevron L");
            a.localRotation = Quaternion.Euler(0, 0, 40f);
            var b = Shapes.Box(t, new Vector3(0.13f, 0, 0.8f), new Vector3(0.1f, 0.5f, 0.3f), Palette.Text, "Chevron R");
            b.localRotation = Quaternion.Euler(0, 0, -40f);
            return go;
        }

        private void OnDestroy()
        {
            if (machine != null) { machine.Sides.Changed -= RefreshSides; machine.AttachmentsChanged -= RefreshSorters; }
        }

        /// <summary>Blocs de multi-bloc (contrôleurs, ports) : ils occupent toute la case, comme la paroi.</summary>
        public static bool IsFullCell(MachineDef def) => def.IsMultiblockController || def.Behaviour == MachineBehaviour.MultiblockPort;

        /// <summary>Modèle 3D d'une machine (voir <see cref="MachineModels"/>), tourné selon son orientation.
        /// Sert aussi à l'aperçu de pose et aux icônes.</summary>
        public static (Transform root, Transform accent, Transform rotor) BuildModel(Transform parent, MachineDef def, Direction facing)
        {
            var root = new GameObject("Model").transform;
            root.SetParent(parent, false);
            var dir = GridMath.DirectionVector(facing.IsVertical() ? Direction.North : facing);
            root.localRotation = Quaternion.LookRotation(dir);
            var parts = MachineModels.Build(root, def);
            return (root, parts.Accent, parts.Rotor);
        }

        /// <summary>Distance du centre aux plaques de faces latérales (juste devant la carrosserie).</summary>
        private static float PlateOffset(MachineDef def) => IsFullCell(def) ? 0.5f : def.Id == "vault" ? 0.47f : 0.43f;

        private void RefreshSides()
        {
            for (int i = 0; i < 6; i++)
            {
                var s = machine.Sides[(Direction)i];
                bool visible = s.Mode != SideMode.None;
                sidePlates[i].gameObject.SetActive(visible);
                if (visible) Shapes.SetColor(sidePlates[i], s.Mode == SideMode.Input ? Palette.SideInput : Palette.SideOutput);
            }
        }

        private float ComputeTop() => TopAt(0f, 0f);

        /// <summary>Hauteur du modèle à la verticale d'un point (repère local de la case) : la plaque « dessus »
        /// et le trieur du dessus s'y posent, au lieu de flotter au niveau d'une cheminée.</summary>
        private float TopAt(float x, float z)
        {
            var p = transform.TransformPoint(new Vector3(x, 0f, z));
            float top = 0f;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z) continue;
                top = Mathf.Max(top, b.max.y - transform.position.y);
            }
            return top > 0f ? top : BodyHeight;
        }

        /// <summary>Repositionne la vue après un déplacement de la machine.</summary>
        public void SyncPosition() => transform.localPosition = GridMath.CellCenter(machine.Position);

        private void Update()
        {
            bool working = machine.Status == MachineStatus.Working;
            if (rotor != null && working) rotor.Rotate(0f, 240f * Time.deltaTime, 0f, Space.Self);

            // Le réacteur rougit avec la chaleur (couleur d'état : réservée à cet usage).
            if (machine is ReactorMachine reactor)
            {
                float h = reactor.HeatRatio;
                var c = h < 0.5f ? Color.Lerp(accentColor, Palette.Warning, h * 2f) : Color.Lerp(Palette.Warning, Palette.Danger, (h - 0.5f) * 2f);
                Shapes.SetColor(accentRenderer, c);
            }
            else if (working)
            {
                bob += Time.deltaTime * 3f;
                Shapes.SetColor(accentRenderer, Color.Lerp(accentColor, Palette.Text, 0.12f + 0.12f * Mathf.Sin(bob)));
            }
            else Shapes.SetColor(accentRenderer, accentColor);
        }
    }
}
