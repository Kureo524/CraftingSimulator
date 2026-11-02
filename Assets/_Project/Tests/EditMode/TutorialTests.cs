using System.Linq;
using Nucleaire.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nucleaire.Tests
{
    public class TutorialTests : SimTestBase
    {
        private TutorialDef tutorial;

        [SetUp]
        public void SetUp()
        {
            tutorial = AssetDatabase.LoadAssetAtPath<TutorialDef>("Assets/_Project/Data/Tutorial/Tutorial.asset");
            Assert.IsNotNull(tutorial);
            Session = new GameSession(Db, Settings, tutorial);
        }

        [Test]
        public void DuringTutorial_ActionsAreLimited()
        {
            Assert.IsTrue(Session.Tutorial.Active);
            Assert.IsFalse(Session.Actions.TryPlace(R("item_solar_panel"), new Vector3Int(1, 1, 0), out var err));
            StringAssert.Contains("tutoriel", err);
            var vault = World.FirstMachine<Simulation.VaultMachine>();
            Assert.AreEqual(0, Session.Actions.TakeFromVault(vault, R("iron_ore"), 1));
        }

        [Test]
        public void Steps_UnlockActionsProgressively_AndAdvanceOnPlayerActions()
        {
            var t = Session.Tutorial;
            t.Next();                                // Bienvenue → Livre
            t.NotifyRecipeBookOpened();
            Assert.IsTrue(t.Check(), "Ouvrir le livre valide l'étape");

            var vault = World.FirstMachine<Simulation.VaultMachine>();
            Assert.AreEqual(1, Session.Actions.TakeFromVault(vault, R("iron_ore"), 1), "Le vault est débloqué à son étape");
            Assert.IsTrue(t.Check(), "Avoir du minerai valide l'étape du vault");

            Assert.IsTrue(Session.Actions.TryPlace(R("item_solar_panel"), new Vector3Int(1, 1, 0), out _), "Poser est débloqué à l'étape de l'énergie");
            Assert.IsTrue(t.Check());
            Assert.IsFalse(Session.Permissions.IsAllowed(PlayerPermission.Craft), "L'établi n'est pas encore présenté");
        }

        [Test]
        public void Skip_UnlocksEverything()
        {
            Session.Tutorial.Skip();
            Assert.IsFalse(Session.Tutorial.Active);
            Assert.AreEqual(PlayerPermission.All, Session.Permissions.Allowed);
            Assert.IsTrue(Session.Actions.TryPlace(R("item_solar_panel"), new Vector3Int(1, 1, 0), out _));
        }

        [Test]
        public void Finishing_UnlocksEverything()
        {
            while (Session.Tutorial.Active) Session.Tutorial.Next();
            Assert.AreEqual(PlayerPermission.All, Session.Permissions.Allowed);
        }

        [Test]
        public void EveryStep_NeedsOnlyActionsAlreadyUnlocked()
        {
            var needs = new System.Collections.Generic.Dictionary<TutorialCondition, PlayerPermission>
            {
                [TutorialCondition.OpenRecipeBook] = PlayerPermission.RecipeBook,
                [TutorialCondition.HaveItem] = PlayerPermission.VaultTake,
                [TutorialCondition.PlaceMachine] = PlayerPermission.Place,
                [TutorialCondition.MachinePowered] = PlayerPermission.Place,
                [TutorialCondition.Produce] = PlayerPermission.Deposit,
                [TutorialCondition.VaultOutputFiltered] = PlayerPermission.Sides,
                [TutorialCondition.ConfigureOutputSide] = PlayerPermission.Sides,
            };
            var unlocked = PlayerPermission.None;
            foreach (var s in tutorial.Steps)
            {
                unlocked |= s.unlocks;
                if (needs.TryGetValue(s.condition, out var need))
                    Assert.AreEqual(need, unlocked & need, $"L'étape « {s.title} » demande une action pas encore débloquée");
            }
            Assert.AreEqual(PlayerPermission.All, tutorial.Steps.Last().unlocks, "La dernière étape débloque tout");
        }

        [Test]
        public void FinishingTutorial_ResetsTheBoard()
        {
            var t = Session.Tutorial;
            while (t.Active && t.Current.condition == TutorialCondition.None || t.Index < 2) t.Next();
            var vault = World.FirstMachine<Simulation.VaultMachine>();
            Session.Inventory.Add(R("item_solar_panel"), 1);
            while (t.Active) t.Next();
            Assert.IsTrue(Session.BoardWasReset);
            Assert.AreEqual(1, World.Machines.Count, "Seul le vault reste");
            Assert.AreEqual(0, Session.Inventory.Count(R("iron_ore")));
            Assert.AreEqual(Db.StartingInventory.Where(x => x.IsValid && x.Resource.Id == "item_solar_panel").Sum(x => x.Amount),
                Session.Inventory.Count(R("item_solar_panel")), "Inventaire de départ restauré");
        }

        [Test]
        public void SkippingTutorial_AlsoResetsTheBoard()
        {
            Session.Tutorial.Skip();
            Assert.IsTrue(Session.BoardWasReset);
            Assert.AreEqual(1, World.Machines.Count);
        }
    }
}