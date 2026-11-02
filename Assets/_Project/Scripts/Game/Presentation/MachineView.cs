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
        private readonly Transform[] sidePlates = new Transform[4];
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
            BuildShape(m.MachineDef);

            for (int i = 0; i < 4; i++)
            {
                var d = (Direction)i;
                var dir = GridMath.DirectionVector(d);
                var plate = Shapes.Box(transform, dir * 0.43f + Vector3.up * 0.26f,
                    Vector3.one, Palette.SideInput, "Side " + d);
                plate.localRotation = Quaternion.LookRotation(dir);
                plate.localScale = new Vector3(0.34f, 0.2f, 0.04f);
                sidePlates[i] = plate;
            }
            m.Sides.Changed += RefreshSides;
            RefreshSides();
        }

        private void OnDestroy()
        {
            if (machine != null) machine.Sides.Changed -= RefreshSides;
        }

        private void BuildShape(MachineDef def)
        {
            var body = Palette.MachineBody;
            switch (def.Id)
            {
                case "solar_panel":
                    Shapes.Box(transform, new Vector3(0, 0.08f, 0), new Vector3(0.84f, 0.16f, 0.84f), Palette.MachineBodyDark, "Base");
                    accent = Shapes.Box(transform, new Vector3(0, 0.3f, 0), new Vector3(0.8f, 0.04f, 0.8f), accentColor, "Panel");
                    accent.localRotation = Quaternion.Euler(-18f, 0, 0);
                    break;
                case "turbine":
                case "electric_pump":
                    Shapes.Box(transform, new Vector3(0, 0.08f, 0), new Vector3(0.86f, 0.16f, 0.86f), Palette.MachineBodyDark, "Base");
                    Shapes.Cylinder(transform, new Vector3(0, 0.38f, 0), 0.7f, 0.46f, body, "Body");
                    accent = Shapes.Cylinder(transform, new Vector3(0, 0.63f, 0), 0.5f, 0.04f, accentColor, "Accent");
                    rotor = Shapes.Box(transform, new Vector3(0, 0.68f, 0), new Vector3(0.56f, 0.03f, 0.08f), Palette.Text2, "Rotor");
                    break;
                case "fission_reactor":
                    Shapes.Box(transform, new Vector3(0, 0.08f, 0), new Vector3(0.9f, 0.16f, 0.9f), Palette.MachineBodyDark, "Base");
                    Shapes.Cylinder(transform, new Vector3(0, 0.55f, 0), 0.78f, 0.8f, body, "Vessel");
                    accent = Shapes.Cylinder(transform, new Vector3(0, 0.97f, 0), 0.5f, 0.06f, accentColor, "Core");
                    break;
                case "supercritical_shifter":
                    Shapes.Box(transform, new Vector3(0, BodyHeight / 2f, 0), new Vector3(0.86f, BodyHeight, 0.86f), body, "Body");
                    accent = Shapes.Sphere(transform, new Vector3(0, BodyHeight + 0.18f, 0), 0.34f, accentColor, "Core");
                    break;
                case "battery":
                    Shapes.Box(transform, new Vector3(0, 0.36f, 0), new Vector3(0.6f, 0.72f, 0.6f), body, "Body");
                    accent = Shapes.Box(transform, new Vector3(0, 0.74f, 0), new Vector3(0.3f, 0.05f, 0.3f), accentColor, "Cap");
                    break;
                case "vault":
                    Shapes.Box(transform, new Vector3(0, 0.36f, 0), new Vector3(0.92f, 0.72f, 0.92f), body, "Body");
                    accent = Shapes.Box(transform, new Vector3(0, 0.73f, 0), new Vector3(0.7f, 0.03f, 0.7f), accentColor, "Lid");
                    break;
                case "workbench":
                    Shapes.Box(transform, new Vector3(0, 0.22f, 0), new Vector3(0.9f, 0.44f, 0.7f), body, "Legs");
                    accent = Shapes.Box(transform, new Vector3(0, 0.47f, 0), new Vector3(0.96f, 0.06f, 0.76f), accentColor, "Top");
                    break;
                default:
                    Shapes.Box(transform, new Vector3(0, BodyHeight / 2f, 0), new Vector3(0.84f, BodyHeight, 0.84f), body, "Body");
                    accent = Shapes.Box(transform, new Vector3(0, BodyHeight + 0.02f, 0), new Vector3(0.6f, 0.04f, 0.6f), accentColor, "Accent");
                    break;
            }
            accentRenderer = accent.GetComponent<Renderer>();
        }

        private void RefreshSides()
        {
            for (int i = 0; i < 4; i++)
            {
                var s = machine.Sides[(Direction)i];
                bool visible = s.Mode != SideMode.None;
                sidePlates[i].gameObject.SetActive(visible);
                if (visible) Shapes.SetColor(sidePlates[i], s.Mode == SideMode.Input ? Palette.SideInput : Palette.SideOutput);
            }
        }

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
