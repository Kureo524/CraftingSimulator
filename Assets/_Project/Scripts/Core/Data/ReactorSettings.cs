using System;
using UnityEngine;

namespace Nucleaire.Data
{
    /// <summary>Paramètres du réacteur à fission (règle « refroidissement à l'eau »).</summary>
    [Serializable]
    public sealed class ReactorSettings
    {
        [Header("Ressources")]
        public ResourceDef fuel;
        public ResourceDef coolant;
        public ResourceDef waste;
        public ResourceDef heatedCoolant;

        [Header("Combustion (par mB de combustible brûlé)")]
        [Min(1)] public int maxBurnRate = 10;          // mB de combustible / tick au maximum
        [Min(0)] public int coolantPerFuel = 20;        // mB d'eau nécessaires pour évacuer la chaleur
        [Min(0)] public int heatedCoolantPerFuel = 20;  // mB de vapeur produits
        [Min(0)] public int wastePerFuel = 1;           // mB de déchets produits
        [Min(0)] public float heatPerFuel = 5f;          // chaleur ajoutée si l'eau manque complètement

        [Header("Chaleur")]
        [Min(0)] public float ambientHeat = 20f;
        [Min(0)] public float passiveCooling = 2f;      // chaleur perdue par tick
        [Min(1)] public float meltdownHeat = 1000f;     // surchauffe : combustible perdu, arrêt
        [Min(0)] public float restartHeat = 100f;       // température sous laquelle on peut relancer
    }
}
