using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Multiplicateurs effectifs d'une machine selon ses améliorations installées.</summary>
    public static class MachineStats
    {
        public const float MinMultiplier = 0.1f;

        public static float Multiplier(IEnumerable<UpgradeDef> upgrades, MachineStat stat)
        {
            float sum = upgrades.Where(u => u != null).SelectMany(u => u.Modifiers).Where(m => m.stat == stat).Sum(m => m.percent);
            return Mathf.Max(MinMultiplier, 1f + sum);
        }
    }
}
