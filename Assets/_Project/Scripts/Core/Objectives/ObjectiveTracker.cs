using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using Nucleaire.Simulation;

namespace Nucleaire.Objectives
{
    /// <summary>Suit la production cumulée de l'atelier et valide les objectifs.</summary>
    public sealed class ObjectiveTracker
    {
        private readonly World world;
        private readonly HashSet<ObjectiveDef> completed = new();
        private readonly Dictionary<ObjectiveDef, long> completedAtTick = new();

        public ObjectiveTracker(World world)
        {
            this.world = world;
            Objectives = world.Db.Objectives.Where(o => o != null).OrderBy(o => o.Order).ToList();
            world.ResourceProduced += (_, _) => Check();
        }

        public IReadOnlyList<ObjectiveDef> Objectives { get; }
        public event Action<ObjectiveDef> Completed;

        public long Progress(ObjectiveDef o) => Math.Min(world.Produced(o.Target.Resource), o.Target.Amount);
        public float Ratio(ObjectiveDef o) => o.Target.Amount == 0 ? 1f : (float)Progress(o) / o.Target.Amount;
        public bool IsCompleted(ObjectiveDef o) => completed.Contains(o);
        public long? CompletedAt(ObjectiveDef o) => completedAtTick.TryGetValue(o, out var t) ? t : null;
        public ObjectiveDef Current => Objectives.FirstOrDefault(o => !IsCompleted(o));

        public void Reset()
        {
            completed.Clear();
            completedAtTick.Clear();
        }

        private void Check()
        {
            foreach (var o in Objectives)
            {
                if (completed.Contains(o) || world.Produced(o.Target.Resource) < o.Target.Amount) continue;
                completed.Add(o);
                completedAtTick[o] = world.CurrentTick;
                world.Log(EventSeverity.Success, $"Objectif atteint : {o.Title} !");
                Completed?.Invoke(o);
            }
        }
    }
}
