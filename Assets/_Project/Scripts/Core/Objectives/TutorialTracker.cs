using System;
using System.Linq;
using Nucleaire.Data;
using Nucleaire.Simulation;

namespace Nucleaire.Objectives
{
    /// <summary>Suit l'avancée du tutoriel. Les conditions sont évaluées sur l'état réel de la partie.</summary>
    public sealed class TutorialTracker
    {
        private readonly GameSession session;
        private bool bookOpened;

        public TutorialTracker(GameSession session, TutorialDef def)
        {
            this.session = session;
            Def = def;
        }

        public TutorialDef Def { get; }
        public int Index { get; private set; }
        public bool Finished => Def == null || Index >= Def.Steps.Count;
        public TutorialStep Current => Finished ? null : Def.Steps[Index];
        public int Count => Def?.Steps.Count ?? 0;
        public event Action Advanced;

        public void NotifyRecipeBookOpened() => bookOpened = true;

        public bool IsMet(TutorialStep s)
        {
            if (s == null) return false;
            var w = session.World;
            switch (s.condition)
            {
                case TutorialCondition.None: return false;
                case TutorialCondition.OpenRecipeBook: return bookOpened;
                case TutorialCondition.HaveItem: return session.Inventory.Count(s.resource) >= s.amount;
                case TutorialCondition.PlaceMachine: return w.Machines.Any(m => m.MachineDef == s.machine);
                case TutorialCondition.MachinePowered: return w.Machines.Any(m => m.MachineDef == s.machine && m.Energy > 0);
                case TutorialCondition.Produce: return w.Produced(s.resource) >= s.amount;
                case TutorialCondition.VaultOutputFiltered:
                    return w.Machines.OfType<VaultMachine>().Any(v => DirectionExtensions.All.Any(d => v.Sides[d].Mode == SideMode.Output && v.Sides[d].Filter != null));
                case TutorialCondition.ConfigureOutputSide:
                    return w.Machines.Any(m => m is not VaultMachine && m is ProcessorMachine p && p.Recipes.Any(r => r.Inputs.Count > 0)
                                               && DirectionExtensions.All.Any(d => m.Sides[d].Mode == SideMode.Output));
                default: return false;
            }
        }

        /// <summary>Passe à l'étape suivante si la condition est remplie. Renvoie vrai si on a avancé.</summary>
        public bool Check()
        {
            if (Finished || !IsMet(Current)) return false;
            Next();
            return true;
        }

        public void Next()
        {
            if (Finished) return;
            Index++;
            Advanced?.Invoke();
        }

        public void Previous()
        {
            if (Index == 0) return;
            Index--;
            Advanced?.Invoke();
        }
    }
}
