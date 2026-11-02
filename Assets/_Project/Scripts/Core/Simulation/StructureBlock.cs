using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Bloc passif de multi-bloc (cadre, verre, composant interne) posé sur une case.</summary>
    public sealed class StructureBlock : IGridEntity
    {
        public StructureBlock(StructureDef def, Vector3Int position)
        {
            StructureDef = def;
            Position = position;
        }

        public StructureDef StructureDef { get; }
        public PlaceableDef Def => StructureDef;
        public Vector3Int Position { get; }
        public StructureRole Role => StructureDef.Role;
    }
}
