using System;
using System.Collections.Generic;

namespace Nucleaire.Simulation
{
    public enum EventSeverity { Info, Success, Warning, Error }

    public readonly struct SimEvent
    {
        public readonly long Tick;
        public readonly EventSeverity Severity;
        public readonly string Message;
        public readonly IGridEntity Source;

        public SimEvent(long tick, EventSeverity severity, string message, IGridEntity source)
        {
            Tick = tick; Severity = severity; Message = message; Source = source;
        }
    }

    /// <summary>Journal des évènements de l'atelier (affiché au joueur).</summary>
    public sealed class EventLog
    {
        public const int MaxEntries = 200;
        private readonly List<SimEvent> entries = new();

        public IReadOnlyList<SimEvent> Entries => entries;
        public event Action<SimEvent> Added;

        public void Add(SimEvent e)
        {
            entries.Add(e);
            if (entries.Count > MaxEntries) entries.RemoveAt(0);
            Added?.Invoke(e);
        }
    }
}
