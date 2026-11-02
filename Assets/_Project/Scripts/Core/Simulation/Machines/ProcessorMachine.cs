using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using UnityEngine;

namespace Nucleaire.Simulation
{
    /// <summary>Machine générique : exécute n'importe quel <see cref="ProcessRecipe"/> qui la référence.
    /// Règles : énergie par tick (sous-alimentée ⇒ ralentit), sorties pleines ⇒ bloquée.</summary>
    public sealed class ProcessorMachine : Machine
    {
        private readonly IReadOnlyList<ProcessRecipe> recipes;
        private ProcessRecipe current;
        private float progressTicks;

        public ProcessorMachine(MachineDef def, World world, Vector3Int pos)
            : base(def, world, pos, world.Db.ProcessesFor(def).Any(p => p.Inputs.Count > 0) ? SideMode.Input : SideMode.Output)
        {
            recipes = world.Db.ProcessesFor(def);
            var energy = world.Db.Energy;
            Inputs = new ResourceStorage(ResourceStorage.SlotsFor(recipes.Select(r => r.Inputs)), CapacityFor,
                recipes.SelectMany(r => r.Inputs).Select(s => s.Resource).Where(r => r != null));
            Outputs = new ResourceStorage(ResourceStorage.SlotsFor(recipes.Select(r => r.Outputs)), CapacityFor,
                recipes.SelectMany(r => r.Outputs).Select(s => s.Resource).Where(r => r != null && r != energy));
            ConsumesEnergyFlag = recipes.Any(r => r.EnergyPerTick > 0);
            ProducesEnergyFlag = recipes.Any(r => r.Outputs.Any(o => o.Resource == energy));
        }

        private bool ConsumesEnergyFlag { get; }
        private bool ProducesEnergyFlag { get; }
        public override bool ConsumesEnergy => ConsumesEnergyFlag;
        public override bool ProducesEnergy => ProducesEnergyFlag;

        public IReadOnlyList<ProcessRecipe> Recipes => recipes;
        public ProcessRecipe CurrentRecipe => current;
        /// <summary>Recette imposée par le joueur (null = choix automatique).</summary>
        public ProcessRecipe LockedRecipe { get; private set; }
        public override float Progress => current == null ? 0f : Mathf.Clamp01(progressTicks / current.DurationTicks);

        public float SpeedMultiplier => Multiplier(MachineStat.Speed);
        public int EnergyCostPerTick(ProcessRecipe r) =>
            r == null || r.EnergyPerTick == 0 ? 0 : Math.Max(1, Mathf.RoundToInt(r.EnergyPerTick * Multiplier(MachineStat.EnergyUsage)));

        public void LockRecipe(ProcessRecipe r)
        {
            if (r != null && !recipes.Contains(r)) throw new ArgumentException($"{r.Id} n'appartient pas à {MachineDef.Id}");
            LockedRecipe = r;
        }

        protected override void OnTick()
        {
            if (current == null && !TryStart()) return;
            Run();
        }

        private bool TryStart()
        {
            var candidates = LockedRecipe != null ? new[] { LockedRecipe } : recipes;
            ProcessRecipe blocked = null;
            foreach (var r in candidates)
            {
                if (!Inputs.HasAll(r.Inputs)) continue;
                if (!OutputsFit(r)) { blocked ??= r; continue; }
                foreach (var i in r.Inputs) Inputs.Extract(i.Resource, i.Amount);
                current = r;
                progressTicks = 0f;
                return true;
            }

            if (blocked != null) SetStatus(MachineStatus.OutputFull, $"Sortie pleine pour « {blocked.DisplayName} » : vide ou évacue les produits.");
            else if (LockedRecipe == null && Inputs.IsEmpty)
                SetStatus(MachineStatus.Idle, recipes.Any(r => r.Inputs.Count > 0)
                    ? "Aucun ingrédient : dépose-en depuis l'inventaire ou relie une face d'entrée."
                    : "");
            else SetStatus(MachineStatus.MissingInput, DescribeMissing(candidates));
            return false;
        }

        private void Run()
        {
            int cost = EnergyCostPerTick(current);
            float factor = 1f;
            if (cost > 0)
            {
                int required = Mathf.CeilToInt(cost * current.MinPowerRatio);
                if (Energy == 0 || Energy < required)
                {
                    SetStatus(MachineStatus.NoPower, required > 0
                        ? $"Exige {required} J/tick sans interruption (disponible : {Energy} J). Il faut plus de production ou une batterie."
                        : $"Besoin de {cost} J/tick. Relie un câble à une face d'entrée.");
                    return;
                }
                int got = ConsumeEnergy(cost);
                factor = (float)got / cost;
            }

            if (progressTicks < current.DurationTicks)
                progressTicks += SpeedMultiplier * factor;

            if (progressTicks >= current.DurationTicks)
            {
                if (!OutputsFit(current))
                {
                    SetStatus(MachineStatus.OutputFull, $"Impossible de sortir les produits de « {current.DisplayName} ».");
                    return;
                }
                foreach (var o in current.Outputs)
                {
                    if (o.Resource == EnergyDef) AddEnergy(o.Amount);
                    else Outputs.Insert(o.Resource, o.Amount);
                    World.RecordProduction(o.Resource, o.Amount);
                }
                current = null;
                progressTicks = 0f;
            }

            if (factor < 1f) SetStatus(MachineStatus.LowPower, $"Énergie à {factor:P0} : la machine ralentit.");
            else SetStatus(MachineStatus.Working, "");
        }

        private bool OutputsFit(ProcessRecipe r)
        {
            int energyOut = r.Outputs.Where(o => o.Resource == EnergyDef).Sum(o => o.Amount);
            if (energyOut > 0 && EnergyCapacity - Energy < energyOut) return false;
            return Outputs.CanFitAll(r.Outputs.Where(o => o.Resource != EnergyDef));
        }

        private string DescribeMissing(IReadOnlyList<ProcessRecipe> candidates)
        {
            if (candidates.Count == 0) return "Aucun procédé défini pour cette machine.";
            // On décrit le procédé le plus proche d'être réalisable.
            var best = candidates.OrderByDescending(r => r.Inputs.Count(i => Inputs.Has(i))).First();
            var missing = best.Inputs.Where(i => !Inputs.Has(i)).Select(i => $"{i.Amount - Inputs.Amount(i.Resource)}{i.Resource.Kind.Unit()} {i.Resource.DisplayName}");
            return $"« {best.DisplayName} » : il manque {string.Join(", ", missing)}.";
        }
    }
}
