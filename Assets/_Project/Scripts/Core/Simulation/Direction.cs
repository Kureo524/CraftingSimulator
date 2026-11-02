using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Faces d'une case de la grille (absolues : la configuration des faces ne dépend pas de la caméra).</summary>
    public enum Direction { North = 0, East = 1, South = 2, West = 3 }

    public static class DirectionExtensions
    {
        public static readonly Direction[] All = { Direction.North, Direction.East, Direction.South, Direction.West };

        public static Vector2Int Offset(this Direction d) => d switch
        {
            Direction.North => new Vector2Int(0, 1),
            Direction.East => new Vector2Int(1, 0),
            Direction.South => new Vector2Int(0, -1),
            _ => new Vector2Int(-1, 0),
        };

        public static Direction Opposite(this Direction d) => (Direction)(((int)d + 2) % 4);

        public static string DisplayName(this Direction d) => d switch
        {
            Direction.North => "Nord",
            Direction.East => "Est",
            Direction.South => "Sud",
            _ => "Ouest",
        };
    }
}
