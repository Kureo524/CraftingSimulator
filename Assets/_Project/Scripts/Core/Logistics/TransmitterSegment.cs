using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;

namespace Nucleaire.Logistics
{
    /// <summary>Réglage d'un côté de câble : normal, tirer (depuis la machine), pousser (vers la machine),
    /// ou coupé (liaison avec le câble voisin interrompue).</summary>
    public enum CableSide { Normal, Pull, Push, Cut }

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

        private readonly CableSide[] sides = new CableSide[6];

        public CableSide this[Direction d] => sides[(int)d];
        internal void SetSide(Direction d, CableSide s) => sides[(int)d] = s;

        public bool ConnectsTo(TransmitterSegment other)
        {
            if (other == null || other.TransmitterDef != TransmitterDef) return false;
            if (!DirectionExtensions.TryFromOffset(other.Position - Position, out var d)) return false;
            return sides[(int)d] != CableSide.Cut && other.sides[(int)d.Opposite()] != CableSide.Cut;
        }
    }
}
