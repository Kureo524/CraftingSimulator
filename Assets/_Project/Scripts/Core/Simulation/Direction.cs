using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Faces d'une case (absolues). La grille est en (x, y) au sol et z pour l'étage.</summary>
    public enum Direction { North = 0, East = 1, South = 2, West = 3, Up = 4, Down = 5 }

    public static class DirectionExtensions
    {
        public static readonly Direction[] All = { Direction.North, Direction.East, Direction.South, Direction.West, Direction.Up, Direction.Down };
        public static readonly Direction[] Horizontal = { Direction.North, Direction.East, Direction.South, Direction.West };

        public static Vector3Int Offset(this Direction d) => d switch
        {
            Direction.North => new Vector3Int(0, 1, 0),
            Direction.East => new Vector3Int(1, 0, 0),
            Direction.South => new Vector3Int(0, -1, 0),
            Direction.West => new Vector3Int(-1, 0, 0),
            Direction.Up => new Vector3Int(0, 0, 1),
            _ => new Vector3Int(0, 0, -1),
        };

        public static Direction Opposite(this Direction d) => d switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            Direction.West => Direction.East,
            Direction.Up => Direction.Down,
            _ => Direction.Up,
        };

        public static bool IsVertical(this Direction d) => d is Direction.Up or Direction.Down;

        public static string DisplayName(this Direction d) => d switch
        {
            Direction.North => "Nord",
            Direction.East => "Est",
            Direction.South => "Sud",
            Direction.West => "Ouest",
            Direction.Up => "Haut",
            _ => "Bas",
        };

        public static bool TryFromOffset(Vector3Int o, out Direction d)
        {
            foreach (var x in All) if (x.Offset() == o) { d = x; return true; }
            d = default;
            return false;
        }
    }
}
