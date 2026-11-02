using System;
using UnityEngine;

namespace Nucleaire.Data
{
    /// <summary>Réglages des multi-blocs : ce que rapporte chaque composant interne, et ce que coûte la taille.</summary>
    [Serializable]
    public sealed class MultiblockSettings
    {
        [Header("Fission")]
        [Min(1)] public int burnPerAssembly = 2;            // mB/tick de combustion max par assemblage
        [Min(0)] public int upkeepPerAssembly = 10;          // J/tick de contrôle par assemblage
        [Min(1)] public int fluidCapacityPerInterior = 4000; // mB de cuve par case intérieure

        [Header("Fusion (deutérium-tritium)")]
        public ResourceDef fusionFuel;
        [Min(0)] public int ignitionPerCoil = 150000;   // J à accumuler pour allumer le plasma
        [Min(0)] public int energyPerCoil = 6000;       // J/tick produits par bobine
        [Min(0)] public int sustainPerCoil = 1000;      // J/tick pour entretenir le plasma
        [Min(1)] public int fuelPerCoilPer10Ticks = 1;  // mB de combustible D-T
        [Min(0)] public int bufferPerCoil = 60000;      // J de réserve en plus de l'allumage

        [Header("Matrice à induction")]
        [Min(0)] public int capacityPerCell = 1000000;  // J par cellule
        [Min(0)] public int baseRate = 1000;            // J/tick d'entrée et de sortie sans fournisseur
        [Min(0)] public int ratePerProvider = 4000;     // J/tick par fournisseur
        [Min(0)] public int leakPerInterior = 2;        // J/tick perdus par case intérieure

        [Header("Déphaseur")]
        [Min(1)] public int spsBufferTicks = 3;         // réserve d'énergie, en ticks de consommation
    }
}
