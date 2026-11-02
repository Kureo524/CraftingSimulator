using System.Linq;
using Nucleaire.Data;
using Nucleaire.EditorTools;
using Nucleaire.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Nucleaire.Tests
{
    /// <summary>Base des tests de simulation : une partie neuve sur le vrai contenu du jeu.</summary>
    public abstract class SimTestBase
    {
        protected RecipeDatabase Db;
        protected SimulationSettings Settings;
        protected GameSession Session;
        protected World World => Session.World;

        [SetUp]
        public void BaseSetUp()
        {
            Db = RecipeDatabaseSync.FindDatabase();
            Settings = ScriptableObject.CreateInstance<SimulationSettings>();
            Session = new GameSession(Db, Settings);
        }

        [TearDown]
        public void BaseTearDown() => Object.DestroyImmediate(Settings);

        protected ResourceDef R(string id) => Db.Resources.First(r => r.Id == id);
        protected MachineDef M(string id) => Db.Machines.First(m => m.Id == id);
        protected TransmitterDef T(string id) => Db.Transmitters.First(t => t.Id == id);
        protected UpgradeDef U(string id) => Db.Upgrades.First(u => u.Id == id);

        protected T Place<T>(string machineId, int x, int y) where T : Machine => (T)World.Place(M(machineId), new Vector3Int(x, y, 0));

        protected void Line(string transmitterId, int x0, int x1, int y)
        {
            for (int x = x0; x <= x1; x++) World.Place(T(transmitterId), new Vector3Int(x, y, 0));
        }

        protected StructureDef S(string id) => Db.Structures.First(s => s.Id == id);

        /// <summary>Construit un multi-bloc : boîte creuse de cadres, contrôleur au milieu de la face sud (étage min+1),
        /// ports aux positions données (réglage de toutes leurs faces), composants internes depuis le coin bas.</summary>
        protected T BuildMultiblock<T>(string controllerId, Vector3Int min, Vector3Int size, string interiorId, int interiorCount,
            params (Vector3Int pos, SideMode mode, int slot)[] ports) where T : Machine
        {
            var max = min + size - Vector3Int.one;
            var ctrl = new Vector3Int(min.x + size.x / 2, min.y, min.z + 1);
            // Au moins un port : par défaut, une entrée au milieu de la face nord.
            if (ports.Length == 0) ports = new[] { (new Vector3Int(min.x + size.x / 2, max.y, min.z + 1), SideMode.Input, -1) };
            T controller = null;
            int placed = 0;
            for (int z = min.z; z <= max.z; z++)
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                var c = new Vector3Int(x, y, z);
                bool border = x == min.x || x == max.x || y == min.y || y == max.y || z == min.z || z == max.z;
                if (!border)
                {
                    if (interiorId != null && placed < interiorCount) { World.Place(S(interiorId), c); placed++; }
                    continue;
                }
                if (c == ctrl) { controller = (T)World.Place(M(controllerId), c, Direction.South); continue; }
                var port = ports.FirstOrDefault(p => p.pos == c);
                if (port.pos == c && ports.Length > 0 && ports.Any(p => p.pos == c))
                {
                    var pm = (Machine)World.Place(M("multiblock_port"), c);
                    foreach (var d in DirectionExtensions.All) pm.Sides.Set(d, new SideSetting(port.mode, null, port.slot));
                    continue;
                }
                World.Place(S("structural_frame"), c);
            }
            World.Step(); // validation
            return controller;
        }

        /// <summary>Remplit le tampon d'énergie d'une machine (sans passer par ses faces).</summary>
        protected void Power(Machine m, int amount) => m.InsertManual(Db.Energy, amount);
    }
}
