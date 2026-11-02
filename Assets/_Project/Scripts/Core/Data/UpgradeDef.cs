using System.Collections.Generic;
using UnityEngine;

namespace Nucleaire.Data
{
    /// <summary>Amélioration installable dans une machine. Chaque amélioration est un compromis.</summary>
    [CreateAssetMenu(menuName = "Nucleaire/Amélioration", fileName = "NewUpgrade")]
    public sealed class UpgradeDef : ScriptableObject
    {
        [SerializeField] private ResourceDef item;
        [SerializeField] private List<StatModifier> modifiers = new();
        [Tooltip("Comportements de machine compatibles.")]
        [SerializeField] private List<MachineBehaviour> compatibleBehaviours = new() { MachineBehaviour.Processor };
        [SerializeField, Min(1)] private int maxPerMachine = 2;

        public string Id => name;
        public ResourceDef Item => item;
        public string DisplayName => item != null ? item.DisplayName : name;
        public IReadOnlyList<StatModifier> Modifiers => modifiers;
        public IReadOnlyList<MachineBehaviour> CompatibleBehaviours => compatibleBehaviours;
        public int MaxPerMachine => maxPerMachine;

        public bool IsCompatibleWith(MachineDef machine) =>
            machine != null && machine.UpgradeSlots > 0 && compatibleBehaviours.Contains(machine.Behaviour);

#if UNITY_EDITOR
        public void EditorInit(ResourceDef itemDef, List<StatModifier> mods, List<MachineBehaviour> compat, int max)
        {
            item = itemDef; modifiers = mods; compatibleBehaviours = compat; maxPerMachine = max;
        }
#endif
    }
}
