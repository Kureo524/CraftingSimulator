using System.Collections.Generic;
using UnityEngine;

namespace Nucleaire.Data
{
    /// <summary>Un procédé exécuté automatiquement par une machine : entrées → sorties en un nombre de ticks,
    /// en consommant de l'énergie à chaque tick. Les générateurs sont des procédés qui produisent de l'énergie.
    /// Ajouter un procédé = créer un asset, aucune modification de code.</summary>
    [CreateAssetMenu(menuName = "Nucleaire/Procédé (machine)", fileName = "NewProcess")]
    public sealed class ProcessRecipe : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private MachineDef machine;
        [SerializeField] private List<ResourceStack> inputs = new();
        [SerializeField] private List<ResourceStack> outputs = new();
        [SerializeField, Min(1)] private int durationTicks = 20;
        [SerializeField, Min(0)] private int energyPerTick = 10;

        public string Id => name;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public MachineDef Machine => machine;
        public IReadOnlyList<ResourceStack> Inputs => inputs;
        public IReadOnlyList<ResourceStack> Outputs => outputs;
        public int DurationTicks => durationTicks;
        public int EnergyPerTick => energyPerTick;

#if UNITY_EDITOR
        public void EditorInit(string display, MachineDef m, List<ResourceStack> ins, List<ResourceStack> outs, int duration, int energy)
        {
            displayName = display; machine = m; inputs = ins; outputs = outs; durationTicks = duration; energyPerTick = energy;
        }
#endif
    }
}
