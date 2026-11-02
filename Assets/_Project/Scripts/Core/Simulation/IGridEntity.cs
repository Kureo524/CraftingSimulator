using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Tout ce qui occupe une case de l'atelier.</summary>
    public interface IGridEntity
    {
        PlaceableDef Def { get; }
        Vector3Int Position { get; }
    }

    /// <summary>Peut recevoir des ressources par une de ses faces.</summary>
    public interface IResourceReceiver
    {
        /// <param name="side">Face du receveur par laquelle arrive la ressource.</param>
        /// <returns>Quantité réellement acceptée.</returns>
        int Receive(ResourceDef resource, int amount, Direction side, bool simulate = false);
    }
}
