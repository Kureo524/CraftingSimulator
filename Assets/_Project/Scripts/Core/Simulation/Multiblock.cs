using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    public enum MultiblockKind { Fission, Fusion, Sps, Induction }

    /// <summary>Contrôleur d'une structure multi-bloc.</summary>
    public interface IMultiblockController
    {
        Multiblock Structure { get; }
    }

    /// <summary>Forme d'un multi-bloc : une boîte creuse (de 3×3×3 à 7×7×4) dont la paroi est faite de cadres structurels.
    /// Le verre (décoratif) peut remplacer un cadre partout sauf sur les arêtes ; les ports (au moins un) et le contrôleur
    /// (un seul) peuvent remplacer n'importe quel cadre de la paroi. L'intérieur est vide ou contient les composants du type.
    /// La structure se revérifie dès que l'atelier change.</summary>
    public sealed class Multiblock
    {
        public const int MinSize = 3, MaxSize = 7, MaxHeight = 4;

        private readonly Machine controller;
        private readonly Dictionary<StructureRole, int> components = new();
        private readonly List<PortMachine> ports = new();
        private readonly List<Vector3Int> missing = new();
        private int version = -1;

        public Multiblock(Machine controller, MultiblockKind kind)
        {
            this.controller = controller;
            Kind = kind;
        }

        public MultiblockKind Kind { get; }
        public bool Formed { get; private set; }
        /// <summary>Ce qui empêche la structure de fonctionner (null si elle est complète).</summary>
        public string Problem { get; private set; }
        /// <summary>Boîte de la structure (min et max inclus).</summary>
        public Vector3Int Min { get; private set; }
        public Vector3Int Max { get; private set; }
        public Vector3Int Size => Max - Min + Vector3Int.one;
        public int InteriorVolume { get; private set; }
        public IReadOnlyList<PortMachine> Ports => ports;
        /// <summary>Cases de paroi encore vides (aperçu des blocs à poser).</summary>
        public IReadOnlyList<Vector3Int> Missing => missing;
        public int Count(StructureRole r) => components.TryGetValue(r, out var n) ? n : 0;

        public static IReadOnlyList<StructureRole> InteriorRoles(MultiblockKind k) => k switch
        {
            MultiblockKind.Fission => new[] { StructureRole.FuelAssembly },
            MultiblockKind.Fusion => new[] { StructureRole.FusionCoil },
            MultiblockKind.Sps => new[] { StructureRole.SpsCoil },
            _ => new[] { StructureRole.InductionCell, StructureRole.InductionProvider },
        };

        private static bool IsShell(IGridEntity e) => e switch
        {
            StructureBlock b => b.StructureDef.IsWall,
            PortMachine => true,
            IMultiblockController => true,
            _ => false,
        };

        /// <summary>Revérifie la structure si l'atelier a changé depuis la dernière fois.</summary>
        public void Refresh()
        {
            var world = controller.World;
            if (version == world.StructureVersion) return;
            version = world.StructureVersion;
            Validate(world);
        }

        private void Validate(World world)
        {
            foreach (var p in ports) if (p.Controller == controller) p.Link(null);
            ports.Clear();
            components.Clear();
            missing.Clear();
            Formed = false;
            Problem = null;
            InteriorVolume = 0;

            // 1) La paroi : tous les blocs de paroi reliés au contrôleur.
            var shell = new HashSet<Vector3Int> { controller.Position };
            var stack = new Stack<Vector3Int>();
            stack.Push(controller.Position);
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                foreach (var d in DirectionExtensions.All)
                {
                    var n = c + d.Offset();
                    if (shell.Contains(n) || !IsShell(world.At(n))) continue;
                    shell.Add(n);
                    stack.Push(n);
                }
            }

            var min = shell.Aggregate(Vector3Int.Min);
            var max = shell.Aggregate(Vector3Int.Max);
            var size = max - min + Vector3Int.one;
            if (size.x < MinSize || size.y < MinSize || size.z < MinSize)
            {
                // Pas encore de boîte : gabarit 3×3×3 derrière le contrôleur.
                Template(world, out min, out max);
                Min = min; Max = max;
                foreach (var c in Boundary(min, max)) if (world.At(c) == null) missing.Add(c);
                Problem = shell.Count <= 1
                    ? "Construis une boîte creuse autour : au moins 3×3×3, des cadres structurels aux coins (gabarit affiché dans l'atelier)."
                    : $"Trop petite : {size.x}×{size.y}×{size.z}. Il faut au moins 3×3×3.";
                return;
            }
            Min = min; Max = max;
            if (size.x > MaxSize || size.y > MaxSize || size.z > MaxHeight)
            {
                Problem = $"Trop grande : {size.x}×{size.y}×{size.z}. Au plus {MaxSize}×{MaxSize}×{MaxHeight}.";
                return;
            }

            // 2) Chaque case de la boîte.
            var allowed = InteriorRoles(Kind);
            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            {
                var c = new Vector3Int(x, y, z);
                var e = world.At(c);
                int onBorder = (x == min.x || x == max.x ? 1 : 0) + (y == min.y || y == max.y ? 1 : 0) + (z == min.z || z == max.z ? 1 : 0);
                if (onBorder > 0)
                {
                    if (e == null) { missing.Add(c); continue; }
                    if (!shell.Contains(c)) { Problem ??= $"{e.Def.DisplayName} n'a pas sa place dans la paroi ({Where(c)})."; continue; }
                    if (onBorder >= 2 && e is StructureBlock { Role: StructureRole.Glass })
                        Problem ??= $"Le verre ne va pas sur les arêtes : mets-y un cadre structurel ({Where(c)}).";
                    if (e is IMultiblockController && e != controller) Problem ??= "Un seul contrôleur par structure.";
                    if (e is PortMachine port) ports.Add(port);
                }
                else
                {
                    InteriorVolume++;
                    if (e == null) continue;
                    if (e is StructureBlock b && allowed.Contains(b.Role)) components[b.Role] = Count(b.Role) + 1;
                    else Problem ??= $"{e.Def.DisplayName} n'a rien à faire à l'intérieur ({Where(c)}).";
                }
            }

            if (missing.Count > 0) Problem ??= $"Paroi incomplète : {missing.Count} bloc(s) manquant(s) (en rouge dans l'atelier).";
            if (Problem == null && allowed.Sum(Count) == 0)
                Problem = Kind switch
                {
                    MultiblockKind.Fission => "Il faut au moins un assemblage de combustible à l'intérieur.",
                    MultiblockKind.Fusion => "Il faut au moins une bobine de confinement à l'intérieur.",
                    MultiblockKind.Sps => "Il faut au moins une bobine supraconductrice à l'intérieur.",
                    _ => "Il faut au moins une cellule d'induction à l'intérieur.",
                };
            if (Problem == null && ports.Count == 0)
                Problem = "Il faut au moins un port de structure dans la paroi (n'importe où) pour faire entrer et sortir.";
            if (Problem == null && Kind == MultiblockKind.Induction && Count(StructureRole.InductionCell) == 0)
                Problem = "Il faut au moins une cellule d'induction (les fournisseurs ne stockent rien).";
            if (Problem != null) { ports.Clear(); return; }

            Formed = true;
            foreach (var p in ports) p.Link(controller);
        }

        private void Template(World world, out Vector3Int min, out Vector3Int max)
        {
            var back = controller.Facing.Opposite().Offset();
            var c = controller.Position;
            var side = new Vector3Int(Mathf.Abs(back.y), Mathf.Abs(back.x), 0);
            var a = c - side;
            var b = c + side + back * 2;
            int z0 = Mathf.Min(c.z, Mathf.Max(0, world.Settings.levels - MinSize));
            min = Vector3Int.Min(a, b); max = Vector3Int.Max(a, b);
            min.z = z0; max.z = z0 + MinSize - 1;
        }

        private static IEnumerable<Vector3Int> Boundary(Vector3Int min, Vector3Int max)
        {
            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
                if (x == min.x || x == max.x || y == min.y || y == max.y || z == min.z || z == max.z)
                    yield return new Vector3Int(x, y, z);
        }

        private static string Where(Vector3Int c) => $"case {c.x},{c.y}, étage {c.z + 1}";
    }
}
