using UnityEngine;

namespace Nucleaire.Data
{
    /// <summary>Câble, tuyau, tube pressurisé ou convoyeur. Les transmetteurs adjacents de même type forment un réseau.</summary>
    [CreateAssetMenu(menuName = "Nucleaire/Transmetteur", fileName = "NewTransmitter")]
    public sealed class TransmitterDef : PlaceableDef
    {
        [SerializeField] private ResourceKind carries = ResourceKind.Item;
        [Tooltip("Quantité maximale déplacée par tick par le réseau entier.")]
        [SerializeField, Min(1)] private int throughputPerTick = 100;
        [Tooltip("Contenance de chaque segment (le réseau additionne ses segments).")]
        [SerializeField, Min(1)] private int capacityPerSegment = 200;

        public ResourceKind Carries => carries;
        public int ThroughputPerTick => throughputPerTick;
        public int CapacityPerSegment => capacityPerSegment;

#if UNITY_EDITOR
        public void EditorInitTransmitter(ResourceKind k, int throughput, int capacity)
        {
            carries = k; throughputPerTick = throughput; capacityPerSegment = capacity;
        }
#endif
    }
}
