using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nucleaire.Data
{
    public enum TutorialCondition
    {
        None,                 // le joueur clique sur « Suivant »
        OpenRecipeBook,       // ouvrir le livre de recettes
        HaveItem,             // avoir « amount » × resource dans l'inventaire
        PlaceMachine,         // avoir posé une machine « machine »
        MachinePowered,       // une machine « machine » a de l'énergie
        Produce,              // avoir produit « amount » × resource
        VaultOutputFiltered,  // une face du vault en sortie avec un filtre
        ConfigureOutputSide,  // une face de sortie configurée sur une machine (hors vault)
    }

    [Serializable]
    public sealed class TutorialStep
    {
        public string title;
        [TextArea(3, 8)] public string body;
        public TutorialCondition condition;
        public ResourceDef resource;
        public MachineDef machine;
        [Min(1)] public int amount = 1;
    }

    /// <summary>Tutoriel guidé : une suite d'étapes, chacune validée par une condition de jeu.</summary>
    [CreateAssetMenu(menuName = "Nucleaire/Tutoriel", fileName = "Tutorial")]
    public sealed class TutorialDef : ScriptableObject
    {
        [SerializeField] private List<TutorialStep> steps = new();
        public IReadOnlyList<TutorialStep> Steps => steps;

#if UNITY_EDITOR
        public void EditorSetSteps(List<TutorialStep> s) => steps = s;
#endif
    }
}
