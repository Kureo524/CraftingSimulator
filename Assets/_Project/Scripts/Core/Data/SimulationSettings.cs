using UnityEngine;

namespace Nucleaire.Data
{
    /// <summary>Paramètres globaux de la simulation (rythme, transferts directs, taille de l'atelier).</summary>
    [CreateAssetMenu(menuName = "Nucleaire/Réglages de simulation", fileName = "SimulationSettings")]
    public sealed class SimulationSettings : ScriptableObject
    {
        [Min(1)] public int ticksPerSecond = 10;
        public Vector2Int gridSize = new(24, 24);
        [Tooltip("Nombre d'étages constructibles.")]
        [Min(1)] public int levels = 4;
        [Tooltip("Contenance d'une bonbonne de l'inventaire (liquide ou gaz), en mB.")]
        [Min(100)] public int canisterCapacity = 4000;

        [Header("Transfert direct machine → machine voisine (par tick et par face)")]
        [Min(1)] public int directItemTransfer = 2;
        [Min(1)] public int directFluidTransfer = 1000;
        [Min(1)] public int directEnergyTransfer = 50000;

        [Header("Réacteur")]
        [Range(0f, 1f)] public float reactorWarningRatio = 0.7f;

        public int DirectTransfer(ResourceKind kind) => kind switch
        {
            ResourceKind.Item => directItemTransfer,
            ResourceKind.Energy => directEnergyTransfer,
            _ => directFluidTransfer,
        };
    }
}
