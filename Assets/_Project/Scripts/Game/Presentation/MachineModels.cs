using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;

namespace Nucleaire.Game
{
    /// <summary>Modèles 3D des machines, en formes simples (boîtes, cylindres, sphères) mais avec une silhouette
    /// propre à chacune : on reconnaît une machine à sa forme avant sa couleur. Repère local : +Z = l'avant,
    /// emprise ≤ 0,9 case ; entre 0,16 et 0,36 de haut, le corps fait 0,84 de large pour porter les plaques de faces.
    /// Les mêmes modèles servent à l'aperçu de pose et aux icônes de l'inventaire.</summary>
    public static class MachineModels
    {
        /// <summary>Pièces utiles à l'animation : <c>accent</c> prend la couleur d'identité (et pulse en marche),
        /// <c>rotor</c> tourne sur son axe Y local quand la machine travaille.</summary>
        public struct Parts
        {
            public Transform Accent, Rotor;
        }

        private static Color body, dark, metal, tint, accent, glow;

        public static Parts Build(Transform t, MachineDef def)
        {
            accent = Palette.Muted(def.Color);
            body = Palette.MachineBody;
            dark = Palette.MachineBodyDark;
            metal = Color.Lerp(Palette.MachineBody, Palette.Text2, 0.38f);
            tint = Color.Lerp(body, accent, 0.45f);
            glow = Color.Lerp(accent, Palette.Text, 0.25f);

            if (def.IsMultiblockController) return Controller(t, def);
            if (def.Behaviour == MachineBehaviour.MultiblockPort) return Port(t);
            // Hors paroi de multi-bloc, la carrosserie elle-même prend une pointe de la couleur d'identité.
            body = Color.Lerp(Palette.MachineBody, accent, 0.16f);
            return def.Id switch
            {
                "electric_furnace" => Furnace(t),
                "crusher" => Crusher(t),
                "electrolyser" => Electrolyser(t),
                "chemical_infuser" => Infuser(t),
                "chemical_oxidizer" => Oxidizer(t),
                "coal_generator" => CoalGenerator(t),
                "gas_generator" => GasGenerator(t),
                "crystallizer" => Crystallizer(t),
                "dissolution_chamber" => Dissolution(t),
                "electric_pump" => Pump(t),
                "enrichment_chamber" => Enrichment(t),
                "isotopic_centrifuge" => Centrifuge(t),
                "neutron_activator" => Activator(t),
                "solar_panel" => Solar(t),
                "turbine" => Turbine(t),
                "vault" => Vault(t),
                "vent" => Vent(t),
                _ => Generic(t),
            };
        }

        // ---------------- Briques ----------------

        private static Transform Box(Transform t, float x, float y, float z, float sx, float sy, float sz, Color c, string n = "Box") =>
            Shapes.Box(t, new Vector3(x, y, z), new Vector3(sx, sy, sz), c, n);

        private static Transform Cyl(Transform t, float x, float y, float z, float d, float h, Color c, string n = "Cylinder") =>
            Shapes.Cylinder(t, new Vector3(x, y, z), d, h, c, n);

        /// <summary>Cylindre couché le long de X (ou de Z si <paramref name="alongZ"/>).</summary>
        private static Transform CylLying(Transform t, float x, float y, float z, float d, float len, Color c, bool alongZ = false, string n = "Cylinder")
        {
            var cyl = Shapes.Cylinder(t, new Vector3(x, y, z), d, len, c, n);
            cyl.localRotation = alongZ ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(0f, 0f, 90f);
            return cyl;
        }

        private static void Plinth(Transform t, float size = 0.9f, float h = 0.1f) => Box(t, 0, h / 2f, 0, size, h, size, dark, "Plinth");

        /// <summary>Petit voyant en façade.</summary>
        private static void Led(Transform t, float x, float y, Color c) => Box(t, x, y, 0.425f, 0.06f, 0.06f, 0.02f, c, "Led");

        // ---------------- Machines ----------------

        /// <summary>Four : bloc de briques, gueule rougeoyante à l'avant, cheminée à l'arrière.</summary>
        private static Parts Furnace(Transform t)
        {
            Plinth(t);
            Box(t, 0, 0.36f, 0, 0.84f, 0.52f, 0.84f, tint, "Body");
            Box(t, 0, 0.63f, 0, 0.88f, 0.06f, 0.88f, dark, "Cap");
            for (int i = 0; i < 3; i++) Box(t, 0, 0.18f + i * 0.14f, -0.425f, 0.84f, 0.02f, 0.02f, dark, "Brick line");
            Box(t, 0, 0.43f, 0.41f, 0.5f, 0.26f, 0.06f, dark, "Mouth");
            var fire = Box(t, 0, 0.41f, 0.435f, 0.38f, 0.16f, 0.02f, glow, "Fire");
            Cyl(t, -0.24f, 0.82f, -0.22f, 0.18f, 0.32f, metal, "Chimney");
            Cyl(t, -0.24f, 0.99f, -0.22f, 0.22f, 0.02f, dark, "Chimney cap");
            return new Parts { Accent = fire };
        }

        /// <summary>Broyeur : trémie évasée sur le dessus, deux mâchoires (rouleaux) qui tournent.</summary>
        private static Parts Crusher(Transform t)
        {
            Plinth(t);
            Box(t, 0, 0.3f, 0, 0.84f, 0.4f, 0.84f, body, "Body");
            Box(t, 0, 0.52f, 0, 0.66f, 0.06f, 0.66f, dark, "Throat");
            Box(t, 0, 0.6f, 0, 0.78f, 0.1f, 0.78f, tint, "Hopper 1");
            Box(t, 0, 0.7f, 0, 0.9f, 0.1f, 0.9f, tint, "Hopper 2");
            Box(t, 0, 0.705f, 0, 0.7f, 0.1f, 0.7f, dark, "Hopper hole");
            var rollers = new GameObject("Rollers").transform;
            rollers.SetParent(t, false);
            rollers.localPosition = new Vector3(0, 0.74f, 0);
            CylLying(rollers, 0, 0, -0.13f, 0.2f, 0.6f, metal, n: "Roller A");
            CylLying(rollers, 0, 0, 0.13f, 0.2f, 0.6f, metal, n: "Roller B");
            var stripe = Box(t, 0, 0.31f, 0.425f, 0.84f, 0.06f, 0.02f, Palette.Warning, "Hazard");
            Shapes.SetColor(stripe, Color.Lerp(Palette.Warning, body, 0.35f));
            var label = Box(t, 0, 0.42f, 0.425f, 0.4f, 0.1f, 0.02f, accent, "Plate");
            return new Parts { Accent = label };
        }

        /// <summary>Électrolyseur : cuve basse et deux colonnes d'électrodes reliées par un tube.</summary>
        private static Parts Electrolyser(Transform t)
        {
            Plinth(t);
            Box(t, 0, 0.26f, 0, 0.84f, 0.32f, 0.84f, body, "Tank");
            var bath = Box(t, 0, 0.43f, 0, 0.72f, 0.03f, 0.72f, glow, "Bath");
            Cyl(t, -0.2f, 0.66f, -0.05f, 0.24f, 0.46f, Color.Lerp(metal, accent, 0.2f), "Column A");
            Cyl(t, 0.2f, 0.66f, -0.05f, 0.24f, 0.46f, Color.Lerp(metal, accent, 0.2f), "Column B");
            Cyl(t, -0.2f, 0.9f, -0.05f, 0.18f, 0.04f, Palette.Text2, "Cap A");
            Cyl(t, 0.2f, 0.9f, -0.05f, 0.18f, 0.04f, Palette.Text2, "Cap B");
            CylLying(t, 0, 0.82f, -0.05f, 0.07f, 0.4f, metal, n: "Bridge");
            Box(t, -0.2f, 0.62f, 0.07f, 0.04f, 0.3f, 0.02f, Palette.Danger, "Anode");
            Box(t, 0.2f, 0.62f, 0.07f, 0.04f, 0.3f, 0.02f, Palette.Accent, "Cathode");
            return new Parts { Accent = bath };
        }

        /// <summary>Infuseur chimique : ballon sphérique central, deux petits réservoirs d'injection.</summary>
        private static Parts Infuser(Transform t)
        {
            Plinth(t);
            Box(t, 0, 0.27f, 0, 0.84f, 0.34f, 0.84f, body, "Body");
            Cyl(t, 0, 0.47f, 0, 0.52f, 0.06f, dark, "Seat");
            var flask = Shapes.Sphere(t, new Vector3(0, 0.68f, 0), 0.46f, accent, "Flask");
            Cyl(t, 0, 0.93f, 0, 0.12f, 0.1f, metal, "Neck");
            Cyl(t, -0.32f, 0.6f, -0.3f, 0.16f, 0.28f, tint, "Injector A");
            Cyl(t, 0.32f, 0.6f, -0.3f, 0.16f, 0.28f, tint, "Injector B");
            Led(t, -0.25f, 0.38f, Palette.Success);
            return new Parts { Accent = flask };
        }

        /// <summary>Oxydeur : grande cuve verticale bombée et sa colonne de ventilation.</summary>
        private static Parts Oxidizer(Transform t)
        {
            Plinth(t);
            Box(t, 0, 0.22f, 0, 0.84f, 0.24f, 0.84f, body, "Base");
            Cyl(t, -0.06f, 0.54f, -0.04f, 0.62f, 0.44f, tint, "Tank");
            var band = Cyl(t, -0.06f, 0.56f, -0.04f, 0.65f, 0.1f, accent, "Band");
            Shapes.Sphere(t, new Vector3(-0.06f, 0.76f, -0.04f), 0.48f, tint, "Dome");
            Cyl(t, 0.32f, 0.66f, 0.28f, 0.12f, 0.64f, metal, "Vent stack");
            Cyl(t, 0.32f, 0.99f, 0.28f, 0.16f, 0.02f, dark, "Vent cap");
            return new Parts { Accent = band };
        }

        /// <summary>Générateur à charbon : chaudière trapue, foyer orange à l'avant, haute cheminée.</summary>
        private static Parts CoalGenerator(Transform t)
        {
            Plinth(t);
            Box(t, 0, 0.32f, 0, 0.84f, 0.44f, 0.84f, tint, "Boiler");
            Box(t, 0, 0.56f, -0.06f, 0.7f, 0.06f, 0.6f, dark, "Top");
            Box(t, 0, 0.4f, 0.41f, 0.42f, 0.24f, 0.06f, dark, "Firebox");
            var ember = Box(t, 0, 0.37f, 0.435f, 0.32f, 0.12f, 0.02f, Color.Lerp(Palette.Warning, Palette.Danger, 0.4f), "Ember");
            for (int i = 0; i < 3; i++) Box(t, -0.1f + i * 0.1f, 0.47f, 0.435f, 0.03f, 0.08f, 0.02f, dark, "Grate");
            Cyl(t, 0.22f, 0.78f, -0.2f, 0.22f, 0.4f, dark, "Chimney");
            Cyl(t, 0.22f, 0.99f, -0.2f, 0.26f, 0.02f, metal, "Chimney cap");
            Box(t, -0.22f, 0.62f, -0.2f, 0.26f, 0.06f, 0.26f, accent, "Coal");
            return new Parts { Accent = ember };
        }

        /// <summary>Générateur à gaz : deux bouteilles couchées sur le capot, pot d'échappement.</summary>
        private static Parts GasGenerator(Transform t)
        {
            Plinth(t);
            Box(t, 0, 0.29f, 0, 0.84f, 0.38f, 0.84f, body, "Body");
            CylLying(t, 0, 0.6f, -0.18f, 0.24f, 0.74f, tint, n: "Bottle A");
            CylLying(t, 0, 0.6f, 0.14f, 0.24f, 0.74f, tint, n: "Bottle B");
            var stripe = Box(t, 0, 0.6f, -0.02f, 0.06f, 0.27f, 0.62f, accent, "Strap");
            Box(t, 0.24f, 0.37f, 0.425f, 0.2f, 0.14f, 0.02f, dark, "Grille");
            Cyl(t, 0.33f, 0.62f, 0.36f, 0.1f, 0.3f, metal, "Exhaust");
            Led(t, -0.2f, 0.38f, Palette.Accent);
            return new Parts { Accent = stripe };
        }

        /// <summary>Cristalliseur : plateau à quatre montants, gros cristal qui pointe au centre.</summary>
        private static Parts Crystallizer(Transform t)
        {
            Plinth(t);
            Box(t, 0, 0.22f, 0, 0.84f, 0.24f, 0.84f, body, "Base");
            Box(t, 0, 0.36f, 0, 0.7f, 0.04f, 0.7f, dark, "Tray");
            foreach (var (x, z) in new[] { (-0.33f, -0.33f), (0.33f, -0.33f), (-0.33f, 0.33f), (0.33f, 0.33f) })
                Box(t, x, 0.6f, z, 0.06f, 0.48f, 0.06f, metal, "Post");
            Box(t, 0, 0.86f, 0, 0.76f, 0.05f, 0.76f, tint, "Roof");
            var crystal = Box(t, 0, 0.6f, 0, 0.24f, 0.36f, 0.24f, glow, "Crystal");
            crystal.localRotation = Quaternion.Euler(0, 45f, 0);
            var small = Box(t, 0.16f, 0.46f, 0.1f, 0.1f, 0.16f, 0.1f, accent, "Shard");
            small.localRotation = Quaternion.Euler(0, 20f, 18f);
            return new Parts { Accent = crystal };
        }

        /// <summary>Chambre de dissolution : cuve ronde trapue, moteur d'agitation qui tourne sur le couvercle.</summary>
        private static Parts Dissolution(Transform t)
        {
            Plinth(t);
            Box(t, 0, 0.17f, 0, 0.84f, 0.14f, 0.84f, body, "Skid");
            Cyl(t, 0, 0.44f, 0, 0.8f, 0.42f, tint, "Vat");
            var lid = Cyl(t, 0, 0.67f, 0, 0.84f, 0.05f, accent, "Lid");
            Cyl(t, 0, 0.77f, 0, 0.24f, 0.16f, metal, "Motor");
            var rotor = Box(t, 0, 0.88f, 0, 0.5f, 0.03f, 0.06f, Palette.Text2, "Stirrer");
            Box(t, 0, 0.44f, 0.4f, 0.16f, 0.14f, 0.04f, dark, "Hatch");
            return new Parts { Accent = lid, Rotor = rotor };
        }

        /// <summary>Pompe : moteur couché, tuyau d'aspiration qui plonge dans le sol, ventilateur.</summary>
        private static Parts Pump(Transform t)
        {
            Plinth(t, 0.86f, 0.12f);
            Box(t, 0, 0.24f, 0, 0.84f, 0.2f, 0.84f, body, "Base");
            CylLying(t, 0, 0.5f, -0.04f, 0.4f, 0.64f, tint, alongZ: true, n: "Motor");
            var ring = CylLying(t, 0, 0.5f, 0.12f, 0.44f, 0.06f, accent, alongZ: true, n: "Ring");
            Cyl(t, 0.3f, 0.5f, 0.28f, 0.14f, 0.6f, metal, "Riser");
            CylLying(t, 0, 0.5f, 0.31f, 0.3f, 0.06f, dark, alongZ: true, n: "Fan guard");
            var fan = new GameObject("Fan").transform;
            fan.SetParent(t, false);
            fan.localPosition = new Vector3(0, 0.5f, 0.36f);
            fan.localRotation = Quaternion.Euler(90f, 0, 0);
            Box(fan, 0, 0, 0, 0.32f, 0.02f, 0.06f, Palette.Text2, "Blade A");
            Box(fan, 0, 0, 0, 0.06f, 0.02f, 0.32f, Palette.Text2, "Blade B");
            return new Parts { Accent = ring, Rotor = fan };
        }

        /// <summary>Chambre d'enrichissement : colonne haute et carrée, cerclée de trois bandes, hublot.</summary>
        private static Parts Enrichment(Transform t)
        {
            Plinth(t);
            Box(t, 0, 0.3f, 0, 0.84f, 0.4f, 0.84f, body, "Base");
            Box(t, 0, 0.72f, -0.05f, 0.56f, 0.46f, 0.56f, tint, "Column");
            Transform first = null;
            for (int i = 0; i < 3; i++)
            {
                var b = Box(t, 0, 0.56f + i * 0.14f, -0.05f, 0.6f, 0.04f, 0.6f, accent, "Band");
                first ??= b;
            }
            Box(t, 0, 0.97f, -0.05f, 0.4f, 0.04f, 0.4f, metal, "Cap");
            Cyl(t, 0, 0.38f, 0.41f, 0.18f, 0.03f, dark, "Port").localRotation = Quaternion.Euler(90f, 0, 0);
            return new Parts { Accent = first };
        }

        /// <summary>Centrifugeuse : haut cylindre fin dont le chapeau tourne, sur un socle carré.</summary>
        private static Parts Centrifuge(Transform t)
        {
            Plinth(t);
            Box(t, 0, 0.22f, 0, 0.84f, 0.24f, 0.84f, body, "Base");
            Cyl(t, 0, 0.62f, 0, 0.44f, 0.6f, tint, "Drum");
            var band = Cyl(t, 0, 0.5f, 0, 0.47f, 0.06f, accent, "Band");
            var rotor = new GameObject("Head").transform;
            rotor.SetParent(t, false);
            rotor.localPosition = new Vector3(0, 0.93f, 0);
            Cyl(rotor, 0, 0, 0, 0.36f, 0.08f, metal, "Cap");
            Box(rotor, 0, 0.05f, 0, 0.44f, 0.03f, 0.05f, Palette.Text2, "Bar");
            Led(t, 0.28f, 0.3f, Palette.Success);
            return new Parts { Accent = band, Rotor = rotor };
        }

        /// <summary>Activateur à neutrons : cœur sphérique lumineux dans un anneau de quatre aimants.</summary>
        private static Parts Activator(Transform t)
        {
            Plinth(t);
            Box(t, 0, 0.27f, 0, 0.84f, 0.34f, 0.84f, body, "Base");
            Cyl(t, 0, 0.46f, 0, 0.6f, 0.04f, dark, "Ring base");
            var core = Shapes.Sphere(t, new Vector3(0, 0.66f, 0), 0.3f, glow, "Core");
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var m = Box(t, 0, 0.66f, 0, 0.12f, 0.36f, 0.12f, tint, "Magnet");
                m.localPosition = Quaternion.Euler(0, a, 0) * new Vector3(0, 0, 0.3f) + Vector3.up * 0.66f;
                m.localRotation = Quaternion.Euler(0, a, 0);
            }
            Box(t, 0, 0.38f, 0.425f, 0.3f, 0.08f, 0.02f, accent, "Plate");
            return new Parts { Accent = core };
        }

        /// <summary>Panneau solaire : poteau et panneau incliné quadrillé.</summary>
        private static Parts Solar(Transform t)
        {
            Box(t, 0, 0.06f, 0, 0.84f, 0.12f, 0.84f, dark, "Base");
            Box(t, 0, 0.24f, 0, 0.84f, 0.12f, 0.84f, body, "Frame");
            Box(t, 0, 0.38f, -0.1f, 0.1f, 0.2f, 0.1f, metal, "Pole");
            var panel = new GameObject("Panel").transform;
            panel.SetParent(t, false);
            panel.localPosition = new Vector3(0, 0.52f, 0);
            panel.localRotation = Quaternion.Euler(-24f, 0, 0);
            var cells = Box(panel, 0, 0, 0, 0.86f, 0.04f, 0.86f, accent, "Cells");
            for (int i = -1; i <= 1; i++)
            {
                Box(panel, i * 0.215f, 0.022f, 0, 0.02f, 0.01f, 0.86f, Palette.Text2, "Grid");
                Box(panel, 0, 0.022f, i * 0.215f, 0.86f, 0.01f, 0.02f, Palette.Text2, "Grid");
            }
            Box(panel, 0, -0.03f, 0, 0.9f, 0.02f, 0.9f, metal, "Rim");
            return new Parts { Accent = cells };
        }

        /// <summary>Turbine : carter cylindrique haut, hélice à trois pales qui tourne.</summary>
        private static Parts Turbine(Transform t)
        {
            Plinth(t);
            Box(t, 0, 0.2f, 0, 0.84f, 0.2f, 0.84f, body, "Base");
            Cyl(t, 0, 0.56f, 0, 0.72f, 0.52f, tint, "Casing");
            var band = Cyl(t, 0, 0.4f, 0, 0.75f, 0.06f, accent, "Band");
            Cyl(t, 0, 0.83f, 0, 0.6f, 0.02f, dark, "Throat");
            var rotor = new GameObject("Rotor").transform;
            rotor.SetParent(t, false);
            rotor.localPosition = new Vector3(0, 0.87f, 0);
            Cyl(rotor, 0, 0, 0, 0.12f, 0.08f, metal, "Hub");
            for (int i = 0; i < 3; i++)
            {
                var blade = Box(rotor, 0, 0, 0, 0.1f, 0.02f, 0.3f, Palette.Text2, "Blade");
                blade.localPosition = Quaternion.Euler(0, i * 120f, 0) * new Vector3(0, 0, 0.16f);
                blade.localRotation = Quaternion.Euler(0, i * 120f, 15f);
            }
            return new Parts { Accent = band, Rotor = rotor };
        }

        /// <summary>Vault : coffre-fort massif, porte ronde à volant à l'avant.</summary>
        private static Parts Vault(Transform t)
        {
            Box(t, 0, 0.4f, 0, 0.9f, 0.8f, 0.9f, tint, "Body");
            Box(t, 0, 0.81f, 0, 0.92f, 0.04f, 0.92f, dark, "Lid");
            foreach (var x in new[] { -0.44f, 0.44f })
            foreach (var z in new[] { -0.44f, 0.44f })
                Box(t, x, 0.4f, z, 0.06f, 0.8f, 0.06f, metal, "Corner");
            var door = Cyl(t, 0, 0.48f, 0.45f, 0.5f, 0.03f, accent, "Door");
            door.localRotation = Quaternion.Euler(90f, 0, 0);
            Cyl(t, 0, 0.48f, 0.47f, 0.16f, 0.03f, metal, "Hub").localRotation = Quaternion.Euler(90f, 0, 0);
            Box(t, 0, 0.48f, 0.48f, 0.38f, 0.04f, 0.02f, Palette.Text2, "Spoke");
            Box(t, 0, 0.48f, 0.48f, 0.04f, 0.38f, 0.02f, Palette.Text2, "Spoke");
            return new Parts { Accent = door };
        }

        /// <summary>Évent : caisson bas avec une grille et un ventilateur sur le dessus.</summary>
        private static Parts Vent(Transform t)
        {
            Plinth(t);
            Box(t, 0, 0.27f, 0, 0.84f, 0.34f, 0.84f, body, "Body");
            Cyl(t, 0, 0.47f, 0, 0.7f, 0.06f, tint, "Collar");
            var fan = new GameObject("Fan").transform;
            fan.SetParent(t, false);
            fan.localPosition = new Vector3(0, 0.52f, 0);
            for (int i = 0; i < 4; i++)
            {
                var b = Box(fan, 0, 0, 0, 0.1f, 0.02f, 0.28f, Palette.Text2, "Blade");
                b.localPosition = Quaternion.Euler(0, i * 90f, 0) * new Vector3(0, 0, 0.15f);
                b.localRotation = Quaternion.Euler(0, i * 90f, 20f);
            }
            var grille = new Transform[5];
            for (int i = 0; i < 5; i++) grille[i] = Box(t, -0.24f + i * 0.12f, 0.57f, 0, 0.03f, 0.03f, 0.66f, accent, "Grille");
            return new Parts { Accent = grille[2], Rotor = fan };
        }

        private static Parts Generic(Transform t)
        {
            Plinth(t);
            Box(t, 0, 0.34f, 0, 0.84f, 0.48f, 0.84f, tint, "Body");
            var a = Box(t, 0, 0.6f, -0.06f, 0.6f, 0.04f, 0.48f, accent, "Accent");
            Box(t, 0, 0.44f, 0.425f, 0.52f, 0.07f, 0.02f, Palette.Text2, "Front");
            return new Parts { Accent = a };
        }

        // ---------------- Multi-blocs ----------------

        /// <summary>Cadre de bloc de paroi : un cube plein de 0,98 et douze arêtes en saillie qui ne se chevauchent pas
        /// (aucune face superposée à une autre, donc pas de scintillement).</summary>
        public static void WallBlock(Transform t, Color bodyColor, Color edgeColor, float edge = 0.09f, bool solid = true)
        {
            if (solid) Shapes.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.98f, 0.98f, 0.98f), bodyColor, "Body");
            float o = 0.5f - edge / 2f, inner = 1f - 2f * edge;
            foreach (var x in new[] { -o, o })
            foreach (var z in new[] { -o, o })
                Shapes.Box(t, new Vector3(x, 0.5f, z), new Vector3(edge, 1f, edge), edgeColor, "Edge");
            foreach (var y in new[] { edge / 2f, 1f - edge / 2f })
            foreach (var s in new[] { -o, o })
            {
                Shapes.Box(t, new Vector3(0, y, s), new Vector3(inner, edge, edge), edgeColor, "Edge");
                Shapes.Box(t, new Vector3(s, y, 0), new Vector3(edge, edge, inner), edgeColor, "Edge");
            }
        }

        public static Color WallEdge => Color.Lerp(Palette.MachineBody, Palette.Text2, 0.22f);

        /// <summary>Contrôleur : bloc de paroi avec un écran à l'avant et, sur le dessus, le signe de son type.</summary>
        private static Parts Controller(Transform t, MachineDef def)
        {
            WallBlock(t, body, Color.Lerp(WallEdge, accent, 0.35f));
            Box(t, 0, 0.52f, 0.495f, 0.62f, 0.5f, 0.01f, dark, "Screen frame");
            var screen = Box(t, 0, 0.55f, 0.5f, 0.5f, 0.32f, 0.01f, accent, "Screen");
            for (int i = 0; i < 3; i++) Box(t, -0.12f + i * 0.12f, 0.33f, 0.5f, 0.06f, 0.04f, 0.01f, i == 0 ? Palette.Success : metal, "Button");
            switch (def.Behaviour)
            {
                case MachineBehaviour.Reactor:
                    foreach (var (x, z) in new[] { (-0.17f, -0.17f), (0.17f, -0.17f), (-0.17f, 0.17f), (0.17f, 0.17f) })
                        Cyl(t, x, 1.0f, z, 0.16f, 0.04f, accent, "Rod cap");
                    break;
                case MachineBehaviour.FusionReactor:
                    Cyl(t, 0, 1.0f, 0, 0.56f, 0.03f, accent, "Torus");
                    Cyl(t, 0, 1.005f, 0, 0.36f, 0.03f, dark, "Torus hole");
                    break;
                case MachineBehaviour.Sps:
                    for (int i = 0; i < 3; i++) Box(t, 0, 1.0f, -0.2f + i * 0.2f, 0.6f, 0.03f, 0.08f, accent, "Coil");
                    break;
                default:
                    for (int i = 0; i < 3; i++) Box(t, -0.2f + i * 0.2f, 1.0f, 0, 0.1f, 0.03f, 0.6f, accent, "Cell");
                    break;
            }
            return new Parts { Accent = screen };
        }

        /// <summary>Port : bloc de paroi avec une bride ronde sur chaque face latérale.</summary>
        private static Parts Port(Transform t)
        {
            WallBlock(t, body, WallEdge);
            Transform top = Box(t, 0, 0.995f, 0, 0.4f, 0.01f, 0.4f, accent, "Top");
            foreach (var d in DirectionExtensions.Horizontal)
            {
                var dv = GridMath.DirectionVector(d);
                var ring = Shapes.Cylinder(t, dv * 0.495f + Vector3.up * 0.5f, 0.5f, 0.01f, Color.Lerp(dark, accent, 0.25f), "Flange " + d);
                ring.localRotation = Quaternion.FromToRotation(Vector3.up, dv);
                var hole = Shapes.Cylinder(t, dv * 0.5f + Vector3.up * 0.5f, 0.3f, 0.004f, dark, "Bore " + d);
                hole.localRotation = Quaternion.FromToRotation(Vector3.up, dv);
            }
            return new Parts { Accent = top };
        }
    }
}
