using UnityEngine;

namespace Nucleaire.Data
{
    /// <summary>Rôle d'un bloc de structure dans un multi-bloc.</summary>
    public enum StructureRole
    {
        Frame,             // cadre structurel (paroi, obligatoire aux coins)
        Glass,             // verre structurel (paroi)
        FuelAssembly,      // fission : assemblage de combustible (intérieur)
        FusionCoil,        // fusion : bobine de confinement (intérieur)
        SpsCoil,           // déphaseur : bobine supraconductrice (intérieur)
        InductionCell,     // matrice : cellule d'induction (intérieur)
        InductionProvider, // matrice : fournisseur d'induction (intérieur)
    }

    /// <summary>Bloc passif d'un multi-bloc : paroi (cadre, verre) ou composant interne.</summary>
    [CreateAssetMenu(menuName = "Nucleaire/Bloc de structure", fileName = "NewStructure")]
    public sealed class StructureDef : PlaceableDef
    {
        [SerializeField] private StructureRole role;
        public StructureRole Role => role;
        public bool IsWall => role is StructureRole.Frame or StructureRole.Glass;
    }
}
