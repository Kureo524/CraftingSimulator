using UnityEngine;

namespace Nucleaire.Game
{
    /// <summary>Conversion grille ↔ monde. Case (x, y) → centre (x + 0.5, 0, y + 0.5). Le nord est +Z.</summary>
    public static class GridMath
    {
        public static Vector3 CellCenter(Vector2Int c, float height = 0f) => new(c.x + 0.5f, height, c.y + 0.5f);

        public static Vector2Int WorldToCell(Vector3 p) => new(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));

        public static Vector3 DirectionVector(Simulation.Direction d)
        {
            var o = Simulation.DirectionExtensions.Offset(d);
            return new Vector3(o.x, 0f, o.y);
        }

        public static bool RayToGround(Ray ray, out Vector3 point)
        {
            point = default;
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0) return false;
            point = ray.origin + ray.direction * t;
            return true;
        }
    }
}
