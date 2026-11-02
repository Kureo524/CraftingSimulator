using System;
using UnityEngine;

namespace Nucleaire.Data
{
    [Serializable]
    public sealed class VentSettings
    {
        [Min(1)] public int ventPerTick = 100;  // mB détruits par tick
        [Min(0)] public int energyPerTick = 5;  // coût de la purge
    }
}
