using Nucleaire.Simulation;
using UnityEngine;

namespace Nucleaire.Game
{
    /// <summary>Relie la plaque 3D d'un trieur à son trieur (pour le clic et le mode suppression).</summary>
    public sealed class SorterRef : MonoBehaviour
    {
        public FaceSorter Sorter;
    }
}
