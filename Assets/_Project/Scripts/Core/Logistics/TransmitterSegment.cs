using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;

namespace Nucleaire.Logistics
{
    /// <summary>Un morceau de câble/tuyau/tube/convoyeur posé sur une case.</summary>
    public sealed class TransmitterSegment : IGridEntity
    {
        public TransmitterSegment(TransmitterDef def, Vector3Int position)
        {
            TransmitterDef = def;
            Position = position;
        }

        public TransmitterDef TransmitterDef { get; }
        public PlaceableDef Def => TransmitterDef;
        public Vector3Int Position { get; }
        public TransmitterNetwork Network { get; internal set; }

        public bool ConnectsTo(TransmitterSegment other) => other != null && other.TransmitterDef == TransmitterDef;
    }
}
