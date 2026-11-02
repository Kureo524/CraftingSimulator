using Nucleaire.Simulation;
using UnityEngine;

namespace Nucleaire.Game
{
    /// <summary>Conversion grille ↔ monde. Case (x, y, étage z) → monde (x + 0.5, z × hauteur, y + 0.5). Le nord est +Z monde.</summary>
    public static class GridMath
    {
        public const float LevelHeight = 1f;

        public static Vector3 CellCenter(Vector3Int c, float height = 0f) => new(c.x + 0.5f, c.z * LevelHeight + height, c.y + 0.5f);

        public static Vector3Int WorldToCell(Vector3 p) => new(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z), Mathf.FloorToInt(p.y / LevelHeight));

        /// <summary>Direction de grille → vecteur monde.</summary>
        public static Vector3 DirectionVector(Direction d)
        {
            var o = d.Offset();
            return new Vector3(o.x, o.z, o.y);
        }

        /// <summary>Normale d'une face touchée par un rayon → direction de grille.</summary>
        public static bool NormalToDirection(Vector3 n, out Direction d)
        {
            var a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            Vector3Int o = a.y >= a.x && a.y >= a.z ? new Vector3Int(0, 0, n.y > 0 ? 1 : -1)
                : a.x >= a.z ? new Vector3Int(n.x > 0 ? 1 : -1, 0, 0)
                : new Vector3Int(0, n.z > 0 ? 1 : -1, 0);
            return DirectionExtensions.TryFromOffset(o, out d);
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

    /// <summary>Lien entre un objet 3D (collider) et l'entité de la simulation qu'il représente.</summary>
    public sealed class EntityRef : MonoBehaviour
    {
        public IGridEntity Entity;
    }
}
