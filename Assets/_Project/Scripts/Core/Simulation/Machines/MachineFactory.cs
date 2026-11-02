using System;
using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    public static class MachineFactory
    {
        public static Machine Create(MachineDef def, World world, Vector3Int pos) => def.Behaviour switch
        {
            MachineBehaviour.Processor => new ProcessorMachine(def, world, pos),
            MachineBehaviour.Reactor => new ReactorMachine(def, world, pos),
            MachineBehaviour.Vault => new VaultMachine(def, world, pos),
            MachineBehaviour.Battery => new BatteryMachine(def, world, pos),
            MachineBehaviour.Vent => new VentMachine(def, world, pos),
            _ => throw new ArgumentOutOfRangeException(nameof(def), def.Behaviour, "Comportement inconnu"),
        };
    }
}
