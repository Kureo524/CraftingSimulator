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

        /// <summary>Remplit le tampon d'énergie d'une machine (sans passer par ses faces).</summary>
        protected void Power(Machine m, int amount) => m.InsertManual(Db.Energy, amount);
    }
}
