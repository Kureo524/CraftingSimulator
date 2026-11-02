using System;
using UnityEngine;

namespace Nucleaire.Data
{
    public enum MachineStat
    {
        Speed,          // multiplicateur de vitesse de traitement
        EnergyUsage,    // multiplicateur de consommation d'énergie
        Capacity,       // multiplicateur de capacité des réservoirs
        CoolantUsage,   // réacteur : eau consommée par mB de combustible
        HeatedCoolant,  // réacteur : vapeur produite par mB de combustible
    }

    /// <summary>Modificateur multiplicatif : valeur finale = base × (1 + somme des pourcentages).</summary>
    [Serializable]
    public struct StatModifier
    {
        public MachineStat stat;
        [Tooltip("+0.5 = +50 %, -0.4 = -40 %")]
        public float percent;

        public StatModifier(MachineStat stat, float percent) { this.stat = stat; this.percent = percent; }

        public override string ToString() => $"{stat} {(percent >= 0 ? "+" : "")}{percent * 100f:0}%";
    }
}
