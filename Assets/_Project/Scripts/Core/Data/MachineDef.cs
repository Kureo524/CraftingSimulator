using UnityEngine;

namespace Nucleaire.Data
{
    /// <summary>Définition d'une machine : capacités, comportement et paramètres spécifiques.
    /// Les procédés qu'elle exécute sont des <see cref="ProcessRecipe"/> qui la référencent.</summary>
    [CreateAssetMenu(menuName = "Nucleaire/Machine", fileName = "NewMachine")]
    public sealed class MachineDef : PlaceableDef
    {
        [SerializeField] private MachineBehaviour behaviour = MachineBehaviour.Processor;

        [Header("Capacités")]
        [SerializeField, Min(0)] private int energyCapacity = 2000;
        [SerializeField, Min(1)] private int itemCapacity = 64;      // par ressource solide
        [SerializeField, Min(1)] private int fluidCapacity = 4000;   // par ressource liquide/gaz (mB)
        [SerializeField, Min(0)] private int upgradeSlots = 2;

        [Header("Comportements spécifiques")]
        [SerializeField] private ReactorSettings reactor = new ReactorSettings();
        [SerializeField] private VentSettings vent = new VentSettings();

        public MachineBehaviour Behaviour => behaviour;
        public int EnergyCapacity => energyCapacity;
        public int ItemCapacity => itemCapacity;
        public int FluidCapacity => fluidCapacity;
        public int UpgradeSlots => upgradeSlots;
        public ReactorSettings Reactor => reactor;
        public VentSettings Vent => vent;

        public int CapacityFor(ResourceKind kind) => kind switch
        {
            ResourceKind.Item => itemCapacity,
            ResourceKind.Energy => energyCapacity,
            _ => fluidCapacity,
        };

#if UNITY_EDITOR
        public void EditorInitMachine(MachineBehaviour b, int energy, int items, int fluids, int upgrades)
        {
            behaviour = b; energyCapacity = energy; itemCapacity = items; fluidCapacity = fluids; upgradeSlots = upgrades;
        }
#endif
    }
}
